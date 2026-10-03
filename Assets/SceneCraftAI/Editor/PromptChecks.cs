using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using SceneCraftAI.Planning;
using SceneCraftAI.Runtime;
using UnityEngine;

namespace SceneCraftAI.Editor
{
    public static class PromptChecks
    {
        [Serializable]
        private sealed class Probe
        {
            public string name;
            public string prompt;
            public int expectedRooms;
            public string expectedType;
            public string category;
            public int expectedCount = -1;
            public int actualRooms;
            public string actualType;
            public int actualCount;
            public string roomTypes;
            public string inventory;
            public bool passed;
            public string error;
        }

        [Serializable]
        private sealed class Results
        {
            public string date = "2026-10-03";
            public string scope = "Local planning and simulated cloud normalization, no paid API requests";
            public List<Probe> probes = new List<Probe>();
            public bool cloudObjectIdsPreserved;
            public float requestedLivingWidth;
            public float normalizedLivingWidth;
            public float sharedWallOffsetBeforeClamp;
            public float sharedWallOffsetAfterClamp;
        }

        public static void Run()
        {
            Results results = new Results();
            results.probes.Add(Check("English room list",
                "A lived-in apartment with a living room, a master bedroom, a guest bedroom, a kitchen and a bathroom",
                5, "house"));
            results.probes.Add(Check("English bedroom quantity",
                "An apartment with one living room, two bedrooms, a kitchen and a bathroom",
                5, "house"));
            results.probes.Add(Check("Chinese bedroom quantity",
                "一套住宅，一个客厅、两间卧室、一个厨房和一个卫生间",
                5, "house"));
            results.probes.Add(Check("Primary bedroom alias",
                "An apartment with one living room, one primary bedroom, one secondary bedroom, one kitchen and one bathroom",
                5, "house"));
            results.probes.Add(Check("Strict English furniture list",
                "Create a living room. Only place one sofa, one coffee table, one TV stand, two plants and one vase",
                1, "living_room", "plant", 2));
            results.probes.Add(Check("Empty English room",
                "Create an empty living room without furniture", 1, "living_room", "*", 0));
            results.probes.Add(Check("Negative bed mention",
                "Create a living room with one sofa and one TV stand but no bed",
                1, "living_room", "bed", 0));
            results.probes.Add(Check("Standalone kitchen",
                "Create a kitchen. Only include one refrigerator and one stove",
                1, "kitchen", "refrigerator", 1));
            results.probes.Add(Check("Excluded exact asset",
                "Create a living room with one sofa and no tableRound",
                1, "living_room", "dining_table", 0));
            results.probes.Add(Check("Room scoped vase counts",
                "A house with a living room, a master bedroom and a guest bedroom. Put one vase in each bedroom",
                3, "house", "vase", 2));
            Probe relations = Check("Offline relation direction",
                "Create a living room. Only place one sofa and one coffee table. Put the coffee table behind the sofa",
                1, "living_room");
            SceneSpec relationSpec = new OfflineScenePlanner().Build(relations.prompt);
            SceneObjectRequest table = relationSpec.objects.Find(item => item.category == "coffee_table");
            relations.passed = relations.passed && table != null && table.relation == "behind";
            relations.inventory += " | table relation=" + (table == null ? "missing" : table.relation);
            results.probes.Add(relations);

            SceneSpec cloud = new SceneSpec { roomType = "house" };
            cloud.room = new RoomSpec { id = "custom_living", type = "living_room", width = 4.2f, depth = 4f };
            cloud.rooms.Add(cloud.room);
            cloud.rooms.Add(new RoomSpec { id = "custom_bedroom", type = "bedroom", width = 4f, depth = 3.8f });
            cloud.objects.Add(new SceneObjectRequest
            {
                id = "custom_desk", roomId = "custom_bedroom", category = "desk", relation = "near_window"
            });
            results.requestedLivingWidth = cloud.room.width;
            SceneSpec normalized = DeepSeekProtocol.ParseAndNormalize(SceneSpecJson.ToJson(cloud),
                "A living room and a bedroom with a work desk");
            results.cloudObjectIdsPreserved = normalized.objects.Exists(item => item.id == "custom_desk");
            results.normalizedLivingWidth = normalized.room.width;

            SceneSpec house = new OfflineScenePlanner().Build(
                "A lived-in apartment with a living room, a master bedroom, a guest bedroom, a kitchen and a bathroom");
            results.sharedWallOffsetBeforeClamp = SharedWallOffset(house);
            MethodInfo clamp = typeof(SceneRuntimeController).GetMethod("ClampRoom",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (clamp == null) throw new MissingMethodException("Runtime room clamp was not found");
            for (int i = 0; i < house.rooms.Count; i++) clamp.Invoke(null, new object[] { house.rooms[i] });
            results.sharedWallOffsetAfterClamp = SharedWallOffset(house);

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "UserData", "CodeReview", "2026-10-03");
            Directory.CreateDirectory(folder);
            string output = Path.Combine(folder, "prompt-checks.json");
            File.WriteAllText(output, JsonUtility.ToJson(results, true));
            Debug.Log("Prompt review written to " + output);
        }

