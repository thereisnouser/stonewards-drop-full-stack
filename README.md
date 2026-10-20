# Stonewards Drop Full Stack

**Drop the entire inventory slot with the existing drop button.** No extra hotkey, menu, or additional dependencies beyond BepInEx 5.

## Features

- Drops every item in the selected stack instead of dropping one at a time.
- Keeps normal single-item drops unchanged.
- Uses Stonewards' original `FirstPersonController.CmdReleaseItem` command, without changing where items land or directly modifying inventory contents.
- Suppresses duplicate bulk-drop commands while the local client still reports the same unchanged slot (up to five seconds).
- An optional `Enabled` setting restores the vanilla action without uninstalling the plugin.

> **Testing status:** v0.1.1 is an unverified diagnostic update after v0.1.0 was reported to drop single items. It has not yet been compiled against the current game's assemblies or tested in multiplayer. In particular, confirm the game host accepts requests for multiple items. Do not publish a prebuilt release before testing.

## Install from source

Requires **Stonewards**, **BepInEx 5** in the selected game/r2modman profile, and the **.NET SDK**.

Open PowerShell in this repository:

```powershell
.\build-and-install.ps1 `
  -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\<user>\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards-drop-full-stack"
```

Adjust both paths to the actual installation. If BepInEx is installed in the game directory, omit `-ProfileDir`.

The script builds a `netstandard2.1` plugin and installs `StonewardsDropFullStack.dll` under `BepInEx/plugins/StonewardsDropFullStack` in the chosen profile.

Use an r2modman profile named **`stonewards-drop-full-stack`** for consistency with this repository.

## How it works

A Harmony prefix inspects the local player's native release request. When the requested amount is one item and the selected slot contains more, the plugin replaces the request's `count` with the *actual number in that slot*. The native Mirror command then performs the drop. The mod never fabricates resources or directly alters network inventory data.

Only players who want the full-stack drop behavior need to install the client-side mod; behavior on an unmodded host still requires in-game validation.

## Configuration

After the first game launch, BepInEx creates:

```text
BepInEx/config/thereisnouser.stonewards.dropfullstack.cfg
```

```ini
[General]
Enabled = true
```

Set `Enabled = false` for vanilla single-item dropping.

## Diagnosing a drop that still releases one item

After building and launching through the correct r2modman profile, press R **once** on a small expendable stack (e.g. 3 stone) and inspect:

```powershell
Select-String -Path "$env:APPDATA\r2modmanPlus-local\Stonewards\profiles\stonewards-drop-full-stack\BepInEx\LogOutput.log" -Pattern '\[DropFullStack\]'
```

The expected messages are `client CmdReleaseItem hook active` and `CLIENT release ... EXPANDED`. If none appear, confirm the installed DLL and the launched mod profile. If the hook loads but no `CLIENT release` message appears on pressing R, the R action uses a different game path. If `EXPANDED` is logged but the world still receives one item, host-side handling needs inspection. On a local host, the optional `SERVER incoming release` entry records the actual incoming command count.

**Important:** Avoid testing large/valuable stacks until the quantity of items remaining in inventory and spawned on the ground has been confirmed. Do not infer successful bulk dropping from an `EXPANDED` log alone.

## Suggested tests

1. Drop a stack of five ore once. Confirm the slot empties and a ground stack of five appears.
2. Pick it up and confirm the quantity is exactly five.
3. Drop one item from a slot holding only one item.
4. Repeatedly tap the normal drop button under network lag: check for duplicate ground items.
5. Repeat as a client of an unmodded host and alongside `stonewards-ore-auto-pickup`.

## Compatibility and limits

- Built for **BepInEx 5** and the game's existing inventory system.
- Does **not** change item stack limits, automatic pickup, or forge behavior.
- Patches the same release command observed by `stonewards-ore-auto-pickup` but does not skip the native command when a valid drop is sent.
- Logs a startup warning if the native method or its signature changes.
- Game assemblies are referenced locally; **no game DLLs are distributed** with this repository.
