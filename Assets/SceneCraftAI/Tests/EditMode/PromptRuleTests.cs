using NUnit.Framework;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using SceneCraftAI.Layout;
using SceneCraftAI.Planning;

namespace SceneCraftAI.Tests
{
    public sealed class PromptRuleTests
    {
        [TestCase("An apartment with one living room, two bedrooms, a kitchen and a bathroom", 5)]
        [TestCase("An apartment with a living room, a primary bedroom, a secondary bedroom, a kitchen and a restroom", 5)]
        [TestCase("一套住宅：客厅、两间卧室、厨房和卫生间", 5)]
        [TestCase("Create a living room with one sofa, but no bed or bedroom", 1)]
        [TestCase("An apartment with one kitchen", 1)]
        public void BilingualRoomCountsRespectRolesAndNegation(string prompt, int count)
        {
            Assert.That(new OfflineScenePlanner().Build(prompt).rooms.Count, Is.EqualTo(count));
        }

        [TestCase("Create a kitchen. Only include one refrigerator and one stove", "kitchen", "refrigerator", 2)]
        [TestCase("Create a bathroom. Only place one toilet and one sink vanity", "bathroom", "toilet", 2)]
        [TestCase("Create a dining room. Only include one dining table and four chairs", "dining_room", "dining_table", 5)]
        public void SingleRoomProgramsUseCompleteFurnitureLexicon(string prompt, string type, string category, int total)
        {
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            Assert.That(spec.roomType, Is.EqualTo(type));
            Assert.That(spec.room.type, Is.EqualTo(type));
            Assert.That(spec.objects.Count, Is.EqualTo(total));
            Assert.That(spec.objects.Exists(item => item.category == category), Is.True);
        }

        [TestCase("Create a living room with one sofa and no tableRound")]
        [TestCase("客厅放一张沙发，不要 tableRound.fbx")]
        [TestCase("Create a living room. Do not add tableRound, desks or chairs")]
        public void AssetHintsCannotRestoreExcludedModels(string prompt)
        {
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            new AssetCatalog().ApplyAssetHints(spec, prompt);
            Assert.That(spec.objects.Exists(item => item.category == "dining_table"), Is.False);
        }

        [Test]
        public void EachBedroomGetsItsOwnVaseAndLocalSupport()
        {
            const string prompt = "A house with a living room, a master bedroom and a guest bedroom. Put one vase in each bedroom";
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            new AssetCatalog().ApplyAssetHints(spec, prompt);
            OfflineScenePlanner.ApplyPromptRules(spec, prompt);
            Assert.That(spec.objects.FindAll(item => item.category == "vase").Count, Is.EqualTo(2));
            foreach (RoomSpec room in spec.rooms)
            {
                int expected = room.type == "bedroom" ? 1 : 0;
                Assert.That(spec.objects.FindAll(item => item.category == "vase" && item.roomId == room.id).Count, Is.EqualTo(expected));
            }
            foreach (SceneObjectRequest vase in spec.objects.FindAll(item => item.category == "vase"))
                Assert.That(spec.objects.Find(item => item.id == vase.anchorId).roomId, Is.EqualTo(vase.roomId));
        }

        [Test]
        public void SeparateRoomQuantitiesDoNotCollapseIntoGlobalCount()
        {
            const string prompt = "客厅和卧室。客厅放两盆植物，卧室放一盆植物";
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            foreach (RoomSpec room in spec.rooms)
                Assert.That(spec.objects.FindAll(item => item.category == "plant" && item.roomId == room.id).Count,
                    Is.EqualTo(room.type == "living_room" ? 2 : 1));
            string before = SceneSpecJson.ToJson(spec);
            OfflineScenePlanner.ApplyPromptRules(spec, prompt);
            Assert.That(SceneSpecJson.ToJson(spec), Is.EqualTo(before));
        }

        [Test]
        public void EnglishRoomQualifiersAfterFurnitureSelectTheCorrectRoom()
        {
            const string prompt = "A living room and a bedroom. Put two plants in the living room and one plant in the bedroom";
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            foreach (RoomSpec room in spec.rooms)
                Assert.That(spec.objects.FindAll(item => item.category == "plant" && item.roomId == room.id).Count,
                    Is.EqualTo(room.type == "living_room" ? 2 : 1));
        }

