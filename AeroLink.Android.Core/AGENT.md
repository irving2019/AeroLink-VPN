# AeroLink VPN — Recovery Agent Instructions

Repository:
https://github.com/irving2019/AeroLink-VPN

Goal:
Transform AeroLink VPN into a fully working cross-platform VPN client.

Primary targets:
1. Android VPN support
2. macOS VPN support
3. Stabilize Avalonia UI
4. Refactor backend architecture

---

# CURRENT CRITICAL ISSUES

## Android

Symptoms:

- VPN key icon appears
- UI shows "Connected"
- No internet access
- No traffic passes through tunnel

Root causes likely include:

- Missing proper VpnService implementation
- Missing protect(fd)
- No tun2socks routing
- Go backend not integrated correctly
- No JNI bridge
- Incorrect Android VPN lifecycle handling
- Desktop-oriented architecture incorrectly reused on Android

---

## macOS

Symptoms:

- Window opens incorrectly
- Broken/scaled UI
- No VPN connection
- No active tunnel

Root causes likely include:

- Missing NetworkExtension implementation
- Missing PacketTunnelProvider
- No Apple entitlements
- No signing/notarization
- Avalonia macOS scaling issues

---

# REQUIRED ARCHITECTURE

---

# ANDROID REQUIREMENTS

## MUST IMPLEMENT

### 1. Native Android VPN Service

Create:

```kotlin
class AeroVpnService : VpnService()

Requirements:

establish TUN interface
configure routing
configure DNS
foreground service
reconnect handling
background persistence
2. Proper TUN Configuration

Required:

builder.addAddress("10.0.0.2", 24)
builder.addRoute("0.0.0.0", 0)
builder.addDnsServer("1.1.1.1")

Must support:

IPv4
IPv6
DNS routing
3. protect(fd)

MANDATORY.

Without this Android creates VPN loops.

Required:

vpnService.protect(socket)

or fd-based protect.

4. Replace Desktop Go Binary Approach

Current architecture incorrectly uses desktop binaries.

Android requires:

go build -buildmode=c-shared

or:

gomobile bind

Expected output:

libaerolink.so
5. JNI Bridge

Implement communication between:

Kotlin ↔ Go backend

Functions required:

start tunnel
stop tunnel
reconnect
statistics
logs
state callbacks
6. tun2socks / sing-box Integration

Current routing stack is incomplete.

Implement one of:

sing-box
tun2socks
gVisor stack

Without this:

traffic does not pass
TCP/UDP fails
DNS leaks occur
7. Android Lifecycle Stability

Fix:

VPN dying in background
Android process kills
service recreation

Required:

foreground notification
sticky service
boot reconnect support
network change handling
8. Android Permissions

Ensure:

<uses-permission android:name="android.permission.INTERNET"/>
<uses-permission android:name="android.permission.FOREGROUND_SERVICE"/>
<uses-permission android:name="android.permission.POST_NOTIFICATIONS"/>

And:

<service
    android:name=".AeroVpnService"
    android:permission="android.permission.BIND_VPN_SERVICE">
macOS REQUIREMENTS
MUST IMPLEMENT
1. Native macOS VPN Layer

DO NOT use Linux-style TUN logic.

macOS requires:

NetworkExtension
PacketTunnelProvider

Use:

Swift
Objective-C bridge if needed
2. PacketTunnelProvider

Implement:

class PacketTunnelProvider: NEPacketTunnelProvider

Required features:

establish tunnel
route traffic
DNS
reconnect
tunnel monitoring
3. Entitlements

Add:

com.apple.developer.networking.networkextension

Without this VPN cannot function.

4. Signing + Notarization

Required:

Apple Developer signing
notarization
hardened runtime

Without signing:

tunnel extensions fail
VPN API blocked
5. Fix Avalonia macOS UI

Current issues:

broken scaling
uneven window
Retina issues
bad margins

Required:

update Avalonia to latest stable
fix scaling
native toolbar support
macOS-specific layout fixes

Suggested:

<UseMacOSNativeToolbar>true</UseMacOSNativeToolbar>
BACKEND REQUIREMENTS

Refactor backend into platform abstraction.

Required protocol support:

VLESS
VMESS
Xray
WireGuard
AmneziaWG
REQUIRED BACKEND API

Implement unified API:

connect()
disconnect()
reconnect()
getStats()
getLogs()
getState()
LOGGING REQUIREMENTS

Android:

adb logcat integration

macOS:

Console.app logging

Desktop:

structured logging
UI REQUIREMENTS

Implement:

responsive layout
Retina support
mobile support
dark/light themes
proper DPI scaling
TESTING REQUIREMENTS

Android:

Test on:

Android 10+
Samsung
Xiaomi
Pixel
Huawei

Must verify:

internet access
reconnect
sleep/wake
mobile/wifi switching

macOS:

Test on:

Intel Macs
Apple Silicon
macOS 13+

Must verify:

tunnel establishment
reconnect
sleep/wake recovery
IMPORTANT NOTES

Current architecture is desktop-first and unsuitable for Android/macOS VPN APIs.

Android and macOS REQUIRE native VPN integration.

Do NOT attempt to reuse Linux tunnel logic directly.

RECOMMENDED FINAL STACK

Desktop:

Avalonia
Go backend

Android:

Kotlin
Jetpack Compose
VpnService
JNI
Go shared library
sing-box

macOS:

Swift
NetworkExtension
PacketTunnelProvider
SUCCESS CONDITIONS

Android:

VPN key appears
internet works
traffic routed
reconnect stable
background stable

macOS:

tunnel connects
traffic routed
UI stable
reconnect works
PRIORITY ORDER
Android VPN functionality
Android routing
Android lifecycle
macOS VPN implementation
macOS UI fixes
Backend refactor
UI polish

И ещё лучше — положить рядом второй файл:

# QUESTION.md

```md
# Critical Problems To Solve

## Android

Problem:
VPN shows connected but no internet works.

Likely causes:
- Missing protect(fd)
- Missing tun2socks
- Broken VpnService integration
- No JNI bridge
- Go desktop binary reused incorrectly

Tasks:
- Implement real Android VPN stack
- Implement routing
- Implement DNS handling
- Implement reconnect handling

---

## macOS

Problem:
UI opens but VPN never connects.

Likely causes:
- No PacketTunnelProvider
- Missing NetworkExtension
- Missing entitlements
- Unsigned extension

Tasks:
- Implement native PacketTunnelProvider
- Configure entitlements
- Add signing/notarization
- Fix Avalonia Retina scaling

---

## Avalonia Issues

Problem:
Broken window scaling and layout on macOS.

Tasks:
- Update Avalonia
- Fix Retina DPI
- Fix layout margins
- Add native toolbar support

---

## Backend Problems

Problem:
Desktop architecture reused for mobile systems.

Tasks:
- Refactor backend abstraction
- Add platform-specific adapters
- Create JNI bindings
- Implement unified tunnel API