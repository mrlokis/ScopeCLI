# ⚡ ScopeCLI

**ScopeCLI** is an ultra‑optimized Minecraft launcher that runs entirely in the command line.  
No heavy GUIs, no wasted resources – just a clean, fast terminal interface with full control over your game.

---

## ✨ Features

- ✅ **Interactive CLI** – choose nickname, mod loader, game version, and mod list right in the terminal.
- ✅ **Mod list support** – download multiple mods at once from a simple text file (local or remote).
- ✅ **Parallel downloads** – all mods are downloaded concurrently with live progress bars.
- ✅ **Global RAM optimization** – when run as administrator, the launcher can free up memory for smoother gameplay.
- ✅ **Smart relaunch** – if you enable optimization without admin rights, the launcher restarts itself with elevated privileges, preserving all your settings.
- ✅ **System memory monitoring** – shows free RAM and warns if it’s too low before launching.
- 🚧 **Automatic mod loader installation** – currently you need to provide the correct game version yourself (e.g. `1.20.1-forge`). Full mod loader setup is planned.

---

## 📦 Installation (Windows PowerShell)

Run this single command in **PowerShell 5.1 or higher** (recommended: Run as Administrator):

```powershell
iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/mrlokis/ScopeCLI/refs/heads/main/installer.ps1'))
```

> 💡 If you use PowerShell Core (v7+), the command may differ.  
> After installation, a shortcut to ScopeCLI will be added to your Start Menu and Desktop – use it to launch the program.

---

## 🚀 Getting Started

After installation, launch ScopeCLI from the Start Menu or Desktop shortcut.  
You will be guided through an interactive setup:

1. **Enter your nickname** – used for offline mode.
2. **Select a mod loader** – `Vanilla`, `Forge`, `NeoForge`, or `Fabric`.
3. **Provide a mod list** – a URL or a local file path (drag & drop works!).
4. **Enter the game version** – e.g. `1.20.1` (for modded loaders you may need to include the loader, like `1.20.1-forge`).
5. **Choose global optimization** – if enabled, the launcher will restart as administrator and apply memory‑saving tweaks.
6. **Confirm launch** – the game starts with your chosen settings.

### 🔧 Mod list format

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

When you enable global optimization, the launcher attempts to free up as much RAM as possible before starting Minecraft:

- It clears the working set of essential system processes.
- It forces a garbage collection and flushes system caches.
- These changes are **temporary** – after you finish playing, a restart will restore everything to normal.

If you are **not** running as administrator, the launcher will ask for permission to restart itself with admin rights, preserving your nickname, game version, and mod loader choice.

---

## 🧱 Current Status & Roadmap

- [x] Interactive terminal UI
- [x] Mod list download (from URL or local file)
- [x] Parallel mod downloading with progress
- [ ] Global memory optimization (admin mode)
- [x] Auto‑restart with admin rights
- [ ] Automatic Fabric/Forge/Quilt installation
- [ ] Multiple profile support
- [ ] Self‑update mechanism
- [ ] Config save/load
- [ ] Game "assembly" isolation
- [ ] Localization

---

## 🤝 Contributing

Bug reports, feature ideas, and pull requests are always welcome!  
Feel free to open an [issue](https://github.com/mrlokis/ScopeCLI/issues) or fork the repository.

---

## 📄 License

This project is licensed under the MIT License – see the [LICENSE](LICENSE) file for details.

---

**ScopeCLI** – Minecraft, the way it should be: fast, lean, and under your command.
