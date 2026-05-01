using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace AeroLink.Services;

public class XrayService
{
    private Process? _xrayProcess;
    private readonly string _corePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core");
    private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core", "config_xray.json");

    public async Task<bool> ConnectAsync(string jsonConfig, bool isProxyMode = false)
    {
        try
        {
            await DisconnectAsync();

            string exeName = "xray.exe";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                exeName = "xray.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                exeName = "xray-Linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    exeName = "xray_v8a";
                else
                    exeName = "xray-MacOS64";
            }

            string exePath = Path.Combine(_corePath, exeName);

            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Ядро {exeName} не найдено по пути {exePath}");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{exePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Не удалось выдать права chmod: {ex.Message}");
                }
            }

            if (isProxyMode)
            {
                string proxyInbound = @"{ ""tag"": ""proxy-in"", ""port"": 10808, ""listen"": ""127.0.0.1"", ""protocol"": ""socks"", ""settings"": {""auth"": ""noauth"", ""udp"": true } }";

                jsonConfig = Regex.Replace(//System.Text.RegularExpressions
                    jsonConfig,
                    @"""inbounds""\s*:\s*\[.*?\]",
                    $"\"inbounds\": [\n{proxyInbound}\n]",
                    RegexOptions.Singleline
                    );
            }

            await File.WriteAllTextAsync(_configPath, jsonConfig);

            var processInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-c \"{_configPath}\"", // Указываем путь к конфигу
                WorkingDirectory = _corePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            _xrayProcess = Process.Start(processInfo);

            await Task.Delay(1000);

            return _xrayProcess != null && !_xrayProcess.HasExited;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка запуска X-Ray: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_xrayProcess != null && !_xrayProcess.HasExited)
        {
            try
            {
                _xrayProcess.Kill();
                _xrayProcess.Dispose();
            }
            catch
            {
                // Не обрабатываю исключения только убиваю процессы
            }
            _xrayProcess = null;
        }

        foreach (var process in Process.GetProcessesByName("xray"))
            try
            {
                process.Kill();
            }
            catch
            {
                // Добиваю зависшие копии на всякий случай
            }

        await Task.Delay(200);
    }
}

