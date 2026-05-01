using System;

namespace AeroLink.Services
{
    public static class VpnBridge
    {
        public static Action<string> StartVpnAction { get; set; }

        public static Action StopVpnAction { get; set; }
    }
}
