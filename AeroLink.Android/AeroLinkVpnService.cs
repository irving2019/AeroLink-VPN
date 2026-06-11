using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using Android.Util;
using AndroidX.Core.App;
using System;
using System.Threading.Tasks;
using AeroLink.Models;

namespace AeroLink.Android
{
    [Service(
        Name = "com.CompanyName.AeroLink.AeroLinkVpnService",
        Permission = "android.permission.BIND_VPN_SERVICE", 
        Exported = true,
        ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeSpecialUse)]
    [IntentFilter(new[] { "android.net.VpnService" })]
    public class AeroLinkVpnService : VpnService
    {
        private const int NotificationId = 1;
        private const string ChannelId = "aerolink_vpn_channel";
        private ParcelFileDescriptor _tunInterface;
        private volatile bool _isRunning;

        public override void OnCreate()
        {
            base.OnCreate();
            CreateNotificationChannel();
            VpnStateManager.SetState(VpnConnectionState.Disconnected);
            Log.Info("AeroLinkVPN", "Service created");
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (intent?.Action == "START")
            {
                Log.Info("AeroLinkVPN", "START command received");
                string config = intent.GetStringExtra("CONFIG_TEXT") ?? GetDefaultConfig();
                Task.Run(() => StartVpnInternal(config));
            }
            else if (intent?.Action == "STOP")
            {
                Log.Info("AeroLinkVPN", "STOP command received");
                StopVpn();
            }

            return StartCommandResult.Sticky;
        }

        private void StartVpnInternal(string config)
        {
            try
            {
                if (_isRunning)
                {
                    Log.Warn("AeroLinkVPN", "VPN already running, ignoring restart");
                    return;
                }

                _isRunning = true;
                Log.Info("AeroLinkVPN", "Starting VPN tunnel setup...");

                // Show foreground notification
                StartForeground(NotificationId, BuildNotification("Connecting..."));

                var builder = new VpnService.Builder(this);

                // Session name
                builder.SetSession("AeroLink VPN");

                // Parse config using AeroLink's shared ConfigParser
                AmneziaConfig? parsedConfig = null;
                try
                {
                    parsedConfig = ConfigParser.Parse(config);
                }
                catch (Exception parseEx)
                {
                    Log.Error("AeroLinkVPN", $"Failed to parse VPN config: {parseEx.Message}");
                }

                // Parse and configure MTU (default to 1420 to prevent fragmentation/packet loss)
                int mtu = 1420;
                try
                {
                    var mtuMatch = System.Text.RegularExpressions.Regex.Match(config, @"(?i)^\s*Mtu\s*=\s*(\d+)", System.Text.RegularExpressions.RegexOptions.Multiline);
                    if (mtuMatch.Success && int.TryParse(mtuMatch.Groups[1].Value, out int parsedMtu))
                    {
                        mtu = parsedMtu;
                    }
                }
                catch (Exception mtuEx)
                {
                    Log.Warn("AeroLinkVPN", $"Error parsing MTU from config: {mtuEx.Message}");
                }
                builder.SetMtu(mtu);
                Log.Info("AeroLinkVPN", $"MTU set to: {mtu}");

                // Configure TUN addresses
                bool addedAnyAddress = false;
                if (parsedConfig?.Interface != null && !string.IsNullOrWhiteSpace(parsedConfig.Interface.Address))
                {
                    var addrParts = parsedConfig.Interface.Address.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in addrParts)
                    {
                        var trimmed = part.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        string ip = trimmed;
                        int prefix = 32;

                        int slashIdx = trimmed.IndexOf('/');
                        if (slashIdx >= 0)
                        {
                            ip = trimmed.Substring(0, slashIdx);
                            string prefixStr = trimmed.Substring(slashIdx + 1);
                            if (int.TryParse(prefixStr, out int parsedPrefix))
                            {
                                prefix = parsedPrefix;
                            }
                        }
                        else
                        {
                            if (ip.Contains(':'))
                            {
                                prefix = 128;
                            }
                        }

                        try
                        {
                            builder.AddAddress(ip, prefix);
                            addedAnyAddress = true;
                            Log.Info("AeroLinkVPN", $"Added TUN address: {ip}/{prefix}");
                        }
                        catch (Exception addrEx)
                        {
                            Log.Error("AeroLinkVPN", $"Failed to add TUN address {ip}/{prefix}: {addrEx.Message}");
                        }
                    }
                }

                if (!addedAnyAddress)
                {
                    Log.Warn("AeroLinkVPN", "No valid addresses added from config, using fallback 10.8.0.2/24");
                    builder.AddAddress("10.8.0.2", 24);
                }

                // Configure DNS servers
                bool addedAnyDns = false;
                if (parsedConfig?.Interface != null && !string.IsNullOrWhiteSpace(parsedConfig.Interface.DNS))
                {
                    var dnsParts = parsedConfig.Interface.DNS.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in dnsParts)
                    {
                        var trimmed = part.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        try
                        {
                            builder.AddDnsServer(trimmed);
                            addedAnyDns = true;
                            Log.Info("AeroLinkVPN", $"Added DNS server: {trimmed}");
                        }
                        catch (Exception dnsEx)
                        {
                            Log.Error("AeroLinkVPN", $"Failed to add DNS server {trimmed}: {dnsEx.Message}");
                        }
                    }
                }

