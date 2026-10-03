#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SceneCraftAI.Domain;
using SceneCraftAI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SceneCraftAI.Runtime
{
    public sealed class SceneCapture : MonoBehaviour
    {
        private const int Width = 1600;
        private const int Height = 900;
        private const int Rate = 15;
        private string output;
        private Camera cameraView;
        private SceneRuntimeController controller;
        private RuntimeSelectionController selection;
        private SceneCraftHud hud;
        private Canvas canvas;
        private Transform selectionPanel;
        private RenderTexture target;
        private Texture2D pixels;
        private Text caption;
        private readonly List<string> errors = new List<string>();
        private readonly Report report = new Report();
        private int originalCaptureRate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-sceneCapture");
            if (index < 0 || index + 1 >= args.Length) return;
            SceneCapture capture = new GameObject("PortfolioCapture").AddComponent<SceneCapture>();
            capture.output = Path.GetFullPath(args[index + 1]);
        }

        private void OnEnable() { Application.logMessageReceived += ReadLog; }
        private void OnDisable() { Application.logMessageReceived -= ReadLog; }

        private void ReadLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) errors.Add(message);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            controller = FindObjectOfType<SceneRuntimeController>();
            selection = FindObjectOfType<RuntimeSelectionController>();
            hud = FindObjectOfType<SceneCraftHud>();
            cameraView = Camera.main;
            yield return WaitForBuild();
            if (controller == null || hud == null || cameraView == null) { Fail("Runtime components are missing"); yield break; }
            hud.SetLanguage(UiLanguage.English, false);
            Find<InputField>("PromptInput").text = UiText.EnglishPrompt;
            controller.Generate(UiText.EnglishPrompt, false);
            yield return WaitForBuild();
            if (controller.CurrentSpec == null) { Fail("The apartment could not be built"); yield break; }
            cameraView.GetComponent<OrbitCameraController>().enabled = false;
            selection.enabled = false;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            originalCaptureRate = Time.captureFramerate;
            Time.captureFramerate = Rate;
            target = new RenderTexture(Width, Height, 24);
            target.Create();
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cameraView;
            canvas.planeDistance = 0.8f;
            CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Width / 1920f;
            canvas.scaleFactor = scaler.scaleFactor;
            selectionPanel = Find<RectTransform>("SelectionPanel");
            caption = CreateCaption();
            report.width = Width;
            report.height = Height;
            report.playbackFps = Rate;
            report.capturedAt = DateTimeOffset.UtcNow.ToString("o");
            report.rooms = controller.CurrentSpec.rooms.Count;
            report.requestedObjects = controller.CurrentSpec.objects.Count;
            report.placedObjects = controller.CurrentSpec.placements.Count;
            report.layoutScore = controller.CurrentQuality.Score;
            report.auditNotes = controller.CurrentAudit.Issues.Count;
            report.cloudRequests = 0;
            HomeView(35f);
            selectionPanel.gameObject.SetActive(false);
            caption.text = "ROOM-FIRST DESIGN\n5 connected rooms from a single short brief";
            Capture("apartment-overview.png");
            for (int frame = 0; frame < 120; frame++)
            {
                HomeView(Mathf.Lerp(35f, 95f, frame / 119f));
                Capture("tour-" + frame.ToString("0000") + ".png");
                yield return null;
            }

            HomeView(35f);
            selectionPanel.gameObject.SetActive(true);
            Vector3 delta;
            SceneObjectView edited = FindMovableView(out delta);
            if (edited == null) { Fail("No valid manual move was available"); yield break; }
            selection.Select(edited);
            string editedId = edited.Spec.id;
            Vector3 initialPosition = edited.Spec.position;
            report.editedCategory = edited.Spec.category;
            caption.text = "INTERACTIVE EDITING\nValidated movement and undo on the generated scene";
            controller.BeginObjectMove(edited);
            for (int frame = 0; frame < 60; frame++)
            {
                if (frame < 30)
                {
                    string reason;
                    controller.PreviewObjectMove(edited, initialPosition + delta * Mathf.SmoothStep(0f, 1f, frame / 29f), out reason);
                }
                if (frame == 30)
                {
                    report.manualMoveAccepted = controller.CommitObjectMove(edited, initialPosition + delta);
                    selection.Clear();
                    selection.Select(edited);
                    Capture("furniture-editing.png");
                }
                if (frame == 45)
                {
                    controller.Undo();
                    yield return null;
                    edited = Array.Find(FindObjectsOfType<SceneObjectView>(), item => item.Spec.id == editedId);
                    report.undoRestoredPosition = edited != null && Vector3.Distance(edited.Spec.position, initialPosition) < 0.001f;
                    selection.Select(edited);
                }
                Capture("edit-" + frame.ToString("0000") + ".png");
                yield return null;
            }

            SceneObjectView table = FindView("coffee_table");
            if (table == null) { Fail("Coffee table is missing"); yield break; }
            selection.Select(table);
            Find<Button>("锁定位置").onClick.Invoke();
            string lockedId = table.Spec.id;
            Vector3 lockedPosition = table.Spec.position;
            SceneSpec initialLayout = SceneSpecJson.FromJson(SceneSpecJson.ToJson(controller.CurrentSpec));
            caption.text = "ALTERNATIVE LAYOUT\nKeep the coffee table locked while the other objects are replanned";
            Capture("layout-before.png");
            int layoutFrame = 0;
            for (; layoutFrame < 30; layoutFrame++) { Capture("layout-" + layoutFrame.ToString("0000") + ".png"); yield return null; }
            controller.NextLayout();
            for (int wait = 0; controller.IsBusy && wait < 240; wait++, layoutFrame++)
            {
                Capture("layout-" + layoutFrame.ToString("0000") + ".png");
                yield return null;
            }
            if (controller.IsBusy) { Fail("Alternative layout timed out"); yield break; }
            PlacedObjectSpec locked = controller.CurrentSpec.placements.Find(item => item.id == lockedId);
            report.lockedPositionPreserved = locked != null && locked.locked && Vector3.Distance(locked.position, lockedPosition) < 0.001f;
            foreach (PlacedObjectSpec item in controller.CurrentSpec.placements)
            {
                PlacedObjectSpec previous = initialLayout.placements.Find(other => other.id == item.id);
                if (previous != null && (Vector3.Distance(previous.position, item.position) > 0.001f || Mathf.Abs(Mathf.DeltaAngle(previous.rotationY, item.rotationY)) > 0.01f))
                    report.changedObjects++;
            }
            report.alternativeChanged = report.changedObjects > 0;
            report.alternativeScore = controller.CurrentQuality.Score;
            selection.Select(FindView("coffee_table"));
            Capture("layout-after.png");
            for (; layoutFrame < 120; layoutFrame++) { Capture("layout-" + layoutFrame.ToString("0000") + ".png"); yield return null; }
            report.layoutClipFrames = layoutFrame;

            selection.Clear();
            selectionPanel.gameObject.SetActive(false);
            SceneSpec previousSpec = controller.CurrentSpec;
            string brief = hud.PromptText;
            for (int frame = 0; frame < 60; frame++)
            {
                if (frame == 15) hud.SetLanguage(UiLanguage.Chinese, false);
                if (frame == 45) hud.SetLanguage(UiLanguage.English, false);
                caption.text = "BILINGUAL INTERFACE\nEnglish / 中文 without changing the brief or rebuilding the scene";
                Capture("language-" + frame.ToString("0000") + ".png");
                if (frame == 30) Capture("interface-chinese.png");
                yield return null;
            }
            report.languagePreservedScene = ReferenceEquals(previousSpec, controller.CurrentSpec) && brief == hud.PromptText;
            HomeView(35f);
            Capture("interface-english.png");

            canvas.enabled = false;
            RoomSpec bedroom = controller.CurrentSpec.rooms.Find(room => room.description.Contains("master_bedroom") || room.id.Contains("master_bedroom"));
            RoomSpec kitchen = controller.CurrentSpec.rooms.Find(room => room.type == "kitchen");
            RoomSpec bathroom = controller.CurrentSpec.rooms.Find(room => room.type == "bathroom");
            foreach (RoomSpec room in new[] { bedroom, kitchen, bathroom })
            {
                if (room == null) continue;
                RoomView(room);
                Capture(room == bedroom ? "bedroom-details.png" : room.type + "-details.png");
                yield return null;
            }
            canvas.enabled = true;
            report.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "capture-result.json"), JsonUtility.ToJson(report, true));
            bool passed = report.manualMoveAccepted && report.undoRestoredPosition && report.lockedPositionPreserved && report.alternativeChanged && report.languagePreservedScene && errors.Count == 0;
            Debug.Log("SCENE_CAPTURE: completed, checks passed " + passed);
            Application.Quit(passed ? 0 : 2);
        }

        private IEnumerator WaitForBuild()
        {
            for (int frame = 0; controller != null && controller.IsBusy && frame < 240; frame++) yield return null;
            yield return null;
        }

        private void HomeView(float yaw)
        {
            Bounds bounds = new Bounds(controller.CurrentSpec.rooms[0].center, Vector3.zero);
            foreach (RoomSpec room in controller.CurrentSpec.rooms)
                bounds.Encapsulate(new Bounds(room.center + Vector3.up * 1.4f, new Vector3(room.width, room.height, room.depth)));
            cameraView.orthographic = true;
            cameraView.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * 0.58f;
            Quaternion rotation = Quaternion.Euler(66f, yaw, 0f);
            Vector3 focus = bounds.center - rotation * Vector3.right * 2.1f;
            cameraView.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 34f, rotation);
        }

        private void RoomView(RoomSpec room)
        {
            cameraView.orthographicSize = Mathf.Max(room.width, room.depth) * 0.62f;
            Quaternion rotation = Quaternion.Euler(76f, 0f, 0f);
            cameraView.transform.SetPositionAndRotation(room.center + Vector3.up * 0.8f - rotation * Vector3.forward * 24f, rotation);
        }

        private Text CreateCaption()
        {
            GameObject root = new GameObject("CaptureCaption", typeof(RectTransform), typeof(Text));
            root.transform.SetParent(hud.transform, false);
            Text text = root.GetComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 20);
            text.fontSize = 20;
            text.color = new Color(0.78f, 0.84f, 0.9f);
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(430f, -100f);
            rect.sizeDelta = new Vector2(1400f, 64f);
            return text;
        }

        private SceneObjectView FindView(string category)
        {
            return Array.Find(FindObjectsOfType<SceneObjectView>(), item => item.Spec.category == category);
        }

        private SceneObjectView FindMovableView(out Vector3 delta)
        {
            SceneObjectView[] views = FindObjectsOfType<SceneObjectView>();
            foreach (string category in new[] { "floor_lamp", "plant", "chair", "sofa" })
            foreach (SceneObjectView view in views)
            {
                if (view.Spec.category != category) continue;
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    controller.BeginObjectMove(view);
                    string reason;
                    bool valid = controller.PreviewObjectMove(view, view.Spec.position + direction * 0.35f, out reason);
                    controller.CancelObjectMove(view);
                    if (valid) { delta = direction * 0.35f; return view; }
                }
            }
            delta = Vector3.zero;
            return null;
        }

        private T Find<T>(string objectName) where T : Component
        {
            foreach (T item in hud.GetComponentsInChildren<T>(true)) if (item.name == objectName) return item;
            throw new InvalidOperationException("HUD component is missing: " + objectName);
        }

        private void Capture(string name)
        {
            RenderTexture previousTarget = cameraView.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                cameraView.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                cameraView.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            }
            finally { cameraView.targetTexture = previousTarget; RenderTexture.active = previousActive; }
        }

        private void Fail(string message)
        {
            Debug.LogError("SCENE_CAPTURE: " + message);
            Application.Quit(2);
        }

        private void OnDestroy()
        {
            Time.captureFramerate = originalCaptureRate;
            if (target != null) { target.Release(); Destroy(target); }
            if (pixels != null) Destroy(pixels);
        }

        [Serializable]
        private sealed class Report
        {
            public string capturedAt;
            public string editedCategory;
            public int width, height, playbackFps, rooms, requestedObjects, placedObjects, auditNotes, cloudRequests, layoutClipFrames, changedObjects;
            public float layoutScore, alternativeScore;
            public bool manualMoveAccepted, undoRestoredPosition, lockedPositionPreserved, alternativeChanged, languagePreservedScene;
            public string[] errors;
        }
    }
}
#endif
