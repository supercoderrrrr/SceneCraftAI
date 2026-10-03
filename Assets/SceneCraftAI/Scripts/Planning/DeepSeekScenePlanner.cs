using System;
using System.Collections;
using System.Collections.Generic;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using SceneCraftAI.Layout;
using UnityEngine;
using UnityEngine.Networking;

namespace SceneCraftAI.Planning
{
    public sealed class DeepSeekScenePlanner : IScenePlanner
    {
        public const string DefaultModel = "deepseek-v4-flash";
        public const string DefaultEndpoint = "https://api.deepseek.com/chat/completions";

        private readonly MonoBehaviour host;
        private readonly string apiKey;
        private readonly string model;

        public DeepSeekScenePlanner(MonoBehaviour host, string apiKey, string model)
        {
            this.host = host;
            this.apiKey = apiKey;
            this.model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        }

        public void Plan(string prompt, Action<SceneSpec> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onError("DEEPSEEK_SCENECRAFTAI_APIKEY is not configured.");
                return;
            }

            host.StartCoroutine(Send(prompt, onSuccess, onError));
        }

        private IEnumerator Send(string prompt, Action<SceneSpec> onSuccess, Action<string> onError)
        {
            string body = DeepSeekProtocol.BuildPlanRequest(prompt, model);
            using (UnityWebRequest request = new UnityWebRequest(DefaultEndpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                request.timeout = 90;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError(DescribeFailure(request));
                    yield break;
                }

                try
                {
                    string sceneJson = DeepSeekProtocol.ReadSceneJson(request.downloadHandler.text);
                    SceneSpec spec = DeepSeekProtocol.ParseAndNormalize(sceneJson, prompt);
                    onSuccess(spec);
                }
                catch (Exception exception)
                {
                    onError("DeepSeek scene JSON could not be parsed: " + exception.Message);
                }
            }
        }

