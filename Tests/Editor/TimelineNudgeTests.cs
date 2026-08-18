using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace Baleev.TimelineNudge.Editor.Tests
{
    public sealed class TimelineNudgeTests
    {
        private const string TestFolder = "Assets/__TimelineNudgeTests";
        private const string UxmlPath =
            "Packages/com.baleev.timeline-nudge/Editor/UI/TimelineNudgeWindow.uxml";
        private const string TokensPath =
            "Packages/com.baleev.timeline-nudge/Editor/UI/TimelineNudgeTokens.uss";
        private const string StylesPath =
            "Packages/com.baleev.timeline-nudge/Editor/UI/TimelineNudgeWindow.uss";

        private readonly List<string> _createdAssetPaths = new();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "__TimelineNudgeTests");
        }

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            foreach (string path in _createdAssetPaths)
                AssetDatabase.DeleteAsset(path);
            _createdAssetPaths.Clear();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void CalculateDelta_UsesTimelineFrameRate()
        {
            double delta = TimelineNudgeCommands.CalculateDelta(
                10d,
                5,
                30d,
                TimelineNudgeDirection.Right);

            Assert.That(delta, Is.EqualTo(5d / 30d).Within(0.000000001d));
        }

        [Test]
        public void CalculateDelta_SupportsFractionalFrameRate()
        {
            double delta = TimelineNudgeCommands.CalculateDelta(
                10d,
                10,
                29.97d,
                TimelineNudgeDirection.Right);

            Assert.That(delta, Is.EqualTo(10d / 29.97d).Within(0.000000001d));
        }

        [Test]
        public void CalculateDelta_ClampsWholeGroupAtZero()
        {
            double delta = TimelineNudgeCommands.CalculateDelta(
                0.1d,
                5,
                30d,
                TimelineNudgeDirection.Left);

            Assert.That(delta, Is.EqualTo(-0.1d).Within(0.000000001d));
        }

        [Test]
        public void TryNudge_RightMovesAllClipsAndPreservesOtherProperties()
        {
            TimelineAsset timeline = CreateTimelineAsset(30d);
            AnimationTrack firstTrack = timeline.CreateTrack<AnimationTrack>(null, "First");
            ActivationTrack secondTrack = timeline.CreateTrack<ActivationTrack>(null, "Second");
            var sourceClip = new AnimationClip { name = "Source" };
            sourceClip.SetCurve(
                string.Empty,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, 2f, 1f));
            AssetDatabase.AddObjectToAsset(sourceClip, timeline);
            TimelineClip first = firstTrack.CreateClip(sourceClip);
            first.start = 100d / 30d;
            first.duration = 2d;
            TimelineClip second = CreateClip(secondTrack, 140d / 30d, 3d);
            first.clipIn = 0.25d;
            first.timeScale = 1.5d;
            double originalGap = second.start - first.start;

            bool changed = TimelineNudgeCommands.TryNudge(
                timeline,
                new[] { first, second },
                TimelineNudgeDirection.Right,
                10,
                out string error);

            Assert.That(changed, Is.True, error);
            Assert.That(first.start, Is.EqualTo(110d / 30d).Within(0.000000001d));
            Assert.That(second.start, Is.EqualTo(150d / 30d).Within(0.000000001d));
            Assert.That(second.start - first.start, Is.EqualTo(originalGap).Within(0.000000001d));
            Assert.That(first.duration, Is.EqualTo(2d));
            Assert.That(second.duration, Is.EqualTo(3d));
            Assert.That(first.clipIn, Is.EqualTo(0.25d));
            Assert.That(first.timeScale, Is.EqualTo(1.5d));
            Assert.That(first.GetParentTrack(), Is.SameAs(firstTrack));
            Assert.That(second.GetParentTrack(), Is.SameAs(secondTrack));
        }

        [Test]
        public void TryNudge_LeftClampsGroupAndPreservesSpacing()
        {
            TimelineAsset timeline = CreateTimelineAsset(30d);
            ActivationTrack track = timeline.CreateTrack<ActivationTrack>(null, "Track");
            TimelineClip first = CreateClip(track, 0.1d, 1d);
            TimelineClip second = CreateClip(track, 1d, 1d);
            double originalGap = second.start - first.start;

            bool changed = TimelineNudgeCommands.TryNudge(
                timeline,
                new[] { first, second },
                TimelineNudgeDirection.Left,
                5,
                out string error);

            Assert.That(changed, Is.True, error);
            Assert.That(first.start, Is.EqualTo(0d).Within(0.000000001d));
            Assert.That(second.start, Is.EqualTo(0.9d).Within(0.000000001d));
            Assert.That(second.start - first.start, Is.EqualTo(originalGap).Within(0.000000001d));
        }

        [Test]
        public void TryNudge_IsOneUndoOperationForWholeSelection()
        {
            TimelineAsset timeline = CreateTimelineAsset(25d);
            ActivationTrack firstTrack = timeline.CreateTrack<ActivationTrack>(null, "First");
            AnimationTrack secondTrack = timeline.CreateTrack<AnimationTrack>(null, "Second");
            TimelineClip first = CreateClip(firstTrack, 2d, 1d);
            TimelineClip second = CreateAnimationClip(secondTrack, 5d, 1d);
            AssetDatabase.SaveAssets();
            Undo.ClearAll();

            bool changed = TimelineNudgeCommands.TryNudge(
                timeline,
                new[] { first, second },
                TimelineNudgeDirection.Right,
                5,
                out string error);
            Assert.That(changed, Is.True, error);

            Undo.PerformUndo();

            Assert.That(first.start, Is.EqualTo(2d).Within(0.000000001d));
            Assert.That(second.start, Is.EqualTo(5d).Within(0.000000001d));
        }

        [Test]
        public void TryNudge_DoesNothingWhenEarliestClipIsAlreadyAtZero()
        {
            TimelineAsset timeline = CreateTimelineAsset(30d);
            ActivationTrack track = timeline.CreateTrack<ActivationTrack>(null, "Track");
            TimelineClip first = CreateClip(track, 0d, 1d);
            TimelineClip second = CreateClip(track, 2d, 1d);

            bool changed = TimelineNudgeCommands.TryNudge(
                timeline,
                new[] { first, second },
                TimelineNudgeDirection.Left,
                5,
                out string error);

            Assert.That(changed, Is.False);
            Assert.That(error, Is.Empty);
            Assert.That(first.start, Is.EqualTo(0d));
            Assert.That(second.start, Is.EqualTo(2d));
        }

        [Test]
        public void Group_RestoresClipsAfterTheyMove()
        {
            TimelineAsset timeline = CreateTimelineAsset(30d);
            ActivationTrack firstTrack = timeline.CreateTrack<ActivationTrack>(null, "First");
            AnimationTrack secondTrack = timeline.CreateTrack<AnimationTrack>(null, "Second");
            TimelineClip first = CreateClip(firstTrack, 1d, 1d);
            TimelineClip second = CreateAnimationClip(secondTrack, 4d, 1d);
            AssetDatabase.SaveAssets();

            bool created = TimelineNudgeGroupUtility.TryCreateGroup(
                "Camera pair",
                timeline,
                new[] { first, second },
                out TimelineNudgeGroupData group,
                out string createError);
            Assert.That(created, Is.True, createError);

            first.start = 8d;
            second.start = 12d;

            bool resolved = TimelineNudgeGroupUtility.TryResolveGroup(
                group,
                out TimelineAsset resolvedTimeline,
                out TimelineClip[] resolvedClips,
                out string resolveError);

            Assert.That(resolved, Is.True, resolveError);
            Assert.That(resolvedTimeline, Is.SameAs(timeline));
            Assert.That(resolvedClips, Is.EquivalentTo(new[] { first, second }));
        }

        [Test]
        public void Group_DataSurvivesSerializationRoundTrip()
        {
            TimelineAsset timeline = CreateTimelineAsset(30d);
            ActivationTrack track = timeline.CreateTrack<ActivationTrack>(null, "Track");
            TimelineClip clip = CreateClip(track, 1d, 1d);
            AssetDatabase.SaveAssets();

            Assert.That(TimelineNudgeGroupUtility.TryCreateGroup(
                "Serializable",
                timeline,
                new[] { clip },
                out TimelineNudgeGroupData original,
                out string createError), Is.True, createError);

            string json = JsonUtility.ToJson(original);
            TimelineNudgeGroupData restored = JsonUtility.FromJson<TimelineNudgeGroupData>(json);

            Assert.That(restored.Name, Is.EqualTo(original.Name));
            Assert.That(restored.Id, Is.EqualTo(original.Id));
            Assert.That(restored.Clips.Count, Is.EqualTo(1));
            Assert.That(TimelineNudgeGroupUtility.TryResolveGroup(
                restored,
                out _,
                out TimelineClip[] resolvedClips,
                out string resolveError), Is.True, resolveError);
            Assert.That(resolvedClips, Is.EqualTo(new[] { clip }));
        }

        [Test]
        public void UiAssets_LoadAndContainRequiredControls()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensPath);
            StyleSheet styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);

            Assert.That(visualTree, Is.Not.Null);
            Assert.That(tokens, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);

            TemplateContainer root = visualTree.Instantiate();
            Assert.That(root.Q<IntegerField>("frames-field"), Is.Not.Null);
            Assert.That(root.Q<Button>("nudge-left-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("nudge-right-button"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("group-dropdown"), Is.Not.Null);
            Assert.That(root.Q<Button>("select-group-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("save-group-button"), Is.Not.Null);
        }

        private TimelineAsset CreateTimelineAsset(double frameRate)
        {
            string path = $"{TestFolder}/{Guid.NewGuid():N}.playable";
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.editorSettings.frameRate = frameRate;
            AssetDatabase.CreateAsset(timeline, path);
            _createdAssetPaths.Add(path);
            return timeline;
        }

        private static TimelineClip CreateClip(
            ActivationTrack track,
            double start,
            double duration)
        {
            TimelineClip clip = track.CreateDefaultClip();
            clip.start = start;
            clip.duration = duration;
            return clip;
        }

        private static TimelineClip CreateAnimationClip(
            AnimationTrack track,
            double start,
            double duration)
        {
            TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
            clip.start = start;
            clip.duration = duration;
            return clip;
        }
    }
}
