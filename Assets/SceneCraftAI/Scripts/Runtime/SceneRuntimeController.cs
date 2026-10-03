using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using SceneCraftAI.Layout;
using SceneCraftAI.Planning;
using UnityEngine;

namespace SceneCraftAI.Runtime
{
    public enum BuildStage { Requirements, Inventory, Layout, Review, Scene }
    public enum BuildState { Waiting, Running, Done, Skipped, Failed }

    public readonly struct BuildProgress
    {
        public BuildStage Stage { get; }
        public BuildState State { get; }
        public string Note { get; }
        public int Count { get; }
        public float Score { get; }
        public string Detail { get; }

        public BuildProgress(BuildStage stage, BuildState state, string note, int count = 0, float score = 0f, string detail = null)
        {
            Stage = stage;
            State = state;
            Note = note;
            Count = count;
            Score = score;
            Detail = detail;
        }
    }

    public sealed class SceneRuntimeController : MonoBehaviour
    {
        private const string SaveFileName = "scenecraft_scene.json";

        private readonly AssetCatalog catalog = new AssetCatalog();
        private readonly SceneLayoutEngine layout = new SceneLayoutEngine();
        private readonly OfflineScenePlanner offlinePlanner = new OfflineScenePlanner();
        private SceneLayoutOptimizer optimizer;
        private HybridAssetFactory assetFactory;
        private RoomViewBuilder roomBuilder;
        private Transform generatedRoot;
        private bool busy;
        private readonly Stack<string> undoHistory = new Stack<string>();
        private string dragJson;
        private string planJson;
        private int nextVariant;
        private readonly BuildProgress[] progress = new BuildProgress[5];
        private IReadOnlyList<BuildProgress> progressView;
        private int activeStage = -1;
        private bool plannerFallback;

        private const int LayoutCandidateCount = 4;

        public event Action<string> LogMessage;
        public event Action<SceneSpec> SceneChanged;
        public event Action<bool> BusyChanged;
        public event Action<string> PlanSummaryChanged;
        public event Action EvaluationChanged;
        public event Action ProgressChanged;

