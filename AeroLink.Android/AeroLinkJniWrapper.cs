using System;
using System.Runtime.InteropServices;

namespace AeroLink.Android
{
    /// <summary>
    /// JNI wrapper for Go-based AeroLink VPN backend (Aerolinkcore).
    /// Provides safe marshaling between managed and native code.
    /// </summary>
    public static class AeroLinkJniWrapper
    {
        /// <summary>
        /// Result of a VPN operation.
        /// </summary>
        public class VpnResult
        {
            public bool IsSuccess { get; set; }
            public string Message { get; set; }
            public Exception InnerException { get; set; }

            public VpnResult(bool success, string message, Exception ex = null)
            {
                IsSuccess = success;
                Message = message;
                InnerException = ex;
            }

            public override string ToString()
            {
                return $"{(IsSuccess ? "SUCCESS" : "FAILED")}: {Message}";
            }
        }

        /// <summary>
        /// Start VPN tunnel using Go backend.
        /// </summary>
        /// <param name="tunFileDescriptor">File descriptor of TUN interface</param>
        /// <param name="configText">WireGuard/AmneziaWG configuration text</param>
        /// <returns>VPN operation result</returns>
        public static VpnResult StartVpn(int tunFileDescriptor, string configText)
        {
            if (tunFileDescriptor < 0)
            {
                return new VpnResult(false, "Invalid file descriptor", null);
            }

            if (string.IsNullOrWhiteSpace(configText))
            {
                return new VpnResult(false, "Configuration is empty", null);
            }

            try
            {
                string response = global::Aerolinkcore.Aerolinkcore.StartVPN(tunFileDescriptor, configText);

                bool success = response?.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase) ?? false;
                return new VpnResult(success, response ?? "No response", null);
            }
            catch (Exception ex)
            {
                return new VpnResult(false, $"JNI call failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Stop VPN tunnel.
        /// </summary>
        /// <returns>VPN operation result</returns>
        public static VpnResult StopVpn()
        {
            try
            {
                string response = global::Aerolinkcore.Aerolinkcore.StopVPN();

                bool success = response?.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase) ?? 
                               response?.StartsWith("STOPPED", StringComparison.OrdinalIgnoreCase) ?? false;

                return new VpnResult(success, response ?? "No response", null);
            }
            catch (Exception ex)
            {
                return new VpnResult(false, $"JNI call failed: {ex.Message}", ex);
            }
        }
    }
}
