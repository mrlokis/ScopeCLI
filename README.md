# ⚡ ScopeCLI

**ScopeCLI** is an ultra‑optimized Minecraft launcher that runs entirely in the command line.  
No heavy GUIs, no wasted resources – just a clean, fast terminal interface with full control over your game.

---

## ✨ Features

- ✅ **Interactive CLI & TUI** – choose nickname, mod loader, game version, and mod list right in the terminal.
- ✅ **Headless / Automation CLI Mode** – launch profiles, list configurations, or run optimizations directly via command-line arguments.
- ✅ **Automatic Mod Loader Installation** – full support for **Vanilla**, **Forge**, **NeoForge**, and **Fabric**.
- ✅ **Mod List Support** – download multiple mods at once from a simple text file (local or remote URL).
- ✅ **Parallel Downloads with Fixed Slots** – downloads are throttled to prevent socket exhaustion and status bar jumping.
- ✅ **Profile Isolation & Persistence** – configs and mods can be isolated per-profile, preventing mod conflicts.
- ✅ **Safe & Elevated RAM Optimization** – working set trimming frees memory without closing apps; elevated mode can optimize background processes.
- ✅ **Smart Relaunch** – preserves your active profile settings when elevating to administrator privileges.
- ✅ **System Memory Monitoring** – visual indicators for free RAM and warnings before launching.

---

## 📦 Installation (Windows PowerShell)

Run this single command in **PowerShell 5.1 or higher** (recommended: Run as Administrator):

```powershell
powershell.exe -ExecutionPolicy Bypass -Command "iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/mrlokis/ScopeCLI/refs/heads/main/installer.ps1'))"
```

> 💡 If you use PowerShell Core (v7+), the command may differ.  
> After installation, a shortcut to ScopeCLI will be added to your Start Menu and Desktop – use it to launch the program.

---

## 🚀 Getting Started

### 1. Interactive Mode (TUI)
Simply run `ScopeCLI` without arguments:
```powershell
ScopeCLI
```
You will be guided through an interactive setup:
1. **Choose Profile** – select an existing profile or create a new one.
2. **Select Mod Loader** – `Vanilla`, `Forge`, `NeoForge`, or `Fabric`.
3. **Provide Mod List** – a URL or local file path (drag & drop works!).
4. **Enter Game Version** – e.g. `1.20.1` (the launcher handles loader installation automatically).
5. **Memory Optimization** – choose whether to apply memory-saving tweaks.
6. **Launch Game** – game starts with your chosen settings and allocated RAM.

### 2. Command-Line Arguments (Headless Mode)
You can automate or script launches with CLI commands:

```powershell
# Launch an existing profile
ScopeCLI launch --profile "MyProfile"

# Launch with server auto-connect and specific RAM
ScopeCLI launch --profile "MyProfile" --server "mc.example.com:25565" --ram 4096 --optimize

# List all profiles
ScopeCLI profiles list

# View or delete a profile
ScopeCLI profiles show "MyProfile"
ScopeCLI profiles delete "MyProfile"

# Run standalone memory optimization
ScopeCLI optimize
ScopeCLI optimize --aggressive

# View help
ScopeCLI --help
```

---

## 🔧 Mod List Format

The mod list must be a plain text file with one mod per line, in the format:

```
filename.jar|https://example.com/mod.jar
AnotherMod.jar|https://example.com/another.jar
```

Lines starting with `#` are ignored.  
You can also provide a **URL** pointing to such a file – the launcher will download it automatically.

> 📂 **Tip:** just drag & drop your `.txt` file onto the console window – the path will be pasted automatically.

---

## ⚙ Global RAM Optimization

When you enable optimization, the launcher frees up RAM before starting Minecraft:

- **Safe Mode:** trims the working set of running processes and triggers garbage collection. This safely recovers memory without closing any user applications.
- **Aggressive (Admin) Mode:** terminates non-whitelisted high-memory processes using a safety whitelist (`System.psl` and `Social.psl`).
- When elevated privileges are needed, the launcher restarts itself as Administrator while preserving your active profile settings.

---

## 🧱 Current Status & Architecture

### Architecture
- **Dependency Inversion & Service Abstractions:**
  - `IProfileService` – Profile CRUD, persistence, state archiving/restoring.
  - `ILauncherService` – Minecraft launch and Forge/NeoForge/Fabric automated setup.
  - `IModSynchronizer` – Mod download queue with concurrency throttling and orphan cleanup.
  - `ISystemOptimizer` – Safe memory working set trimming and process management.
  - `IArchiveService` – Zip-Slip protected configuration and mod archiving.
  - `IConsoleUi` – Centralized Spectre.Console rendering and non-interactive shell fallback.

---

## 📄 License

This project is licensed under the MIT License – see the [LICENSE](LICENSE) file for details.

---

**ScopeCLI** – Minecraft, the way it should be: fast, lean, and under your command.
