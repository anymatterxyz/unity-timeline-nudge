using System;
using UnityEditor;

namespace Baleev.TimelineNudge.Editor
{
    internal static class TimelineNudgePreferences
    {
        internal const int DefaultFrames = 5;
        internal const bool DefaultAutoFrameGroups = true;

        private const string FramesKey = "Baleev.TimelineNudge.Frames";
        private const string AutoFrameGroupsKey = "Baleev.TimelineNudge.AutoFrameGroups";

        internal static int Frames
        {
            get => Math.Max(1, EditorPrefs.GetInt(FramesKey, DefaultFrames));
            set => EditorPrefs.SetInt(FramesKey, Math.Max(1, value));
        }

        internal static bool AutoFrameGroups
        {
            get => EditorPrefs.GetBool(AutoFrameGroupsKey, DefaultAutoFrameGroups);
            set => EditorPrefs.SetBool(AutoFrameGroupsKey, value);
        }
    }
}
