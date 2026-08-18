using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace Baleev.TimelineNudge.Editor
{
    [Serializable]
    internal sealed class TimelineNudgeClipReference
    {
        [SerializeField] private string _clipAssetGlobalId;
        [SerializeField] private string _trackGlobalId;
        [SerializeField] private string _displayName;

        internal TimelineNudgeClipReference(
            string clipAssetGlobalId,
            string trackGlobalId,
            string displayName)
        {
            _clipAssetGlobalId = clipAssetGlobalId;
            _trackGlobalId = trackGlobalId;
            _displayName = displayName;
        }

        internal string ClipAssetGlobalId => _clipAssetGlobalId;
        internal string TrackGlobalId => _trackGlobalId;
        internal string DisplayName => _displayName;
    }

    [Serializable]
    internal sealed class TimelineNudgeGroupData
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private string _timelineGlobalId;
        [SerializeField] private string _timelineName;
        [SerializeField] private List<TimelineNudgeClipReference> _clips = new();

        internal TimelineNudgeGroupData(
            string name,
            string timelineGlobalId,
            string timelineName,
            IEnumerable<TimelineNudgeClipReference> clips)
        {
            _id = Guid.NewGuid().ToString("N");
            _name = name;
            _timelineGlobalId = timelineGlobalId;
            _timelineName = timelineName;
            _clips = clips?.ToList() ?? new List<TimelineNudgeClipReference>();
        }

        internal string Id => _id;
        internal string Name => _name;
        internal string TimelineGlobalId => _timelineGlobalId;
        internal string TimelineName => _timelineName;
        internal IReadOnlyList<TimelineNudgeClipReference> Clips => _clips;

        internal void PreserveId(string id)
        {
            _id = id;
        }
    }

    [FilePath("ProjectSettings/TimelineNudgeGroups.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class TimelineNudgeGroupStore : ScriptableSingleton<TimelineNudgeGroupStore>
    {
        [SerializeField] private List<TimelineNudgeGroupData> _groups = new();

        internal IReadOnlyList<TimelineNudgeGroupData> Groups => _groups;

        internal TimelineNudgeGroupData FindById(string id)
        {
            return _groups.FirstOrDefault(group => group.Id == id);
        }

        internal TimelineNudgeGroupData FindByName(string name)
        {
            return _groups.FirstOrDefault(group => string.Equals(
                group.Name,
                name,
                StringComparison.OrdinalIgnoreCase));
        }

        internal void Add(TimelineNudgeGroupData group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            _groups.Add(group);
            Save(true);
        }

        internal void Replace(string id, TimelineNudgeGroupData replacement)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));

            int index = _groups.FindIndex(group => group.Id == id);
            if (index < 0)
                throw new InvalidOperationException("The Timeline Nudge group no longer exists.");

            replacement.PreserveId(id);
            _groups[index] = replacement;
            Save(true);
        }

        internal bool Remove(string id)
        {
            int removed = _groups.RemoveAll(group => group.Id == id);
            if (removed > 0)
                Save(true);
            return removed > 0;
        }
    }

    internal static class TimelineNudgeGroupUtility
    {
        internal static bool TryCreateGroup(
            string name,
            TimelineAsset timeline,
            IEnumerable<TimelineClip> clips,
            out TimelineNudgeGroupData group,
            out string error)
        {
            group = null;
            error = string.Empty;

            string trimmedName = name?.Trim();
            if (string.IsNullOrEmpty(trimmedName))
            {
                error = "Введите имя группы.";
                return false;
            }

            if (timeline == null)
            {
                error = "Откройте Timeline перед сохранением группы.";
                return false;
            }

            TimelineClip[] clipArray = clips?
                .Where(clip => clip != null)
                .Distinct()
                .ToArray() ?? Array.Empty<TimelineClip>();
            if (clipArray.Length == 0)
            {
                error = "Выберите хотя бы один клип для группы.";
                return false;
            }

            if (!TryGetPersistentId(timeline, out string timelineId))
            {
                error = "Сохраните TimelineAsset на диск перед созданием группы.";
                return false;
            }

            TimelineClip[] allTimelineClips = EnumerateClips(timeline).ToArray();
            var references = new List<TimelineNudgeClipReference>(clipArray.Length);

            foreach (TimelineClip clip in clipArray)
            {
                TrackAsset track = clip.GetParentTrack();
                Object clipAsset = clip.asset;
                if (track == null || clipAsset == null)
                {
                    error = $"Клип «{clip.displayName}» не имеет постоянного asset и не может быть сохранён в группе.";
                    return false;
                }

                if (!TryGetPersistentId(clipAsset, out string clipAssetId)
                    || !TryGetPersistentId(track, out string trackId))
                {
                    error = $"Клип «{clip.displayName}» ещё не сохранён на диск.";
                    return false;
                }

                TimelineClip[] sameAsset = allTimelineClips
                    .Where(candidate => candidate.asset == clipAsset)
                    .ToArray();
                if (sameAsset.Length > 1
                    && sameAsset.Count(candidate => candidate.GetParentTrack() == track) != 1)
                {
                    error = $"Клип «{clip.displayName}» нельзя отличить от другого клипа с тем же asset на треке «{track.name}».";
                    return false;
                }

                references.Add(new TimelineNudgeClipReference(
                    clipAssetId,
                    trackId,
                    clip.displayName));
            }

            group = new TimelineNudgeGroupData(
                trimmedName,
                timelineId,
                timeline.name,
                references);
            return true;
        }

        internal static bool TryResolveGroup(
            TimelineNudgeGroupData group,
            out TimelineAsset timeline,
            out TimelineClip[] clips,
            out string error)
        {
            timeline = null;
            clips = Array.Empty<TimelineClip>();
            error = string.Empty;

            if (group == null)
            {
                error = "Группа не выбрана.";
                return false;
            }

            timeline = ResolveObject<TimelineAsset>(group.TimelineGlobalId);
            if (timeline == null)
            {
                error = $"Timeline для группы «{group.Name}» не найден.";
                return false;
            }

            TimelineClip[] allClips = EnumerateClips(timeline).ToArray();
            var resolved = new List<TimelineClip>(group.Clips.Count);
            var usedClips = new HashSet<TimelineClip>();

            foreach (TimelineNudgeClipReference reference in group.Clips)
            {
                Object clipAsset = ResolveObject<Object>(reference.ClipAssetGlobalId);
                if (clipAsset == null)
                {
                    error = $"Клип «{reference.DisplayName}» из группы «{group.Name}» был удалён или заменён.";
                    return false;
                }

                TimelineClip[] candidates = allClips
                    .Where(candidate => candidate.asset == clipAsset)
                    .ToArray();

                TimelineClip match = candidates.Length == 1
                    ? candidates[0]
                    : ResolveUsingStoredTrack(candidates, reference.TrackGlobalId);

                if (match == null || !usedClips.Add(match))
                {
                    error = $"Клип «{reference.DisplayName}» из группы «{group.Name}» найден неоднозначно.";
                    return false;
                }

                resolved.Add(match);
            }

            if (resolved.Count == 0)
            {
                error = $"Группа «{group.Name}» пуста.";
                return false;
            }

            clips = resolved.ToArray();
            return true;
        }

        internal static IEnumerable<TimelineClip> EnumerateClips(TimelineAsset timeline)
        {
            if (timeline == null)
                yield break;

            foreach (TrackAsset rootTrack in timeline.GetRootTracks())
            {
                foreach (TrackAsset track in EnumerateTrackHierarchy(rootTrack))
                {
                    foreach (TimelineClip clip in track.GetClips())
                        yield return clip;
                }
            }
        }

        private static TimelineClip ResolveUsingStoredTrack(
            IEnumerable<TimelineClip> candidates,
            string trackGlobalId)
        {
            TrackAsset storedTrack = ResolveObject<TrackAsset>(trackGlobalId);
            if (storedTrack == null)
                return null;

            TimelineClip[] onStoredTrack = candidates
                .Where(candidate => candidate.GetParentTrack() == storedTrack)
                .ToArray();
            return onStoredTrack.Length == 1 ? onStoredTrack[0] : null;
        }

        private static IEnumerable<TrackAsset> EnumerateTrackHierarchy(TrackAsset track)
        {
            if (track == null)
                yield break;

            yield return track;
            foreach (TrackAsset child in track.GetChildTracks())
            {
                foreach (TrackAsset descendant in EnumerateTrackHierarchy(child))
                    yield return descendant;
            }
        }

        private static bool TryGetPersistentId(Object target, out string value)
        {
            value = string.Empty;
            if (target == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target)))
                return false;

            value = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            return !string.IsNullOrEmpty(value);
        }

        private static T ResolveObject<T>(string value) where T : Object
        {
            if (string.IsNullOrEmpty(value)
                || !GlobalObjectId.TryParse(value, out GlobalObjectId globalObjectId))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as T;
        }
    }

    internal static class TimelineNudgeGroupSelection
    {
        internal static bool TrySelect(TimelineNudgeGroupData group, out string error)
        {
            if (!TimelineNudgeGroupUtility.TryResolveGroup(
                    group,
                    out TimelineAsset timeline,
                    out TimelineClip[] clips,
                    out error))
            {
                return false;
            }

            TimelineEditorWindow timelineWindow = TimelineEditor.GetOrCreateWindow();
            if (timelineWindow == null)
            {
                error = "Не удалось открыть окно Timeline.";
                return false;
            }

            if (TimelineEditor.inspectedAsset != timeline)
                timelineWindow.SetTimeline(timeline);

            EditorApplication.delayCall += () =>
            {
                if (timelineWindow == null)
                    return;

                if (TimelineEditor.inspectedAsset != timeline)
                    timelineWindow.SetTimeline(timeline);

                TimelineEditor.selectedClips = clips;
                TimelineEditor.Refresh(RefreshReason.WindowNeedsRedraw);
                timelineWindow.Focus();
            };

            return true;
        }
    }
}
