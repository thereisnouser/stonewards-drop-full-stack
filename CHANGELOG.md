# Changelog

## 0.1.1 (diagnostic test)

- Expand any positive native release request smaller than the selected stack (not only requests of exactly one item).
- Remove an unnecessary instance/owner identity check and preserve local-player authorization.
- Log why release requests are skipped, the selected slot/count, and successful expansions.
- Add read-only host-side diagnostics for the native Mirror server handler, when present.
- Preserve native fallback on patch errors and the duplicate-request guard.

## 0.1.0

- Initial source release.
- Normal item-drop action drops the entire selected inventory slot.
- Added duplicate-request protection for delayed network inventory synchronization.
- Added configuration toggle, build/install script, and test checklist.
