# GestureDock 🪟⚡

> **The modern Windows 11 Task View companion overlay with 1-click Clear All, Home, draggable positioning, and zero-latency hardware dismissal.**

[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4?logo=windows)](https://microsoft.com/windows)
[![Runtime](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![UIAccess](https://img.shields.io/badge/Security-UIAccess%20Signed-green)](Install-TaskView-Overlay.ps1)

---

## 🌟 Overview

**GestureDock** enhances the native Windows 11 Task View multitasking screen without changing your touchpad gesture settings. 

When you perform your standard **3-finger swipe UP** gesture (or press <kbd>Win</kbd> + <kbd>Tab</kbd>), Windows 11 smoothly transitions into Task View. GestureDock immediately detects this event and presents a sleek, dark frosted-acrylic capsule dock floating seamlessly over Task View:

```
┌────────────────────────────────────────────────────────────────────────┐
│  [✋ ⋮⋮]  │  [ 🏠 Home ]  │  [ 🧹 Clear All ]  │  [ ✕ Close All ]     │
└────────────────────────────────────────────────────────────────────────┘
```

- **✋ Draggable Grip**: Click and drag the hand icon to position the dock anywhere on your monitor. Double-click the grip anytime to reset it back to default.
- **🏠 Home**: Instantly minimizes all open windows and reveals the desktop via Windows native COM Shell dispatcher.
- **🧹 Clear All**: Closes all active user windows immediately at once (including File Explorer windows, PowerShell, Settings, and browser windows).
- **✕ Close All**: Opens an interactive Fluent dialog showing all open applications with checkboxes so you can pick which apps to close.
- **⚡ Zero-Lag Instant Dismissal**: Disappears at the hardware event timestamp (0ms) the moment you click any window card or desktop, eliminating lingering artifacts.

---

## ✨ Features

### 1. 100% Native Gesture Preservation
GestureDock **does not** hook, replace, or alter your Windows Touchpad settings. Your native Windows 11 3-finger swipe up, swipe down, and <kbd>Win</kbd> + <kbd>Tab</kbd> animations remain completely intact and buttery smooth.

### 2. True UIAccess Floating Layer
In Windows 11, Task View is an elevated DirectX composition backdrop hosted by `explorer.exe` in the OS UIAccess Z-Band. Standard windows cannot appear on top of Task View. GestureDock includes a 1-click self-signed UIAccess setup script that grants it genuine UIAccess authority, allowing the dock to float directly on top of Task View without fighting the shell.

### 3. Zero-Latency Hardware-Level Dismissal
Traditional background focus monitors lag behind Windows 11's zoom-in animation when an app is selected. GestureDock installs ultra-fast low-level input hooks (`WH_MOUSE_LL` and `WH_KEYBOARD_LL`):
- The instant you click any app tile or the desktop, GestureDock hides at the raw hardware click timestamp before Windows even begins zooming in.
- Pressing <kbd>Esc</kbd>, <kbd>Enter</kbd>, or <kbd>Win</kbd> immediately dismisses the dock.

### 4. Smooth DPI & Multi-Monitor Support
Fully Per-Monitor V2 DPI aware (`PerMonitorV2`). Coordinates and sizes scale dynamically whether you're using a 1080p display at 100% DPI or a 4K display at 150%/200% scaling.

### 5. Safe Process Isolation
GestureDock will **never** terminate critical system processes:
- `explorer.exe` taskbar, desktop shell, and system trays are strictly protected.
- Only interactive user folder windows (`CabinetWClass`), user terminals (`pwsh`, `powershell`, `cmd`, `WindowsTerminal`), Settings (`SystemSettings`), and third-party apps are closed.
- Graceful `WM_CLOSE` is sent first to protect unsaved work.

---

## 📥 Installation Guide (For Users)

### Quick 1-Click Setup:

1. **Download the Release**:
   Download the latest release ZIP from the [GitHub Releases](https://github.com/Chandan-14r/GestureDock/releases) page.

2. **Extract the ZIP**:
   Extract all contents to a folder on your PC.

3. **Run the Installer**:
   - Right-click **`Install-TaskView-Overlay.bat`** and select **Run as administrator**.
   - The installer will automatically:
     1. Stop any running background instances.
     2. Create and trust a local self-signed Code Signing Certificate for UIAccess in your machine's root certificate store.
     3. Install the application files into `C:\Program Files\GestureDock`.
     4. Digitally sign `GestureDock.exe` with Authenticode UIAccess authorization.
     5. Keep your desktop completely clean without unwanted shortcut clutter (accessible via Task View gesture and system tray).
     6. Register a Windows Task Scheduler task (`GestureDock-AutoStart`) to launch silently on boot/logon with highest privileges.

4. **Enjoy!**
   Swipe up with 3 fingers on your touchpad or press <kbd>Win</kbd> + <kbd>Tab</kbd> to see GestureDock in action!

---

## 🗑️ Uninstallation

To completely remove GestureDock from your system:
1. Right-click **`Uninstall-GestureDock.bat`** and select **Run as administrator**.
2. It will automatically stop running processes, remove the auto-start task, clean up desktop shortcuts, and delete `C:\Program Files\GestureDock`.

---

## 🛠️ Building from Source (For Developers)

### Requirements:
- Windows 10 (Build 19041+) or Windows 11 (22H2 / 23H2 / 24H2)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PowerShell 7+ or Windows PowerShell 5.1

### 1. Clone the Repository:
```powershell
git clone https://github.com/Chandan-14r/GestureDock.git
cd GestureDock
```

### 2. Build the Standard Release:
```powershell
dotnet publish GestureDock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

### 3. Build the UIAccess Release:
1. In `app.manifest`, set `uiAccess="true"`:
   ```xml
   <requestedExecutionLevel level="asInvoker" uiAccess="true" />
   ```
2. Publish to the `publish_uiaccess` folder:
   ```powershell
   dotnet publish GestureDock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish_uiaccess
   ```
3. Revert `app.manifest` back to `uiAccess="false"`.
4. Run the installer:
   ```powershell
   .\Install-TaskView-Overlay.bat
   ```

---

## 🏗️ Architecture & Technical Details

```
GestureDock/
├── App.xaml / App.xaml.cs          # App lifecycle, mutex single-instance, CLI routing
├── app.manifest                    # DPI awareness and UIAccess privilege specification
├── GestureDock.csproj              # .NET 8 WPF single-file project configuration
├── Interop/
│   └── NativeMethods.cs            # Win32 APIs (User32, DwmApi, low-level hooks, window bands)
├── Models/
│   ├── AppSettings.cs              # Settings data structures & theme enumerations
│   └── WindowAppItem.cs            # Representation of open closable windows
├── Services/
│   ├── TaskViewDetector.cs         # Low-overhead (15ms) Task View detection & DWM cloaking filter
│   ├── WindowManager.cs            # Desktop window enumeration, filtering & graceful termination
│   ├── DesktopManager.cs           # Native Shell.Application COM Show Desktop integration
│   ├── OverlayManager.cs           # Multi-monitor positioning, geometry & visibility coordinator
│   ├── SettingsManager.cs          # JSON settings persistence in %AppData%\GestureDock
│   └── StartupManager.cs           # Task Scheduler and desktop shortcut registration
├── UI/
│   ├── Overlay/
│   │   ├── OverlayWindow.xaml      # Frosted acrylic floating pill with drag grip
│   │   ├── OverlayWindow.xaml.cs   # Low-level hooks (WH_MOUSE_LL), drag capture & fast dismiss
│   │   ├── CloseConfirmWindow.xaml # Fluent app selection & review dialog
│   │   └── CloseConfirmWindow.xaml.cs
│   ├── Settings/
│   │   └── SettingsWindow.xaml     # Dock configuration interface
│   ├── Tray/
│   │   └── TrayIconManager.cs      # System tray icon, context menu & quick actions
│   └── Diagnostic/
│       └── DiagnosticWindow.xaml   # Real-time state inspection & event logger
├── icons/                          # Fluent icons (Home, ClearAll, CloseAll, GestureDock)
├── Install-TaskView-Overlay.bat    # 1-Click elevation launcher for installer
├── Install-TaskView-Overlay.ps1    # Automated cert generation, Program Files install & auto-start
├── Uninstall-GestureDock.bat       # 1-Click elevation launcher for uninstaller
└── Uninstall-GestureDock.ps1       # Clean uninstall script
```

### Detection Strategy
Windows 11 hosts Task View inside modern XAML island windows:
- **Class**: `XamlExplorerHostIslandWindow`
- **Window Title**: `Task View` / `Task Switching`
- **Owning Process**: `explorer.exe`

`TaskViewDetector` attaches its background thread to the active interactive input desktop (`OpenInputDesktop` + `SetThreadDesktop`), polling foreground window transitions at 15ms intervals (<0.01% CPU). It verifies `DwmGetWindowAttribute` with `DWMWA_CLOAKED` so that dismissing Task View via swipe-down or background clicks is registered immediately.

---

## 📜 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

Developed with ❤️ by [Chandan-14r](https://github.com/Chandan-14r).