        private static float SharedWallOffset(SceneSpec house)
        {
            float maximum = 0f;
            for (int i = 0; i < house.rooms.Count; i++)
            {
                RoomSpec room = house.rooms[i];
                for (int j = 0; j < room.openings.Count; j++)
                {
                    WallOpeningSpec opening = room.openings[j];
                    if (string.IsNullOrWhiteSpace(opening.connectsToRoomId)) continue;
                    RoomSpec target = house.rooms.Find(item => item.id == opening.connectsToRoomId);
                    WallOpeningSpec paired = target.openings.Find(item => item.connectsToRoomId == room.id);
                    maximum = Mathf.Max(maximum, Mathf.Abs(WallPlane(room, opening.wall) - WallPlane(target, paired.wall)));
                }
            }
            return maximum;
        }

        private static float WallPlane(RoomSpec room, string wall)
        {
            if (wall == "left") return room.center.x - room.width * 0.5f;
            if (wall == "right") return room.center.x + room.width * 0.5f;
            if (wall == "front") return room.center.z - room.depth * 0.5f;
            return room.center.z + room.depth * 0.5f;
        }

        private static Probe Check(string name, string prompt, int rooms, string type,
            string category = "", int count = -1)
        {
            Probe probe = new Probe
            {
                name = name, prompt = prompt, expectedRooms = rooms, expectedType = type,
                category = category, expectedCount = count
            };
            try
            {
                SceneSpec spec = new OfflineScenePlanner().Build(prompt);
                OfflineScenePlanner.ApplyPromptRules(spec, prompt);
                new AssetCatalog().ApplyAssetHints(spec, prompt);
                probe.actualRooms = spec.rooms.Count;
                probe.actualType = spec.roomType;
                probe.actualCount = category == "*" ? spec.objects.Count
                    : spec.objects.FindAll(item => item.category == category).Count;
                List<string> types = new List<string>();
                for (int i = 0; i < spec.rooms.Count; i++) types.Add(spec.rooms[i].type);
                probe.roomTypes = string.Join(", ", types);
                Dictionary<string, int> inventory = new Dictionary<string, int>();
                for (int i = 0; i < spec.objects.Count; i++)
                {
                    SceneObjectRequest item = spec.objects[i];
                    string key = item.roomId + "/" + item.category;
                    int current;
                    inventory.TryGetValue(key, out current);
                    inventory[key] = current + 1;
                }
                List<string> summary = new List<string>();
                foreach (KeyValuePair<string, int> item in inventory) summary.Add(item.Key + "=" + item.Value);
                probe.inventory = string.Join(", ", summary);
                probe.passed = probe.actualRooms == rooms && probe.actualType == type &&
                    (count < 0 || probe.actualCount == count);
            }
            catch (Exception error)
            {
                probe.error = error.Message;
            }
            return probe;
        }
    }
}
