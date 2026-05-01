using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace AeroLink.Models;

public static class ConfigParser
{ 
    public static string GetRawWireGuardConfig(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        if (input.StartsWith("[Interface]", StringComparison.OrdinalIgnoreCase))
            return input;

        if (input.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Обнаружена ссылка VLESS! Передаем в Xray.");
            return null;
        }

        if (input.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
            return ParseAmneziaLink(input);

        Console.WriteLine("Ошибка, неизвестный формат конфигурации!");
        return null;
    }

    private static string ParseAmneziaLink(string vpnLink)
    {
        try
        {
            string base64Data = vpnLink.Substring(6);

            base64Data = base64Data.Replace('-', '+').Replace('_', '/');

            switch(base64Data.Length % 4)
            {
                case 2: base64Data += "=="; break;
                case 3: base64Data += "="; break;
            }

            byte[] data = Convert.FromBase64String(base64Data);
            string jsonConfig = Encoding.UTF8.GetString(data);

            Console.WriteLine("vpn:// расшифрован, Ваш JSON:");
            Console.WriteLine(jsonConfig.Substring(0, Math.Min(jsonConfig.Length, 100)) + "...");

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при расшифровке vpn:// : {ex.Message}");
            return null;
        }
    }
    public static AmneziaConfig Parse(string configText)
    {
        configText = configText.Replace("\r\n", "\n").Trim();

        var config = new AmneziaConfig();
        string currentSection = "";

        using var reader = new StringReader(configText);
        string? line;

        // Читаем текст построчно
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            //Пропускаем пустые строки и комментарии
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line;

                if (currentSection == "[Peer]")
                    config.Peers.Add(new PeerConfig());
                continue;
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
                continue;

            var key = parts[0];
            var value = parts[1];

            if (currentSection == "[Interface]")
            {
                switch (key)
                {
                    case "PrivateKey": config.Interface.PrivateKey = value; break;
                    case "Address": config.Interface.Address = value; break;
                    case "DNS": config.Interface.DNS = value; break;
                }
            }

            else if (currentSection == "[Peer]" && config.Peers.Count > 0)
            {
                var peer = config.Peers[^1];
                switch(key)
                {
                    case "PublicKey": peer.PublicKey = value; break;
                    case "Endpoint": peer.Endpoint = value; break;
                    case "AllowedIPs": peer.AllowedIPs = value; break;
                }
            }
        }
        return config;
    }

    public static string DecodeAndDecompressAmneziaLink(string vpnLink)
    {
       try
        {
            string base64 = vpnLink.Substring(6).Replace("\r", "").Replace("\n", "").Replace(" ", "");
            base64 = base64.Replace("-", "+").Replace("_", "/");
            base64 = Regex.Replace(base64, @"[^a-zA-Z0-9\+/=]", "");

            int mod4 = base64.Length % 4;

            if (mod4 > 0)
                base64 += new string('=', 4 - mod4);

            byte[] compressedData = Convert.FromBase64String(base64);

            using var compressedStream = new MemoryStream(compressedData);
            compressedStream.Seek(4, SeekOrigin.Begin);

            using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();

            zlibStream.CopyTo(resultStream);

            string jsonText = Encoding.UTF8.GetString(resultStream.ToArray());

            using JsonDocument doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            if (root.TryGetProperty("containers", out var containers) && containers.GetArrayLength() > 0)
            {
                var firstContainer = containers[0];
                if (firstContainer.TryGetProperty("awg", out var awg) && awg.TryGetProperty("last_config", out var lastConfigProp))
                {
                    string lastConfigStr = lastConfigProp.GetString() ?? "";

                    using JsonDocument lastConfigDoc = JsonDocument.Parse(lastConfigStr);
                    var awgRoot = lastConfigDoc.RootElement;

                    if (awgRoot.TryGetProperty("config", out var configProp))
                    {
                        string finalConfig = configProp.GetString() ?? "";

                        finalConfig = finalConfig.Replace("I2 = \\n", "")
                                                 .Replace("I3 = \\n", "")
                                                 .Replace("I4 = \\n", "")
                                                 .Replace("I5 = \\n", "");


                        finalConfig = finalConfig.Replace("\\r", "").Replace("\\n", Environment.NewLine);
                        finalConfig = finalConfig.Replace("$PRIMARY_DNS", "8.8.8.8").Replace("$SECONDARY_DNS", "1.1.1.1");

                        return finalConfig;
                    }
                }
            }
            return $"ОШИБКА: Структура JSON не совпадает!\n{jsonText}";
        }
        catch (Exception ex)
        {
            return $"КРИТИЧЕСКАЯ ОШИБКА РАСПАКОВКИ: {ex.Message}";
        }
    }
}