                if (!addedAnyDns)
                {
                    Log.Warn("AeroLinkVPN", "No valid DNS servers added, using fallbacks");
                    builder.AddDnsServer("8.8.8.8");
                    builder.AddDnsServer("8.8.4.4");
                    builder.AddDnsServer("1.1.1.1");
                }

                // Configure routes based on peer AllowedIPs
                bool addedAnyRoute = false;
                if (parsedConfig?.Peers != null)
                {
                    foreach (var peer in parsedConfig.Peers)
                    {
                        if (string.IsNullOrWhiteSpace(peer.AllowedIPs)) continue;

                        var routeParts = peer.AllowedIPs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in routeParts)
                        {
                            var trimmed = part.Trim();
                            if (string.IsNullOrEmpty(trimmed)) continue;

                            string routeIp = trimmed;
                            int routePrefix = 32;

                            int slashIdx = trimmed.IndexOf('/');
                            if (slashIdx >= 0)
                            {
                                routeIp = trimmed.Substring(0, slashIdx);
                                string prefixStr = trimmed.Substring(slashIdx + 1);
                                if (int.TryParse(prefixStr, out int parsedPrefix))
                                {
                                    routePrefix = parsedPrefix;
                                }
                            }
                            else
                            {
                                if (routeIp.Contains(':'))
                                {
                                    routePrefix = 128;
                                }
                            }

                            try
                            {
                                builder.AddRoute(routeIp, routePrefix);
                                addedAnyRoute = true;
                                Log.Info("AeroLinkVPN", $"Added TUN route: {routeIp}/{routePrefix}");
                            }
                            catch (Exception routeEx)
                            {
                                Log.Error("AeroLinkVPN", $"Failed to add TUN route {routeIp}/{routePrefix}: {routeEx.Message}");
                            }
                        }
                    }
                }

                if (!addedAnyRoute)
                {
                    Log.Warn("AeroLinkVPN", "No routes added from Peer AllowedIPs, routing all traffic by default");
                    builder.AddRoute("0.0.0.0", 0);
                    builder.AddRoute("::", 0);
                }

                // Disable VPN for this app to prevent loops
                builder.AddDisallowedApplication(this.PackageName);
                builder.AddDisallowedApplication("com.android.systemui");

                // Establish TUN interface
                _tunInterface = builder.Establish();

                if (_tunInterface == null)
                {
                    Log.Error("AeroLinkVPN", "Failed to establish TUN interface!");
                    _isRunning = false;
                    StopForeground(StopForegroundFlags.Remove);
                    StopSelf();
                    return;
                }

                int fd = _tunInterface.Fd;
                Log.Info("AeroLinkVPN", $"TUN interface established with FD: {fd}");

