# 🚀 OpenKVM v5.0: Enterprise Shield Edition

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![Framework: .NET 7.0](https://img.shields.io/badge/Framework-.NET%207.0-purple.svg)]()

**OpenKVM** is an ultra-lightweight, high-performance, zero-dependency network KVM switch written natively in C#. It allows you to seamlessly share a single mouse and keyboard across multiple separate computers over a local network, bypassing the bloat, telemetry, and performance overhead of modern Electron-based options.

The **v5.0 Enterprise Shield Edition** introduces native cryptographic packet scrambling, absolute coordinate mapping for mixed-resolution monitors, automated network node discovery, and robust kernel hook safeguards.

---

## ⚡ Core Features

* **Dual-Channel Low-Level Hijacking (`WH_MOUSE_LL` & `WH_KEYBOARD_LL`):** Intercepts and swallows local hardware events at the OS kernel boundary when operating in remote mode, streaming inputs directly across your router.
* **Topographical Monitor Positioning:** Supports standard screen edge boundary mapping. Route your mouse across the **Right, Left, Top, or Bottom** border walls to seamlessly leap onto the secondary rig.
* **Absolute Coordinate Normalization:** Converts screen vectors into an absolute percentage coordinate scale between `0` and `65535`. This guarantees flawless cursor tracking when mixing a primary 4K master rig with a 1080p target laptop.
* **Drag-and-Drop TCP File Streaming:** Drop any file directly onto the OpenKVM program panel. The Master serializes the file structure, pumps it over a high-speed TCP lane, and the Slave reconstructs it instantly on your remote **Desktop**.
* **Asynchronous Text Clipboard Synchronization:** Copy text on your primary machine, cross the screen border threshold, and paste it instantly on your target computer.
* **Automated UDP Node Beaconing:** Slave units broadcast passive identity discovery packets over the local submask (`255.255.255.255`). Master rigs catch these beacons and map target connections automatically—no manual IP typing required.
* **System Tray Minimization Overlay:** Clicking **✕** cleanly background-tasks the service directly into your native Windows notification system tray, freeing up your desktop real estate.

---

## 🔒 Security & Guardrail Profile

OpenKVM implements strict defense-in-depth measures to protect your physical system inputs and private local data over the air:

### 🛡️ Symmetric Stream Cipher Engine
Every operational packet (mouse trajectories, button states, file fragments, and virtual keyboard keycodes) is passed through a rolling symmetric XOR cipher utilizing a Pre-Shared Key (PSK). Plaintext keystrokes are scrambled in cache memory before hitting your local network adapters, completely mitigating packet-sniffing or credential-harvesting vulnerabilities from malicious entities on your Wi-Fi network.

### ⏱️ Fail-Safe Heartbeat Watchdog
If your local router drops connection or the Slave computer locks up while your master cursor is hidden, a dedicated watchdog monitor triggers. If a lightweight "Keep-Alive" heartbeat ping fails to clear every 1500ms, OpenKVM instantly detaches all global OS hooks, sets `remoteMode = false`, and restores full hardware control to your main desktop screen.

### 👑 Manifest Privilege Escalation
OpenKVM automatically requests Windows administrative elevation on boot. This ensures its low-level hooks possess the security clearance required to operate inside system-protected windows like **Task Manager, Command Prompt, or games running with Administrator rights**.

---

## 💻 Modding & Open Source Extension Hooks

OpenKVM is designed to be fully moddable. If you want to customize coordinate mutations, add custom macro integrations, or inject alternative packet processing filters, you do not need to rewrite the driver loop. Simply hook your custom code directly into these explicit entry points inside `Program.cs`:

```csharp
private void ModHook_BeforePacketSend(ref byte action, ref int x, ref int y)
{
    // Write your custom Master modifications here before shipping across routers
}

private void ModHook_AfterPacketReceived(ref byte action, ref int x, ref int y)
{
    // Write your custom Slave processing overrides here immediately after unpacking
}
```
💾 Installation & Local Deployment
Option A: The Windows Setup Wizard (Recommended)
Download OpenKVM_v5.0_Setup.exe from the latest release thread.

Run the installer (Accept the UAC Administrator credential prompt).

Follow the wizard configuration deck to launch the service.

Option B: Compiling from Source
Ensure you have the .NET 7.0 SDK installed on your terminal ecosystem.

DOS
# Clone the repository workspace
git clone [https://github.com/MintyLiteralGod/OpenKVM.git](https://github.com/MintyLiteralGod/OpenKVM.git)
cd OpenKVM

# Compile a self-contained standalone optimized single-file binary release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=true
❓ Frequently Asked Questions (FAQ)
🔴 Why does OpenKVM request Administrator privileges on startup?
Windows security policies explicitly block standard user-level hooks from injecting inputs or monitoring keystrokes when an elevated window (such as a command prompt, installer, or Task Manager) is active on the screen. Running OpenKVM as Admin guarantees that your mouse and keyboard will never lock up or stop responding when navigating sensitive windows.

🔴 Do I need to manually configure my IP addresses?
No. If the Slave Receiver is turned on and running first, OpenKVM’s active background mDNS beaconing loop will broadcast its presence. The Master Station will automatically pick up the slave node over your local router and populate the target interface address space automatically.

🔴 Is it safe to type my passwords while OpenKVM is active?
Yes. Thanks to the integrated cryptographic framework layer, all keystroke frames are encrypted using a fast, allocation-free symmetric stream cipher before being broadcast onto your local router channels. Ensure both your Master and Slave units are pointing to the exact same Pre-Shared Key (PSK).

🔴 Does this program require an active internet connection?
Absolutely not. OpenKVM operates completely offline, processing commands locally over your home router subnet. No telemetry, usage data, or configuration parameters are ever uploaded to any cloud server space.

📄 License
Distributed under the MIT License. See LICENSE for more information.

Developed openly by MintyLiteralGod.
