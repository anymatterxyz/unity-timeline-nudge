using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

namespace Baleev.TimelineNudge.Editor
{
    internal static class TimelineNudgeFraming
    {
        private const string FrameSelectedActionTypeName =
            "UnityEditor.Timeline.FrameSelectedAction";

        private static readonly MethodInfo FrameRangeMethod = typeof(TimelineEditor).Assembly
            .GetType(FrameSelectedActionTypeName)
            ?.GetMethod(
                "FrameRange",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(float), typeof(float) },
                null);

        internal static bool IsSupported => FrameRangeMethod != null;

        internal static bool TryFrameClips(
            IEnumerable<TimelineClip> clips,
            out string error)
        {
            error = string.Empty;

            if (!TryGetClipRange(clips, out double start, out double end))
            {
                error = "Не удалось определить временной диапазон выбранной группы.";
                return false;
            }

            if (FrameRangeMethod == null)
            {
                error = "Эта версия Timeline не поддерживает автоматическое кадрирование группы.";
                return false;
            }

            try
            {
                FrameRangeMethod.Invoke(null, new object[] { (float)start, (float)end });
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException?.Message ?? exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static bool TryGetClipRange(
            IEnumerable<TimelineClip> clips,
            out double start,
            out double end)
        {
            start = double.PositiveInfinity;
            end = double.NegativeInfinity;

            if (clips == null)
                return false;

            foreach (TimelineClip clip in clips)
            {
                if (clip == null
                    || double.IsNaN(clip.start)
                    || double.IsInfinity(clip.start)
                    || double.IsNaN(clip.end)
                    || double.IsInfinity(clip.end))
                {
                    continue;
                }

                start = Math.Min(start, clip.start);
                end = Math.Max(end, clip.end);
            }

            return !double.IsPositiveInfinity(start)
                && !double.IsNegativeInfinity(end)
                && start <= end;
        }
    }
}
