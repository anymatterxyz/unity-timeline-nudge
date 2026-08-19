# Changelog

## 0.2.1

- Changed the main Timeline Nudge window to a dockable Editor tab, placed beside the Inspector when possible.
- Added the remappable `Ctrl+Alt+N` (`Cmd+Alt+N` on macOS) shortcut for opening the dockable window.
- Added a separate **Timeline Nudge (Floating)** menu item for the utility-window layout.
- Deferred saved-group framing until Timeline finishes applying its restored selection, preventing the viewport from resetting after docking.
- Added vertical Timeline framing so restoring a group also scrolls to the center of its selected track range.

## 0.2.0

- Added automatic Timeline framing after restoring a saved clip group.
- Framing fits the complete selected clip range using Timeline's native Frame Selected behaviour.
- Added a persisted **Center on selection** toggle, enabled by default.

## 0.1.0

- Added a compact UI Toolkit Editor window for frame-accurate Timeline clip movement.
- Added multi-clip movement across tracks using the active TimelineAsset frame rate.
- Added group-preserving left clamping at time zero, one-step Undo, and one refresh per operation.
- Added Timeline-scoped `Alt+Left Arrow` and `Alt+Right Arrow` shortcuts.
- Added persistent named clip groups that reopen their Timeline and restore the selection.
- Added guarded update, replacement, and deletion flows for saved groups.
- Added EditMode coverage for movement, Undo, persistence, group restoration, and UI assets.
