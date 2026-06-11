using Android.App;
using Android.Content;
using Android.Util;

namespace AeroLink.Android
{
    [BroadcastReceiver(Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced })]
    public class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == Intent.ActionBootCompleted || 
                intent.Action == Intent.ActionMyPackageReplaced)
            {
                Log.Info("AeroLinkVPN", "Device boot or package update detected");
                // Could implement auto-reconnect logic here
            }
        }
    }
}
