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
        private bool compareOnly;
        private readonly List<Texture2D> snapshots = new List<Texture2D>();
        private const int RoomWidth = 720;
        private const int RoomHeight = 560;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-sceneCapture");
            bool comparison = false;
            if (index < 0) { index = Array.IndexOf(args, "-sceneCompare"); comparison = true; }
            if (index < 0 || index + 1 >= args.Length) return;
            SceneCapture capture = new GameObject("PortfolioCapture").AddComponent<SceneCapture>();
            capture.output = Path.GetFullPath(args[index + 1]);
            capture.compareOnly = comparison;
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
            if (compareOnly) { yield return CompareLayout(); yield break; }
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

        private IEnumerator CompareLayout()
        {
            SceneObjectView table = FindView("coffee_table");
            if (table == null) { Fail("Coffee table is missing"); yield break; }
            controller.ToggleLock(table);
            string lockedId = table.Spec.id;
            SceneSpec before = SceneSpecJson.FromJson(SceneSpecJson.ToJson(controller.CurrentSpec));
            RoomSpec living = before.rooms.Find(room => room.id == table.Spec.roomId);
            RoomSpec kitchen = before.rooms.Find(room => room.type == "kitchen");
            if (living == null || kitchen == null) { Fail("Comparison rooms are missing"); yield break; }
            canvas.enabled = false;
            Texture2D livingBefore = SnapshotRoom(living);
            Texture2D kitchenBefore = SnapshotRoom(kitchen);
            controller.NextLayout();
            yield return WaitForBuild();
            if (controller.IsBusy) { Fail("Alternative layout timed out"); yield break; }
            SceneSpec after = controller.CurrentSpec;
            Comparison result = new Comparison { capturedAt = DateTimeOffset.UtcNow.ToString("o"), lockedId = lockedId };
            result.roomsUnchanged = before.rooms.Count == after.rooms.Count;
            foreach (RoomSpec room in before.rooms)
            {
                RoomSpec current = after.rooms.Find(item => item.id == room.id);
                result.roomsUnchanged &= current != null && JsonUtility.ToJson(room) == JsonUtility.ToJson(current);
            }
            result.inventoryUnchanged = before.objects.Count == after.objects.Count;
            foreach (SceneObjectRequest request in before.objects)
                result.inventoryUnchanged &= after.objects.Exists(item => item.id == request.id && item.category == request.category && item.roomId == request.roomId);
            foreach (PlacedObjectSpec current in after.placements)
            {
                PlacedObjectSpec previous = before.placements.Find(item => item.id == current.id);
                if (previous == null) continue;
                result.objects.Add(new Change
                {
                    id = current.id, roomId = current.roomId, category = current.category, locked = current.locked,
                    before = previous.position, after = current.position,
                    distance = Vector3.Distance(previous.position, current.position),
                    yawBefore = previous.rotationY, yawAfter = current.rotationY,
                    yawChange = Mathf.Abs(Mathf.DeltaAngle(previous.rotationY, current.rotationY))
                });
            }
            Change locked = result.objects.Find(item => item.id == result.lockedId);
            result.lockPreserved = locked != null && locked.locked && locked.distance < 0.001f && locked.yawChange < 0.01f;
            Change moved = result.objects.Find(item => item.roomId == kitchen.id && item.category == "refrigerator");
            if (moved == null || moved.distance < 0.25f) { Fail("The refrigerator did not move enough for an honest comparison"); yield break; }
            result.highlightedId = moved.id;
            result.highlightedDistance = moved.distance;
            Change bookshelf = result.objects.Find(item => item.roomId == living.id && item.category == "bookshelf");
            if (bookshelf == null || bookshelf.distance < 0.25f) { Fail("The bookshelf did not move enough for an honest comparison"); yield break; }
            result.livingHighlightedId = bookshelf.id;
            result.livingHighlightedDistance = bookshelf.distance;
            result.beforeScore = report.layoutScore;
            result.afterScore = controller.CurrentQuality.Score;
            result.auditNotes = controller.CurrentAudit.Issues.Count;
            result.errors = errors.ToArray();
            Texture2D livingAfter = SnapshotRoom(living);
            Texture2D kitchenAfter = SnapshotRoom(kitchen);
            Canvas board = CreateBoard();
            DrawComparison(board.transform, living, livingBefore, livingAfter, locked, bookshelf, true, result.auditNotes);
            Capture("layout-lock-comparison.png");
            for (int frame = 0; frame < 60; frame++) { Capture("comparison-" + frame.ToString("0000") + ".png"); yield return null; }
            ClearBoard(board.transform);
            DrawComparison(board.transform, kitchen, kitchenBefore, kitchenAfter, moved, null, false, result.auditNotes);
            Capture("layout-move-comparison.png");
            for (int frame = 60; frame < 120; frame++) { Capture("comparison-" + frame.ToString("0000") + ".png"); yield return null; }
            result.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "comparison-result.json"), JsonUtility.ToJson(result, true));
            bool passed = result.lockPreserved && result.roomsUnchanged && result.inventoryUnchanged && errors.Count == 0;
            Debug.Log("SCENE_COMPARE: completed, checks passed " + passed + ", refrigerator moved " + moved.distance.ToString("0.00") + " m");
            Application.Quit(passed ? 0 : 2);
        }

        private Texture2D SnapshotRoom(RoomSpec room)
        {
            Renderer[] renderers = controller.GetComponentsInChildren<Renderer>();
            bool[] enabled = new bool[renderers.Length];
            RenderTexture photo = new RenderTexture(RoomWidth, RoomHeight, 24);
            RenderTexture previousTarget = cameraView.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    enabled[i] = renderer.enabled;
                    SceneObjectView view = renderer.GetComponentInParent<SceneObjectView>();
                    bool visible = view != null && view.Spec.roomId == room.id && view.Spec.category != "ceiling_light";
                    if (view == null)
                    {
                        Transform ancestor = renderer.transform.parent;
                        while (ancestor != null && ancestor != controller.transform && ancestor.name != "Room [" + room.id + "]") ancestor = ancestor.parent;
                        visible = ancestor != null && ancestor != controller.transform &&
                            !renderer.name.StartsWith("front_", StringComparison.Ordinal) && !renderer.name.StartsWith("left_", StringComparison.Ordinal);
                    }
                    renderer.enabled = enabled[i] && visible;
                }
                cameraView.orthographic = true;
                cameraView.orthographicSize = Mathf.Max(room.width, room.depth) * 0.63f;
                cameraView.aspect = (float)RoomWidth / RoomHeight;
                Quaternion rotation = Quaternion.Euler(64f, 35f, 0f);
                cameraView.transform.SetPositionAndRotation(room.center + Vector3.up * 0.65f - rotation * Vector3.forward * 24f, rotation);
                cameraView.targetTexture = photo;
                cameraView.Render();
                RenderTexture.active = photo;
                Texture2D snapshot = new Texture2D(RoomWidth, RoomHeight, TextureFormat.RGB24, false);
                snapshot.ReadPixels(new Rect(0f, 0f, RoomWidth, RoomHeight), 0, 0);
                snapshot.Apply();
                snapshots.Add(snapshot);
                return snapshot;
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                cameraView.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                photo.Release();
                Destroy(photo);
            }
        }

        private Canvas CreateBoard()
        {
            GameObject root = new GameObject("ComparisonBoard", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            Canvas board = root.GetComponent<Canvas>();
            board.renderMode = RenderMode.ScreenSpaceCamera;
            board.worldCamera = cameraView;
            board.planeDistance = 0.5f;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            cameraView.aspect = (float)Width / Height;
            return board;
        }

        private void ClearBoard(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                root.GetChild(i).gameObject.SetActive(false);
                Destroy(root.GetChild(i).gameObject);
            }
        }

        private void DrawComparison(Transform root, RoomSpec room, Texture2D before, Texture2D after, Change change, Change secondary, bool locked, int auditNotes)
        {
            Color green = new Color(0.38f, 0.91f, 0.66f);
            Color orange = new Color(1f, 0.72f, 0.35f);
            Color white = new Color(0.9f, 0.94f, 0.97f);
            Color muted = new Color(0.60f, 0.69f, 0.77f);
            Box(root, Vector2.zero, new Vector2(Width, Height), new Color(0.035f, 0.055f, 0.075f));
            Label(root, new Vector2(48f, 28f), new Vector2(1500f, 44f), locked ? "01  LOCK AN OBJECT, THEN TRY ANOTHER LAYOUT" : "02  UNLOCKED FURNITURE CAN CHANGE POSITION", 32, white);
            Label(root, new Vector2(48f, 80f), new Vector2(1500f, 32f), locked ? "Living room: the coffee table stays in place" : "Kitchen: the refrigerator is replanned by the same operation", 24, muted);
            for (int side = 0; side < 2; side++)
            {
                Vector2 origin = new Vector2(side == 0 ? 48f : 832f, 164f);
                Box(root, origin - new Vector2(2f, 2f), new Vector2(RoomWidth + 4, RoomHeight + 4), new Color(0.19f, 0.25f, 0.3f));
                GameObject imageRoot = new GameObject("RoomSnapshot", typeof(RectTransform), typeof(RawImage));
                imageRoot.transform.SetParent(root, false);
                Rect(imageRoot.GetComponent<RectTransform>(), origin, new Vector2(RoomWidth, RoomHeight));
                imageRoot.GetComponent<RawImage>().texture = side == 0 ? before : after;
                Label(root, origin - new Vector2(0f, 41f), new Vector2(RoomWidth, 34f), side == 0 ? "BEFORE" : "AFTER", 26, white);
                Vector2 point = RoomPoint(room, side == 0 ? change.before : change.after);
                Color color = locked ? green : orange;
                Outline(root, origin + point - Vector2.one * 33f, Vector2.one * 66f, color);
                if (!locked && side == 1)
                {
                    Vector2 oldPoint = RoomPoint(room, change.before);
                    Outline(root, origin + oldPoint - Vector2.one * 29f, Vector2.one * 58f, muted);
                    Arrow(root, origin + oldPoint, origin + point, color);
                }
                Vector2 tag = new Vector2(Mathf.Clamp(point.x - 130f, 8f, RoomWidth - 270f), Mathf.Clamp(point.y + 47f, 8f, RoomHeight - 65f));
                Box(root, origin + tag, new Vector2(260f, 56f), new Color(0.035f, 0.055f, 0.075f, 0.95f));
                Label(root, origin + tag + new Vector2(10f, 4f), new Vector2(245f, 50f), locked ? "LOCKED COFFEE TABLE\nPosition + rotation kept" : (side == 0 ? "REFRIGERATOR\nOriginal position" : "REFRIGERATOR\nMoved " + change.distance.ToString("0.00") + " m"), 19, color);
                if (secondary != null)
                {
                    Vector2 other = RoomPoint(room, side == 0 ? secondary.before : secondary.after);
                    Outline(root, origin + other - Vector2.one * 33f, Vector2.one * 66f, orange);
                    if (side == 1) Arrow(root, origin + RoomPoint(room, secondary.before), origin + other, orange);
                    Vector2 otherTag = new Vector2(Mathf.Clamp(other.x - 130f, 8f, RoomWidth - 270f), Mathf.Clamp(other.y + 47f, 8f, RoomHeight - 65f));
                    Box(root, origin + otherTag, new Vector2(260f, 56f), new Color(0.035f, 0.055f, 0.075f, 0.95f));
                    Label(root, origin + otherTag + new Vector2(10f, 4f), new Vector2(245f, 50f), side == 0 ? "BOOKSHELF\nOriginal position" : "BOOKSHELF\nMoved " + secondary.distance.ToString("0.00") + " m / " + secondary.yawChange.ToString("0") + " deg", 19, orange);
                }
            }
            string movement = locked ? "Green table: " + change.distance.ToString("0.00") + " m / " + change.yawChange.ToString("0") + " deg    Orange bookshelf: " + secondary.distance.ToString("0.00") + " m / " + secondary.yawChange.ToString("0") + " deg" : "Orange arrow: original position to new position    Distance: " + change.distance.ToString("0.00") + " m";
            Label(root, new Vector2(48f, 752f), new Vector2(1500f, 34f), movement, 24, locked ? green : orange);
            Label(root, new Vector2(48f, 801f), new Vector2(1500f, 30f), "Same floor plan, same inventory, same camera    No furniture was manually moved for this comparison", 21, white);
            Label(root, new Vector2(48f, 846f), new Vector2(1500f, 28f), "Room cutaway: other rooms, ceiling lights and camera-facing walls hidden    " + auditNotes + " layout audit notes remain", 18, muted);
        }

        private Vector2 RoomPoint(RoomSpec room, Vector3 world)
        {
            float previousAspect = cameraView.aspect;
            cameraView.aspect = (float)RoomWidth / RoomHeight;
            cameraView.orthographicSize = Mathf.Max(room.width, room.depth) * 0.63f;
            Quaternion rotation = Quaternion.Euler(64f, 35f, 0f);
            cameraView.transform.SetPositionAndRotation(room.center + Vector3.up * 0.65f - rotation * Vector3.forward * 24f, rotation);
            Vector3 point = cameraView.WorldToViewportPoint(world + Vector3.up * 0.4f);
            cameraView.aspect = previousAspect;
            return new Vector2(point.x * RoomWidth, (1f - point.y) * RoomHeight);
        }

        private static void Rect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;
        }

        private static Image Box(Transform root, Vector2 position, Vector2 size, Color color)
        {
            GameObject item = new GameObject("Annotation", typeof(RectTransform), typeof(Image));
            item.transform.SetParent(root, false);
            Image image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Rect(image.rectTransform, position, size);
            return image;
        }

        private void Label(Transform root, Vector2 position, Vector2 size, string value, int fontSize, Color color)
        {
            GameObject item = new GameObject("AnnotationLabel", typeof(RectTransform), typeof(Text));
            item.transform.SetParent(root, false);
            Text text = item.GetComponent<Text>();
            text.font = caption.font;
            text.fontSize = fontSize;
            text.text = value;
            text.color = color;
            text.raycastTarget = false;
            Rect(text.rectTransform, position, size);
        }

        private static void Outline(Transform root, Vector2 position, Vector2 size, Color color)
        {
            Box(root, position, new Vector2(size.x, 3f), color);
            Box(root, position + new Vector2(0f, size.y), new Vector2(size.x, 3f), color);
            Box(root, position, new Vector2(3f, size.y), color);
            Box(root, position + new Vector2(size.x, 0f), new Vector2(3f, size.y + 3f), color);
        }

        private static void Arrow(Transform root, Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            if (delta.magnitude < 1f) return;
            float angle = -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Image shaft = Box(root, from, new Vector2(delta.magnitude, 4f), color);
            shaft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            foreach (float offset in new[] { -155f, 155f })
            {
                Image head = Box(root, to, new Vector2(22f, 4f), color);
                head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + offset);
            }
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
            foreach (Texture2D snapshot in snapshots) if (snapshot != null) Destroy(snapshot);
        }

        [Serializable]
        private sealed class Comparison
        {
            public string capturedAt, lockedId, highlightedId, livingHighlightedId;
            public bool roomsUnchanged, inventoryUnchanged, lockPreserved;
            public float highlightedDistance, livingHighlightedDistance, beforeScore, afterScore;
            public int auditNotes;
            public string[] errors;
            public List<Change> objects = new List<Change>();
        }

        [Serializable]
        private sealed class Change
        {
            public string id, roomId, category;
            public bool locked;
            public Vector3 before, after;
            public float distance, yawBefore, yawAfter, yawChange;
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
