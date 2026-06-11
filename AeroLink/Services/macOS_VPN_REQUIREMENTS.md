# macOS VPN Implementation Requirements

## Overview
AeroLink on macOS requires a native implementation using Apple's NetworkExtension framework. The current architecture is Windows/Linux-oriented and cannot be directly adapted for macOS VPN APIs.

## Critical Requirements

### 1. NetworkExtension Framework
macOS VPN functionality REQUIRES the use of NetworkExtension, which provides:
- `NEVPNManager` - for managing VPN configurations
- `NEPacketTunnelProvider` - for packet-level tunneling
- Cannot be implemented in managed .NET code alone

### 2. Separate Extension Project
Create a separate Swift/Objective-C project:
```
AeroLink.macOS.VpnExtension/
  ├── VpnExtension.swift       # Main tunnel provider
  ├── PacketHandler.swift      # Packet processing
  └── Configuration.swift      # VPN setup
```

### 3. Developer Entitlements
Add to entitlements.plist:
```xml
<key>com.apple.developer.networking.networkextension</key>
<true/>
<key>com.apple.developer.networking.vpn</key>
<true/>
```

### 4. Code Signing Requirements
- Apple Developer account required
- Extension must be signed with development certificate
- Main app must be signed with same team ID
- macOS 11.0+ required

### 5. Minimal Swift Implementation Template

```swift
import NetworkExtension

class PacketTunnelProvider: NEPacketTunnelProvider {
	override func startTunnel(options: [String : NSObject]?, completionHandler: @escaping (Error?) -> Void) {
		let settings = NEPacketTunnelNetworkSettings(tunnelRemoteAddress: "10.8.0.1")

		let ipv4Settings = NEIPv4Settings(addresses: ["10.8.0.2"], subnetMasks: ["255.255.255.0"])
		ipv4Settings.configurationMethod = .manual
		settings.ipv4Settings = ipv4Settings

		let dnsSettings = NEDNSSettings(servers: ["8.8.8.8", "1.1.1.1"])
		dnsSettings.matchDomains = [""] // Match all domains
		settings.dnsSettings = dnsSettings

		setTunnelSettings(settings) { error in
			if error == nil {
				completionHandler(nil)
			} else {
				completionHandler(error)
			}
		}
	}

	override func stopTunnel(with reason: NEProviderStopReason, completionHandler: @escaping () -> Void) {
		completionHandler()
	}
}
```

### 6. C# Wrapper for macOS
Create wrapper in `AeroLink/Services/MacOsVpnService.cs`:
- Interact with native extension via IPC or file descriptors
- Handle VPN state management
- Manage error scenarios

### 7. UI Changes for macOS
- Update Avalonia configuration for macOS
- Add macOS-specific settings UI
- Handle permissions prompts

### 8. Testing
Required testing on:
- Intel Mac (10.15+)
- Apple Silicon Mac (11.0+)
- Test wake/sleep, network switching, disconnect/reconnect

## Current Blockers

1. **No Swift/Obj-C Project**: The solution lacks a NetworkExtension provider
2. **Missing Entitlements**: Project not configured with VPN capabilities
3. **No Code Signing**: Extension cannot run without proper signing
4. **Avalonia Scaling Issues**: UI doesn't render correctly on Retina displays

## Implementation Priority

1. Create Swift VPN extension project
2. Implement PacketTunnelProvider
3. Configure entitlements and signing
4. Create C# wrapper for IPC communication
5. Update UI for macOS
6. Test on actual hardware

## Resources
- [Apple NetworkExtension Documentation](https://developer.apple.com/documentation/networkextension)
- [PacketTunnelProvider Guide](https://developer.apple.com/documentation/networkextension/nepackettunnelprovider)
- [VPN Configuration](https://developer.apple.com/documentation/networkextension/nevpnmanager)

## Notes
- NetworkExtension requires user approval via System Preferences
- VPN extension runs in system context with elevated privileges
- Cannot share code with desktop version directly
- Requires separate App Group for IPC
