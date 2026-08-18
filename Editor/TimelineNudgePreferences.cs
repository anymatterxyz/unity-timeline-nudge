using System;
using UnityEditor;

namespace Baleev.TimelineNudge.Editor
{
    internal static class TimelineNudgePreferences
    {
        internal const int DefaultFrames = 5;

        private const string FramesKey = "Baleev.TimelineNudge.Frames";

        internal static int Frames
        {
            get => Math.Max(1, EditorPrefs.GetInt(FramesKey, DefaultFrames));
            set => EditorPrefs.SetInt(FramesKey, Math.Max(1, value));
        }
    }
}
