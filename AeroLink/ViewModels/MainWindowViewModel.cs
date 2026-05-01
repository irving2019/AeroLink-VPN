using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Linq;
using AeroLink.Models;
using System.IO;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AeroLink.ViewModels;

public partial class MainWindowViewModel: ViewModelBase
{
    private readonly string _profilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "profiles.json"
        );

    private readonly Services.VpnService _vpnService = new();
    private readonly Services.XrayService _xrayService = new();

    [ObservableProperty]
    private bool _isPaneOpen;

    [RelayCommand]
    private void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    public ObservableCollection<VpnProfile> Profiles { get; set; } = new();

    //текущий статус подключения
    [ObservableProperty]
    private string _connectionStatus = "Статус: отключено";

    //текст на главной кнопке
    [ObservableProperty]
    private string _actionButtonText = "Подключиться";

    //bool подключения
    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private string _rawConfigText = "";

    [ObservableProperty]
    private string _profileName = "Неизвестный профиль";

    [ObservableProperty]
    private bool _isCodeVisible = true;

    [ObservableProperty]
    private VpnProfile? _selectedProfile;

    [ObservableProperty]
    private string _connectionTimeText = "Статус: Отключено";

    private DispatcherTimer? _timer;
    private int _secondsConnected;

    [ObservableProperty]
    private bool _isDarkTheme = false;

    [ObservableProperty]
    private bool _isProxyMode = false;

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    partial void OnSelectedProfileChanged(VpnProfile? value)
    {
        if (value != null)
        {
            RawConfigText = value.RawConfig;
            ProfileName = value.Name;
            IsCodeVisible = false;
        }
        else
        {
            IsCodeVisible = true;
            ProfileName = "";
            RawConfigText = "";
        }
    }

    public MainWindowViewModel()
    {
        LoadProfiles();
    }

    private void LoadProfiles()
    {
        try
        {
            if (File.Exists(_profilePath))
            {
                var json = File.ReadAllText(_profilePath);
                var loaded = JsonSerializer.Deserialize<List<VpnProfile>>(json);

                if (loaded != null)
                    foreach (var p in loaded)
                        Profiles.Add(p);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки профилей: {ex.Message}");
        }
    }

    private void SaveProfiles()
    {
        var json = JsonSerializer.Serialize(Profiles.ToList());

        File.WriteAllText(_profilePath, json);
    }

    private AmneziaConfig? _activeConfig;

    [RelayCommand]
    private async Task AddNewProfile()
    {
        if (IsConnected)
            await ToggleConnection();

        SelectedProfile = null;
        ProfileName = "Новый профиль";
        RawConfigText = "";
        IsCodeVisible = true;
        ConnectionTimeText = "";
    }

    [RelayCommand]
    private async Task DeleteProfile(VpnProfile profile)
    {
        if (profile != null && Profiles.Contains(profile))
        {
            if (IsConnected && SelectedProfile == profile)
                await ToggleConnection();

            Profiles.Remove(profile);
            SaveProfiles();

            if (SelectedProfile == profile)
            {
                SelectedProfile = null;
                ConnectionTimeText = "";
            }
        }
    }

    private void StartTimer()
    {
        _secondsConnected = 0;
        ConnectionTimeText = "00:00:00";
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) =>
        {
            _secondsConnected++;
            var time = TimeSpan.FromSeconds(_secondsConnected);
            ConnectionTimeText = time.ToString(@"hh\:mm\:ss");
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer.Stop();
        ConnectionTimeText = "Статус: отключено!";
    }

    [RelayCommand]
    private async Task ToggleConnection()
    {
       if (SelectedProfile == null)
        {
            ConnectionTimeText = "Выберите профиль слева!";
            return;
        }

       if (!IsConnected)
        {
            ActionButtonText = "Подождите...";
            bool success = false;

            if (OperatingSystem.IsAndroid())
            {
                Services.VpnBridge.StartVpnAction?.Invoke(SelectedProfile.RawConfig);

                success = true;
            }
            else
            {
                if (SelectedProfile.Engine == "Xray")
                    success = await _xrayService.ConnectAsync(SelectedProfile.RawConfig, IsProxyMode);
                else
                {
                    var amneziaConfig = ConfigParser.Parse(SelectedProfile.RawConfig);

                    if (amneziaConfig == null)
                    {
                        ConnectionStatus = "Ошибка: конфигурационный файл пуст или повреждён";
                        ActionButtonText = "Подключиться";
                        return;
                    }

                    success = await _vpnService.ConnectAsync(amneziaConfig, SelectedProfile.RawConfig);
                }
            }

            if (success)
            {
                IsConnected = true;
                ActionButtonText = "Отключиться";
                ConnectionStatus = "Подключено";
                StartTimer();
            }
            else
            {
                ConnectionStatus = "Ошибка запуска ядра!";
                ConnectionTimeText = "";
                ActionButtonText = "Подключиться";
            }
        }
        else
        {
            ActionButtonText = "Отключение...";

            if (OperatingSystem.IsAndroid())
                Services.VpnBridge.StopVpnAction?.Invoke();
            else
            {
                if (SelectedProfile.Engine == "Xray")
                    await _xrayService.DisconnectAsync();
                else
                    await _vpnService.DisconnectAsync();
            }

            IsConnected = false;
            ActionButtonText = "Подключиться";
            ConnectionStatus = "Статус: Отключено";
            StopTimer();
        }
    }


    [RelayCommand]
    private void ParseConfig()
    {
        if (string.IsNullOrWhiteSpace(RawConfigText)) 
            return;

        try
        {
            string trimmedText = RawConfigText.Trim();

            if (trimmedText.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
            {
                var result = VlessParser.Parse(trimmedText);

                if (result.ProfileName == "Ошибка")
                {
                    ConnectionStatus = result.JsonConfig;
                    return;
                }

                ProfileName = result.ProfileName;
                RawConfigText = result.JsonConfig;

                if (!Profiles.Any(p => p.Name == ProfileName))
                {
                    var newProfile = new VpnProfile { Name = ProfileName, RawConfig = RawConfigText, Engine = "Xray" };
                    Profiles.Add(newProfile);
                    SaveProfiles();
                    SelectedProfile = newProfile;
                }
                else
                    SelectedProfile = Profiles.FirstOrDefault(p => p.Name == ProfileName);
            }
            else
                ConnectionStatus = "Ошибка, в конфиге не найден сервер!";
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Ошибка: {ex.Message}";
        }

        try
        {
            if (RawConfigText.Trim().StartsWith("vpn://"))
            {
                string base64 = RawConfigText.Trim().Substring(6).Replace("-", "+").Replace("_", "/");
                base64 = System.Text.RegularExpressions.Regex.Replace(base64, @"[^a-zA-Z0-9\+/=]", "");
                int mod4 = base64.Length % 4;

                if (mod4 > 0)
                    base64 += new string('=', 4 - mod4);

                try
                {
                    byte[] data = Convert.FromBase64String(base64);
                    using var ms = new MemoryStream(data); ms.Seek(4, System.IO.SeekOrigin.Begin);
                    using var zlib = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionMode.Decompress);
                    using var result = new MemoryStream(); zlib.CopyTo(result);
                    string json = System.Text.Encoding.UTF8.GetString(result.ToArray());

                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("description", out var desc))
                        ProfileName = desc.GetString() ?? "Мой VPN";
                }
                catch
                {

                }

                RawConfigText = ConfigParser.DecodeAndDecompressAmneziaLink(RawConfigText.Trim());

                if (RawConfigText.StartsWith("ОШИБКА") || RawConfigText.StartsWith("КРИТИЧЕСКАЯ"))
                {
                    ConnectionStatus = "Сбой расшифровки ссылки!";
                    return;
                }
            }

            _activeConfig = ConfigParser.Parse(RawConfigText);

            if (_activeConfig.Peers.Count > 0)
            {
                ConnectionStatus = "Готово к подключению!";
                IsCodeVisible = false;
            }
            else
                ConnectionStatus = "Ошибка: В конфиге не найден сервер!";


            if (_activeConfig != null)
            {
                if (!Profiles.Any(p => p.Name == ProfileName))
                {
                    var newProfile = new VpnProfile
                    {
                        Name = ProfileName,
                        RawConfig = RawConfigText,
                        Engine = "AmneziaWG"
                    };

                    Profiles.Add(newProfile);
                    SaveProfiles();

                    SelectedProfile = newProfile;
                }
                else
                    SelectedProfile = Profiles.FirstOrDefault(p => p.Name == ProfileName);

                IsCodeVisible = false;
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Ошибка: {ex.Message}";
        }
    }
}   