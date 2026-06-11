using Android.App;
using Android.Content; 
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using AeroLink;
using AeroLink.Services;

namespace AeroLink.Android
{
    [Activity(
    Label = "AeroLink",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity<App>
    {
        private string _pendingConfig;

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder).WithInterFont();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Setup VPN state callbacks
            VpnStateManager.StateChanged += (state) =>
            {
                Log.Info("AeroLinkVPN", $"VPN state changed to: {state}");
            };

            VpnStateManager.ErrorOccurred += (error) =>
            {
                Log.Error("AeroLinkVPN", $"VPN error: {error}");
            };

            VpnBridge.StartVpnAction = (configText) =>
            {
                RunOnUiThread(() =>
                {
                    Log.Info("AeroLinkVPN", "START command received in MainActivity");
                    _pendingConfig = configText;
                    VpnStateManager.SetState(VpnConnectionState.Connecting);

                    var intent = global::Android.Net.VpnService.Prepare(this);

                    if (intent != null)
                    {
                        Log.Info("AeroLinkVPN", "Requesting VPN permission from user");
                        StartActivityForResult(intent, 0);
                    }
                    else
                    {
                        Log.Info("AeroLinkVPN", "VPN permission already granted, starting service");
                        StartAeroLinkService(configText);
                    }
                });
            };

            VpnBridge.StopVpnAction = () =>
            {
                RunOnUiThread(() =>
                {
                    Log.Info("AeroLinkVPN", "STOP command received in MainActivity");
                    VpnStateManager.SetState(VpnConnectionState.Disconnecting);

                    var intent = new Intent(this, typeof(AeroLinkVpnService));
                    intent.SetAction("STOP");
                    StartService(intent);
                });
            };
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == 0 && resultCode == Result.Ok)
            {
                Log.Info("AeroLinkVPN", "👉 Пользователь нажал ОК! Запускаем ядро.");
                StartAeroLinkService(_pendingConfig);
            }
            else if (requestCode == 0)
            {
                Log.Warn("AeroLinkVPN", "❌ Пользователь ОТКЛОНИЛ разрешение на VPN.");
            }
            base.OnActivityResult(requestCode, resultCode, data);
        }

        private void StartAeroLinkService(string config)
        {
            var intent = new Intent(this, typeof(AeroLinkVpnService));
            intent.SetAction("START");
            intent.PutExtra("CONFIG_TEXT", config);
            StartService(intent);
        }
    }
}