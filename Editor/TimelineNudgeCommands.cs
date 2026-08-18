using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

namespace Baleev.TimelineNudge.Editor
{
    internal enum TimelineNudgeDirection
    {
        Left = -1,
        Right = 1
    }

    internal static class TimelineNudgeCommands
    {
        private const string UndoName = "Nudge Timeline Clips";

        internal static bool TryNudgeSelected(
            TimelineNudgeDirection direction,
            int frames,
            out string error)
        {
            return TryNudge(
                TimelineEditor.inspectedAsset,
                TimelineEditor.selectedClips,
                direction,
                frames,
                out error);
        }

        internal static bool TryNudge(
            TimelineAsset timeline,
            IEnumerable<TimelineClip> clips,
            TimelineNudgeDirection direction,
            int frames,
            out string error)
        {
            error = string.Empty;

            if (timeline == null)
            {
                error = "Откройте Timeline перед смещением клипов.";
                return false;
            }

            if (frames < 1)
            {
                error = "Количество кадров должно быть не меньше 1.";
                return false;
            }

            TimelineClip[] clipArray = clips?
                .Where(clip => clip != null)
                .Distinct()
                .ToArray() ?? Array.Empty<TimelineClip>();

            if (clipArray.Length == 0)
            {
                error = "Выберите хотя бы один клип в Timeline.";
                return false;
            }

            TrackAsset lockedTrack = clipArray
                .Select(clip => clip.GetParentTrack())
                .FirstOrDefault(track => track != null && track.lockedInHierarchy);
            if (lockedTrack != null)
            {
                error = $"Трек «{lockedTrack.name}» заблокирован. Группа не была перемещена.";
                return false;
            }

            double frameRate = timeline.editorSettings.frameRate;
            if (!IsValidFrameRate(frameRate))
            {
                error = "Текущий Timeline содержит некорректный FPS.";
                return false;
            }

            double delta = CalculateDelta(
                clipArray.Min(clip => clip.start),
                frames,
                frameRate,
                direction);

            if (Math.Abs(delta) <= double.Epsilon)
                return false;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);

            try
            {
                UndoExtensions.RegisterClips(clipArray, UndoName, false);

                foreach (TimelineClip clip in clipArray)
                    clip.start += delta;

                foreach (TrackAsset track in clipArray
                             .Select(clip => clip.GetParentTrack())
                             .Where(track => track != null)
                             .Distinct())
                {
                    EditorUtility.SetDirty(track);
                }

                EditorUtility.SetDirty(timeline);
                TimelineEditor.Refresh(RefreshReason.ContentsModified);
                return true;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        internal static double CalculateDelta(
            double earliestStart,
            int frames,
            double frameRate,
            TimelineNudgeDirection direction)
        {
            if (frames < 1)
                throw new ArgumentOutOfRangeException(nameof(frames), frames, "Frames must be at least 1.");
            if (!IsValidFrameRate(frameRate))
                throw new ArgumentOutOfRangeException(nameof(frameRate), frameRate, "Frame rate must be finite and positive.");
            if (double.IsNaN(earliestStart) || double.IsInfinity(earliestStart))
                throw new ArgumentOutOfRangeException(nameof(earliestStart), earliestStart, "Start time must be finite.");

            double seconds = frames / frameRate;
            return direction == TimelineNudgeDirection.Left
                ? -Math.Min(seconds, Math.Max(0d, earliestStart))
                : seconds;
        }

        internal static bool IsValidFrameRate(double frameRate)
        {
            return frameRate > 0d
                && !double.IsNaN(frameRate)
                && !double.IsInfinity(frameRate);
        }
    }
}
