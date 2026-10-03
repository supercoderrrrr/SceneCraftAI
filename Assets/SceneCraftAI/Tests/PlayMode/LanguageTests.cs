using System.Collections;
using NUnit.Framework;
using SceneCraftAI.Domain;
using SceneCraftAI.Runtime;
using SceneCraftAI.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SceneCraftAI.Tests
{
    public sealed class LanguageTests
    {
        private bool hadPreference;
        private int previousPreference;
        private UiLanguage previousLanguage;

        [SetUp]
        public void PreserveUserPreference()
        {
            hadPreference = PlayerPrefs.HasKey(UiText.PreferenceKey);
            previousPreference = PlayerPrefs.GetInt(UiText.PreferenceKey);
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            previousLanguage = hud == null ? UiText.FromPreference(previousPreference) : hud.Language;
        }

        [TearDown]
        public void RestoreUserPreference()
        {
            if (hadPreference) PlayerPrefs.SetInt(UiText.PreferenceKey, previousPreference);
            else PlayerPrefs.DeleteKey(UiText.PreferenceKey);
            PlayerPrefs.Save();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            if (hud != null) hud.SetLanguage(previousLanguage, false);
        }

        [UnityTest]
        public IEnumerator FreshHudDefaultsToEnglishWithoutAStoredChoice()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            yield return WaitForBuild(controller);
            PlayerPrefs.DeleteKey(UiText.PreferenceKey);
            GameObject root = new GameObject("Default Language Test");
            try
            {
                SceneCraftHud hud = root.AddComponent<SceneCraftHud>();
                hud.Initialize(controller, Object.FindObjectOfType<RuntimeSelectionController>());
                Assert.That(hud.Language, Is.EqualTo(UiLanguage.English));
                Assert.That(hud.PromptText, Is.EqualTo(UiText.EnglishPrompt));
                Assert.That(Find<Dropdown>(hud, "LanguageSelect").value, Is.EqualTo(0));
                Assert.That(Find<Button>(hud, "本地生成").GetComponentInChildren<Text>().text, Is.EqualTo("Build locally"));
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LanguageMenuOpensAndSelectsBothOptions()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            hud.SetLanguage(UiLanguage.English, false);
            Dropdown menu = Find<Dropdown>(hud, "LanguageSelect");
            Assert.That(menu.options.Count, Is.EqualTo(2));
            Assert.That(menu.options[0].text, Is.EqualTo("English"));
            Assert.That(menu.options[1].text, Is.EqualTo("中文"));
            menu.Show();
            yield return null;
            Transform list = menu.transform.Find("Dropdown List");
            Assert.That(list, Is.Not.Null);
            Toggle[] options = list.GetComponentsInChildren<Toggle>();
            Assert.That(options.Length, Is.EqualTo(2));
            Toggle chinese = System.Array.Find(options, item => item.GetComponentInChildren<Text>().text == "中文");
            Assert.That(chinese, Is.Not.Null);
            chinese.isOn = true;
            Assert.That(hud.Language, Is.EqualTo(UiLanguage.Chinese));
            Assert.That(Find<Button>(hud, "本地生成").GetComponentInChildren<Text>().text, Is.EqualTo("本地生成"));
            Assert.That(PlayerPrefs.GetInt(UiText.PreferenceKey), Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.25f);
            menu.Show();
            yield return null;
            list = menu.transform.Find("Dropdown List");
            options = list.GetComponentsInChildren<Toggle>();
            Toggle english = System.Array.Find(options, item => item.GetComponentInChildren<Text>().text == "English");
            english.isOn = true;
            Assert.That(hud.Language, Is.EqualTo(UiLanguage.English));
            Assert.That(PlayerPrefs.GetInt(UiText.PreferenceKey), Is.EqualTo(0));
            yield return new WaitForSecondsRealtime(0.25f);
        }

        [UnityTest]
        public IEnumerator StoredChoiceAppliesOnTheNextHudInitialization()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud mainHud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            mainHud.SetLanguage(UiLanguage.Chinese);
            GameObject root = new GameObject("Stored Language Test");
            try
            {
                SceneCraftHud hud = root.AddComponent<SceneCraftHud>();
                hud.Initialize(controller, Object.FindObjectOfType<RuntimeSelectionController>());
                Assert.That(hud.Language, Is.EqualTo(UiLanguage.Chinese));
                Assert.That(hud.PromptText, Is.EqualTo(UiText.ChinesePrompt));
                Assert.That(Find<Dropdown>(hud, "LanguageSelect").captionText.text, Is.EqualTo("中文"));
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwitchingLanguagePreservesInputSelectionSceneAndRunningStages()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            RuntimeSelectionController selection = Object.FindObjectOfType<RuntimeSelectionController>();
            yield return WaitForBuild(controller);
            controller.Generate("Create a living room. Only place one sofa and one coffee table", false);
            yield return WaitForBuild(controller);
            SceneSpec previous = controller.CurrentSpec;
            string json = SceneSpecJson.ToJson(previous);
            SceneObjectView view = Object.FindObjectOfType<SceneObjectView>();
            selection.Select(view);
            InputField input = Find<InputField>(hud, "PromptInput");
            input.text = "My custom brief / 我的自定义需求";
            hud.SetLanguage(UiLanguage.English, false);
            hud.SetLanguage(UiLanguage.Chinese, false);
            Assert.That(input.text, Is.EqualTo("My custom brief / 我的自定义需求"));
            Assert.That(selection.Selected, Is.SameAs(view));
            Assert.That(controller.CurrentSpec, Is.SameAs(previous));
            Assert.That(SceneSpecJson.ToJson(previous), Is.EqualTo(json));
            Assert.That(Find<RectTransform>(hud, "RunDetails").gameObject.activeSelf, Is.False);
            foreach (BuildProgress step in controller.Progress)
                Assert.That(ReadStep(hud, step.Stage, "StepState").text, Is.EqualTo("完成"));
            controller.Generate("Create a bedroom. Only place one bed", false);
            Assert.That(controller.IsBusy, Is.True);
            Find<Button>(hud, "查看运行记录").onClick.Invoke();
            Text record = Find<RectTransform>(hud, "LogContent").GetComponentInChildren<Text>();
            string details = record.text.Substring(record.text.IndexOf("\n\n") + 2);
            hud.SetLanguage(UiLanguage.English, false);
            Assert.That(record.text, Does.EndWith(details));
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(controller.CurrentSpec, Is.SameAs(previous));
            Assert.That(controller.Progress[0].State, Is.EqualTo(BuildState.Running));
            Assert.That(ReadStep(hud, BuildStage.Requirements, "StepState").text, Is.EqualTo("Running"));
            Assert.That(Find<Button>(hud, "保存场景").interactable, Is.False);
            Assert.That(input.interactable, Is.False);
            Assert.That(Find<Dropdown>(hud, "LanguageSelect").interactable, Is.True);
            yield return WaitForBuild(controller);
            Assert.That(controller.CurrentSpec.placements.Count, Is.EqualTo(1));
            Assert.That(Find<Text>(hud, "RoomSummary").text, Is.EqualTo("Bedroom"));
            Assert.That(input.text, Is.EqualTo("My custom brief / 我的自定义需求"));
            Find<Button>(hud, "查看运行记录").onClick.Invoke();
            selection.Clear();
            controller.Generate(UiText.EnglishPrompt, false);
            yield return WaitForBuild(controller);
        }

        [UnityTest]
        public IEnumerator BothLanguagesRenderAtWideAndCompactResolutions()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            SceneCraftHud hud = Object.FindObjectOfType<SceneCraftHud>();
            yield return WaitForBuild(controller);
            controller.Generate(UiText.EnglishPrompt, false);
            yield return WaitForBuild(controller);
            foreach (UiLanguage language in new[] { UiLanguage.English, UiLanguage.Chinese })
            {
                hud.SetLanguage(language, false);
                string suffix = language == UiLanguage.English ? "en" : "zh";
                HudTests.CapturePanel(hud, 1920, 1080, "hud-" + suffix + "-wide.png");
                HudTests.CapturePanel(hud, 1280, 720, "hud-" + suffix + "-compact.png");
            }
            yield return null;
        }

        private static Text ReadStep(SceneCraftHud hud, BuildStage stage, string child)
        {
            return Find<RectTransform>(hud, "Workflow_" + stage).Find(child).GetComponent<Text>();
        }

        private static T Find<T>(SceneCraftHud hud, string name) where T : Component
        {
            foreach (T component in hud.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            Assert.Fail("Missing HUD component " + name);
            return null;
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
