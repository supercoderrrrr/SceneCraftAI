using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SceneCraftAI.Domain;
using SceneCraftAI.Planning;
using SceneCraftAI.Runtime;
using SceneCraftAI.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SceneCraftAI.Tests
{
    public sealed class HudTests
    {
        [UnityTest]
        public IEnumerator LocalGenerationReportsRealStagesAndColorsCompletedRowsGreen()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            List<BuildStage> started = new List<BuildStage>();
            Action observer = () =>
            {
                int running = 0;
                for (int i = 0; i < controller.Progress.Count; i++)
                {
                    BuildProgress step = controller.Progress[i];
                    if (step.State != BuildState.Running) continue;
                    running++;
                    Assert.That(controller.IsBusy, Is.True);
                    if (!started.Contains(step.Stage)) started.Add(step.Stage);
                    Assert.That(ReadStep(hud, step.Stage, "StepState").text, Is.EqualTo(UiText.Get(hud.Language, "进行中")));
                    for (int earlier = 0; earlier < i; earlier++)
                        Assert.That(controller.Progress[earlier].State, Is.EqualTo(BuildState.Done));
                }
                Assert.That(running, Is.LessThanOrEqualTo(1));
            };
            controller.ProgressChanged += observer;
            try
            {
                controller.Generate("Create a living room. Only place one sofa and one coffee table", false);
                Assert.That(controller.IsBusy, Is.True);
                Assert.That(controller.Progress[0].State, Is.EqualTo(BuildState.Running));
                Assert.That(Find<Button>(hud, "保存场景").interactable, Is.False);
                yield return WaitForBuild(controller);
            }
            finally
            {
                controller.ProgressChanged -= observer;
            }
            CollectionAssert.AreEqual((BuildStage[])Enum.GetValues(typeof(BuildStage)), started);
            foreach (BuildProgress step in controller.Progress)
            {
                Assert.That(step.State, Is.EqualTo(BuildState.Done));
                Text label = ReadStep(hud, step.Stage, "StepTitle");
                Assert.That(label.color.g, Is.GreaterThan(label.color.r));
                Assert.That(ReadStep(hud, step.Stage, "StepState").text, Is.EqualTo(UiText.Get(hud.Language, "完成")));
            }
            Assert.That(Find<Button>(hud, "保存场景").interactable, Is.True);
            Assert.That(Find<Text>(hud, "RoomCount").text, Is.EqualTo("1"));
            Assert.That(Find<Text>(hud, "ObjectCount").text, Is.EqualTo("2"));
            Assert.That(Find<RectTransform>(hud, "RunDetails").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator HudResubscribesOnceAndResetsRowsAfterClearing()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            for (int i = 0; i < 3; i++)
            {
                hud.gameObject.SetActive(false);
                hud.gameObject.SetActive(true);
            }
            FieldInfo eventField = typeof(SceneRuntimeController).GetField("ProgressChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            Delegate subscribers = (Delegate)eventField.GetValue(controller);
            int subscriptions = 0;
            foreach (Delegate subscriber in subscribers.GetInvocationList())
                if (ReferenceEquals(subscriber.Target, hud)) subscriptions++;
            Assert.That(subscriptions, Is.EqualTo(1));
            controller.ClearScene();
            foreach (BuildProgress step in controller.Progress)
            {
                Assert.That(step.State, Is.EqualTo(BuildState.Waiting));
                Assert.That(ReadStep(hud, step.Stage, "StepState").text, Is.EqualTo(UiText.Get(hud.Language, "等待")));
            }
            Assert.That(Find<Text>(hud, "RoomCount").text, Is.EqualTo("—"));
            Assert.That(Find<Button>(hud, "换个布局").interactable, Is.False);
            controller.Generate("一套生活化住宅：客厅、主卧、次卧、厨房和卫生间", false);
            yield return WaitForBuild(controller);
        }

        [UnityTest]
        public IEnumerator FailureMarksOnlyTheActiveStepAndAllowsRetry()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            controller.Generate("Create a living room", false);
            Invoke(controller, "HandlePlanError", "Controlled failure for the HUD test");
            Assert.That(controller.Progress[0].State, Is.EqualTo(BuildState.Failed));
            Assert.That(controller.Progress[1].State, Is.EqualTo(BuildState.Waiting));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(Find<Text>(hud, "SceneStatus").text, Is.EqualTo(UiText.Get(hud.Language, "生成未完成，请查看运行记录")));
            Assert.That(ReadStep(hud, BuildStage.Requirements, "StepTitle").color.r,
                Is.GreaterThan(ReadStep(hud, BuildStage.Requirements, "StepTitle").color.g));
            controller.Generate("Create a living room. Only place one sofa", false);
            yield return WaitForBuild(controller);
            Assert.That(controller.CurrentSpec.placements.Count, Is.EqualTo(1));
            Assert.That(controller.Progress[4].State, Is.EqualTo(BuildState.Done));
        }

        [UnityTest]
        public IEnumerator DisabledHudCatchesUpWithoutReceivingEvents()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            hud.gameObject.SetActive(false);
            controller.Generate("Create a bedroom. Only place one bed", false);
            yield return WaitForBuild(controller);
            hud.gameObject.SetActive(true);
            Assert.That(Find<Text>(hud, "RoomSummary").text, Is.EqualTo(UiText.Get(hud.Language, "卧室")));
            Assert.That(Find<Text>(hud, "ObjectCount").text, Is.EqualTo("1"));
            Assert.That(ReadStep(hud, BuildStage.Scene, "StepState").text, Is.EqualTo(UiText.Get(hud.Language, "完成")));
        }

        [UnityTest]
        public IEnumerator AlternativeLayoutReusesThePlanAndCloudReviewFallbackIsHonest()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            controller.NextLayout();
            yield return WaitForBuild(controller);
            Assert.That(controller.Progress[0].State, Is.EqualTo(BuildState.Skipped));
            Assert.That(ReadStep(hud, BuildStage.Requirements, "StepState").text, Is.EqualTo(UiText.Get(hud.Language, "沿用")));
            controller.Generate("Create a living room. Only place one sofa", false);
            Invoke(controller, "HandlePlanError", "Switch to a simulated cloud result");
            Invoke(controller, "SetBusy", true);
            SceneSpec spec = new OfflineScenePlanner().Build("Create a living room. Only place one sofa");
            Invoke(controller, "ReviewPlan", spec, null, "test-model");
            yield return WaitForBuild(controller);
            Assert.That(controller.Progress[3].Detail, Does.Contain("云端复查未完成"));
            Assert.That(controller.Progress[3].State, Is.EqualTo(BuildState.Done));
        }

        [UnityTest]
        public IEnumerator DetailsToggleKeepsDiagnosticsAvailableWithoutBulletPrefixes()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            controller.Generate("一套生活化住宅：客厅、主卧、次卧、厨房和卫生间", false);
            yield return WaitForBuild(controller);
            Button toggle = Find<Button>(hud, "查看运行记录");
            toggle.onClick.Invoke();
            Assert.That(Find<RectTransform>(hud, "RunDetails").gameObject.activeSelf, Is.True);
            Text log = Find<RectTransform>(hud, "LogContent").GetComponentInChildren<Text>();
            Assert.That(log.text, Does.Contain("Planner received"));
            Assert.That(log.text, Does.Not.Contain("• "));
            Assert.That(Find<ScrollRect>(hud, "LogScroll").content.rect.height, Is.GreaterThan(216f));
            toggle.onClick.Invoke();
            Assert.That(Find<RectTransform>(hud, "RunDetails").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingTheControllerStopsGenerationAndRetryStartsCleanly()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            yield return WaitForBuild(controller);
            controller.Generate("Create a living room. Only place one sofa", false);
            controller.enabled = false;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Progress[0].State, Is.EqualTo(BuildState.Failed));
            yield return null;
            Assert.That(controller.Progress[1].State, Is.EqualTo(BuildState.Waiting));
            controller.enabled = true;
            controller.Generate("Create a living room. Only place one sofa", false);
            yield return WaitForBuild(controller);
            Assert.That(controller.CurrentSpec.placements.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InterruptedBuildKeepsThePreviousSceneUntilVisualsAreReplaced()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            yield return WaitForBuild(controller);
            SceneSpec previous = controller.CurrentSpec;
            controller.Generate("Create a living room. Only place one sofa", false);
            for (int i = 0; i < 30 && controller.Progress[4].State != BuildState.Running; i++) yield return null;
            Assert.That(controller.Progress[4].State, Is.EqualTo(BuildState.Running));
            Assert.That(controller.CurrentSpec, Is.SameAs(previous));
            controller.enabled = false;
            controller.enabled = true;
            yield return null;
            Assert.That(controller.CurrentSpec, Is.SameAs(previous));
            controller.Generate("Create a living room. Only place one sofa", false);
            yield return WaitForBuild(controller);
            Assert.That(controller.CurrentSpec, Is.Not.SameAs(previous));
        }

        [UnityTest]
        public IEnumerator LoadingASavedSceneDoesNotPretendToRunGenerationSteps()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            controller.Generate("Create a living room. Only place one sofa", false);
            yield return WaitForBuild(controller);
            controller.Save();
            controller.ClearScene();
            controller.Load();
            Assert.That(controller.CurrentSpec.placements.Count, Is.EqualTo(1));
            foreach (BuildProgress step in controller.Progress)
                Assert.That(step.State, Is.EqualTo(BuildState.Waiting));
            Assert.That(Find<Text>(hud, "ObjectCount").text, Is.EqualTo("1"));
        }

        [UnityTest]
        public IEnumerator PanelRendersAtWideAndCompactResolutions()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            controller.Generate("一套生活化住宅：客厅、主卧、次卧、厨房和卫生间", false);
            yield return WaitForBuild(controller);
            CapturePanel(hud, 1920, 1080, "hud-wide.png");
            CapturePanel(hud, 1280, 720, "hud-compact.png");
            yield return null;
        }

        internal static void CapturePanel(SceneCraftHud hud, int width, int height, string name)
        {
            Canvas canvas = hud.GetComponent<Canvas>();
            CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
            Camera camera = Camera.main;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            CanvasScaler.ScaleMode previousScaleMode = scaler.uiScaleMode;
            float previousScale = scaler.scaleFactor;
            RenderTexture target = new RenderTexture(width, height, 24);
            Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = width / 1920f;
                canvas.scaleFactor = scaler.scaleFactor;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply();
                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UserData", "HudPreview");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG());
                RectTransform viewport = Find<ScrollRect>(hud, "ControlsScroll").viewport;
                Vector3[] corners = new Vector3[4];
                viewport.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = camera.WorldToScreenPoint(corner);
                    Assert.That(point.x, Is.InRange(-1f, width + 1f));
                    Assert.That(point.y, Is.InRange(-1f, height + 1f));
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                scaler.uiScaleMode = previousScaleMode;
                scaler.scaleFactor = previousScale;
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private static Text ReadStep(SceneCraftHud hud, BuildStage step, string child)
        {
            return Find<RectTransform>(hud, "Workflow_" + step).Find(child).GetComponent<Text>();
        }

        private static T Find<T>(SceneCraftHud hud, string name) where T : Component
        {
            foreach (T component in hud.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            Assert.Fail("Missing HUD component " + name);
            return null;
        }

        private static void Invoke(SceneRuntimeController controller, string method, params object[] arguments)
        {
            typeof(SceneRuntimeController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, arguments);
        }

        private static IEnumerator WaitForBuild(SceneRuntimeController controller)
        {
            Assert.That(controller, Is.Not.Null);
            for (int i = 0; i < 60 && controller.IsBusy; i++) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            yield return null;
        }
    }
}
