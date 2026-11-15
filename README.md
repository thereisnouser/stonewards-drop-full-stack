# Stonewards Drop Full Stack

**Drop an entire stack from the selected inventory slot with one press of Stonewards' normal Release key.** No extra keybind or menu is needed.

## ✨ Features

- **Full-stack drops** — drop all items in the selected slot in a single action.
- **Native controls** — use the existing Release action and the game's normal drop position.
- **Duplicate-request protection** — suppress repeated requests for the same unchanged stack while inventory synchronization is pending.
- **Optional toggle** — restore the original one-item drop through the configuration file.

## 🎮 How to use

Select an inventory slot containing a stack and press **Release** (**R** in the tested keyboard setup). The entire stack drops at once; a slot containing only one item behaves normally.

The mod follows your existing in-game Release binding. It does not add a new key or change item stack limits.

## ⚙️ Configuration

The mod is enabled by default. After the plugin has loaded, you can edit its BepInEx configuration file:

`BepInEx/config/thereisnouser.stonewards.dropfullstack.cfg`

```ini
[General]
Enabled = true
```

Set `Enabled = false` to restore the game's standard one-item drop.

## 📦 Installation

Install [DropFullStack on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/DropFullStack/) with **r2modman** or another compatible Thunderstore mod manager, then launch Stonewards through the manager.

The package declares **BepInExPack 5.4.2305** as a dependency.

## 🛠️ Development

### Requirements

- **.NET SDK** for building the C# project.
- A local **Stonewards** installation for its managed assemblies.
- **BepInEx 5** in the game installation or selected mod-manager profile.

### Building

From the repository directory, build and install the plugin to your active profile:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir '<Stonewards game directory>' `
  -ProfileDir '<r2modman profile directory>'
```

The recommended test profile name is `stonewards-drop-full-stack`. Omit `-ProfileDir` if BepInEx is installed directly in the game directory.

To build a Thunderstore upload archive:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\package-thunderstore.ps1 `
  -GameDir '<Stonewards game directory>' `
  -ProfileDir '<r2modman profile directory>'
```

The script creates `dist/DropFullStack-0.1.2.zip` containing the compiled plugin, the Thunderstore README, changelog, manifest, and 256×256 icon. It checks the release version and verifies that the packaged README matches `thunderstore/README.md`.

### Implementation

The plugin patches `FirstPersonController.CmdReleaseItem` with Harmony. For the local player's selected inventory slot, it replaces the native release quantity with the full stack count while preserving the item ID, slot index, drop position, and original command handling. Pending requests for an unchanged stack are temporarily suppressed.

## 🔗 Links

[Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/DropFullStack/) · [Report an issue](https://github.com/thereisnouser/stonewards-drop-full-stack/issues) · [Changelog](CHANGELOG.md)

---

*Made by thereisnouser*
