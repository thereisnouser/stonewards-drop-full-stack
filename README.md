# Stonewards Drop Full Stack

**Drop your entire selected item stack with one press of the normal Release key.** No additional keybind or menu is needed.

## ✨ Features

- **One-press full-stack drop** — drops the full quantity from the selected inventory slot.
- **Native controls** — keeps the game's Release key and original dropping location.
- **Duplicate protection** — avoids immediately repeating a full-stack request while its inventory count is still awaiting synchronization.
- **Optional toggle** — restores the usual one-item drop when disabled.

## 🎮 How to use

Select an item stack and press the game's **Release** key (normally **R**). Instead of one item, the entire selected stack is released.

A slot containing just one item is unchanged. The mod does not raise the maximum stack size.

## ⚙️ Configuration

After the first launch, edit `BepInEx/config/thereisnouser.stonewards.dropfullstack.cfg`:

```ini
[General]
Enabled = true
```

Set `Enabled = false` to restore the standard single-item drop. The key itself remains configured in Stonewards.

## 📦 Installation

Install the package using **r2modman** or a compatible Thunderstore mod manager after it has been published, then start Stonewards through that manager. The manifest includes BepInEx as a dependency.

### Build from source

The source requires the **.NET SDK**, the installed Stonewards assemblies, and **BepInEx 5** in the active game or r2modman profile.

```powershell
.\build-and-install.ps1 -GameDir '<Stonewards game directory>' -ProfileDir '<r2modman profile directory>'
```

For local development, use an r2modman profile named `stonewards-drop-full-stack`, matching the GitHub repository.

## 🔧 Compatibility

- **Game:** Stonewards (full-stack drop tested on the October 2026 game build).
- **Mod loader:** BepInEx 5.4.23.5 used during testing.
- **Multiplayer:** intercepts the local player's native release request. Operation while connected to an unmodded remote host still needs verification.

## ⚠️ Known limitations

- The behavior on other players' hosts has not yet been confirmed.
- If Stonewards changes the native release command in a future update, the plugin disables its patch rather than guessing the new signature.

## 🛠️ Development

### Implementation

The plugin uses a Harmony prefix on `FirstPersonController.CmdReleaseItem` and changes **only its count parameter**, using the quantity in the selected inventory slot. The game's native command handles item removal and spawning.

The correct argument order is `(owner, itemId, selectedSlot, count, position)`. Diagnostic-only hooks from early test builds are not included in the public source.

Build a Thunderstore ZIP with:

```powershell
.\package-thunderstore.ps1 -GameDir '<Stonewards game directory>' -ProfileDir '<r2modman profile directory>'
```

This validates matching version numbers and the icon size, then compiles and packages the real plugin DLL into `dist/`.

## 🔗 Links

[Issues](https://github.com/thereisnouser/stonewards-drop-full-stack/issues) · [Changelog](CHANGELOG.md)

---

*Made by thereisnouser*
