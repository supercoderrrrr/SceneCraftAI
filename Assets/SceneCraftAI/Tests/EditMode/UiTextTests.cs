using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SceneCraftAI.Domain;
using SceneCraftAI.Planning;
using SceneCraftAI.Runtime;
using SceneCraftAI.UI;

namespace SceneCraftAI.Tests
{
    public sealed class UiTextTests
    {
        [TestCase(0, UiLanguage.English)]
        [TestCase(1, UiLanguage.Chinese)]
        [TestCase(-1, UiLanguage.English)]
        [TestCase(2, UiLanguage.English)]
        public void MissingOrInvalidLanguageUsesEnglish(int value, UiLanguage expected)
        {
            Assert.That(UiText.FromPreference(value), Is.EqualTo(expected));
        }

        [Test]
        public void EnglishLabelsAndFormatsAreComplete()
        {
            Dictionary<string, string> entries = (Dictionary<string, string>)typeof(UiText)
                .GetField("English", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            foreach (KeyValuePair<string, string> entry in entries)
            {
                Assert.That(entry.Value, Is.Not.Empty, entry.Key);
                Assert.That(Regex.IsMatch(entry.Value, "[\\u3400-\\u9fff]"), Is.False, entry.Key);
                Assert.DoesNotThrow(() => UiText.Get(UiLanguage.English, entry.Key, 12, 91.5f), entry.Key);
                Assert.DoesNotThrow(() => UiText.Get(UiLanguage.Chinese, entry.Key, 12, 91.5f), entry.Key);
            }
            Assert.That(UiText.Get(UiLanguage.English, "tableRound"), Is.EqualTo("tableRound"));
        }

        [Test]
        public void ProgressNotesKeepCountsScoresAndFallbackDetails()
        {
            BuildProgress inventory = new BuildProgress(BuildStage.Inventory, BuildState.Done, "已整理 {0} 个物件请求", 56);
            BuildProgress layout = new BuildProgress(BuildStage.Layout, BuildState.Done, "已选出布局，评分 {1:0.0}", score: 91.5f);
            BuildProgress review = new BuildProgress(BuildStage.Review, BuildState.Done, "{0} 项约束提醒，请检查摆放", 3,
                detail: "云端复查未完成，保留本地检查结果");
            Assert.That(UiText.ProgressNote(UiLanguage.English, inventory), Is.EqualTo("Prepared 56 object requests"));
            Assert.That(UiText.ProgressNote(UiLanguage.Chinese, inventory), Is.EqualTo("已整理 56 个物件请求"));
            Assert.That(UiText.ProgressNote(UiLanguage.English, layout), Is.EqualTo("Layout selected, score 91.5"));
            Assert.That(UiText.ProgressNote(UiLanguage.Chinese, layout), Is.EqualTo("已选出布局，评分 91.5"));
            Assert.That(UiText.ProgressNote(UiLanguage.English, review), Does.Contain("Cloud review skipped"));
            Assert.That(UiText.ProgressNote(UiLanguage.English, review), Does.Contain("3 constraint notes"));
            Assert.That(UiText.ProgressNote(UiLanguage.Chinese, review), Does.Contain("3 项约束提醒"));
            Assert.That(UiText.ProgressNote(UiLanguage.English, default(BuildProgress)), Is.Empty);
        }

        [TestCase("living_room", "客厅", "Living room")]
        [TestCase("bedroom", "卧室", "Bedroom")]
        [TestCase("master_bedroom", "主卧", "Primary bedroom")]
        [TestCase("secondary_bedroom", "次卧", "Guest bedroom")]
        [TestCase("kitchen", "厨房", "Kitchen")]
        [TestCase("bathroom", "卫生间", "Bathroom")]
        public void RoomNamesUseTheSelectedLanguage(string type, string chinese, string english)
        {
            RoomSpec room = new RoomSpec { type = type };
            Assert.That(UiText.RoomName(UiLanguage.English, room), Is.EqualTo(english));
            Assert.That(UiText.RoomName(UiLanguage.Chinese, room), Is.EqualTo(chinese));
        }

        [Test]
        public void NormalizedBedroomKeepsItsRoleName()
        {
            Assert.That(UiText.RoomName(UiLanguage.English, new RoomSpec { type = "bedroom", id = "master_bedroom_02" }),
                Is.EqualTo("Primary bedroom"));
            Assert.That(UiText.RoomName(UiLanguage.Chinese, new RoomSpec { type = "bedroom", description = "secondary_bedroom" }),
                Is.EqualTo("次卧"));
        }

        [TestCase("sofa", "沙发", "Sofa")]
        [TestCase("coffee_table", "茶几", "Coffee table")]
        [TestCase("flower_pot", "花盆", "Flower pot")]
        [TestCase("toilet", "马桶", "Toilet")]
        public void ObjectCategoriesAreTranslatedWithoutChangingAssetIds(string category, string chinese, string english)
        {
            Assert.That(UiText.CategoryName(UiLanguage.English, category), Is.EqualTo(english));
            Assert.That(UiText.CategoryName(UiLanguage.Chinese, category), Is.EqualTo(chinese));
        }

        [Test]
        public void BothDefaultBriefsDescribeTheSameFiveRoomProgram()
        {
            OfflineScenePlanner planner = new OfflineScenePlanner();
            SceneSpec english = planner.Build(UiText.EnglishPrompt);
            SceneSpec chinese = planner.Build(UiText.ChinesePrompt);
            Assert.That(english.rooms.Count, Is.EqualTo(5));
            Assert.That(chinese.rooms.Count, Is.EqualTo(5));
            List<string> englishRooms = english.rooms.ConvertAll(room => UiText.RoomName(UiLanguage.English, room));
            List<string> chineseRooms = chinese.rooms.ConvertAll(room => UiText.RoomName(UiLanguage.English, room));
            CollectionAssert.AreEquivalent(chineseRooms, englishRooms);
            CollectionAssert.AreEquivalent(chinese.objects.ConvertAll(item => item.category), english.objects.ConvertAll(item => item.category));
        }
    }
}
