# SysMemClean

**SysMemClean** is an ultra-lightweight Windows utility designed to monitor and optimize physical memory usage in the background. It purges system working sets and standby lists by calling native low-level Windows APIs (`ntdll.dll`).

---

## ⚡ Key Features

* **Kernel-Level Memory Purging:** Calls `NtSetSystemInformation` to clear Working Sets, System Working Sets, Modified Page Lists, Standby Lists, and Priority 0 Standby Lists.
* **Minimal Resource Footprint:** Automatically compresses its own working set (`EmptyWorkingSet`) after every cleaning cycle to use as little RAM as possible.
* **System Tray Mode:** Runs quietly in the notification area (System Tray) alongside the system clock.
* **Flexible Cleaning Triggers:**
  * **Threshold-Based:** Triggers automatic purging when RAM consumption exceeds a specified percentage.
  * **Interval-Based:** Forces periodic memory cleanup at custom time intervals, bypassing the percentage threshold.
* **Persistent Settings:** Automatically saves and restores user configurations via the Windows Registry (`HKCU\SOFTWARE\SysMemClean`).
* **Autostart Option:** Includes an integrated toggle to launch automatically upon Windows startup.

---

## 🛠️ Build & Compilation

No heavy IDE (like Visual Studio) is required. You can compile the executable directly using the native C# compiler included with Windows via the Command Prompt (CMD):

```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /win32manifest:app.manifest /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll Program.cs

Requirements
OS: Windows 7 / 8 / 10 / 11 (64-bit recommended)

Framework: .NET Framework 4.0 or higher (pre-installed on modern Windows versions)

Author
Developed by marco_tch.