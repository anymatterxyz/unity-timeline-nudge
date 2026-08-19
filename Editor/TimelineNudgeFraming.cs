using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

namespace Baleev.TimelineNudge.Editor
{
    internal static class TimelineNudgeFraming
    {
        private const string FrameSelectedActionTypeName =
            "UnityEditor.Timeline.FrameSelectedAction";
        private const string TimelineWindowTypeName =
            "UnityEditor.Timeline.TimelineWindow";
        private const string TimelineTreeViewTypeName =
            "UnityEditor.Timeline.TimelineTreeViewGUI";
        private const string TimelineTrackGuiTypeName =
            "UnityEditor.Timeline.TimelineTrackBaseGUI";

        private const BindingFlags StaticMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly Assembly TimelineEditorAssembly = typeof(TimelineEditor).Assembly;

        private static readonly MethodInfo FrameRangeMethod = TimelineEditorAssembly
            .GetType(FrameSelectedActionTypeName)
            ?.GetMethod(
                "FrameRange",
                StaticMembers,
                null,
                new[] { typeof(float), typeof(float) },
                null);

        private static readonly Type TimelineWindowType =
            TimelineEditorAssembly.GetType(TimelineWindowTypeName);
        private static readonly Type TimelineTreeViewType =
            TimelineEditorAssembly.GetType(TimelineTreeViewTypeName);
        private static readonly Type TimelineTrackGuiType =
            TimelineEditorAssembly.GetType(TimelineTrackGuiTypeName);

        private static readonly PropertyInfo TimelineWindowInstanceProperty =
            TimelineWindowType?.GetProperty("instance", StaticMembers);
        private static readonly PropertyInfo TimelineWindowAllTracksProperty =
            TimelineWindowType?.GetProperty("allTracks", InstanceMembers);
        private static readonly PropertyInfo TimelineWindowTreeViewProperty =
            TimelineWindowType?.GetProperty("treeView", InstanceMembers);
        private static readonly PropertyInfo TimelineTrackProperty =
            TimelineTrackGuiType?.GetProperty("track", InstanceMembers);
        private static readonly MethodInfo FrameTrackItemMethod = TimelineTreeViewType
            ?.GetMethods(InstanceMembers)
            .FirstOrDefault(method =>
                method.Name == "FrameItem" && method.GetParameters().Length == 1);

        internal static bool IsSupported => FrameRangeMethod != null && IsVerticalSupported;

        internal static bool IsVerticalSupported =>
            TimelineWindowInstanceProperty != null
            && TimelineWindowAllTracksProperty != null
            && TimelineWindowTreeViewProperty != null
            && TimelineTrackProperty != null
            && FrameTrackItemMethod != null;

        internal static bool TryFrameClips(
            IEnumerable<TimelineClip> clips,
            out string error)
        {
            error = string.Empty;
            TimelineClip[] clipArray = clips?
                .Where(clip => clip != null)
                .ToArray();

            if (!TryGetClipRange(clipArray, out double start, out double end))
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
                if (!TryFrameTracks(clipArray, out error))
                    return false;

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

        internal static bool TryFrameTracks(
            IEnumerable<TimelineClip> clips,
            out string error)
        {
            error = string.Empty;
            if (!IsVerticalSupported)
            {
                error = "Эта версия Timeline не поддерживает вертикальное кадрирование группы.";
                return false;
            }

            try
            {
                object timelineWindow = TimelineWindowInstanceProperty.GetValue(null);
                object treeView = timelineWindow == null
                    ? null
                    : TimelineWindowTreeViewProperty.GetValue(timelineWindow);
                var trackGuis = timelineWindow == null
                    ? null
                    : TimelineWindowAllTracksProperty.GetValue(timelineWindow) as IEnumerable;
                if (treeView == null || trackGuis == null)
                {
                    error = "Окно Timeline ещё не подготовило список треков.";
                    return false;
                }

                var orderedTracks = new List<TrackAsset>();
                var guiByTrack = new Dictionary<TrackAsset, object>();
                foreach (object trackGui in trackGuis)
                {
                    if (trackGui == null)
                        continue;

                    TrackAsset track = TimelineTrackProperty.GetValue(trackGui) as TrackAsset;
                    if (track == null || guiByTrack.ContainsKey(track))
                        continue;

                    orderedTracks.Add(track);
                    guiByTrack.Add(track, trackGui);
                }

                if (!TryGetVerticalCenterTrack(
                        orderedTracks,
                        clips,
                        out TrackAsset centerTrack)
                    || !guiByTrack.TryGetValue(centerTrack, out object centerTrackGui))
                {
                    error = "Не удалось найти треки выбранной группы в окне Timeline.";
                    return false;
                }

                FrameTrackItemMethod.Invoke(treeView, new[] { centerTrackGui });
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

        internal static bool TryGetVerticalCenterTrack(
            IReadOnlyList<TrackAsset> uiOrderedTracks,
            IEnumerable<TimelineClip> clips,
            out TrackAsset centerTrack)
        {
            centerTrack = null;
            if (uiOrderedTracks == null || clips == null || uiOrderedTracks.Count == 0)
                return false;

            var selectedTracks = new HashSet<TrackAsset>(clips
                .Where(clip => clip != null)
                .Select(clip => clip.GetParentTrack())
                .Where(track => track != null));
            if (selectedTracks.Count == 0)
                return false;

            int firstIndex = -1;
            int lastIndex = -1;
            for (int index = 0; index < uiOrderedTracks.Count; index++)
            {
                if (!selectedTracks.Contains(uiOrderedTracks[index]))
                    continue;

                if (firstIndex < 0)
                    firstIndex = index;
                lastIndex = index;
            }

            if (firstIndex < 0)
                return false;

            centerTrack = uiOrderedTracks[(firstIndex + lastIndex) / 2];
            return centerTrack != null;
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
