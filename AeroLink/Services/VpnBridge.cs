using System;

namespace AeroLink.Services
{
    /// <summary>
    /// Bridge between UI and platform-specific VPN implementations.
    /// Handles Android VPN service communication.
    /// </summary>
    public static class VpnBridge
    {
        /// <summary>
        /// Called when UI requests VPN connection.
        /// Parameter: config text (WireGuard/AmneziaWG configuration).
        /// Android implementation will pass this to AeroLinkVpnService.
        /// </summary>
        public static Action<string> StartVpnAction { get; set; }

        /// <summary>
        /// Called when UI requests VPN disconnection.
        /// Android implementation will stop AeroLinkVpnService.
        /// </summary>
        public static Action StopVpnAction { get; set; }

        /// <summary>
        /// Called to report VPN state changes.
        /// Parameter: state string ("connected", "disconnected", "connecting", "error")
        /// </summary>
        public static Action<string> OnVpnStateChanged { get; set; }

        /// <summary>
        /// Called to report VPN connection errors.
        /// Parameter: error message
        /// </summary>
        public static Action<string> OnVpnError { get; set; }
    }
}
