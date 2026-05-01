using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AeroLink.ViewModels;
using Avalonia.Controls;
using System.IO;

namespace AeroLink.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        public async void OpenFileButton_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите конфиг VPN",
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();

                if (DataContext is MainWindowViewModel vm)
                {
                    vm.RawConfigText = content;

                    vm.ParseConfigCommand.Execute(null);
                }
            }
        }
    }
}