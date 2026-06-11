using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AeroLink.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using System.IO;
using System;

namespace AeroLink.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null && topLevel.InputPane != null)
            {
                topLevel.InputPane.StateChanged += InputPane_StateChanged;
                
                // If keyboard is already open, adjust layout immediately
                var occludedRect = topLevel.InputPane.OccludedRect;
                if (occludedRect.Height > 0)
                {
                    AdjustLayout(occludedRect.Height);
                }
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null && topLevel.InputPane != null)
            {
                topLevel.InputPane.StateChanged -= InputPane_StateChanged;
            }
            base.OnDetachedFromVisualTree(e);
        }

        private void InputPane_StateChanged(object? sender, InputPaneStateEventArgs e)
        {
            AdjustLayout(e.EndRect.Height);
        }

        private void AdjustLayout(double keyboardHeight)
        {
            var grid = this.FindControl<Grid>("MainContentGrid");
            if (grid != null)
            {
                if (keyboardHeight > 0)
                {
                    grid.Margin = new Thickness(20, 20, 20, keyboardHeight);
                }
                else
                {
                    grid.Margin = new Thickness(20);
                }
            }
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