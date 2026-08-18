using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;
using UnityEngine;

namespace Baleev.TimelineNudge.Editor
{
    internal static class TimelineNudgeShortcuts
    {
        [TimelineShortcut(
            "Timeline Nudge/Move Selected Clips Left",
            KeyCode.LeftArrow,
            ShortcutModifiers.Alt)]
        private static void NudgeLeft()
        {
            Nudge(TimelineNudgeDirection.Left);
        }

        [TimelineShortcut(
            "Timeline Nudge/Move Selected Clips Right",
            KeyCode.RightArrow,
            ShortcutModifiers.Alt)]
        private static void NudgeRight()
        {
            Nudge(TimelineNudgeDirection.Right);
        }

        private static void Nudge(TimelineNudgeDirection direction)
        {
            if (!CanExecute())
                return;

            TimelineNudgeCommands.TryNudgeSelected(
                direction,
                TimelineNudgePreferences.Frames,
                out _);
        }

        private static bool CanExecute()
        {
            if (EditorGUIUtility.editingTextField)
                return false;

            TimelineEditorWindow timelineWindow = TimelineEditor.GetWindow();
            return timelineWindow != null
                && EditorWindow.focusedWindow == timelineWindow
                && TimelineEditor.inspectedAsset != null
                && TimelineEditor.selectedClips.Length > 0;
        }
    }
}