        public SceneSpec CurrentSpec { get; private set; }
        public LayoutQualityReport CurrentQuality { get; private set; }
        public SceneAuditReport CurrentAudit { get; private set; }
        public bool IsBusy { get { return busy; } }
        public string CurrentPlanSummary { get; private set; }
        public IReadOnlyList<BuildProgress> Progress { get { return progressView; } }
        public string SavePath
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UserData", SaveFileName);
#else
                string buildFolder = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(buildFolder, "SceneCraftData", SaveFileName);
#endif
            }
        }

        private void Awake()
        {
            progressView = Array.AsReadOnly(progress);
            ResetProgress();
            assetFactory = new HybridAssetFactory();
            roomBuilder = new RoomViewBuilder();
            optimizer = new SceneLayoutOptimizer(layout);
            Log("Asset Library: " + assetFactory.RealAssetCount + " curated prefab mappings available; procedural fallback remains enabled.");
        }

        private void OnDisable()
        {
            if (busy) FailBuild();
        }

        public void Generate(string prompt, bool useCloud)
        {
            if (busy) return;
            string safePrompt = string.IsNullOrWhiteSpace(prompt)
                ? "一套生活化住宅：客厅、主卧、次卧、厨房和卫生间"
                : prompt.Trim();

            ResetProgress();
            plannerFallback = false;
            SetBusy(true);
            SetProgress(BuildStage.Requirements, BuildState.Running, useCloud ? "DeepSeek 正在解析房间与偏好" : "正在解析房间与偏好");
            Log("Planner received: “" + safePrompt + "”");
            if (useCloud)
            {
                string apiKey = ReadEnvironmentVariable("DEEPSEEK_SCENECRAFTAI_APIKEY");
                string model = ReadEnvironmentVariable("SCENECRAFT_DEEPSEEK_MODEL");
                if (string.IsNullOrWhiteSpace(model)) model = DeepSeekScenePlanner.DefaultModel;
                Log("DeepSeek Planner: requesting structured scene JSON with " + model + "...");
                DeepSeekScenePlanner cloudPlanner = new DeepSeekScenePlanner(this, apiKey, model);
                cloudPlanner.Plan(
                    safePrompt,
                    spec => ReviewPlan(spec, apiKey, model),
                    error =>
                    {
                        Log(error);
                        Log("DeepSeek Planner unavailable; falling back to deterministic offline planning.");
                        plannerFallback = true;
                        SetProgress(BuildStage.Requirements, BuildState.Running, "云端不可用，改用本地解析");
                        StartCoroutine(PlanLocally(safePrompt));
                    });
            }
            else
            {
                Log("Offline Planner: analyzing room type, style, and object relations...");
                StartCoroutine(PlanLocally(safePrompt));
            }
        }

        private void BuildScene(SceneSpec spec)
        {
            StartCoroutine(PrepareScene(spec, null, null, false));
        }

        private void ReviewPlan(SceneSpec spec, string apiKey, string model)
        {
            StartCoroutine(PrepareScene(spec, apiKey, model, true));
        }

        private IEnumerator PlanLocally(string prompt)
        {
            yield return null;
            offlinePlanner.Plan(prompt, BuildScene, HandlePlanError);
        }

        private IEnumerator PrepareScene(SceneSpec spec, string apiKey, string model, bool useCloud)
        {
            SetProgress(BuildStage.Requirements, BuildState.Done, plannerFallback ? "云端不可用，已使用本地解析" : "已确认房间与设计需求");
            SetProgress(BuildStage.Inventory, BuildState.Running, "正在核对数量、禁用项和资源偏好");
            // Present the stage before starting its synchronous work
            yield return null;
            try
            {
                ApplyPrompt(spec);
                Log("Planner result: " + DescribePlan(spec));
                LogPipelineStages(spec);
                Log(string.Format("Asset Retriever: matching {0} requested objects against {1} local assets.", spec.objects.Count, catalog.Definitions.Count));
            }
            catch (Exception exception)
            {
                Log("Scene build failed: " + exception.Message);
                FailBuild();
            }
            if (!busy) yield break;
            SetProgress(BuildStage.Inventory, BuildState.Done, "已整理 {0} 个物件请求", spec.objects.Count);
            SetProgress(BuildStage.Layout, BuildState.Running, "正在比较 {0} 个候选布局", LayoutCandidateCount);
            yield return null;
            LayoutResult initial = null;
            try
            {
                initial = OptimizePlan(spec, 0);
            }
            catch (Exception exception)
            {
                Log("Scene build failed: " + exception.Message);
                FailBuild();
            }
            if (initial == null) yield break;
            SetProgress(BuildStage.Layout, BuildState.Done, "已选出布局，评分 {1:0.0}", score: initial.Quality.Score);
            SetProgress(BuildStage.Review, BuildState.Running, useCloud
                ? "DeepSeek 正在复查空间关系" : "正在检查门口、碰撞与支撑面");
            yield return null;
            if (!useCloud)
            {
                CompleteBuild(initial);
                yield break;
            }

            Log("DeepSeek Critic: reviewing semantic relations for one controlled pass...");
            DeepSeekSceneCritic critic = new DeepSeekSceneCritic(this, apiKey, model);
            critic.Review(
                initial.Spec,
                initial.Quality,
                reviewed => ApplyReview(initial, reviewed),
                error =>
                {
                    Log(error);
                    Log("DeepSeek Critic skipped; keeping the locally scored layout.");
                    CompleteBuild(initial, "云端复查未完成，保留本地检查结果");
                });
        }

        private void ApplyReview(LayoutResult initial, SceneSpec reviewed)
        {
            try
            {
                if (!HasSameObjectSet(initial.Spec, reviewed))
                {
                    Log("DeepSeek Critic proposal rejected: it changed the requested object set.");
                    CompleteBuild(initial, "复查建议更改了物件清单，保留原方案");
                    return;
                }

                reviewed.room = initial.Spec.room;
                reviewed.rooms = initial.Spec.rooms;
                reviewed.connections = initial.Spec.connections;
                reviewed.roomType = initial.Spec.roomType;
                reviewed.style = initial.Spec.style;
                reviewed.prompt = initial.Spec.prompt;
                for (int i = 0; i < reviewed.objects.Count; i++)
                {
                    for (int originalIndex = 0; originalIndex < initial.Spec.objects.Count; originalIndex++)
                    {
                        if (initial.Spec.objects[originalIndex].id != reviewed.objects[i].id) continue;
                        reviewed.objects[i].roomId = initial.Spec.objects[originalIndex].roomId;
                        reviewed.objects[i].locked = initial.Spec.objects[originalIndex].locked;
                        reviewed.objects[i].lockedPosition = initial.Spec.objects[originalIndex].lockedPosition;
                        reviewed.objects[i].lockedRotationY = initial.Spec.objects[originalIndex].lockedRotationY;
                        break;
                    }
                }
                ApplyPrompt(reviewed);
                LayoutResult revised = OptimizePlan(reviewed, 0);
                if (revised.Quality.Score > initial.Quality.Score + 0.05f)
                {
                    Log(string.Format(
                        "DeepSeek Critic accepted: layout score improved from {0:0.0} to {1:0.0}.",
                        initial.Quality.Score,
                        revised.Quality.Score));
                    CompleteBuild(revised);
                }
                else
                {
                    Log(string.Format(
                        "DeepSeek Critic proposal kept as advice only: local score {0:0.0} did not exceed {1:0.0}.",
                        revised.Quality.Score,
                        initial.Quality.Score));
                    CompleteBuild(initial);
                }
            }
            catch (Exception exception)
            {
                Log("DeepSeek Critic application failed: " + exception.Message);
                CompleteBuild(initial, "复查建议应用失败，保留原方案");
            }
        }

        private LayoutResult OptimizePlan(SceneSpec spec, int firstVariant)
        {
            SceneSpecDefaults.EnsureHouse(spec);
            bool resized = false;
            for (int i = 0; i < spec.rooms.Count; i++) resized |= RoomGeometry.Normalize(spec.rooms[i]);
            if (resized && spec.rooms.Count > 1)
                HousePromptPlanner.ConnectRooms(spec, spec.connections.Exists(connection => connection.type == "open"));
            SceneSpecDefaults.EnsureOpenings(spec);
            spec.placements.Clear();
            return optimizer.Optimize(spec, catalog, firstVariant, LayoutCandidateCount);
        }

        private void ApplyPrompt(SceneSpec spec)
        {
            int quantityChanges = OfflineScenePlanner.ApplyPromptRules(spec, spec.prompt);
            if (quantityChanges > 0)
                Log("Prompt constraints: reconciled " + quantityChanges + " explicit quantity difference(s).");
            int count = catalog.ApplyAssetHints(spec, spec.prompt);
            if (count > 0) Log("Asset Registry: locked " + count + " explicit Prefab/FBX reference(s) from the prompt.");
            OfflineScenePlanner.ApplyPromptRules(spec, spec.prompt);
            CurrentPlanSummary = BuildPlanSummary(spec);
            if (PlanSummaryChanged != null) PlanSummaryChanged(CurrentPlanSummary);
        }

        private void CompleteBuild(LayoutResult optimized, string reviewNote = null)
        {
            StartCoroutine(FinishBuild(optimized, reviewNote));
        }

        private IEnumerator FinishBuild(LayoutResult optimized, string reviewNote)
        {
            SceneSpec spec = optimized.Spec;
            SceneAuditReport audit = null;
            try
            {
                for (int i = 0; i < optimized.LayoutReport.Messages.Count; i++) Log(optimized.LayoutReport.Messages[i]);
                Log(string.Format(
                    "Layout Optimizer: selected variant {0} from {1} candidates; {2}.",
                    optimized.SelectedVariant + 1, optimized.CandidateCount, optimized.Quality.Summary));
                audit = new LayoutAudit().Audit(spec, catalog);
                SetProgress(BuildStage.Layout, BuildState.Done, "已选出布局，评分 {1:0.0}", score: optimized.Quality.Score);
                FinishReview(audit, reviewNote);
            }
            catch (Exception exception)
            {
                Log("Scene build failed: " + exception.Message);
                FailBuild();
            }
            if (!busy) yield break;
            SetProgress(BuildStage.Scene, BuildState.Running, "正在创建房间、门窗与家具");
            yield return null;
            try
            {
                RebuildVisuals(spec);
                CurrentSpec = spec;
                CurrentQuality = optimized.Quality;
                CurrentAudit = audit;
                SavePlan();
                nextVariant = LayoutCandidateCount;
            }
            catch (Exception exception)
            {
                Log("Scene build failed: " + exception.Message);
                FailBuild();
            }
            if (!busy) yield break;
            SetProgress(BuildStage.Scene, BuildState.Done, "已创建 {0} 个物件，可自由调整", spec.placements.Count);
            undoHistory.Clear();
            dragJson = null;
            Log("Critic: room bounds, relations, circulation, and object intersections evaluated.");
            Log("Scene Audit: " + CurrentAudit.Summary + ".");
            for (int issueIndex = 0; issueIndex < Mathf.Min(4, CurrentAudit.Issues.Count); issueIndex++)
                Log("Audit warning: " + CurrentAudit.Issues[issueIndex]);
            Log(string.Format(
                "Reachability: {0:0}% connected | Support surfaces: {1:0}% valid.",
                optimized.Quality.ConnectivityScore * 100f,
                optimized.Quality.SupportScore * 100f));
            Log("Scene ready. Left-drag furniture to reposition it; use 换一版高分布局 for alternatives.");
            if (SceneChanged != null) SceneChanged(CurrentSpec);
            SetBusy(false);
        }

        private static bool HasSameObjectSet(SceneSpec original, SceneSpec reviewed)
        {
            if (original.objects.Count != reviewed.objects.Count) return false;
            Dictionary<string, string> originalById = new Dictionary<string, string>();
            for (int i = 0; i < original.objects.Count; i++)
                originalById[original.objects[i].id] = original.objects[i].category;
            for (int i = 0; i < reviewed.objects.Count; i++)
            {
                string category;
                if (!originalById.TryGetValue(reviewed.objects[i].id, out category) ||
                    category != reviewed.objects[i].category) return false;
            }

            return true;
        }

        public void NextLayout()
        {
            if (busy || string.IsNullOrWhiteSpace(planJson))
            {
                Log("Generate a scene before requesting another layout.");
                return;
            }

            ResetProgress();
            SetBusy(true);
            SetProgress(BuildStage.Requirements, BuildState.Skipped, "沿用已有空间需求");
            SetProgress(BuildStage.Inventory, BuildState.Skipped, "保留物件清单与位置锁定");
            SetProgress(BuildStage.Layout, BuildState.Running, "正在比较新的候选布局");
            StartCoroutine(ChangeLayout());
        }

        private IEnumerator ChangeLayout()
        {
            yield return null;
            LayoutResult optimized = null;
            SceneAuditReport audit = null;
            try
            {
                PushUndoSnapshot();
                SceneSpec planned = SceneSpecJson.FromJson(planJson);
                optimized = optimizer.Optimize(
                    planned, catalog, nextVariant, LayoutCandidateCount, CurrentSpec);
            }
            catch (Exception exception)
            {
                Log("Alternative layout failed: " + exception.Message);
                FailBuild();
            }
            if (!busy || optimized == null) yield break;
            SetProgress(BuildStage.Layout, BuildState.Done, "已选出布局，评分 {1:0.0}", score: optimized.Quality.Score);
            SetProgress(BuildStage.Review, BuildState.Running, "正在检查新布局的空间约束");
            yield return null;
            try
            {
                audit = new LayoutAudit().Audit(optimized.Spec, catalog);
                FinishReview(audit, null);
            }
            catch (Exception exception)
            {
                Log("Alternative layout failed: " + exception.Message);
                FailBuild();
            }
            if (!busy) yield break;
            SetProgress(BuildStage.Scene, BuildState.Running, "正在更新场景");
            yield return null;
            try
            {
                RebuildVisuals(optimized.Spec);
                CurrentSpec = optimized.Spec;
                CurrentQuality = optimized.Quality;
                CurrentAudit = audit;
                nextVariant += LayoutCandidateCount;
                dragJson = null;
                SetProgress(BuildStage.Scene, BuildState.Done, "布局已更新，可继续调整");
                Log(string.Format(
                    "Alternative Layout: selected variant {0}; {1}; novelty {2:0}%.",
                    optimized.SelectedVariant + 1,
                    optimized.Quality.Summary,
                    optimized.NoveltyScore * 100f));
                if (SceneChanged != null) SceneChanged(CurrentSpec);
            }
            catch (Exception exception)
            {
                Log("Alternative layout failed: " + exception.Message);
                FailBuild();
            }
            finally
            {
                SetBusy(false);
            }
        }

        public void ClearScene()
        {
            if (busy) return;
            if (CurrentSpec != null) PushUndoSnapshot();
            if (generatedRoot != null) Destroy(generatedRoot.gameObject);
            generatedRoot = null;
            CurrentSpec = null;
            CurrentQuality = null;
            CurrentAudit = null;
            planJson = null;
            CurrentPlanSummary = "AI理解：尚未生成场景";
            ResetProgress();
            dragJson = null;
            Log("Scene cleared.");
            if (PlanSummaryChanged != null) PlanSummaryChanged(CurrentPlanSummary);
            if (SceneChanged != null) SceneChanged(null);
        }

        public bool ToggleLock(SceneObjectView view)
        {
            if (view == null || CurrentSpec == null) return false;
            SceneObjectRequest request = FindRequest(view.Spec.id);
            if (request == null) return false;
            PushUndoSnapshot();
            request.locked = !request.locked;
            RoomSpec room = GetRoom(view.Spec.roomId);
            request.lockedPosition = view.Spec.position - room.center;
            request.lockedRotationY = view.Spec.rotationY;
            view.Spec.locked = request.locked;
            SavePlan();
            Log((request.locked ? "Locked " : "Unlocked ") + view.Spec.id + " for alternative layouts.");
            return request.locked;
        }

        private SceneObjectRequest FindRequest(string id)
        {
            if (CurrentSpec == null || string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < CurrentSpec.objects.Count; i++)
                if (CurrentSpec.objects[i].id == id) return CurrentSpec.objects[i];
            return null;
        }

        private void SavePlan()
        {
            if (CurrentSpec == null) return;
            SceneSpec replannable = SceneSpecJson.FromJson(SceneSpecJson.ToJson(CurrentSpec, false));
            replannable.placements.Clear();
            planJson = SceneSpecJson.ToJson(replannable, false);
        }

        private static string DescribePlan(SceneSpec spec)
        {
            if (spec.objects == null || spec.objects.Count == 0)
            {
                return spec.roomType + " | " + spec.rooms.Count + " room(s) | no furniture requested";
            }

            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int i = 0; i < spec.objects.Count; i++)
            {
                string category = spec.objects[i].category;
                int count;
                counts.TryGetValue(category, out count);
                counts[category] = count + 1;
            }

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                parts.Add(pair.Key + " x" + pair.Value);
            }
            return spec.roomType + " | " + spec.rooms.Count + " room(s) | " + string.Join(", ", parts.ToArray());
        }

        private static string BuildPlanSummary(SceneSpec spec)
        {
            if (spec == null || spec.objects == null || spec.objects.Count == 0) return "AI理解：空房间，不放置家具";
            SceneSpecDefaults.EnsureHouse(spec);
            List<string> roomParts = new List<string>();
            for (int roomIndex = 0; roomIndex < spec.rooms.Count; roomIndex++)
            {
                RoomSpec room = spec.rooms[roomIndex];
                Dictionary<string, int> counts = new Dictionary<string, int>();
                for (int objectIndex = 0; objectIndex < spec.objects.Count; objectIndex++)
                {
                    SceneObjectRequest request = spec.objects[objectIndex];
                    if (request.roomId != room.id) continue;
                    int count;
                    counts.TryGetValue(request.category, out count);
                    counts[request.category] = count + 1;
                }
                List<string> objects = new List<string>();
                foreach (KeyValuePair<string, int> pair in counts) objects.Add(pair.Key + "×" + pair.Value);
                roomParts.Add(room.id + ": " + (objects.Count == 0 ? "空" : string.Join("、", objects.ToArray())));
            }
            return "AI理解：" + string.Join("  |  ", roomParts.ToArray());
        }

        private void LogPipelineStages(SceneSpec spec)
        {
            SceneSpecDefaults.EnsureHouse(spec);
            Log(string.Format("Floor Plan Agent: {0} room(s), {1} connection(s).", spec.rooms.Count, spec.connections.Count));
            int floor = 0;
            int wall = 0;
            int ceiling = 0;
            int surface = 0;
            for (int i = 0; i < spec.objects.Count; i++)
            {
                switch (spec.objects[i].placement)
                {
                    case "wall": wall++; break;
                    case "ceiling": ceiling++; break;
                    case "surface": surface++; break;
                    default: floor++; break;
                }
            }
            Log(string.Format("Furniture Agent: {0} floor object(s).", floor));
            Log(string.Format("Wall Agent: {0} wall object(s).", wall));
            Log(string.Format("Ceiling Agent: {0} ceiling object(s).", ceiling));
            Log(string.Format("Manipuland Agent: {0} supported small object(s).", surface));
        }

        private static string ReadEnvironmentVariable(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (string.IsNullOrWhiteSpace(value))
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
            }
