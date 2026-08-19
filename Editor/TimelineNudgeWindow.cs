using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.Timeline;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace Baleev.TimelineNudge.Editor
{
    internal sealed class TimelineNudgeWindow : EditorWindow
    {
        internal const string DockableMenuPath = "Window/Sequencing/Timeline Nudge";
        internal const string FloatingMenuPath = "Window/Sequencing/Timeline Nudge (Floating)";
        internal const string OpenShortcutId = "Timeline Nudge/Open Dockable Window";

        private const string UxmlPath =
            "Packages/com.baleev.timeline-nudge/Editor/UI/TimelineNudgeWindow.uxml";

        private static readonly Vector2 DefaultSize = new(380f, 265f);
        private static readonly Vector2 MinimumSize = new(360f, 230f);
        private static readonly Type InspectorWindowType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");

        private readonly List<TimelineNudgeGroupData> _groupOptions = new();

        private IntegerField _framesField;
        private Label _selectionCount;
        private Button _nudgeLeftButton;
        private Button _nudgeRightButton;
        private DropdownField _groupDropdown;
        private Toggle _autoFrameToggle;
        private Button _selectGroupButton;
        private Button _saveGroupButton;
        private Button _updateGroupButton;
        private Button _deleteGroupButton;
        private VisualElement _saveGroupRow;
        private TextField _groupNameField;
        private Button _confirmSaveButton;
        private Button _cancelSaveButton;
        private HelpBox _statusBox;

        private string _selectedGroupId;
        private double _feedbackExpiresAt;
        [SerializeField] private bool _isUtilityWindow;

        internal static Type PreferredDockTarget => InspectorWindowType;

        [MenuItem(DockableMenuPath)]
        private static void OpenDockableFromMenu()
        {
            OpenDockable();
        }

        [Shortcut(
            OpenShortcutId,
            KeyCode.N,
            ShortcutModifiers.Action | ShortcutModifiers.Alt)]
        private static void OpenDockableFromShortcut()
        {
            OpenDockable();
        }

        [MenuItem(FloatingMenuPath)]
        private static void OpenFloatingFromMenu()
        {
            TimelineNudgeWindow window = FindOpenWindow(isUtility: true);
            if (window == null)
            {
                window = CreateWindow<TimelineNudgeWindow>("Timeline Nudge");
                window._isUtilityWindow = true;
                ConfigureWindow(window, applyDefaultSize: true);
                window.ShowUtility();
            }

            window.Focus();
        }

        private static void OpenDockable()
        {
            TimelineNudgeWindow window = FindOpenWindow(isUtility: false);
            if (window == null)
            {
                Type[] preferredDockTargets = InspectorWindowType == null
                    ? Array.Empty<Type>()
                    : new[] { InspectorWindowType };
                window = CreateWindow<TimelineNudgeWindow>(
                    "Timeline Nudge",
                    preferredDockTargets);
                ConfigureWindow(window, applyDefaultSize: false);
                window.Show();
            }

            window.Focus();
        }

        private static TimelineNudgeWindow FindOpenWindow(bool isUtility)
        {
            return Resources.FindObjectsOfTypeAll<TimelineNudgeWindow>()
                .FirstOrDefault(window => window._isUtilityWindow == isUtility);
        }

        private static void ConfigureWindow(
            TimelineNudgeWindow window,
            bool applyDefaultSize)
        {
            window.minSize = MinimumSize;
            if (applyDefaultSize &&
                (window.position.width < MinimumSize.x ||
                 window.position.height < MinimumSize.y))
            {
                window.position = new Rect(window.position.position, DefaultSize);
            }
        }

        private void OnEnable()
        {
            minSize = MinimumSize;
            Selection.selectionChanged += RefreshUiState;
            EditorApplication.projectChanged += HandleProjectChanged;
            Undo.undoRedoPerformed += RefreshUiState;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= RefreshUiState;
            EditorApplication.projectChanged -= HandleProjectChanged;
            Undo.undoRedoPerformed -= RefreshUiState;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                rootVisualElement.Add(new HelpBox(
                    $"Не удалось загрузить интерфейс Timeline Nudge:\n{UxmlPath}",
                    HelpBoxMessageType.Error));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            if (!BindControls())
            {
                rootVisualElement.Clear();
                rootVisualElement.Add(new HelpBox(
                    "Интерфейс Timeline Nudge повреждён: отсутствуют обязательные элементы.",
                    HelpBoxMessageType.Error));
                return;
            }

            ConfigureControls();
            RefreshGroupChoices();
            RefreshUiState();
        }

        private void OnInspectorUpdate()
        {
            RefreshUiState();
        }

        private bool BindControls()
        {
            _framesField = rootVisualElement.Q<IntegerField>("frames-field");
            _selectionCount = rootVisualElement.Q<Label>("selection-count");
            _nudgeLeftButton = rootVisualElement.Q<Button>("nudge-left-button");
            _nudgeRightButton = rootVisualElement.Q<Button>("nudge-right-button");
            _groupDropdown = rootVisualElement.Q<DropdownField>("group-dropdown");
            _autoFrameToggle = rootVisualElement.Q<Toggle>("auto-frame-toggle");
            _selectGroupButton = rootVisualElement.Q<Button>("select-group-button");
            _saveGroupButton = rootVisualElement.Q<Button>("save-group-button");
            _updateGroupButton = rootVisualElement.Q<Button>("update-group-button");
            _deleteGroupButton = rootVisualElement.Q<Button>("delete-group-button");
            _saveGroupRow = rootVisualElement.Q<VisualElement>("save-group-row");
            _groupNameField = rootVisualElement.Q<TextField>("group-name-field");
            _confirmSaveButton = rootVisualElement.Q<Button>("confirm-save-button");
            _cancelSaveButton = rootVisualElement.Q<Button>("cancel-save-button");
            VisualElement statusContainer = rootVisualElement.Q<VisualElement>("status-container");

            if (_framesField == null
                || _selectionCount == null
                || _nudgeLeftButton == null
                || _nudgeRightButton == null
                || _groupDropdown == null
                || _autoFrameToggle == null
                || _selectGroupButton == null
                || _saveGroupButton == null
                || _updateGroupButton == null
                || _deleteGroupButton == null
                || _saveGroupRow == null
                || _groupNameField == null
                || _confirmSaveButton == null
                || _cancelSaveButton == null
                || statusContainer == null)
            {
                return false;
            }

            _statusBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            statusContainer.Add(_statusBox);
            return true;
        }

        private void ConfigureControls()
        {
            _framesField.isDelayed = true;
            _framesField.SetValueWithoutNotify(TimelineNudgePreferences.Frames);
            _framesField.RegisterValueChangedCallback(evt =>
            {
                int frames = Math.Max(1, evt.newValue);
                _framesField.SetValueWithoutNotify(frames);
                TimelineNudgePreferences.Frames = frames;
                RefreshButtonTooltips();
            });

            _nudgeLeftButton.clicked += () => Nudge(TimelineNudgeDirection.Left);
            _nudgeRightButton.clicked += () => Nudge(TimelineNudgeDirection.Right);
            _autoFrameToggle.SetValueWithoutNotify(TimelineNudgePreferences.AutoFrameGroups);
            _autoFrameToggle.RegisterValueChangedCallback(evt =>
                TimelineNudgePreferences.AutoFrameGroups = evt.newValue);
            _selectGroupButton.clicked += SelectCurrentGroup;
            _saveGroupButton.clicked += ShowSaveGroupRow;
            _updateGroupButton.clicked += UpdateCurrentGroup;
            _deleteGroupButton.clicked += DeleteCurrentGroup;
            _confirmSaveButton.clicked += SaveNewGroup;
            _cancelSaveButton.clicked += HideSaveGroupRow;

            _groupDropdown.RegisterValueChangedCallback(_ =>
            {
                int index = _groupDropdown.index;
                _selectedGroupId = index >= 0 && index < _groupOptions.Count
                    ? _groupOptions[index].Id
                    : null;
                RefreshUiState();
            });

            _groupNameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    SaveNewGroup();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    HideSaveGroupRow();
                    evt.StopPropagation();
                }
            });

            HideSaveGroupRow();
            HideStatus();
            RefreshButtonTooltips();
        }

        private void Nudge(TimelineNudgeDirection direction)
        {
            bool changed = TimelineNudgeCommands.TryNudgeSelected(
                direction,
                TimelineNudgePreferences.Frames,
                out string error);

            if (changed)
            {
                ShowFeedback(
                    $"Выбранные клипы смещены на {TimelineNudgePreferences.Frames} кадр(ов).",
                    HelpBoxMessageType.Info);
            }
            else if (!string.IsNullOrEmpty(error))
            {
                ShowFeedback(error, HelpBoxMessageType.Warning);
            }
            else
            {
                ShowFeedback(
                    "Смещение невозможно: самый ранний клип уже находится на отметке 0.",
                    HelpBoxMessageType.Info);
            }

            RefreshUiState();
        }

        private void ShowSaveGroupRow()
        {
            if (!HasUsableClipSelection())
            {
                ShowFeedback("Выберите клипы, которые нужно запомнить.", HelpBoxMessageType.Warning);
                return;
            }

            _saveGroupRow.style.display = DisplayStyle.Flex;
            _groupNameField.SetValueWithoutNotify(string.Empty);
            _groupNameField.Focus();
        }

        private void HideSaveGroupRow()
        {
            if (_saveGroupRow != null)
                _saveGroupRow.style.display = DisplayStyle.None;
        }

        private void SaveNewGroup()
        {
            if (!TryCreateCurrentGroup(_groupNameField.value, out TimelineNudgeGroupData group))
                return;

            TimelineNudgeGroupStore store = TimelineNudgeGroupStore.instance;
            TimelineNudgeGroupData existing = store.FindByName(group.Name);
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Заменить группу?",
                    $"Группа «{existing.Name}» уже существует. Заменить её текущим выделением?",
                    "Заменить",
                    "Отмена");
                if (!replace)
                    return;

                store.Replace(existing.Id, group);
                _selectedGroupId = existing.Id;
            }
            else
            {
                store.Add(group);
                _selectedGroupId = group.Id;
            }

            HideSaveGroupRow();
            RefreshGroupChoices();
            ShowFeedback($"Группа «{group.Name}» сохранена.", HelpBoxMessageType.Info);
        }

        private void SelectCurrentGroup()
        {
            TimelineNudgeGroupData group = GetSelectedGroup();
            if (!TimelineNudgeGroupSelection.TrySelect(
                    group,
                    TimelineNudgePreferences.AutoFrameGroups,
                    out string error,
                    delayedError =>
                    {
                        if (this != null)
                            ShowFeedback(delayedError, HelpBoxMessageType.Warning, 8d);
                    }))
            {
                ShowFeedback(error, HelpBoxMessageType.Error, 8d);
                return;
            }

            ShowFeedback(
                TimelineNudgePreferences.AutoFrameGroups
                    ? $"Группа «{group.Name}» восстановлена и показана в Timeline."
                    : $"Группа «{group.Name}» восстановлена в Timeline.",
                HelpBoxMessageType.Info);
        }

        private void UpdateCurrentGroup()
        {
            TimelineNudgeGroupData current = GetSelectedGroup();
            if (current == null)
            {
                ShowFeedback("Выберите сохранённую группу.", HelpBoxMessageType.Warning);
                return;
            }

            if (!TryCreateCurrentGroup(current.Name, out TimelineNudgeGroupData replacement))
                return;

            bool update = EditorUtility.DisplayDialog(
                "Обновить группу?",
                $"Заменить состав группы «{current.Name}» текущим выделением?",
                "Обновить",
                "Отмена");
            if (!update)
                return;

            TimelineNudgeGroupStore.instance.Replace(current.Id, replacement);
            _selectedGroupId = current.Id;
            RefreshGroupChoices();
            ShowFeedback($"Группа «{current.Name}» обновлена.", HelpBoxMessageType.Info);
        }

        private void DeleteCurrentGroup()
        {
            TimelineNudgeGroupData group = GetSelectedGroup();
            if (group == null)
                return;

            bool delete = EditorUtility.DisplayDialog(
                "Удалить группу?",
                $"Удалить группу «{group.Name}»? Сами клипы останутся без изменений.",
                "Удалить",
                "Отмена");
            if (!delete)
                return;

            TimelineNudgeGroupStore.instance.Remove(group.Id);
            _selectedGroupId = null;
            RefreshGroupChoices();
            ShowFeedback($"Группа «{group.Name}» удалена.", HelpBoxMessageType.Info);
        }

        private bool TryCreateCurrentGroup(
            string name,
            out TimelineNudgeGroupData group)
        {
            if (TimelineNudgeGroupUtility.TryCreateGroup(
                    name,
                    TimelineEditor.inspectedAsset,
                    TimelineEditor.selectedClips,
                    out group,
                    out string error))
            {
                return true;
            }

            ShowFeedback(error, HelpBoxMessageType.Error, 8d);
            return false;
        }

        private void HandleProjectChanged()
        {
            RefreshGroupChoices();
            RefreshUiState();
        }

        private void RefreshGroupChoices()
        {
            if (_groupDropdown == null)
                return;

            _groupOptions.Clear();
            _groupOptions.AddRange(TimelineNudgeGroupStore.instance.Groups
                .Where(group => group != null)
                .OrderBy(group => group.TimelineName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase));

            if (_groupOptions.Count == 0)
            {
                _selectedGroupId = null;
                _groupDropdown.choices = new List<string> { "Нет сохранённых групп" };
                _groupDropdown.SetValueWithoutNotify(_groupDropdown.choices[0]);
                _groupDropdown.SetEnabled(false);
                RefreshUiState();
                return;
            }

            var labels = _groupOptions
                .Select(group => $"{group.TimelineName} / {group.Name} ({group.Clips.Count})")
                .ToList();
            _groupDropdown.choices = labels;
            _groupDropdown.SetEnabled(true);

            int selectedIndex = _groupOptions.FindIndex(group => group.Id == _selectedGroupId);
            if (selectedIndex < 0)
                selectedIndex = 0;

            _selectedGroupId = _groupOptions[selectedIndex].Id;
            _groupDropdown.SetValueWithoutNotify(labels[selectedIndex]);
            _groupDropdown.index = selectedIndex;
            RefreshUiState();
        }

        private void RefreshUiState()
        {
            if (_framesField == null)
                return;

            TimelineAsset timeline = TimelineEditor.inspectedAsset;
            TimelineClip[] selectedClips = TimelineEditor.selectedClips;
            int selectedCount = selectedClips?.Length ?? 0;
            _selectionCount.text = selectedCount == 0
                ? "Клипы не выбраны"
                : $"Выбрано: {selectedCount}";

            bool hasSelection = timeline != null && selectedCount > 0;
            bool locked = hasSelection && selectedClips.Any(clip =>
                clip?.GetParentTrack() != null && clip.GetParentTrack().lockedInHierarchy);
            bool canNudge = hasSelection
                && !locked
                && TimelineNudgeCommands.IsValidFrameRate(timeline.editorSettings.frameRate);

            _nudgeLeftButton.SetEnabled(canNudge);
            _nudgeRightButton.SetEnabled(canNudge);
            _saveGroupButton.SetEnabled(hasSelection);

            bool hasGroup = GetSelectedGroup() != null;
            _selectGroupButton.SetEnabled(hasGroup);
            _updateGroupButton.SetEnabled(hasGroup && hasSelection);
            _deleteGroupButton.SetEnabled(hasGroup);

            if (EditorApplication.timeSinceStartup < _feedbackExpiresAt)
                return;

            if (timeline == null)
            {
                ShowStatus("Откройте Timeline, чтобы смещать или запоминать клипы.", HelpBoxMessageType.Info);
            }
            else if (selectedCount == 0)
            {
                ShowStatus("Выберите один или несколько клипов в Timeline.", HelpBoxMessageType.Info);
            }
            else if (locked)
            {
                ShowStatus("В выделении есть клип на заблокированном треке.", HelpBoxMessageType.Warning);
            }
            else
            {
                HideStatus();
            }
        }

        private bool HasUsableClipSelection()
        {
            return TimelineEditor.inspectedAsset != null
                && TimelineEditor.selectedClips.Length > 0;
        }

        private TimelineNudgeGroupData GetSelectedGroup()
        {
            return string.IsNullOrEmpty(_selectedGroupId)
                ? null
                : TimelineNudgeGroupStore.instance.FindById(_selectedGroupId);
        }

        private void RefreshButtonTooltips()
        {
            int frames = TimelineNudgePreferences.Frames;
            _nudgeLeftButton.tooltip = $"Сместить всю выбранную группу влево на {frames} кадр(ов) (Alt+Left)";
            _nudgeRightButton.tooltip = $"Сместить всю выбранную группу вправо на {frames} кадр(ов) (Alt+Right)";
        }

        private void ShowFeedback(
            string message,
            HelpBoxMessageType type,
            double durationSeconds = 4d)
        {
            _feedbackExpiresAt = EditorApplication.timeSinceStartup + durationSeconds;
            ShowStatus(message, type);
        }

        private void ShowStatus(string message, HelpBoxMessageType type)
        {
            if (_statusBox == null)
                return;

            _statusBox.text = message;
            _statusBox.messageType = type;
            _statusBox.style.display = DisplayStyle.Flex;
        }

        private void HideStatus()
        {
            if (_statusBox != null)
                _statusBox.style.display = DisplayStyle.None;
        }
    }
}
