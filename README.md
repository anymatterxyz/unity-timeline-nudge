# Timeline Nudge

Frame-accurate movement and reusable selection groups for clips in Unity Timeline.

## Compatibility

- Unity `6000.3` or newer
- Unity Timeline `1.8.12`
- Tested with Unity `6000.3.15f1` and Timeline `1.8.12`

The package is Editor-only and adds no runtime components or scene objects.

## Installation

In Unity, open **Window > Package Management > Package Manager**, choose
**Install package from git URL**, and enter:

```text
https://github.com/anymatterxyz/unity-timeline-nudge.git#v0.2.1
```

Alternatively, add this entry to the project's `Packages/manifest.json` dependencies:

```json
"com.baleev.timeline-nudge": "https://github.com/anymatterxyz/unity-timeline-nudge.git#v0.2.1"
```

## Usage

Open **Window > Sequencing > Timeline Nudge** and select one or more Timeline clips.
The main command opens a dockable Editor tab beside the Inspector when possible. You can
drag the tab into any other Unity dock. Use **Timeline Nudge (Floating)** from the same menu
when you prefer a separate always-on-top utility window.

- Set **Frames** to an integer of `1` or more. The value is remembered by the Editor.
- Use **Move Left** or **Move Right** to move every selected clip by the same amount.
- The conversion uses `TimelineAsset.editorSettings.frameRate` from the active Timeline.
- A left move is clamped as a group, so the earliest selected clip stops at time `0` and relative spacing remains unchanged.
- Each move is one Undo operation and performs one Timeline refresh.

Only `TimelineClip.start` is changed. Tracks, durations, `clipIn`, `timeScale`, playable
assets, neighbouring clips, and AnimationClip contents are left untouched. No ripple edit
or automatic trimming is performed.

## Shortcuts

- `Ctrl+Alt+N` (`Cmd+Alt+N` on macOS): open or focus the dockable Timeline Nudge window.
- `Alt+Left Arrow`: move selected clips left by the current **Frames** value.
- `Alt+Right Arrow`: move selected clips right by the current **Frames** value.

The shortcuts are Timeline-scoped: they run only while the Timeline window has focus,
clips are selected, and a text field is not being edited. They can be rebound in Unity's
Shortcut Manager.

## Saved clip groups

Select clips and choose **Save As...** to create a named group. A saved group can later:

- reopen its source TimelineAsset;
- restore all of its clips as the current Timeline selection;
- automatically fit and center the Timeline view on the complete group range;
- be updated from the current selection;
- be deleted without changing any Timeline clips.

Groups are stored per project in `ProjectSettings/TimelineNudgeGroups.asset`. References
use Unity `GlobalObjectId` values, so moving clips in time does not break a group.

Automatic framing uses Timeline's native **Frame Selected** range behaviour and is enabled
by default. Clear **Center on selection** in the window to keep the current Timeline viewport;
the preference is remembered by the Editor.

A group requires a saved TimelineAsset and clips with persistent playable assets. If the
same playable asset is used more than once on the same track, those clips are inherently
ambiguous in Timeline's public data model and the package refuses to save that selection
instead of restoring the wrong clip later. Group restoration is all-or-nothing when a clip
was deleted or replaced.

## Development

EditMode tests are included under `Tests/Editor` and cover FPS conversion, fractional frame
rates, group clamping at zero, movement across tracks, property preservation, one-step Undo,
saved-group resolution and serialization, and UI asset loading.

## License

See [LICENSE.md](LICENSE.md).