        [TestCase("Create a living room with one sofa. No sofa", 0)]
        [TestCase("Create a living room with one vase. Do not add another vase", 1)]
        public void ExclusionsDistinguishForbiddenItemsFromExtraCopies(string prompt, int count)
        {
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            string category = prompt.Contains("sofa") ? "sofa" : "vase";
            Assert.That(spec.objects.FindAll(item => item.category == category).Count, Is.EqualTo(count));
        }

        [TestCase("behind", "behind")]
        [TestCase("to the left of", "left_of")]
        [TestCase("to the right of", "right_of")]
        [TestCase("in front of", "in_front_of")]
        public void ExplicitDirectionsReplaceOfflineDefaults(string phrase, string relation)
        {
            SceneSpec spec = new OfflineScenePlanner().Build("Create a living room. Only place one sofa and one coffee table. Put the coffee table " + phrase + " the sofa");
            SceneObjectRequest table = spec.objects.Find(item => item.category == "coffee_table");
            Assert.That(table.relation, Is.EqualTo(relation));
            Assert.That(table.anchorId, Is.EqualTo(spec.objects.Find(item => item.category == "sofa").id));
        }

        [Test]
        public void CloudNormalizationPreservesObjectsAndCustomDimensions()
        {
            SceneSpec source = new SceneSpec { roomType = "house" };
            source.rooms.Add(new RoomSpec { id = "custom_living", type = "living_room", width = 4.2f, depth = 4f });
            source.rooms.Add(new RoomSpec { id = "custom_bedroom", type = "bedroom", width = 4f, depth = 3.8f });
            source.objects.Add(new SceneObjectRequest { id = "custom_desk", roomId = "custom_bedroom", category = "desk", relation = "near_window" });
            SceneSpec result = DeepSeekProtocol.ParseAndNormalize(SceneSpecJson.ToJson(source), "A living room and a bedroom with a work desk");
            Assert.That(result.room.width, Is.EqualTo(4.2f));
            Assert.That(result.objects.Count, Is.EqualTo(1));
            Assert.That(result.objects[0].id, Is.EqualTo("custom_desk"));
            Assert.That(result.objects[0].roomId, Is.EqualTo("custom_bedroom"));
        }

        [Test]
        public void SharedDoorsRemainValidAfterRepeatedDimensionNormalization()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("一套住宅：客厅、主卧、次卧、厨房和卫生间");
            foreach (RoomSpec room in spec.rooms) Assert.That(RoomGeometry.Normalize(room), Is.False);
            foreach (RoomConnectionSpec connection in spec.connections)
                Assert.That(RoomGeometry.HasPassage(spec.rooms.Find(room => room.id == connection.roomAId),
                    spec.rooms.Find(room => room.id == connection.roomBId), connection), Is.True, connection.id);
        }

        [Test]
        public void AdditionalRoomsNeverOverlapExistingWings()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("住宅：客厅、主卧、两个次卧、厨房、卫生间、书房和餐厅");
            Assert.That(spec.rooms.Count, Is.EqualTo(8));
            for (int i = 0; i < spec.rooms.Count; i++)
                for (int j = i + 1; j < spec.rooms.Count; j++)
                    Assert.That(RoomGeometry.Overlaps(spec.rooms[i], spec.rooms[j]), Is.False, spec.rooms[i].id + " / " + spec.rooms[j].id);
        }

        [Test]
        public void WindowsOnlyOccupyExteriorWallSegments()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("住宅：客厅、主卧、次卧、厨房、卫生间、书房和餐厅");
            foreach (RoomSpec room in spec.rooms)
                foreach (WallOpeningSpec window in room.openings.FindAll(opening => opening.type == "window"))
                    foreach (RoomSpec other in spec.rooms)
                    {
                        float start, end;
                        if (other == room || !RoomGeometry.TrySharedRange(room, other, window.wall, out start, out end)) continue;
                        float center = RoomGeometry.Along(room, window.wall) + window.center;
                        Assert.That(center + window.width * 0.5f <= start || center - window.width * 0.5f >= end, Is.True);
                    }
        }

        [Test]
        public void ConnectivityRejectsGraphEdgesWithBlockedOrMismatchedDoors()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("一个客厅和一个卧室");
            AssetCatalog catalog = new AssetCatalog();
            new SceneLayoutEngine().Layout(spec, catalog);
            Assert.That(new LayoutEvaluator().Evaluate(spec, catalog).ConnectivityScore, Is.EqualTo(1f));
            spec.rooms[1].center += UnityEngine.Vector3.right * 0.2f;
            Assert.That(new LayoutEvaluator().Evaluate(spec, catalog).ConnectivityScore, Is.LessThan(1f));
        }
    }
}
