using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SceneCraftAI.Domain;
using UnityEngine;

namespace SceneCraftAI.Planning
{
    public sealed class OfflineScenePlanner : IScenePlanner
    {
        public void Plan(string prompt, Action<SceneSpec> onSuccess, Action<string> onError)
        {
            try
            {
                onSuccess(Build(prompt));
            }
            catch (Exception exception)
            {
                onError(exception.Message);
            }
        }

        public SceneSpec Build(string prompt)
        {
            string normalized = (prompt ?? string.Empty).Trim();
            string lower = normalized.ToLowerInvariant();
            SceneSpec spec = new SceneSpec
            {
                prompt = normalized,
                style = DetectStyle(lower)
            };

            var roomTypes = PromptRules.ReadRooms(lower);
            string roomType = roomTypes.Count > 0 ? roomTypes[0]
                : PromptRules.HasPositive(lower, "床") || PromptRules.HasPositive(lower, "bed") ? "bedroom" : "living_room";
            if (roomType == "bedroom" || roomType == "master_bedroom" || roomType == "secondary_bedroom")
            {
                spec.roomType = "bedroom";
                spec.room.width = 5.4f;
                spec.room.depth = 4.5f;
                Add(spec, "bed", "double bed against the wall", "against_wall");
                Add(spec, "nightstand", "matching nightstand beside the bed", "beside_bed");
                Add(spec, "nightstand", "second matching nightstand beside the bed", "beside_bed");
                Add(spec, "wardrobe", "large wardrobe against a side wall", "against_wall");
                Add(spec, "rug", "soft rug under the front of the bed", "center");
                if (ContainsAny(lower, "书桌", "desk", "学习"))
                {
                    Add(spec, "desk", "wood desk near the window", "near_window");
                    Add(spec, "chair", "chair facing the desk", "in_front_of_desk");
                }
            }
            else if (roomType == "study")
            {
                spec.roomType = "study";
                spec.room.width = 5.2f;
                spec.room.depth = 4.2f;
                Add(spec, "desk", "main work desk near the window", "near_window");
                Add(spec, "chair", "chair facing the desk", "in_front_of_desk");
                Add(spec, "bookshelf", "bookshelf against the wall", "against_wall");
                Add(spec, "sofa", "small reading sofa against the wall", "against_wall");
                Add(spec, "floor_lamp", "warm reading lamp beside the sofa", "beside_sofa");
                Add(spec, "plant", "green plant in a free corner", "corner");
            }
            else if (roomType == "kitchen" || roomType == "bathroom" || roomType == "dining_room")
            {
                spec.roomType = roomType;
                spec.room.type = roomType;
                HousePromptPlanner.AddRoomFurniture(spec, spec.room);
            }
            else
            {
                spec.roomType = "living_room";
                spec.room.width = 6.4f;
                spec.room.depth = 5.2f;
                Add(spec, "sofa", "comfortable sofa against the wall", "against_wall");
                Add(spec, "coffee_table", "coffee table centered in front of sofa", "in_front_of_sofa");
                Add(spec, "tv_stand", "TV stand facing the sofa", "opposite_sofa");
                Add(spec, "rug", "large soft rug below the coffee table", "under_coffee_table");
                Add(spec, "plant", "green plant in a bright corner", "corner");
                Add(spec, "floor_lamp", "warm floor lamp beside the sofa", "beside_sofa");
                Add(spec, "wall_art", "modern wall art above the sofa", "wall_above_sofa", "wall");
            }

            spec.room.hasWindow = !ContainsAny(lower,
                "不要窗", "不需要窗", "没有窗", "无窗", "no window", "without window", "windowless");
            AddContextualStageObjects(spec, lower);
            HousePromptPlanner.ExpandIfRequested(spec, lower);
            if (ContainsAny(lower, "空客厅", "空房间", "不要家具", "不放家具", "empty room", "no furniture", "without furniture"))
            {
                spec.objects.Clear();
            }

            if (spec.rooms.Count == 0) spec.room.type = spec.roomType;
            SceneSpecDefaults.EnsureHouse(spec);
            ApplyPromptRules(spec, normalized);
            SceneSpecDefaults.EnsureOpenings(spec);
            return spec;
        }

        private static void AddContextualStageObjects(SceneSpec spec, string prompt)
        {
            if (ContainsAny(prompt, "只", "仅", "only")) return;
            if (CountCategory(spec, "ceiling_light") == 0)
                Add(spec, "ceiling_light", "central ceiling light", "ceiling_center", "ceiling");
            if (spec.roomType == "living_room")
            {
                string tableId = FindFirstId(spec, "coffee_table");
                if (!string.IsNullOrWhiteSpace(tableId) && CountCategory(spec, "vase") == 0)
                    AddAnchored(spec, "vase", "vase on coffee table", "on_top_of", "surface", tableId);
            }
            else if (spec.roomType == "bedroom")
            {
                string nightstandId = FindFirstId(spec, "nightstand");
                if (!string.IsNullOrWhiteSpace(nightstandId) && CountCategory(spec, "table_lamp") == 0)
                    AddAnchored(spec, "table_lamp", "lamp on nightstand", "on_top_of", "surface", nightstandId);
            }
            else if (spec.roomType == "study")
            {
                string deskId = FindFirstId(spec, "desk");
                if (!string.IsNullOrWhiteSpace(deskId) && CountCategory(spec, "books") == 0)
                    AddAnchored(spec, "books", "books on desk", "on_top_of", "surface", deskId);
            }
        }

        private static string FindFirstId(SceneSpec spec, string category)
        {
            for (int i = 0; i < spec.objects.Count; i++)
                if (spec.objects[i].category == category) return spec.objects[i].id;
            return string.Empty;
        }

        private static void AddAnchored(
            SceneSpec spec, string category, string description, string relation, string placement, string anchorId)
        {
            Add(spec, category, description, relation, placement);
            spec.objects[spec.objects.Count - 1].anchorId = anchorId;
        }

        public static int ApplyPromptRules(SceneSpec spec, string prompt)
        {
            return PromptRules.Apply(spec, prompt);
        }

        private static int CountCategory(SceneSpec spec, string category)
        {
            return spec.objects.FindAll(item => item.category == category).Count;
        }

        private static void Add(SceneSpec spec, string category, string description, string relation, string placement = "floor")
        {
            spec.objects.Add(new SceneObjectRequest
            {
                id = category + "_" + (spec.objects.Count + 1).ToString("00"),
                category = category, description = description, relation = relation, placement = placement
            });
        }

        private static string DetectStyle(string prompt)
        {
            if (ContainsAny(prompt, "自然", "原木", "natural", "sage")) return "natural wood sage";
            if (ContainsAny(prompt, "蓝色", "冷色", "blue", "cool")) return "modern blue";
            if (ContainsAny(prompt, "极简", "minimal")) return "minimal modern";
            return "warm modern wood";
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (value.Contains(terms[i])) return true;
            }

            return false;
        }
    }
}
