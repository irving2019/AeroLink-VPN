using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using AeroLink.Models;

namespace AeroLink.Services
{
    public class VpnService
    {
        private readonly string _corePath = Path.Combine(AppContext.BaseDirectory, "Core");
        private readonly string _windowsConfigPath = Path.Combine(AppContext.BaseDirectory, "Core", "aerolink.conf");
        private readonly string _linuxConfigPath = Path.Combine(Path.GetTempPath(), "temp.conf");

        public async Task<bool> ConnectAsync(AmneziaConfig config, string rawConfigText)
        {
            try
            {
                string exeName = "amneziawg-linux";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    exeName = "amneziawg.exe";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    exeName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "amnezia-arm" : "amnezia-intel";
                }
                string exePath = Path.Combine(_corePath, exeName);

                if (!File.Exists(exePath))
                    throw new FileNotFoundException($"Ядро {exeName} не найдено!");

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

                rawConfigText = Regex.Replace(rawConfigText, @"(?m)^[A-Za-z0-9]+\s*=\s*$", "");
                rawConfigText = rawConfigText.Replace("\r\n", "\n").Replace("\n", "\r\n");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await File.WriteAllTextAsync(_windowsConfigPath, rawConfigText.Trim(), new UTF8Encoding(false));

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"/installtunnelservice \"{_windowsConfigPath}\"",
                        WorkingDirectory = _corePath,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true,
                        Verb = "runas" 
                    };

                    var process = Process.Start(processInfo);
                    if (process != null) await process.WaitForExitAsync();

                    return true; 
                }
                else
                {
                    await File.WriteAllTextAsync(_linuxConfigPath, rawConfigText.Trim(), new UTF8Encoding(false));
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"\"{_linuxConfigPath}\"",
                        WorkingDirectory = _corePath,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false
                    };
                    Process.Start(processInfo);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string exePath = Path.Combine(_corePath, "amneziawg.exe");

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "/uninstalltunnelservice aerolink",
                        WorkingDirectory = _corePath,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    var process = Process.Start(processInfo);
                    if (process != null) await process.WaitForExitAsync();

                    if (File.Exists(_windowsConfigPath))
                        File.Delete(_windowsConfigPath);
                }
                else
                {
                    string targetProcess = "amneziawg-linux";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        targetProcess = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "amnezia-arm" : "amnezia-intel";
                    }

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "killall",
                        Arguments = targetProcess,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false
                    };
                    Process.Start(processInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при отключении: {ex.Message}");
            }
        }
    }
}