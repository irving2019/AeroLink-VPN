using System;

namespace AeroLink.Android
{
    /// <summary>
    /// VPN connection states.
    /// </summary>
    public enum VpnConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3,
        Error = 4
    }

    /// <summary>
    /// Manager for VPN connection state and callbacks.
    /// Provides unified way to track VPN state across the application.
    /// </summary>
    public static class VpnStateManager
    {
        private static volatile VpnConnectionState _currentState = VpnConnectionState.Disconnected;
        private static readonly object _stateLock = new object();

        /// <summary>
        /// Get current VPN state.
        /// </summary>
        public static VpnConnectionState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
        }

        /// <summary>
        /// Called when VPN state changes.
        /// Parameter: VpnConnectionState
        /// </summary>
        public static event Action<VpnConnectionState> StateChanged;

        /// <summary>
        /// Called when VPN error occurs.
        /// Parameter: error message
        /// </summary>
        public static event Action<string> ErrorOccurred;

        /// <summary>
        /// Called when VPN statistics are updated.
        /// Parameters: bytesIn, bytesOut
        /// </summary>
        public static event Action<long, long> StatsUpdated;

        /// <summary>
        /// Set new VPN state and trigger callbacks.
        /// </summary>
        public static void SetState(VpnConnectionState newState)
        {
            lock (_stateLock)
            {
                if (_currentState != newState)
                {
                    _currentState = newState;
                    StateChanged?.Invoke(newState);
                }
            }
        }

        /// <summary>
        /// Report VPN error.
        /// </summary>
        public static void ReportError(string errorMessage)
        {
            SetState(VpnConnectionState.Error);
            ErrorOccurred?.Invoke(errorMessage);
        }

        /// <summary>
        /// Update VPN statistics.
        /// </summary>
        public static void UpdateStats(long bytesIn, long bytesOut)
        {
            StatsUpdated?.Invoke(bytesIn, bytesOut);
        }

        /// <summary>
        /// Reset state to disconnected.
        /// </summary>
        public static void Reset()
        {
            SetState(VpnConnectionState.Disconnected);
        }
    }
}
