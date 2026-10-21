# Changelog

## 0.1.1

- Removed diagnostic-only hooks and verbose release logging from the public plugin.
- Kept the corrected slot/count handling and full-stack drop behavior from the tested build.
- Added the new 256×256 icon and standardized GitHub and Thunderstore descriptions.
- Updated release packaging checks and synchronized the plugin, DLL project, and manifest versions.

## 0.1.0

- Initial public release.
- The normal Release action drops the entire selected item stack in one press.
- Keeps native slot selection, item drop position, and multiplayer command handling.
- Includes protection against repeated drop commands while the local inventory is waiting for synchronization.
- Adds a configuration option to restore the vanilla one-item drop.