#endif
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private void RebuildVisuals(SceneSpec spec)
        {
            SceneSpecDefaults.EnsureHouse(spec);
            if (generatedRoot != null) Destroy(generatedRoot.gameObject);
            generatedRoot = new GameObject("Generated Scene").transform;
            generatedRoot.SetParent(transform, false);

            for (int i = 0; i < spec.rooms.Count; i++)
            {
                RoomSpec room = spec.rooms[i];
                Transform roomRoot = new GameObject("Room [" + room.id + "]").transform;
                roomRoot.SetParent(generatedRoot, false);
                roomRoot.localPosition = room.center;
                roomBuilder.Build(room, roomRoot, spec.rooms);
            }

            Transform objectRoot = new GameObject("Objects").transform;
            objectRoot.SetParent(generatedRoot, false);
            for (int i = 0; i < spec.placements.Count; i++)
            {
                PlacedObjectSpec placement = spec.placements[i];
                AssetDefinition asset = catalog.FindById(placement.assetId);
                if (asset != null) assetFactory.Create(asset, placement, objectRoot);
            }
            FrameCameraForHouse(spec);
        }

        private static void FrameCameraForHouse(SceneSpec spec)
        {
            if (Camera.main == null || spec.rooms.Count == 0) return;
            RoomSpec first = spec.rooms[0];
            Bounds bounds = new Bounds(
                first.center + Vector3.up * first.height * 0.5f,
                new Vector3(first.width, first.height, first.depth));
            for (int i = 1; i < spec.rooms.Count; i++)
            {
                RoomSpec room = spec.rooms[i];
                bounds.Encapsulate(new Bounds(
                    room.center + Vector3.up * room.height * 0.5f,
                    new Vector3(room.width, room.height, room.depth)));
            }
            OrbitCameraController orbit = Camera.main.GetComponent<OrbitCameraController>();
            if (orbit != null) orbit.FrameBounds(bounds);
        }

        public void Save()
        {
            if (busy) return;
            if (CurrentSpec == null)
            {
                Log("Nothing to save yet.");
                return;
            }

            try
            {
                string folder = Path.GetDirectoryName(SavePath);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(SavePath, SceneSpecJson.ToJson(CurrentSpec));
                Log("Scene saved to: " + SavePath);
            }
            catch (Exception exception)
            {
                Log("Save failed: " + exception.Message);
            }
        }

        public void Load()
        {
            if (busy) return;
            if (!File.Exists(SavePath))
            {
                Log("No saved scene found at: " + SavePath);
                return;
            }

            try
            {
                SceneSpec spec = SceneSpecJson.FromJson(File.ReadAllText(SavePath));
                RebuildVisuals(spec);
                CurrentSpec = spec;
                SavePlan();
                CurrentPlanSummary = BuildPlanSummary(spec);
                nextVariant = LayoutCandidateCount;
                CurrentQuality = new LayoutEvaluator().Evaluate(spec, catalog);
                CurrentAudit = new LayoutAudit().Audit(spec, catalog);
                undoHistory.Clear();
                dragJson = null;
                ResetProgress();
                Log("Saved scene loaded successfully.");
                if (PlanSummaryChanged != null) PlanSummaryChanged(CurrentPlanSummary);
                if (SceneChanged != null) SceneChanged(CurrentSpec);
            }
            catch (Exception exception)
            {
                Log("Load failed: " + exception.Message);
            }
        }

        public void DeleteObject(SceneObjectView view)
        {
            if (view == null || CurrentSpec == null) return;
            PushUndoSnapshot();
            string id = view.Spec.id;
            CurrentSpec.placements.RemoveAll(item => item.id == id);
            CurrentSpec.objects.RemoveAll(item => item.id == id);
            CurrentAudit = new LayoutAudit().Audit(CurrentSpec, catalog);
            SavePlan();
            Destroy(view.gameObject);
            Log("Removed " + id + " from the scene.");
            if (SceneChanged != null) SceneChanged(CurrentSpec);
        }

        public void BeginObjectMove(SceneObjectView view)
        {
            if (view == null || CurrentSpec == null) return;
            dragJson = SceneSpecJson.ToJson(CurrentSpec);
        }

        public bool PreviewObjectMove(SceneObjectView view, Vector3 position, out string reason)
        {
            reason = string.Empty;
            if (view == null || CurrentSpec == null) return false;
            PlacedObjectSpec candidate = SceneLayoutEngine.Clone(view.Spec);
            candidate.position = position;
            RoomSpec room;
            PlacedObjectSpec localCandidate;
            List<PlacedObjectSpec> localExisting;
            GetRoomContext(candidate, out room, out localCandidate, out localExisting);
            bool valid = layout.IsPlacementValid(localCandidate, room, localExisting, view.Spec.id, out reason);
            view.transform.position = position;
            view.SetPlacementValid(valid);
            return valid;
        }

        public bool CommitObjectMove(SceneObjectView view, Vector3 position)
        {
            string reason;
            if (!PreviewObjectMove(view, position, out reason))
            {
                CancelObjectMove(view);
                Log("Move cancelled: " + reason + ".");
                return false;
            }

            if ((view.Spec.position - position).sqrMagnitude > 0.000001f)
            {
                if (!string.IsNullOrEmpty(dragJson)) undoHistory.Push(dragJson);
                view.Spec.position = position;
                UpdateLockedTransform(view.Spec);
                UpdateEvaluation();
                Log(string.Format("Moved {0} to X {1:0.0}, Z {2:0.0}.", view.Spec.id, position.x, position.z));
            }
            view.SetPlacementValid(true);
            dragJson = null;
            return true;
        }

        public void CancelObjectMove(SceneObjectView view)
        {
            if (view != null)
            {
                view.transform.position = view.Spec.position;
                view.SetPlacementValid(true);
            }
            dragJson = null;
        }

        public bool NudgeObject(SceneObjectView view, float deltaX, float deltaZ)
        {
            if (view == null) return false;
            BeginObjectMove(view);
            return CommitObjectMove(view, view.Spec.position + new Vector3(deltaX, 0f, deltaZ));
        }

        public bool RotateObject(SceneObjectView view, float degrees)
        {
            if (view == null || CurrentSpec == null) return false;
            PlacedObjectSpec candidate = SceneLayoutEngine.Clone(view.Spec);
            candidate.rotationY = Mathf.Repeat(candidate.rotationY + degrees, 360f);
            string reason;
            RoomSpec room;
            PlacedObjectSpec localCandidate;
            List<PlacedObjectSpec> localExisting;
            GetRoomContext(candidate, out room, out localCandidate, out localExisting);
            if (!layout.IsPlacementValid(localCandidate, room, localExisting, view.Spec.id, out reason))
            {
                Log("Rotation cancelled: " + reason + ".");
                return false;
            }

            PushUndoSnapshot();
            view.Spec.rotationY = candidate.rotationY;
            view.transform.rotation = Quaternion.Euler(0f, candidate.rotationY, 0f);
            UpdateLockedTransform(view.Spec);
            UpdateEvaluation();
            Log("Rotated " + view.Spec.id + " to " + view.Spec.rotationY.ToString("0") + "°.");
            return true;
        }

        public bool AutoPlaceObject(SceneObjectView view)
        {
            if (view == null || CurrentSpec == null) return false;
            PlacedObjectSpec candidate = SceneLayoutEngine.Clone(view.Spec);
            RoomSpec room;
            PlacedObjectSpec localCandidate;
            List<PlacedObjectSpec> localExisting;
            GetRoomContext(candidate, out room, out localCandidate, out localExisting);
            Vector3 localPosition;
            if (!layout.TryFindOpenPosition(localCandidate, room, localExisting, view.Spec.id, out localPosition))
            {
                Log("No valid free area was found for " + view.Spec.id + ".");
                return false;
            }
            Vector3 position = localPosition + room.center;
            BeginObjectMove(view);
            return CommitObjectMove(view, position);
        }

        public bool SnapToWall(SceneObjectView view)
        {
            if (view == null || CurrentSpec == null) return false;
            RoomSpec room = GetRoom(view.Spec.roomId);
            Vector3 roomCenter = room.center;
            PlacedObjectSpec best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                PlacedObjectSpec candidate = SceneLayoutEngine.Clone(view.Spec);
                candidate.position -= roomCenter;
                Bounds bounds = candidate.Bounds;
                Vector3 position = candidate.position;
                if (i == 0) position.z = -room.depth * 0.5f + bounds.extents.z + RoomGeometry.WallGap;
                else if (i == 1) position.z = room.depth * 0.5f - bounds.extents.z - RoomGeometry.WallGap;
                else if (i == 2) position.x = -room.width * 0.5f + bounds.extents.x + RoomGeometry.WallGap;
                else position.x = room.width * 0.5f - bounds.extents.x - RoomGeometry.WallGap;
                position.x = Mathf.Clamp(position.x, -room.width * 0.5f + bounds.extents.x, room.width * 0.5f - bounds.extents.x);
                position.z = Mathf.Clamp(position.z, -room.depth * 0.5f + bounds.extents.z, room.depth * 0.5f - bounds.extents.z);
                candidate.position = position;
                string reason;
                List<PlacedObjectSpec> localExisting;
                PlacedObjectSpec ignored;
                RoomSpec ignoredRoom;
                GetRoomContext(view.Spec, out ignoredRoom, out ignored, out localExisting);
                if (!layout.IsPlacementValid(candidate, room, localExisting, view.Spec.id, out reason)) continue;
                candidate.position += roomCenter;
                float distance = (candidate.position - view.Spec.position).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }

            if (best == null)
            {
                Log("No collision-free wall position was found for " + view.Spec.id + ".");
                return false;
            }

            PushUndoSnapshot();
            view.Spec.position = best.position;
            view.transform.position = best.position;
            UpdateLockedTransform(view.Spec);
            UpdateEvaluation();
            Log("Snapped " + view.Spec.id + " to the nearest valid wall without changing its rotation.");
            return true;
        }

        public bool FaceAnchor(SceneObjectView view)
        {
            if (view == null || CurrentSpec == null) return false;
            PlacedObjectSpec anchor = FindPlacement(view.Spec.anchorId);
            if (anchor == null)
            {
                Log("No semantic anchor is assigned to " + view.Spec.id + ".");
                return false;
            }
            Vector3 direction = anchor.position - view.Spec.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return false;
            float yaw = Quaternion.FromToRotation(Vector3.back, direction.normalized).eulerAngles.y;
            float delta = Mathf.DeltaAngle(view.Spec.rotationY, yaw);
            return RotateObject(view, delta);
        }

        public bool SnapToAnchor(SceneObjectView view)
        {
            if (view == null || CurrentSpec == null) return false;
            PlacedObjectSpec anchor = FindPlacement(view.Spec.anchorId);
            if (anchor == null)
            {
                Log("No semantic anchor is assigned to " + view.Spec.id + ".");
                return false;
            }
            PlacedObjectSpec candidate = SceneLayoutEngine.Clone(view.Spec);
            Vector3 directionToAnchor = anchor.position - candidate.position;
            directionToAnchor.y = 0f;
            if (directionToAnchor.sqrMagnitude < 0.0001f) directionToAnchor = Vector3.forward;
            directionToAnchor.Normalize();
            candidate.rotationY = Quaternion.FromToRotation(Vector3.back, directionToAnchor).eulerAngles.y;
            Bounds rotated = candidate.Bounds;
            float anchorRadius = Mathf.Abs(directionToAnchor.x) * anchor.Bounds.extents.x +
                Mathf.Abs(directionToAnchor.z) * anchor.Bounds.extents.z;
            float objectRadius = Mathf.Abs(directionToAnchor.x) * rotated.extents.x +
                Mathf.Abs(directionToAnchor.z) * rotated.extents.z;
            candidate.position = anchor.position - directionToAnchor * (anchorRadius + objectRadius + 0.06f);

            RoomSpec room;
            PlacedObjectSpec localCandidate;
            List<PlacedObjectSpec> localExisting;
            GetRoomContext(candidate, out room, out localCandidate, out localExisting);
            string reason;
            if (!layout.IsPlacementValid(localCandidate, room, localExisting, view.Spec.id, out reason))
            {
                Log("Anchor snap cancelled: " + reason + ".");
                return false;
            }

            PushUndoSnapshot();
            view.Spec.position = candidate.position;
            view.Spec.rotationY = candidate.rotationY;
            view.transform.SetPositionAndRotation(candidate.position, Quaternion.Euler(0f, candidate.rotationY, 0f));
            UpdateLockedTransform(view.Spec);
            UpdateEvaluation();
            Log("Oriented and snapped " + view.Spec.id + " to " + anchor.id + ".");
            return true;
        }

        private void UpdateEvaluation()
        {
            if (CurrentSpec == null) return;
            CurrentQuality = new LayoutEvaluator().Evaluate(CurrentSpec, catalog);
            CurrentAudit = new LayoutAudit().Audit(CurrentSpec, catalog);
            if (EvaluationChanged != null) EvaluationChanged();
        }

        private PlacedObjectSpec FindPlacement(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || CurrentSpec == null) return null;
            for (int i = 0; i < CurrentSpec.placements.Count; i++)
                if (CurrentSpec.placements[i].id == id) return CurrentSpec.placements[i];
            return null;
        }

        private void UpdateLockedTransform(PlacedObjectSpec placement)
        {
            SceneObjectRequest request = FindRequest(placement.id);
            if (request == null || !request.locked) return;
            RoomSpec room = GetRoom(placement.roomId);
            request.lockedPosition = placement.position - room.center;
            request.lockedRotationY = placement.rotationY;
            placement.locked = true;
            SavePlan();
        }

        public RoomSpec GetRoom(string roomId)
        {
            if (CurrentSpec == null) return new RoomSpec();
            SceneSpecDefaults.EnsureHouse(CurrentSpec);
            for (int i = 0; i < CurrentSpec.rooms.Count; i++)
                if (CurrentSpec.rooms[i].id == roomId) return CurrentSpec.rooms[i];
            return CurrentSpec.room;
        }

        private void GetRoomContext(
            PlacedObjectSpec worldCandidate,
            out RoomSpec room,
            out PlacedObjectSpec localCandidate,
            out List<PlacedObjectSpec> localExisting)
        {
            room = GetRoom(worldCandidate.roomId);
            localCandidate = SceneLayoutEngine.Clone(worldCandidate);
            localCandidate.position -= room.center;
            localExisting = new List<PlacedObjectSpec>();
            for (int i = 0; i < CurrentSpec.placements.Count; i++)
            {
                PlacedObjectSpec placement = CurrentSpec.placements[i];
                if (!string.Equals(placement.roomId, room.id, StringComparison.Ordinal)) continue;
                PlacedObjectSpec local = SceneLayoutEngine.Clone(placement);
                local.position -= room.center;
                localExisting.Add(local);
            }
        }

        public void Undo()
        {
            if (busy) return;
            if (undoHistory.Count == 0)
            {
                Log("Nothing to undo.");
                return;
            }

            SceneSpec restored = SceneSpecJson.FromJson(undoHistory.Pop());
            CurrentSpec = restored;
            CurrentQuality = new LayoutEvaluator().Evaluate(restored, catalog);
            CurrentAudit = new LayoutAudit().Audit(restored, catalog);
            dragJson = null;
            SavePlan();
            CurrentPlanSummary = BuildPlanSummary(restored);
            RebuildVisuals(restored);
            ResetProgress();
            Log("Undid the last furniture edit.");
            if (PlanSummaryChanged != null) PlanSummaryChanged(CurrentPlanSummary);
            if (SceneChanged != null) SceneChanged(CurrentSpec);
        }

        private void PushUndoSnapshot()
        {
            if (CurrentSpec != null) undoHistory.Push(SceneSpecJson.ToJson(CurrentSpec));
        }

        private void HandlePlanError(string error)
        {
            Log("Planner failed: " + error);
            FailBuild();
        }

        private void ResetProgress()
        {
            activeStage = -1;
            for (int i = 0; i < progress.Length; i++)
                progress[i] = new BuildProgress((BuildStage)i, BuildState.Waiting, string.Empty);
            if (ProgressChanged != null) ProgressChanged();
        }

        private void SetProgress(BuildStage stage, BuildState state, string note, int count = 0, float score = 0f, string detail = null)
        {
            progress[(int)stage] = new BuildProgress(stage, state, note, count, score, detail);
            if (state == BuildState.Running) activeStage = (int)stage;
            else if (activeStage == (int)stage) activeStage = -1;
            if (ProgressChanged != null) ProgressChanged();
        }

        private void FinishReview(SceneAuditReport audit, string note)
        {
            string auditNote = audit.Passed ? "本地检查未发现约束问题" : "{0} 项约束提醒，请检查摆放";
            SetProgress(BuildStage.Review, BuildState.Done, auditNote, audit.Issues.Count, detail: note);
        }

        private void FailBuild()
        {
            if (activeStage >= 0)
                SetProgress((BuildStage)activeStage, BuildState.Failed, "未能完成，详情见运行记录");
            StopAllCoroutines();
            SetBusy(false);
        }

        private void SetBusy(bool value)
        {
            if (busy == value) return;
            busy = value;
            if (BusyChanged != null) BusyChanged(value);
        }

        private void Log(string message)
        {
            Debug.Log("[SceneCraftAI] " + message);
            if (LogMessage != null) LogMessage(message);
        }

        private static void ClampRoom(RoomSpec room)
        {
            RoomGeometry.Normalize(room);
        }
    }
}
