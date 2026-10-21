# Stonewards Drop Full Stack

**Drop the entire stack from your selected inventory slot with a single press of the normal Release key.** No extra keybind or separate menu is needed.

## ✨ Features

- **Full-stack drop** — release all items in the selected slot at once instead of one at a time.
- **Native controls** — uses the existing Release action and keeps the game's normal drop position.
- **Repeat-request protection** — prevents duplicate full-stack requests while the same inventory count is still awaiting synchronization.
- **Optional toggle** — disable the mod's behavior without uninstalling it.

## 🎮 How to use

Select an inventory slot containing a stack, then press **Release** (**R** in the tested keyboard setup). The whole stack is dropped in one action. A slot containing only one item behaves as usual.

The mod uses your existing in-game Release binding; it does not add a new key or change item stack limits.

## ⚙️ Configuration

The mod is enabled by default. BepInEx creates the following configuration file after the plugin loads:

`BepInEx/config/thereisnouser.stonewards.dropfullstack.cfg`

```ini
[General]
Enabled = true
```

Set `Enabled = false` to restore the standard one-item drop.

## 📦 Installation

### Mod manager

Install the published package using **r2modman** or another compatible Thunderstore mod manager. Launch Stonewards through the mod manager. The package manifest declares **BepInExPack** as a dependency.

### Build from source

You need the **.NET SDK**, Stonewards installed locally, and **BepInEx 5** installed in the game or selected r2modman profile.

From the repository directory, run:

```powershell
.\build-and-install.ps1 `
  -GameDir '<Stonewards game directory>' `
  -ProfileDir '<r2modman profile directory>'
```

For local testing, use an r2modman profile named `stonewards-drop-full-stack`, matching this repository. If BepInEx is installed directly in the game directory, omit `-ProfileDir`.

## 🔧 Compatibility

- **Game:** Stonewards; the full-stack behavior was tested locally on an October 2026 game build.
- **Mod loader:** BepInEx 5 (the package declares BepInExPack 5.4.2305 as a dependency).
- **Multiplayer:** the full-stack action was tested in local gameplay. The plugin modifies the local player's native release request; behavior on a remote host without the mod has not been verified.

## ⚠️ Known limitations

- Compatibility with other players' hosts is not yet confirmed.
- A future game update changing the native release method's signature may prevent the patch from loading.

## 🛠️ Development

### Implementation

The plugin uses Harmony to intercept `FirstPersonController.CmdReleaseItem` and replaces its **count** argument with the number of items held in the selected slot. It leaves the item ID, slot index, drop position, and the game's native item handling unchanged.

Repeated requests for an unchanged stack are temporarily suppressed while inventory synchronization is pending. The plugin does not directly add or remove inventory items.

### Building a Thunderstore package

The repository also includes `package-thunderstore.ps1`:

```powershell
.\package-thunderstore.ps1 `
  -GameDir '<Stonewards game directory>' `
  -ProfileDir '<r2modman profile directory>'
```

It builds the plugin and creates `dist/DropFullStack-0.1.1.zip` with the DLL, `thunderstore/README.md`, changelog, manifest, and 256×256 icon. The script checks package contents and matching source/package versions.

## 🔗 Links

[Report an issue](https://github.com/thereisnouser/stonewards-drop-full-stack/issues) · [Changelog](CHANGELOG.md)

---

*Made by thereisnouser*
