# 🚀 OpenKVM v5.0: Enterprise Shield Edition

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![Framework: .NET 7.0](https://img.shields.io/badge/Framework-.NET%207.0-purple.svg)]()
[![Security: Symmetric Cipher](https://img.shields.io/badge/Security-Encrypted%20Bus-green.svg)]()

**OpenKVM** is an ultra-lightweight, high-performance, zero-dependency network KVM software suite written natively in C#. It allows you to seamlessly share a single physical mouse and keyboard across multiple separate computers over a local network ecosystem. 

By bypassing the heavy resource bloat, background telemetry, and micro-stuttering common in modern Electron-based utilities, OpenKVM delivers near zero-latency peripheral responses directly inside production, flight-simulation, and gaming environments.

---

## 📐 System Architecture

OpenKVM operates symmetrically. You run the exact same binary executable on both machines, setting one as the **Master Workstation** and the other as the **Slave Target Node**.
```
[ MASTER WORKSTATION ]                         [ SLAVE TARGET NODE ]
+------------------------+                     +-----------------------+
|  Low-Level OS Hooks    |                     |  UDP Listener Port    |
| (WH_MOUSE & KEYBOARD)  |                     |        (5555)         |
+-----------+------------+                     +-----------+-----------+
|                                              ^
|  1. Scramble via PSK                         | 3. Descramble via PSK
v                                              | 4. Inject via Win32 API
+-----------+------------+                     +-----------+-----------+
| High-Speed Encrypted   |=[ WiFi/LAN ]=>|  Cursor & Keystroke   |
|   UDP Stream Packet    |     Local Subnet    |   Hardware Injection  |
+------------------------+                     +-----------------------+
|                                              |
+<======= Heartbeat Watchdog ============+
```

---

## ⚡ Core Feature Deep-Dive

* **Dual-Channel Kernel Interception:** Intercepts hardware events at the OS kernel boundary using native global low-level hooks (`WH_MOUSE_LL` and `WH_KEYBOARD_LL`). When active, inputs are swallowed locally and piped directly onto your home network router bus.
* **Topographical Monitor Positioning:** Maps physical layouts with 4-way directionality. Select whether your secondary screen sits to the **Right, Left, Top, or Bottom** of your master monitor to define exactly which border wall triggers the screen transition.
* **Absolute Coordinate Normalization:** Translates screen coordinate points into an absolute mathematical scale ranging between `0` and `65535`. This ensures consistent cursor tracking when bridging mismatched displays, such as a 4K desktop monitor traversing onto a 1080p laptop screen.
* **Drag-and-Drop TCP File Sharing:** Simplifies file sharing down to a simple gesture. Drag any asset or document path from your master machine and drop it directly onto the OpenKVM application window. The engine securely streams the byte arrays over an independent TCP socket on port `5557`, automatically saving the file directly onto the remote Slave's **Desktop**.
* **Asynchronous Text Clipboard Syncing:** Bridges clipboard buffers instantly. Copying text on your master workstation automatically transmits the payload across the boundary line, allowing you to hit paste on the secondary computer with zero manual steps.
* **Automated UDP Beaconing Discovery:** Eliminates the frustration of typing local IPv4 addresses. Slave units broadcast passive identification tokens out onto your local submask address space (`255.255.255.255`). Master rigs actively listen for these beacons on port `5558` and instantly auto-populate the connection targeted field.
* **System Tray Minimization Engine:** Backgrounds itself without taking up space. Closing the application interface frame intercepts the termination request, hiding the panel from your taskbar while keeping the underlying driver bus alive inside the Windows System Tray.

---

## 🔒 Enterprise Guardrail & Security Blueprint

OpenKVM is engineered from the ground up with defensive security guardrails to keep your private keystrokes and data isolated over local wireless channels:

### 🛡️ Allocation-Free Symmetric Cipher Engine
Every data frame traversing the local network is pushed through an inline rolling symmetric cipher utilizing a user-defined Pre-Shared Key (PSK). Keystrokes, mouse operations, and file payloads are scrambled directly inside secure RAM before broadcasting over network cards. This renders the data useless against local network sniffing tools or credential-harvesting vulnerabilities on shared home networks.

### ⏱️ Fail-Safe Watchdog Heartbeat
If your local router stumbles, power-cycles, or the Slave computer crashes while inputs are being swallowed by the Master, your local mouse will not be left permanently frozen. OpenKVM runs a high-priority background watchdog thread. If the Slave node fails to clear a lightweight keep-alive heartbeat confirmation packet within `1500ms`, the Master forces an emergency fallback event—reclaiming local input streams instantly.

### 👑 Programmatic UAC Manifest Escalation
OpenKVM automatically tests and escalates its local process token to full Windows Administrative elevation upon initialization. This security architecture ensures that low-level hardware hooks retain the necessary clearance to execute inputs even when crossing into protected system environments, such as **Task Manager, Command Prompt, or games running with Administrator rights**.

---

## 🔧 Modding & Custom Extensions

OpenKVM is built for customization. If you or your community want to add macro triggers, manipulate coordinate maps, or include alternative data serialization logic, you do not need to touch the foundational networking drivers. Simply inject your custom C# code directly into these explicit modifier hooks:

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
### 📦 Installation & Setup Guide
#Method A: Automated Windows Setup Wizard (Recommended)
Navigate to the Releases tab on this repository page.

Download the unified executable installer package: OpenKVM_v5.0_Setup.exe.

Launch the installer, accept the Windows UAC elevation prompt, and follow the wizard instructions to place short-cut registries onto your Desktop and Start Menu.

#Method B: Compiling Direct From Source
Ensure your local development terminal environment possesses the native .NET 7.0 SDK configuration tier.

### DOS
``` # 1. Clone the master repository manifest
git clone [https://github.com/MintyLiteralGod/OpenKVM.git](https://github.com/MintyLiteralGod/OpenKVM.git)
cd OpenKVM

# 2. Compile an independent, compressed, standalone production single-file release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=true

# 3. Locate your compiled distribution assembly binary
cd bin\Release\net7.0-windows\win-x64\publish\
dir
```
# ❓ Frequently Asked Questions (FAQ)
Q: Why does OpenKVM request Administrator privileges immediately when I open it?
A: Windows security layers explicitly prohibit low-level user-tier keyboard hooks from tracking or injecting input frames when an elevated program (like Task Manager, an installation wizard, or an Admin terminal) gains system focus. Running OpenKVM as Admin ensures your mouse and keyboard transitions remain operational no matter what application window is currently active.

Q: Do I need an active internet connection or account to run OpenKVM?
A: Absolutely not. OpenKVM runs completely offline over your local home router subnets. No telemetry, usage tracking parameters, or configuration properties are ever transmitted to external servers.

Q: Is it safe to enter passwords or credit cards through OpenKVM?
A: Yes. Unlike generic network sharing tools that ship inputs via clear plaintext, OpenKVM scrambles every keystroke via an integrated allocation-free symmetric stream cipher. Ensure you assign a strong, identical Pre-Shared Key (PSK) inside the user fields of both machines to securely align the cryptographic states.

Q: Why isn't my mouse returning to my Master PC when I swipe back?
A: Ensure your Monitor Location setting matches your physical desk layout. For example, if you mapped your slave display to sit on the Right Border, you must swipe your mouse hard to the Left against the left boundary wall of your Slave monitor to return the pointer focus back to your Master screen.

# 📄 License
Distributed completely under the open-source MIT License. Check out the LICENSE file for extended text details.

Maintained and published openly by MintyLiteralGod.
