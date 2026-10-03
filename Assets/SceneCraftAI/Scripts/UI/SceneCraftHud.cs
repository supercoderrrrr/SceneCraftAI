using System.Text;
using System.Collections.Generic;
using SceneCraftAI.Domain;
using SceneCraftAI.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace SceneCraftAI.UI
{
    public sealed class SceneCraftHud : MonoBehaviour
    {
        private readonly Queue<string> logLines = new Queue<string>();
        private SceneRuntimeController controller;
        private RuntimeSelectionController selection;
        private InputField promptInput;
        private Text statusText;
        private Text roomText;
        private Text roomCount;
        private Text objectCount;
        private Text layoutScore;
        private Text workflowCount;
        private Text logText;
        private Text selectionText;
        private Button offlineButton;
        private Button cloudButton;
        private Button alternativeButton;
        private readonly List<Button> selectionButtons = new List<Button>();
        private Button deleteButton;
        private Button lockButton;
        private Font font;
        private bool subscribed;
        private string planSummary;
        private RectTransform controlContent;
        private RectTransform detailsPanel;
        private ScrollRect logScroll;
        private Button detailsButton;
        private readonly List<Button> sceneButtons = new List<Button>();
        private readonly List<WorkflowRow> workflowRows = new List<WorkflowRow>();
        private Texture2D cardTexture;
        private Sprite cardSprite;
        private UiLanguage language;
        private Dropdown languageSelect;
        private readonly List<TextBinding> textBindings = new List<TextBinding>();

        public UiLanguage Language { get { return language; } }
        public string PromptText { get { return promptInput.text; } }

        private sealed class TextBinding
        {
            public Text Target;
            public string Key;
        }

        private static readonly Color Ink = new Color(0.88f, 0.91f, 0.94f);
        private static readonly Color Muted = new Color(0.53f, 0.60f, 0.67f);
        private static readonly Color Green = new Color(0.40f, 0.76f, 0.59f);
        private static readonly Color Blue = new Color(0.49f, 0.72f, 0.87f);
        private static readonly Color Amber = new Color(0.89f, 0.70f, 0.40f);
        private static readonly Color Red = new Color(0.89f, 0.48f, 0.48f);
        private static readonly Color QuietButton = new Color(0.15f, 0.19f, 0.23f);

        private sealed class WorkflowRow
        {
            public Image Background;
            public Image Marker;
            public Text Number;
            public Text Title;
            public Text Note;
            public Text State;
            public string Hint;
        }

        public void Initialize(SceneRuntimeController runtimeController, RuntimeSelectionController selectionController)
        {
            if (controller != null) return;
            controller = runtimeController;
            selection = selectionController;
            language = UiText.FromPreference(PlayerPrefs.GetInt(UiText.PreferenceKey, (int)UiLanguage.English));
            font = CreateFont();
            CreateCardSprite();
            BuildInterface();
            if (isActiveAndEnabled) Subscribe();
            RefreshState();
        }

        private void OnEnable()
        {
            if (controller == null) return;
            Subscribe();
            RefreshState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;

            controller.LogMessage += OnLogMessage;
            controller.SceneChanged += OnSceneChanged;
            controller.BusyChanged += OnBusyChanged;
            controller.PlanSummaryChanged += OnPlanSummaryChanged;
            controller.EvaluationChanged += OnEvaluationChanged;
            controller.ProgressChanged += OnProgressChanged;
            selection.SelectionChanged += OnSelectionChanged;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            if (controller != null)
            {
                controller.LogMessage -= OnLogMessage;
                controller.SceneChanged -= OnSceneChanged;
                controller.BusyChanged -= OnBusyChanged;
                controller.PlanSummaryChanged -= OnPlanSummaryChanged;
                controller.EvaluationChanged -= OnEvaluationChanged;
                controller.ProgressChanged -= OnProgressChanged;
            }

            if (selection != null) selection.SelectionChanged -= OnSelectionChanged;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (cardSprite != null) Destroy(cardSprite);
            if (cardTexture != null) Destroy(cardTexture);
        }

        private void RefreshState()
        {
            OnPlanSummaryChanged(controller.CurrentPlanSummary);
            OnBusyChanged(controller.IsBusy);
            OnProgressChanged();
            OnSelectionChanged(selection.Selected);
        }

        public void SetLanguage(UiLanguage value, bool remember = true)
        {
            language = UiText.FromPreference((int)value);
            if (remember)
            {
                PlayerPrefs.SetInt(UiText.PreferenceKey, (int)language);
                PlayerPrefs.Save();
            }
            languageSelect.SetValueWithoutNotify((int)language);
            for (int i = 0; i < textBindings.Count; i++) textBindings[i].Target.text = T(textBindings[i].Key);
            detailsButton.GetComponentInChildren<Text>().text = T(detailsPanel.gameObject.activeSelf ? "收起运行记录" : "查看运行记录");
            if (controller.IsBusy)
            {
                roomText.text = T("正在规划新的空间");
                statusText.text = T("正在生成，完成后可继续调整");
            }
            else OnEvaluationChanged();
            OnProgressChanged();
            OnSelectionChanged(selection.Selected);
            if (detailsPanel.gameObject.activeSelf) RefreshRecord(false);
        }

        private string T(string key, params object[] values) { return UiText.Get(language, key, values); }

        private void BuildInterface()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform header = CreatePanel("Header", transform, new Color(0.055f, 0.072f, 0.09f, 0.98f));
            SetAnchors(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -74f), Vector2.zero);
            Text title = CreateText("SceneCraft AI", header, 30, FontStyle.Bold, Color.white);
            SetAnchors(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 8f), new Vector2(-20f, -6f));
            title.alignment = TextAnchor.MiddleLeft;

            Text subtitle = CreateText("住宅空间设计演示", header, 15, FontStyle.Normal, Muted);
            SetAnchors(subtitle.rectTransform, new Vector2(0.5f, 0f), Vector2.one, new Vector2(-250f, 8f), new Vector2(-310f, -8f));
            subtitle.alignment = TextAnchor.MiddleRight;
            Text languageLabel = CreateText("界面语言", header, 12, FontStyle.Normal, Muted);
            SetAnchors(languageLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-252f, -17f), new Vector2(-172f, 17f));
            languageLabel.alignment = TextAnchor.MiddleRight;
            languageSelect = CreateLanguageSelect(header);
            SetAnchors(languageSelect.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-156f, -17f), new Vector2(-24f, 17f));

            RectTransform left = CreatePanel("ControlPanel", transform, new Color(0.075f, 0.093f, 0.113f, 0.98f));
            SetAnchors(left, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(400f, -74f));

            ScrollRect controls = CreateScroll("ControlsScroll", left);
            SetAnchors(controls.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0f, 72f), Vector2.zero);
            controlContent = CreateScrollContent(controls, "ControlsContent", 924f);
            Text promptLabel = CreateText("空间需求", controlContent, 21, FontStyle.Bold, Ink);
            SetRect(promptLabel.rectTransform, 24f, -24f, 352f, 28f);
            Text promptHint = CreateText("选好房间，家具和细节由系统补充", controlContent, 13, FontStyle.Normal, Muted);
            SetRect(promptHint.rectTransform, 24f, -56f, 352f, 22f);

            promptInput = CreateInput(controlContent);
            SetRect(promptInput.GetComponent<RectTransform>(), 24f, -84f, 352f, 88f);
            promptInput.text = language == UiLanguage.English ? UiText.EnglishPrompt : UiText.ChinesePrompt;

            offlineButton = CreateButton("本地生成", controlContent, new Color(0.20f, 0.39f, 0.34f));
            SetRect(offlineButton.GetComponent<RectTransform>(), 24f, -184f, 170f, 42f);
            offlineButton.onClick.AddListener(() => controller.Generate(promptInput.text, false));

            cloudButton = CreateButton("DeepSeek 生成", controlContent, new Color(0.24f, 0.34f, 0.44f));
            SetRect(cloudButton.GetComponent<RectTransform>(), 206f, -184f, 170f, 42f);
            cloudButton.onClick.AddListener(() => controller.Generate(promptInput.text, true));

            Button saveButton = CreateButton("保存场景", controlContent, QuietButton);
            SetRect(saveButton.GetComponent<RectTransform>(), 24f, -238f, 170f, 34f);
            saveButton.onClick.AddListener(controller.Save);
            sceneButtons.Add(saveButton);

            Button loadButton = CreateButton("载入场景", controlContent, QuietButton);
            SetRect(loadButton.GetComponent<RectTransform>(), 206f, -238f, 170f, 34f);
            loadButton.onClick.AddListener(controller.Load);
            sceneButtons.Add(loadButton);

            alternativeButton = CreateButton("换个布局", controlContent, QuietButton);
            SetRect(alternativeButton.GetComponent<RectTransform>(), 24f, -282f, 246f, 34f);
            alternativeButton.onClick.AddListener(controller.NextLayout);
            Button clearButton = CreateButton("清空", controlContent, new Color(0.26f, 0.18f, 0.20f));
            SetRect(clearButton.GetComponent<RectTransform>(), 282f, -282f, 94f, 34f);
            clearButton.onClick.AddListener(controller.ClearScene);
            sceneButtons.Add(clearButton);

            RectTransform overview = CreateCard("SceneOverview", controlContent, new Color(0.105f, 0.13f, 0.15f));
            SetRect(overview, 24f, -340f, 352f, 118f);
            roomText = CreateText("尚未生成场景", overview, 14, FontStyle.Normal, Ink);
            roomText.name = "RoomSummary";
            SetRect(roomText.rectTransform, 16f, -12f, 320f, 34f);
            roomCount = CreateMetric(overview, "RoomCount", "房间", 16f);
            objectCount = CreateMetric(overview, "ObjectCount", "物件", 128f);
            layoutScore = CreateMetric(overview, "LayoutScore", "布局评分", 240f);

            statusText = CreateText("准备就绪", controlContent, 13, FontStyle.Normal, Muted);
            statusText.name = "SceneStatus";
            SetRect(statusText.rectTransform, 24f, -468f, 352f, 28f);

            Text workflowLabel = CreateText("生成流程", controlContent, 16, FontStyle.Bold, Ink);
            SetRect(workflowLabel.rectTransform, 24f, -512f, 230f, 24f);
            workflowCount = CreateText("等待开始", controlContent, 12, FontStyle.Normal, Muted);
            workflowCount.name = "WorkflowCount";
            SetRect(workflowCount.rectTransform, 270f, -514f, 106f, 24f);
            workflowCount.alignment = TextAnchor.MiddleRight;
            string[] titles = { "解析需求", "整理物件", "设计布局", "检查方案", "搭建场景" };
            string[] hints = { "确认房间类型与设计偏好", "核对数量、禁用项与资源", "比较候选布局与空间关系", "检查门口、碰撞与支撑面", "创建房间、门窗与家具" };
            for (int i = 0; i < titles.Length; i++)
                CreateWorkflowRow(controlContent, i, titles[i], hints[i]);

            detailsButton = CreateButton("查看运行记录", controlContent, QuietButton);
            SetRect(detailsButton.GetComponent<RectTransform>(), 24f, -872f, 352f, 30f);
            detailsButton.onClick.AddListener(ToggleDetails);
            detailsPanel = CreateCard("RunDetails", controlContent, new Color(0.055f, 0.072f, 0.085f));
            SetRect(detailsPanel, 24f, -914f, 352f, 240f);
            logScroll = CreateScroll("LogScroll", detailsPanel);
            SetAnchors(logScroll.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            RectTransform logContent = CreateScrollContent(logScroll, "LogContent", 216f);
            logText = CreateText("", logContent, 12, FontStyle.Normal, Muted);
            SetAnchors(logText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            logText.alignment = TextAnchor.UpperLeft;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            detailsPanel.gameObject.SetActive(false);

            Text hint = CreateText("右键旋转   中键平移   滚轮缩放\n左键拖动家具   Shift 加大移动步长", left, 12, FontStyle.Normal, Muted);
            SetAnchors(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 16f), new Vector2(-24f, 56f));

            RectTransform selectedPanel = CreatePanel("SelectionPanel", transform, new Color(0.045f, 0.062f, 0.09f, 0.94f));
            SetAnchors(selectedPanel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-340f, 22f), new Vector2(-22f, 486f));
            Text selectedTitle = CreateText("已选择对象", selectedPanel, 16, FontStyle.Bold, Color.white);
            SetRect(selectedTitle.rectTransform, 16f, -14f, 286f, 26f);
            selectionText = CreateText("点击场景中的家具查看信息", selectedPanel, 13, FontStyle.Normal, new Color(0.70f, 0.78f, 0.86f));
            SetRect(selectionText.rectTransform, 16f, -44f, 286f, 58f);

            AddSelectionButton("左转 45°", selectedPanel, 16f, -108f, () => RotateSelected(-45f));
            AddSelectionButton("右转 45°", selectedPanel, 167f, -108f, () => RotateSelected(45f));
            AddSelectionButton("X −", selectedPanel, 16f, -156f, () => NudgeSelected(-NudgeStep(), 0f));
            AddSelectionButton("X +", selectedPanel, 167f, -156f, () => NudgeSelected(NudgeStep(), 0f));
            AddSelectionButton("Z −", selectedPanel, 16f, -204f, () => NudgeSelected(0f, -NudgeStep()));
            AddSelectionButton("Z +", selectedPanel, 167f, -204f, () => NudgeSelected(0f, NudgeStep()));
            AddSelectionButton("吸附最近墙", selectedPanel, 16f, -252f, SnapSelectedToWall);
            AddSelectionButton("自动找空位", selectedPanel, 167f, -252f, AutoPlaceSelected);
            AddSelectionButton("朝向语义锚点", selectedPanel, 16f, -300f, FaceSelectedToAnchor);
            AddSelectionButton("朝向并吸附锚点", selectedPanel, 167f, -300f, SnapSelectedToAnchor);
            lockButton = CreateButton("锁定位置", selectedPanel, new Color(0.34f, 0.48f, 0.28f));
            SetRect(lockButton.GetComponent<RectTransform>(), 16f, -348f, 286f, 40f);
            lockButton.onClick.AddListener(ToggleSelectedLock);
            selectionButtons.Add(lockButton);
            deleteButton = CreateButton("删除对象", selectedPanel, new Color(0.63f, 0.20f, 0.24f));
            SetRect(deleteButton.GetComponent<RectTransform>(), 16f, -396f, 135f, 40f);
            deleteButton.onClick.AddListener(DeleteSelected);
            selectionButtons.Add(deleteButton);
            Button undoButton = CreateButton("撤销上一步", selectedPanel, new Color(0.29f, 0.27f, 0.42f));
            SetRect(undoButton.GetComponent<RectTransform>(), 167f, -396f, 135f, 40f);
            undoButton.onClick.AddListener(controller.Undo);
            sceneButtons.Add(undoButton);
            SetSelectionButtons(false);
        }

        private void AddSelectionButton(string label, Transform parent, float x, float y, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(label, parent, new Color(0.17f, 0.45f, 0.62f));
            SetRect(button.GetComponent<RectTransform>(), x, y, 135f, 40f);
            button.onClick.AddListener(action);
            selectionButtons.Add(button);
        }

        private void RotateSelected(float degrees)
        {
            SceneObjectView view = selection.Selected;
            if (view == null) return;
            controller.RotateObject(view, degrees);
            OnSelectionChanged(view);
        }

        private void NudgeSelected(float x, float z)
        {
            SceneObjectView view = selection.Selected;
            if (view == null) return;
            controller.NudgeObject(view, x, z);
            OnSelectionChanged(view);
        }

        private void SnapSelectedToWall()
        {
            SceneObjectView view = selection.Selected;
            if (view == null) return;
            controller.SnapToWall(view);
            OnSelectionChanged(view);
        }

        private void AutoPlaceSelected()
        {
            SceneObjectView view = selection.Selected;
            if (view == null) return;
            controller.AutoPlaceObject(view);
            OnSelectionChanged(view);
        }

        private void FaceSelectedToAnchor()
        {
            SceneObjectView view = selection.Selected;
            if (view == null) return;
            controller.FaceAnchor(view);
            OnSelectionChanged(view);
        }

        private void SnapSelectedToAnchor()
        {
            SceneObjectView view = selection.Selected;
            if (view == null) return;
            controller.SnapToAnchor(view);
            OnSelectionChanged(view);
        }

        private static float NudgeStep()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 0.5f : 0.1f;
        }

        private void DeleteSelected()
        {
            SceneObjectView view = selection.Selected;
            if (view == null) return;
            selection.Clear();
            controller.DeleteObject(view);
        }

        private void ToggleSelectedLock()
        {
            SceneObjectView view = selection.Selected;
            if (view == null) return;
            controller.ToggleLock(view);
            OnSelectionChanged(view);
        }

        private void OnLogMessage(string message)
        {
            logLines.Enqueue(message);
            while (logLines.Count > 80) logLines.Dequeue();
            if (detailsPanel.gameObject.activeSelf) RefreshRecord();
        }

        private void OnSceneChanged(SceneSpec spec)
        {
            selection.Clear();
            OnEvaluationChanged();
        }

        private void OnPlanSummaryChanged(string summary)
        {
            planSummary = summary;
            if (detailsPanel.gameObject.activeSelf) RefreshRecord();
        }

        private void OnEvaluationChanged()
        {
            SceneSpec spec = controller.CurrentSpec;
            roomText.text = spec == null ? T("尚未生成场景") : DescribeRooms(spec);
            roomCount.text = spec == null ? "—" : spec.rooms.Count.ToString();
            objectCount.text = spec == null ? "—" : spec.placements.Count.ToString();
            layoutScore.text = controller.CurrentQuality == null ? "—" : controller.CurrentQuality.Score.ToString("0.0");
            bool warnings = controller.CurrentAudit != null && !controller.CurrentAudit.Passed;
            statusText.text = spec == null ? T("填写需求后即可生成") : warnings
                ? T("场景可调整，有 {0} 项摆放提醒", controller.CurrentAudit.Issues.Count)
                : T("场景已就绪，可直接调整家具");
            statusText.color = spec == null ? Muted : warnings ? Amber : Green;
            alternativeButton.interactable = spec != null && !controller.IsBusy;
        }

        private void OnBusyChanged(bool isBusy)
        {
            offlineButton.interactable = !isBusy;
            cloudButton.interactable = !isBusy;
            alternativeButton.interactable = !isBusy && controller.CurrentSpec != null;
            promptInput.interactable = !isBusy;
            for (int i = 0; i < sceneButtons.Count; i++) sceneButtons[i].interactable = !isBusy;
            SetSelectionButtons(!isBusy && selection.Selected != null);
            if (isBusy)
            {
                logLines.Clear();
                planSummary = null;
                roomText.text = T("正在规划新的空间");
                roomCount.text = "—";
                objectCount.text = "—";
                layoutScore.text = "—";
                statusText.text = T("正在生成，完成后可继续调整");
                statusText.color = Blue;
            }
            else
            {
                OnEvaluationChanged();
                OnProgressChanged();
            }
        }

        private void OnProgressChanged()
        {
            int finished = 0;
            bool failed = false;
            string activeLabel = null;
            for (int i = 0; i < workflowRows.Count; i++)
            {
                BuildProgress step = controller.Progress[i];
                WorkflowRow row = workflowRows[i];
                Color accent = step.State == BuildState.Done ? Green : step.State == BuildState.Running ? Blue
                    : step.State == BuildState.Failed ? Red : Muted;
                row.Title.color = accent;
                row.Number.color = accent;
                row.Marker.color = new Color(accent.r, accent.g, accent.b, 0.12f);
                row.Background.color = step.State == BuildState.Done ? new Color(0.09f, 0.15f, 0.14f)
                    : step.State == BuildState.Running ? new Color(0.12f, 0.18f, 0.23f)
                    : step.State == BuildState.Failed ? new Color(0.20f, 0.12f, 0.14f)
                    : new Color(0.10f, 0.12f, 0.145f);
                row.Number.text = step.State == BuildState.Done ? "✓" : (i + 1).ToString("00");
                row.Note.text = string.IsNullOrEmpty(step.Note) ? T(row.Hint) : UiText.ProgressNote(language, step);
                row.State.color = accent;
                row.State.text = T(step.State == BuildState.Done ? "完成" : step.State == BuildState.Running ? "进行中"
                    : step.State == BuildState.Skipped ? "沿用" : step.State == BuildState.Failed ? "未完成" : "等待");
                if (step.State == BuildState.Done || step.State == BuildState.Skipped) finished++;
                if (step.State == BuildState.Running) activeLabel = row.Title.text;
                failed |= step.State == BuildState.Failed;
            }
            workflowCount.text = failed ? T("已中止") : finished == 0 && activeLabel == null ? T("等待开始") : finished + " / " + workflowRows.Count;
            workflowCount.color = failed ? Red : finished == workflowRows.Count ? Green : Muted;
            if (failed)
            {
                statusText.text = T("生成未完成，请查看运行记录");
                statusText.color = Red;
            }
            else if (activeLabel != null)
            {
                statusText.text = T("正在执行：{0}", activeLabel);
                statusText.color = Blue;
            }
        }

        private string DescribeRooms(SceneSpec spec)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int i = 0; i < spec.rooms.Count; i++)
            {
                string label = UiText.RoomName(language, spec.rooms[i]);
                int count;
                counts.TryGetValue(label, out count);
                counts[label] = count + 1;
            }
            List<string> labels = new List<string>();
            foreach (KeyValuePair<string, int> entry in counts)
                labels.Add(entry.Key + (entry.Value > 1 ? " (" + entry.Value + ")" : string.Empty));
            return string.Join(language == UiLanguage.English ? ", " : "、", labels.ToArray());
        }

        private void ToggleDetails()
        {
            bool visible = !detailsPanel.gameObject.activeSelf;
            detailsPanel.gameObject.SetActive(visible);
            detailsButton.GetComponentInChildren<Text>().text = T(visible ? "收起运行记录" : "查看运行记录");
            controlContent.sizeDelta = new Vector2(0f, visible ? 1178f : 924f);
            if (visible) RefreshRecord();
        }

        private void RefreshRecord(bool followLatest = true)
        {
            float scrollPosition = logScroll.verticalNormalizedPosition;
            StringBuilder record = new StringBuilder();
            record.Append(T("运行记录保留原始技术信息")).Append("\n\n");
            if (!string.IsNullOrWhiteSpace(planSummary)) record.Append(planSummary).Append("\n\n");
            foreach (string line in logLines) record.AppendLine(line);
            logText.text = record.ToString();
            Canvas.ForceUpdateCanvases();
            logScroll.content.sizeDelta = new Vector2(0f, Mathf.Max(216f, logText.preferredHeight));
            logScroll.verticalNormalizedPosition = followLatest ? 0f : scrollPosition;
        }

        private void OnSelectionChanged(SceneObjectView view)
        {
            bool hasSelection = view != null;
            SetSelectionButtons(hasSelection && !controller.IsBusy);
            if (hasSelection)
            {
                string display = language == UiLanguage.English ? UiText.CategoryName(language, view.Spec.category) : view.AssetDisplayName;
                string source = view.AssetSource.StartsWith("Kenney CC0") ? "Kenney CC0" : T("程序化模型");
                selectionText.text = string.Format("{0}  {1}\n{2}\n{3}   X {4:0.0} / Z {5:0.0}   {6:0}°", display,
                    T(view.Spec.locked ? "已锁定" : "未锁定"), view.Spec.assetId, source, view.Spec.position.x, view.Spec.position.z, view.Spec.rotationY);
            }
            else selectionText.text = T("点击场景中的家具查看信息");
            if (lockButton != null)
            {
                Text label = lockButton.GetComponentInChildren<Text>();
                if (label != null) label.text = T(hasSelection && view.Spec.locked ? "解除位置锁定" : "锁定位置（换布局时保留）");
            }
        }

        private void SetSelectionButtons(bool enabled)
        {
            for (int i = 0; i < selectionButtons.Count; i++) selectionButtons[i].interactable = enabled;
        }

        private Dropdown CreateLanguageSelect(Transform parent)
        {
            RectTransform root = CreateCard("LanguageSelect", parent, QuietButton);
            Dropdown field = root.gameObject.AddComponent<Dropdown>();
            field.targetGraphic = root.GetComponent<Image>();
            Text caption = CreateText("English", root, 14, FontStyle.Normal, Ink);
            caption.name = "LanguageValue";
            SetAnchors(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-26f, 0f));
            caption.alignment = TextAnchor.MiddleLeft;
            Text arrow = CreateText("▾", root, 14, FontStyle.Normal, Muted);
            SetAnchors(arrow.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-24f, 0f), new Vector2(-8f, 0f));
            arrow.alignment = TextAnchor.MiddleCenter;
            RectTransform template = CreateCard("LanguageOptions", root, QuietButton);
            template.pivot = new Vector2(0.5f, 1f);
            SetAnchors(template, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -82f), new Vector2(0f, -4f));
            RectTransform content = new GameObject("OptionsContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(template, false);
            SetAnchors(content, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            RectTransform item = CreateCard("LanguageOption", content, new Color(0.20f, 0.28f, 0.30f));
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = Vector2.one;
            item.pivot = new Vector2(0.5f, 1f);
            item.sizeDelta = new Vector2(0f, 34f);
            Toggle toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = item.GetComponent<Image>();
            Text itemText = CreateText("English", item, 14, FontStyle.Normal, Ink);
            SetAnchors(itemText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-8f, 0f));
            itemText.alignment = TextAnchor.MiddleLeft;
            field.captionText = caption;
            field.itemText = itemText;
            field.template = template;
            field.options = new List<Dropdown.OptionData> { new Dropdown.OptionData("English"), new Dropdown.OptionData("中文") };
            template.gameObject.SetActive(false);
            field.SetValueWithoutNotify((int)language);
            field.onValueChanged.AddListener(index => SetLanguage(UiText.FromPreference(index)));
            return field;
        }

        private InputField CreateInput(Transform parent)
        {
            GameObject inputObject = new GameObject("PromptInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            Image background = inputObject.GetComponent<Image>();
            background.color = new Color(0.055f, 0.072f, 0.085f);
            background.sprite = cardSprite;
            background.type = Image.Type.Sliced;
            InputField field = inputObject.GetComponent<InputField>();
            field.lineType = InputField.LineType.MultiLineNewline;
            Text value = CreateText("Text", inputObject.transform, 16, FontStyle.Normal, Color.white);
            SetAnchors(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 10f), new Vector2(-12f, -10f));
            value.alignment = TextAnchor.UpperLeft;
            Text placeholder = CreateText("例如：客厅、主卧、次卧、厨房、卫生间", inputObject.transform, 14, FontStyle.Normal, Muted);
            SetAnchors(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 10f), new Vector2(-12f, -10f));
            placeholder.alignment = TextAnchor.UpperLeft;
            field.textComponent = value;
            field.placeholder = placeholder;
            return field;
        }

        private Button CreateButton(string label, Transform parent, Color color)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            image.sprite = cardSprite;
            image.type = Image.Type.Sliced;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.disabledColor = new Color(0.16f, 0.18f, 0.22f, 0.65f);
            button.colors = colors;
            Text text = CreateText(label, buttonObject.transform, 14, FontStyle.Normal, Ink);
            SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private Text CreateText(string value, Transform parent, int size, FontStyle style, Color color)
        {
            GameObject textObject = new GameObject(string.IsNullOrEmpty(value) ? "Text" : value, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = T(value);
            if (UiText.HasTranslation(value)) textBindings.Add(new TextBinding { Target = text, Key = value });
            text.raycastTarget = false;
            return text;
        }

        private Text CreateMetric(Transform parent, string objectName, string label, float x)
        {
            Text number = CreateText("—", parent, 25, FontStyle.Normal, Ink);
            number.name = objectName;
            SetRect(number.rectTransform, x, -48f, 96f, 34f);
            Text caption = CreateText(label, parent, 12, FontStyle.Normal, Muted);
            SetRect(caption.rectTransform, x, -86f, 96f, 20f);
            return number;
        }

        private void CreateWorkflowRow(Transform parent, int index, string title, string hint)
        {
            RectTransform card = CreateCard("Workflow_" + (BuildStage)index, parent, QuietButton);
            SetRect(card, 24f, -552f - index * 62f, 352f, 56f);
            RectTransform marker = CreateCard("StepMarker", card, QuietButton);
            SetRect(marker, 12f, -12f, 32f, 32f);
            Text number = CreateText((index + 1).ToString("00"), marker, 13, FontStyle.Normal, Muted);
            number.name = "StepNumber";
            SetAnchors(number.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            number.alignment = TextAnchor.MiddleCenter;
            Text heading = CreateText(title, card, 14, FontStyle.Bold, Ink);
            heading.name = "StepTitle";
            SetRect(heading.rectTransform, 56f, -8f, 190f, 22f);
            Text note = CreateText(hint, card, 11, FontStyle.Normal, Muted);
            note.name = "StepNote";
            SetRect(note.rectTransform, 56f, -32f, 282f, 18f);
            Text state = CreateText("等待", card, 11, FontStyle.Normal, Muted);
            state.name = "StepState";
            SetRect(state.rectTransform, 256f, -8f, 82f, 22f);
            state.alignment = TextAnchor.MiddleRight;
            workflowRows.Add(new WorkflowRow
            {
                Background = card.GetComponent<Image>(), Marker = marker.GetComponent<Image>(),
                Number = number, Title = heading, Note = note, State = state, Hint = hint
            });
        }

        private RectTransform CreateCard(string objectName, Transform parent, Color color)
        {
            RectTransform card = CreatePanel(objectName, parent, color);
            Image background = card.GetComponent<Image>();
            background.sprite = cardSprite;
            background.type = Image.Type.Sliced;
            return card;
        }

        private void CreateCardSprite()
        {
            const int size = 32;
            const float radius = 7f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                byte alpha = (byte)(Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            cardTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            cardTexture.SetPixels32(pixels);
            cardTexture.Apply(false, true);
            cardSprite = Sprite.Create(cardTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
            cardSprite.name = "PanelCorners";
        }

        private static ScrollRect CreateScroll(string objectName, Transform parent)
        {
            GameObject scrollObject = new GameObject(objectName, typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            RectTransform viewport = CreatePanel("Viewport", scrollObject.transform, new Color(0f, 0f, 0f, 0f));
            SetAnchors(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return scroll;
        }

        private static RectTransform CreateScrollContent(ScrollRect scroll, string objectName, float height)
        {
            RectTransform content = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(scroll.viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, height);
            scroll.content = content;
            return content;
        }

        private static RectTransform CreatePanel(string objectName, Transform parent, Color color)
        {
            GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel.GetComponent<RectTransform>();
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Font CreateFont()
        {
            string[] names = { "Microsoft YaHei", "SimHei", "Arial" };
            Font dynamicFont = Font.CreateDynamicFontFromOSFont(names, 18);
            return dynamicFont != null ? dynamicFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