                // Call Go backend with actual config using safe wrapper
                try
                {
                    var result = AeroLinkJniWrapper.StartVpn(fd, config);

                    if (result.IsSuccess)
                    {
                        Log.Info("AeroLinkVPN", $"Go backend success: {result.Message}");

                        // Protect Go sockets from VPN routing loop
                        try
                        {
                            string fdsStr = global::Aerolinkcore.Aerolinkcore.SocketFds ?? "";
                            Log.Info("AeroLinkVPN", $"Go socket FDs to protect: {fdsStr}");
                            if (!string.IsNullOrEmpty(fdsStr))
                            {
                                foreach (var fdStr in fdsStr.Split(','))
                                {
                                    if (int.TryParse(fdStr, out int socketFd))
                                    {
                                        bool protectedOk = Protect(socketFd);
                                        Log.Info("AeroLinkVPN", $"Protecting socket FD {socketFd}: {protectedOk}");
                                    }
                                }
                            }
                        }
                        catch (Exception protectEx)
                        {
                            Log.Error("AeroLinkVPN", $"Failed to protect Go sockets: {protectEx.Message}");
                        }

                        VpnStateManager.SetState(VpnConnectionState.Connected);
                        UpdateNotification("Connected");
                    }
                    else
                    {
                        Log.Error("AeroLinkVPN", $"Go backend error: {result.Message}");
                        if (result.InnerException != null)
                        {
                            Log.Error("AeroLinkVPN", $"Inner exception: {result.InnerException.StackTrace}");
                        }
                        VpnStateManager.ReportError(result.Message);
                        _isRunning = false;
                        StopVpn();
                    }
                }
                catch (Exception goEx)
                {
                    Log.Error("AeroLinkVPN", $"JNI wrapper error: {goEx.Message}\n{goEx.StackTrace}");
                    VpnStateManager.ReportError($"JNI error: {goEx.Message}");
                    _isRunning = false;
                    StopVpn();
                }
            }
            catch (Exception ex)
            {
                Log.Error("AeroLinkVPN", $"VPN startup error: {ex.Message}\n{ex.StackTrace}");
                _isRunning = false;
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
            }
        }

        private void StopVpn()
        {
            try
            {
                Log.Info("AeroLinkVPN", "Stopping VPN...");

                if (_tunInterface != null)
                {
                    try
                    {
                        _tunInterface.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("AeroLinkVPN", $"Error closing TUN: {ex.Message}");
                    }
                    finally
                    {
                        _tunInterface.Dispose();
                        _tunInterface = null;
                    }
                }

                _isRunning = false;
                VpnStateManager.SetState(VpnConnectionState.Disconnected);
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                Log.Info("AeroLinkVPN", "VPN stopped");
            }
            catch (Exception ex)
            {
                Log.Error("AeroLinkVPN", $"Error stopping VPN: {ex.Message}");
                VpnStateManager.ReportError($"Stop error: {ex.Message}");
            }
        }

        public override void OnDestroy()
        {
            Log.Info("AeroLinkVPN", "Service destroyed");
            StopVpn();
            base.OnDestroy();
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(
                    ChannelId,
                    "AeroLink VPN",
                    NotificationImportance.Low)
                {
                    Description = "VPN connection notification"
                };

                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager?.CreateNotificationChannel(channel);
            }
        }

        private Notification BuildNotification(string text)
        {
            var builder = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("AeroLink VPN")
                .SetContentText(text)
                .SetSmallIcon(Resource.Drawable.icon)
                .SetOngoing(true)
                .SetPriority(NotificationCompat.PriorityLow);

            return builder.Build();
        }

        private void UpdateNotification(string text)
        {
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager?.Notify(NotificationId, BuildNotification(text));
        }

        private string GetDefaultConfig()
        {
            return @"[Interface]
Address = 10.8.0.2/24
DNS = 8.8.8.8, 8.8.4.4, 1.1.1.1
PrivateKey = +IPzOmeMC0bfWkLj3fwE/kTCdACPiimOo88F6ROcdzA=
Jc = 4
Jmin = 10
Jmax = 50
S1 = 56
S2 = 108
S3 = 43
S4 = 8
H1 = 147136602-604644934
H2 = 727436620-1417155830
H3 = 1741426169-1852897917
H4 = 1898202819-1951181075
I1 = <r 2><b 0x858000010001000000000669636c6f756403636f6d0000010001c00c000100010000105a00044d583737>

[Peer]
PublicKey = m1y7HcyghL9oIJQc5tzCLCPx1R877ThnBBUr2HrqtAA=
PresharedKey = G7+cdc8DfLdSg+M2MauzHjX86OwF9uB6ACfg/JM8kuo=
AllowedIPs = 0.0.0.0/0, ::/0
Endpoint = wg.ipsec.sbs:39727
PersistentKeepalive = 25";
        }
    }
}