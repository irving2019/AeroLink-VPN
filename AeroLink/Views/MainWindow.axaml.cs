using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Diagnostics;
using System.IO;
using AeroLink.ViewModels;

namespace AeroLink.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 1. Прячем окно в трей при нажатии на крестик
        Closing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
        };

        // 2. Создаем иконку трея прямо здесь, без XAML!
        var trayIcon = new TrayIcon
        {
            Icon = this.Icon, // Берем иконку самолета от окна
            ToolTipText = "AeroLink VPN"
        };

        var menu = new NativeMenu();

        var showItem = new NativeMenuItem("Развернуть");
        showItem.Click += Show_Click;
        menu.Items.Add(showItem);

        var exitItem = new NativeMenuItem("Выход");
        exitItem.Click += Exit_Click;
        menu.Items.Add(exitItem);

        trayIcon.Menu = menu;

        // Регистрируем трей в системе
        var trayIcons = new TrayIcons { trayIcon };
        TrayIcon.SetIcons(Application.Current!, trayIcons);
    }

    private void Show_Click(object? sender, EventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async void Exit_Click(object? sender, EventArgs e)
    {
        // 1. Отключаем активный VPN
        if (DataContext is MainWindowViewModel vm && vm.IsConnected)
        {
            if (vm.ToggleConnectionCommand.CanExecute(null))
                vm.ToggleConnectionCommand.Execute(null);
        }

        // 2. Добиваем службу Amnezia (твой код из OnClosing)
        try
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core", "amneziawg.exe");
            var processInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "/uninstalltunnelservice temp",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(processInfo);
        }
        catch { }

        // 3. Полностью закрываем программу
        Environment.Exit(0);
    }

    // Твой оригинальный рабочий метод выбора конфигурации!
    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите файл конфигурации",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(".conf") { Patterns = new[] { "*.conf" } } }
        });

        if (files.Count >= 1)
        {
            await using var stream = await files[0].OpenReadAsync();
            using var streamReader = new StreamReader(stream);
            var fileContent = await streamReader.ReadToEndAsync();

            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.RawConfigText = fileContent;
                viewModel.ParseConfigCommand.Execute(null);
            }
        }
    }
}