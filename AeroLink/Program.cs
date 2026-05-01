using Avalonia;
using System;
using AeroLink.Services;

namespace AeroLink
{
    internal sealed class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                VpnCleanup.Execute();
            };

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception)
            {
                VpnCleanup.Execute();
                throw;
            }
            finally
            {
                VpnCleanup.Execute();
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