        private static string DescribeFailure(UnityWebRequest request)
        {
            switch (request.responseCode)
            {
                case 401: return "DeepSeek authentication failed. Check DEEPSEEK_SCENECRAFTAI_APIKEY.";
                case 402: return "DeepSeek account balance is insufficient.";
                case 429: return "DeepSeek is busy or the request rate is too high. Please retry shortly.";
                default: return "DeepSeek request failed: HTTP " + request.responseCode + " " + request.error;
            }
        }
    }

    public sealed class DeepSeekSceneCritic
    {
        private readonly MonoBehaviour host;
        private readonly string apiKey;
        private readonly string model;

        public DeepSeekSceneCritic(MonoBehaviour host, string apiKey, string model)
        {
            this.host = host;
            this.apiKey = apiKey;
            this.model = string.IsNullOrWhiteSpace(model) ? DeepSeekScenePlanner.DefaultModel : model.Trim();
        }

        public void Review(
            SceneSpec scene,
            LayoutQualityReport quality,
            Action<SceneSpec> onSuccess,
            Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onError("DeepSeek Critic skipped: API key is not configured.");
                return;
            }

            host.StartCoroutine(Send(scene, quality, onSuccess, onError));
        }

        private IEnumerator Send(
            SceneSpec scene,
            LayoutQualityReport quality,
            Action<SceneSpec> onSuccess,
            Action<string> onError)
        {
            string body = DeepSeekProtocol.BuildReviewRequest(scene, quality, model);
            using (UnityWebRequest request = new UnityWebRequest(DeepSeekScenePlanner.DefaultEndpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                request.timeout = 90;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError("DeepSeek Critic request failed: HTTP " + request.responseCode + " " + request.error);
                    yield break;
                }

                try
                {
                    string sceneJson = DeepSeekProtocol.ReadSceneJson(request.downloadHandler.text);
                    onSuccess(DeepSeekProtocol.ParseAndNormalize(sceneJson, scene.prompt));
                }
                catch (Exception exception)
                {
                    onError("DeepSeek Critic response could not be parsed: " + exception.Message);
                }
            }
        }
    }

    public static class DeepSeekProtocol
    {
        private const int MaxSceneObjects = 80;

        private static readonly HashSet<string> SupportedCategories = new HashSet<string>
        {
            "bed", "nightstand", "wardrobe", "sofa", "coffee_table", "tv_stand", "desk", "chair",
            "dining_table", "bookshelf", "plant", "floor_lamp", "rug", "wall_art",
            "ceiling_light", "table_lamp", "vase", "books", "decor_bowl"
            , "toilet", "sink_vanity", "shower", "kitchen_counter", "refrigerator", "stove",
            "computer_set", "clothes_rack", "fan", "air_conditioner", "flower_pot", "laundry_basket"
        };

        private static readonly HashSet<string> SupportedRelations = new HashSet<string>
        {
            "against_wall", "beside_bed", "in_front_of_sofa", "opposite_sofa", "beside_sofa",
            "near_window", "in_front_of_desk", "corner", "wall_above_sofa", "center",
            "under_coffee_table", "around_table", "left_of", "right_of", "in_front_of", "behind",
            "ceiling_center", "on_top_of", "on_shelf", "wall_high", "auto"
        };

        [Serializable]
        private sealed class RequestEnvelope
        {
            public string model;
            public Message[] messages;
            public ResponseFormat response_format = new ResponseFormat();
            public Thinking thinking = new Thinking();
            public bool stream;
        }

        [Serializable]
        private sealed class Message
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class ResponseFormat
        {
            public string type = "json_object";
        }

        [Serializable]
        private sealed class Thinking
        {
            public string type = "disabled";
        }

        [Serializable]
        private sealed class ResponseEnvelope
        {
            public Choice[] choices = Array.Empty<Choice>();
        }

        [Serializable]
        private sealed class Choice
        {
            public ResponseMessage message = new ResponseMessage();
        }

        [Serializable]
        private sealed class ResponseMessage
        {
            public string content = string.Empty;
        }

        public static string BuildPlanRequest(string prompt, string model)
        {
            RequestEnvelope request = new RequestEnvelope
            {
                model = string.IsNullOrWhiteSpace(model) ? DeepSeekScenePlanner.DefaultModel : model.Trim(),
                messages = new[]
                {
                    new Message { role = "system", content = BuildInstructions() },
                    new Message { role = "user", content = prompt ?? string.Empty }
                },
                stream = false
            };
            return JsonUtility.ToJson(request);
        }

        public static string BuildReviewRequest(
            SceneSpec scene,
            LayoutQualityReport quality,
            string model)
        {
            string sceneJson = SceneSpecJson.ToJson(scene, false);
            RequestEnvelope request = new RequestEnvelope
            {
                model = string.IsNullOrWhiteSpace(model) ? DeepSeekScenePlanner.DefaultModel : model.Trim(),
                messages = new[]
                {
                    new Message
                    {
                        role = "system",
                        content = BuildInstructions() + " You are now the critic pass. Keep every object id and " +
                            "category and roomId exactly unchanged and keep all room dimensions/connections unchanged. Do not add or remove objects. " +
                            "Only improve relation, anchorId and preferredAssetId fields when they are semantically wrong. " +
                            "Never change preferredAssetId when the original prompt explicitly names a catalog id, Prefab " +
                            "or FBX source model. Return the complete " +
                            "SceneSpec JSON, not an explanation. The deterministic Unity solver will calculate coordinates."
                    },
                    new Message
                    {
                        role = "user",
                        content = "Original request: " + (scene.prompt ?? string.Empty) + "\nCurrent deterministic score: " +
                            (quality == null ? "unknown" : quality.Summary) +
                            "\nDeterministic multi-view observation (TOP/FRONT/LEFT/RIGHT):\n" +
                            DescribeViews(scene) + "\nReview this scene:\n" + sceneJson
                    }
                },
                stream = false
            };
            return JsonUtility.ToJson(request);
        }

        public static string DescribeViews(SceneSpec scene)
        {
            if (scene == null || scene.placements == null) return "EMPTY";
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            string[] views = { "TOP(x,z)", "FRONT(x,y|depth=z)", "LEFT(z,y|depth=x)", "RIGHT(-z,y|depth=-x)" };
            for (int viewIndex = 0; viewIndex < views.Length; viewIndex++)
            {
                if (viewIndex > 0) builder.Append("\n");
                builder.Append(views[viewIndex]).Append(": ");
                for (int i = 0; i < scene.placements.Count; i++)
                {
                    PlacedObjectSpec item = scene.placements[i];
                    if (i > 0) builder.Append("; ");
                    builder.Append(item.id).Append('[').Append(item.roomId).Append("]@");
                    if (viewIndex == 0)
                        builder.Append(item.position.x.ToString("0.00")).Append(',').Append(item.position.z.ToString("0.00"));
                    else if (viewIndex == 1)
                        builder.Append(item.position.x.ToString("0.00")).Append(',').Append(item.Bounds.center.y.ToString("0.00"))
                            .Append(" d=").Append(item.position.z.ToString("0.00"));
                    else if (viewIndex == 2)
                        builder.Append(item.position.z.ToString("0.00")).Append(',').Append(item.Bounds.center.y.ToString("0.00"))
                            .Append(" d=").Append(item.position.x.ToString("0.00"));
                    else
                        builder.Append((-item.position.z).ToString("0.00")).Append(',').Append(item.Bounds.center.y.ToString("0.00"))
                            .Append(" d=").Append((-item.position.x).ToString("0.00"));
                    builder.Append(" yaw=").Append(item.rotationY.ToString("0"));
                }
            }
            return builder.ToString();
        }

        public static string ReadSceneJson(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw new InvalidOperationException("DeepSeek returned an empty response.");
            }

            ResponseEnvelope response = JsonUtility.FromJson<ResponseEnvelope>(responseJson);
            if (response == null || response.choices == null || response.choices.Length == 0 ||
                response.choices[0] == null || response.choices[0].message == null ||
                string.IsNullOrWhiteSpace(response.choices[0].message.content))
            {
                throw new InvalidOperationException("DeepSeek returned no scene content.");
            }

            return StripCodeFence(response.choices[0].message.content.Trim());
        }

        public static SceneSpec ParseAndNormalize(string sceneJson, string originalPrompt)
        {
            SceneSpec spec = SceneSpecJson.FromJson(StripCodeFence(sceneJson));
            spec.prompt = originalPrompt ?? string.Empty;
            spec.roomType = NormalizeRoomType(spec.roomType);
            if (string.IsNullOrWhiteSpace(spec.style)) spec.style = "modern";

            List<SceneObjectRequest> normalized = new List<SceneObjectRequest>();
            HashSet<string> usedIds = new HashSet<string>();
            Dictionary<string, int> categoryCounts = new Dictionary<string, int>();
            AssetCatalog assetCatalog = new AssetCatalog();
            for (int i = 0; i < spec.objects.Count && normalized.Count < MaxSceneObjects; i++)
            {
                SceneObjectRequest request = spec.objects[i];
                if (request == null || string.IsNullOrWhiteSpace(request.category)) continue;
                string category = NormalizeCategory(request.category);
                if (!SupportedCategories.Contains(category)) continue;

                int count;
                categoryCounts.TryGetValue(category, out count);
                count++;
                categoryCounts[category] = count;
                string id = string.IsNullOrWhiteSpace(request.id) ? category + "_" + count.ToString("00") : request.id.Trim();
                while (!usedIds.Add(id))
                {
                    count++;
                    categoryCounts[category] = count;
                    id = category + "_" + count.ToString("00");
                }

                string relation = NormalizeRelation(request.relation);
                if (!SupportedRelations.Contains(relation)) relation = "auto";

                request.id = id;
                request.category = category;
                request.description = string.IsNullOrWhiteSpace(request.description)
                    ? "user requested " + category
                    : request.description.Trim();
                request.placement = NormalizePlacement(category);
                request.relation = relation;
                request.anchorId = string.IsNullOrWhiteSpace(request.anchorId) ? string.Empty : request.anchorId.Trim();
                request.preferredAssetId = NormalizePreferredAssetId(assetCatalog, request.preferredAssetId, category);
                normalized.Add(request);
            }

            spec.objects = normalized;
            NormalizeHouseTopology(spec, originalPrompt);
            RepairAnchors(spec.objects);
            spec.placements.Clear();
            return spec;
        }

        private static string BuildInstructions()
        {
            return
                "You are the semantic planner for a Unity indoor scene builder. Return one valid JSON object only. " +
                "Follow SceneSmith-style constrained planning: first identify the exact object set, then spatial relations, " +
                "anchors and functional orientation. Honor exact quantities, exclusions, room type, style, and window request. " +
                "There is no minimum furniture count. If the user requests an empty room, no furniture, 空房间, " +
                "空客厅, or 不要家具, return an empty objects array. If the user explicitly lists furniture or says " +
                "only/只/仅, do not add unrequested furniture. Otherwise you may propose a restrained object set per room. " +
                "For a brief room-only house prompt, auto-design a lived-in home with furniture and small supported objects appropriate to each room. " +
                "Never replace detailed user constraints with default templates. Interpret primary/master and secondary/guest bedroom roles, " +
                "plural room counts and each/every room quantities. For a house prompt, populate rooms with a unique id, type, realistic dimensions and put every object in a " +
                "valid roomId. A single room is represented by one rooms entry. Unity deterministically arranges adjacency " +
                "and builds door/open connections, so do not guess room center coordinates. " +
                "Use only these categories: bed, nightstand, wardrobe, sofa, coffee_table, tv_stand, desk, dining_table, chair, " +
                "bookshelf, plant, floor_lamp, rug, wall_art, ceiling_light, table_lamp, vase, books, decor_bowl, toilet, sink_vanity, shower, kitchen_counter, refrigerator, stove, computer_set, clothes_rack, fan, air_conditioner, flower_pot, laundry_basket. Create one objects entry per physical item, so two plants " +
                "must produce two plant entries with unique ids. Valid relations are against_wall, beside_bed, " +
                "in_front_of_sofa, opposite_sofa, beside_sofa, near_window, in_front_of_desk, corner, " +
                "wall_above_sofa, center, under_coffee_table, around_table, left_of, right_of, in_front_of, behind, " +
                "ceiling_center, on_top_of, on_shelf, wall_high, auto. Surface objects must use on_top_of/on_shelf with an anchorId. " +
                "Relations describe geometry, not prose. For 窗边/near a window use near_window. For 餐桌/dining table " +
                "use category dining_table, never desk. Chairs beside or around a dining table use around_table and their " +
                "anchorId must equal that dining table's id. Use anchorId for left_of, right_of, in_front_of, and behind too. " +
                "Place large anchor furniture conceptually before dependants: a dining table before its chairs, sofa before " +
                "coffee table and TV stand, bed before nightstands. Do not invent coordinates; Unity solves dimensions, " +
                "collision, wall openings, clearance, circulation and facing from these constraints. " +
                "The optional preferredAssetId is an exact catalog lock, not a style guess. Use it only when the user " +
                "explicitly requests a shape, catalog id, Prefab or FBX source name. Exact mappings include: " +
                "圆桌/圆形餐桌/tableRound=dining_table_round; 方形餐桌/tableCross=dining_table_charcoal; " +
                "长方形餐桌/table=dining_table_oak; 方形茶几/tableCoffeeSquare=coffee_table_square; " +
                "tableCoffee=coffee_table_oak; tableCoffeeGlass=coffee_table_charcoal; " +
                "deskCorner=desk_charcoal; chairRounded=chair_blue; chairCushion=chair_sage; " +
                "chairModernCushion=chair_cream. Never infer color from these legacy catalog ids; source Prefab materials " +
                "are separate from model identity. " +
                "Wall art and air_conditioner use placement wall, ceiling_light uses ceiling, table_lamp/vase/books/decor_bowl/computer_set/flower_pot use surface, " +
                "and all others use floor. Room dimensions are meters and should be realistic. The required JSON shape is: " +
                "{\"prompt\":\"string\",\"roomType\":\"house|living_room|bedroom|study\",\"style\":\"string\"," +
                "\"room\":{\"id\":\"living_01\",\"type\":\"living_room\",\"width\":6.0,\"depth\":5.0,\"height\":2.8,\"hasWindow\":true}," +
                "\"rooms\":[{\"id\":\"living_01\",\"type\":\"living_room\",\"width\":6.0,\"depth\":5.0,\"height\":2.8,\"hasWindow\":true}]," +
                "\"connections\":[],\"objects\":[{\"id\":\"sofa_01\",\"roomId\":\"living_01\",\"category\":\"sofa\",\"description\":\"string\"," +
                "\"placement\":\"floor\",\"relation\":\"against_wall\",\"anchorId\":\"\"," +
                "\"preferredAssetId\":\"\",\"required\":true}]}";
        }

        private static string NormalizePreferredAssetId(AssetCatalog catalog, string assetId, string category)
        {
            if (string.IsNullOrWhiteSpace(assetId)) return string.Empty;
            AssetDefinition definition = catalog.FindById(assetId.Trim());
            return definition != null && string.Equals(definition.Category, category, StringComparison.OrdinalIgnoreCase)
                ? definition.Id
                : string.Empty;
        }

        private static string NormalizePlacement(string category)
        {
            if (category == "wall_art" || category == "air_conditioner") return "wall";
            if (category == "ceiling_light") return "ceiling";
            if (category == "table_lamp" || category == "vase" || category == "books" || category == "decor_bowl" ||
                category == "computer_set" || category == "flower_pot")
                return "surface";
            return "floor";
        }

        private static void NormalizeHouseTopology(SceneSpec spec, string prompt)
        {
            if (spec.rooms == null || spec.rooms.Count <= 1)
            {
                SceneSpecDefaults.EnsureHouse(spec);
            }
            else
            {
                if (spec.rooms.Count > 6) spec.rooms.RemoveRange(6, spec.rooms.Count - 6);
                for (int i = 0; i < spec.rooms.Count; i++)
                {
                    RoomSpec room = spec.rooms[i];
                    room.id = string.IsNullOrWhiteSpace(room.id) ? "room_" + (i + 1).ToString("00") : room.id.Trim();
                    room.type = NormalizeRoomType(room.type);
                    RoomGeometry.Normalize(room);
                }
                HousePromptPlanner.ConnectRooms(spec,
                    (prompt ?? string.Empty).IndexOf("开放", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (prompt ?? string.Empty).IndexOf("open plan", StringComparison.OrdinalIgnoreCase) >= 0);
                spec.room = spec.rooms[0];
                spec.roomType = "house";
            }

            HashSet<string> roomIds = new HashSet<string>();
            for (int i = 0; i < spec.rooms.Count; i++) roomIds.Add(spec.rooms[i].id);
            for (int i = 0; i < spec.objects.Count; i++)
                if (string.IsNullOrWhiteSpace(spec.objects[i].roomId) || !roomIds.Contains(spec.objects[i].roomId))
                    spec.objects[i].roomId = spec.room.id;
        }

        private static string NormalizeCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return string.Empty;
            string normalized = category.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            switch (normalized)
            {
                case "diningtable":
                case "dinner_table":
                case "table":
                    return "dining_table";
                case "couch":
                    return "sofa";
                case "coffee_table_table":
                    return "coffee_table";
                default:
                    return normalized;
            }
        }

        private static string NormalizeRelation(string relation)
        {
            if (string.IsNullOrWhiteSpace(relation)) return "auto";
            string normalized = relation.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            switch (normalized)
            {
                case "beside_table":
                case "next_to_table":
                case "around_dining_table":
                    return "around_table";
                case "by_window":
                case "beside_window":
                    return "near_window";
                default:
                    return normalized;
            }
        }

        private static void RepairAnchors(List<SceneObjectRequest> objects)
        {
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < objects.Count; i++) ids.Add(objects[i].id);

            for (int i = 0; i < objects.Count; i++)
            {
                SceneObjectRequest request = objects[i];
                if (!string.IsNullOrWhiteSpace(request.anchorId) && ids.Contains(request.anchorId) &&
                    IsCompatibleAnchor(objects, request)) continue;
                request.anchorId = string.Empty;
                switch (request.relation)
                {
                    case "around_table": request.anchorId = FindFirstRequestId(objects, "dining_table", request.roomId); break;
                    case "in_front_of_desk": request.anchorId = FindFirstRequestId(objects, "desk", request.roomId); break;
                    case "in_front_of_sofa":
                    case "beside_sofa": request.anchorId = FindFirstRequestId(objects, "sofa", request.roomId); break;
                    case "beside_bed": request.anchorId = FindFirstRequestId(objects, "bed", request.roomId); break;
                    case "under_coffee_table": request.anchorId = FindFirstRequestId(objects, "coffee_table", request.roomId); break;
                    case "on_top_of":
                    case "on_shelf":
                        request.anchorId = FindFirstRequestId(objects, "coffee_table", request.roomId);
                        if (string.IsNullOrWhiteSpace(request.anchorId)) request.anchorId = FindFirstRequestId(objects, "dining_table", request.roomId);
                        if (string.IsNullOrWhiteSpace(request.anchorId)) request.anchorId = FindFirstRequestId(objects, "desk", request.roomId);
                        if (string.IsNullOrWhiteSpace(request.anchorId)) request.anchorId = FindFirstRequestId(objects, "nightstand", request.roomId);
                        break;
                }
            }
        }

        private static bool IsCompatibleAnchor(List<SceneObjectRequest> objects, SceneObjectRequest request)
        {
            string anchorCategory = string.Empty;
            string anchorRoomId = string.Empty;
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].id != request.anchorId) continue;
                anchorCategory = objects[i].category;
                anchorRoomId = objects[i].roomId;
                break;
            }

            if (!string.Equals(anchorRoomId, request.roomId, StringComparison.Ordinal)) return false;

            switch (request.relation)
            {
                case "around_table": return anchorCategory == "dining_table" || anchorCategory == "desk";
                case "in_front_of_desk": return anchorCategory == "desk";
                case "in_front_of_sofa":
                case "beside_sofa": return anchorCategory == "sofa";
                case "beside_bed": return anchorCategory == "bed";
                case "under_coffee_table": return anchorCategory == "coffee_table";
                case "on_top_of":
                case "on_shelf":
                    return anchorCategory == "coffee_table" || anchorCategory == "dining_table" ||
                        anchorCategory == "desk" || anchorCategory == "nightstand" || anchorCategory == "bookshelf";
                default: return true;
            }
        }

        private static string FindFirstRequestId(List<SceneObjectRequest> objects, string category, string roomId)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].category == category && objects[i].roomId == roomId) return objects[i].id;
            }

            return string.Empty;
        }

        private static string NormalizeRoomType(string roomType)
        {
            if (string.IsNullOrWhiteSpace(roomType)) return "living_room";
            string normalized = roomType.Trim().ToLowerInvariant();
            if (normalized == "house" || normalized == "living_room" || normalized == "bedroom" || normalized == "study" ||
                normalized == "dining_room" || normalized == "kitchen" || normalized == "bathroom") return normalized;
            if (normalized.Contains("bed")) return "bedroom";
            if (normalized.Contains("study") || normalized.Contains("office")) return "study";
            return "living_room";
        }

        private static string StripCodeFence(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            string trimmed = value.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
            int firstLineEnd = trimmed.IndexOf('\n');
            int closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineEnd < 0 || closingFence <= firstLineEnd) return trimmed;
            return trimmed.Substring(firstLineEnd + 1, closingFence - firstLineEnd - 1).Trim();
        }
    }
}
