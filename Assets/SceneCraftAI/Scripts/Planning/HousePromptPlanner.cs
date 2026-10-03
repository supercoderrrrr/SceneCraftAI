using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SceneCraftAI.Domain;
using UnityEngine;

namespace SceneCraftAI.Planning
{
    /// <summary>
    /// Build room programs and shared openings from a house prompt
    /// </summary>
    public static class HousePromptPlanner
    {
        private const float DoorHeight = 2.1f;

        public static bool ExpandIfRequested(SceneSpec spec, string normalizedPrompt)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            string prompt = (normalizedPrompt ?? string.Empty).ToLowerInvariant();
            List<string> requestedTypes = ReadRoomTypes(prompt);
            if (requestedTypes.Count == 0 && WantsHome(prompt))
            {
                requestedTypes.Clear();
                requestedTypes.Add("living_room");
                requestedTypes.Add("master_bedroom");
                requestedTypes.Add("secondary_bedroom");
                requestedTypes.Add("kitchen");
                requestedTypes.Add("bathroom");
            }
            if (requestedTypes.Count <= 1) return false;

            spec.rooms.Clear();
            spec.connections.Clear();
            spec.objects.Clear();
            for (int i = 0; i < requestedTypes.Count; i++)
            {
                RoomSpec room = CreateRoom(requestedTypes[i], i + 1);
                spec.rooms.Add(room);
                AddRoomFurniture(spec, room);
            }

            spec.room = spec.rooms[0];
            spec.roomType = "house";
            ConnectRooms(spec,
                ContainsAny(prompt, "开放式", "开放连接", "open plan", "open-plan", "without wall"));
            return true;
        }

        public static bool WantsHome(string prompt)
        {
            string value = (prompt ?? string.Empty).ToLowerInvariant();
            return ContainsAny(value, "住宅", "居室", "整套", "户型", "公寓", "房子", "家居", "全屋", "house", "apartment", "home layout");
        }

        public static bool IsHousePrompt(string prompt)
        {
            string value = (prompt ?? string.Empty).ToLowerInvariant();
            return WantsHome(value) || ReadRoomTypes(value).Count > 1;
        }

        public static void ConnectRooms(SceneSpec spec, bool openPlan)
        {
            if (spec == null || spec.rooms == null || spec.rooms.Count == 0) return;
            spec.connections.Clear();
            for (int i = 0; i < spec.rooms.Count; i++)
            {
                RoomSpec room = spec.rooms[i];
                RoomGeometry.Normalize(room);
                room.openings.Clear();
                if (i == 0) room.center = Vector3.zero;
            }
            if (TryArrangeHome(spec, openPlan))
            {
                AddExteriorOpenings(spec);
                return;
            }
            for (int i = 1; i < spec.rooms.Count; i++)
            {
                string parentWall;
                RoomSpec child = spec.rooms[i];
                RoomSpec parent;
                List<RoomSpec> placed = spec.rooms.GetRange(0, i);
                PlaceFree(child, placed, out parent, out parentWall);
                string childWall = OppositeWall(parentWall);
                string connectionType = openPlan && i == 1 ? "open" : "door";
                float sharedLength = SharedWallLength(parent, child, parentWall);
                RoomConnectionSpec connection = new RoomConnectionSpec
                {
                    id = "connection_" + i.ToString("00"),
                    roomAId = parent.id,
                    roomBId = child.id,
                    type = connectionType,
                    width = ConnectionWidth(sharedLength, connectionType)
                };
                spec.connections.Add(connection);
                AddConnectionOpening(parent, child, connection, parentWall);
                AddConnectionOpening(child, parent, connection, childWall);
            }

            AddExteriorOpenings(spec);
        }

        private static bool TryArrangeHome(SceneSpec spec, bool openPlan)
        {
            RoomSpec living = FindFirstRoom(spec, "living_room");
            RoomSpec kitchen = FindFirstRoom(spec, "kitchen");
            RoomSpec bathroom = FindFirstRoom(spec, "bathroom");
            List<RoomSpec> bedrooms = FindRooms(spec, "bedroom");
            if (living == null || kitchen == null || bathroom == null || bedrooms.Count < 2) return false;

            RoomSpec master = bedrooms[0];
            RoomSpec secondary = bedrooms[1];
            living.center = Vector3.zero;
            kitchen.center = new Vector3(-(living.width + kitchen.width) * 0.5f, 0f, -0.75f);
            master.center = new Vector3(-0.85f, 0f, (living.depth + master.depth) * 0.5f);
            secondary.center = new Vector3((living.width + secondary.width) * 0.5f, 0f, -0.35f);
            bathroom.center = new Vector3(
                secondary.center.x + (secondary.width - bathroom.width) * 0.18f,
                0f,
                secondary.center.z + (secondary.depth + bathroom.depth) * 0.5f);

            int connectionIndex = 1;
            Connect(spec, living, kitchen, "left", openPlan ? "open" : "door", connectionIndex++);
            Connect(spec, living, master, "back", "door", connectionIndex++);
            Connect(spec, living, secondary, "right", "door", connectionIndex++);
            Connect(spec, secondary, bathroom, "back", "door", connectionIndex++);

            for (int i = 0; i < spec.rooms.Count; i++)
            {
                RoomSpec room = spec.rooms[i];
                if (room == living || room == kitchen || room == master || room == secondary || room == bathroom) continue;
                List<RoomSpec> placed = new List<RoomSpec> { living, kitchen, master, secondary, bathroom };
                for (int j = 0; j < i; j++) if (!placed.Contains(spec.rooms[j])) placed.Add(spec.rooms[j]);
                RoomSpec parent;
                string wall;
                PlaceFree(room, placed, out parent, out wall);
                Connect(spec, parent, room, wall, "door", connectionIndex++);
            }
            return true;
        }

        private static void Connect(
            SceneSpec spec, RoomSpec owner, RoomSpec target, string ownerWall, string type, int index)
        {
            float sharedLength = SharedWallLength(owner, target, ownerWall);
            RoomConnectionSpec connection = new RoomConnectionSpec
            {
                id = "connection_" + index.ToString("00"),
                roomAId = owner.id,
                roomBId = target.id,
                type = type,
                width = ConnectionWidth(sharedLength, type)
            };
            spec.connections.Add(connection);
            AddConnectionOpening(owner, target, connection, ownerWall);
            AddConnectionOpening(target, owner, connection, OppositeWall(ownerWall));
        }

        private static void PlaceFree(RoomSpec child, List<RoomSpec> placed, out RoomSpec parent, out string wall)
        {
            parent = null;
            wall = "back";
            float bestArea = float.PositiveInfinity;
            Vector3 bestCenter = Vector3.zero;
            string[] walls = { "right", "left", "back", "front" };
            foreach (RoomSpec candidate in placed)
            {
                foreach (string side in walls)
                {
                    PlaceAdjacent(candidate, child, side);
                    if (placed.Exists(room => RoomGeometry.Overlaps(child, room))) continue;
                    Bounds envelope = new Bounds(child.center, new Vector3(child.width, 0f, child.depth));
                    foreach (RoomSpec room in placed)
                        envelope.Encapsulate(new Bounds(room.center, new Vector3(room.width, 0f, room.depth)));
                    float area = envelope.size.x * envelope.size.z;
                    if (area >= bestArea) continue;
                    bestArea = area;
                    bestCenter = child.center;
                    parent = candidate;
                    wall = side;
                }
            }
            if (parent == null) throw new InvalidOperationException("No valid adjacent room placement was found");
            child.center = bestCenter;
        }

        private static RoomSpec FindFirstRoom(SceneSpec spec, string type)
        {
            for (int i = 0; i < spec.rooms.Count; i++) if (spec.rooms[i].type == type) return spec.rooms[i];
            return null;
        }

        private static List<RoomSpec> FindRooms(SceneSpec spec, string type)
        {
            List<RoomSpec> result = new List<RoomSpec>();
            for (int i = 0; i < spec.rooms.Count; i++) if (spec.rooms[i].type == type) result.Add(spec.rooms[i]);
            return result;
        }

        private static void PlaceAdjacent(RoomSpec parent, RoomSpec child, string parentWall)
        {
            switch (parentWall)
            {
                case "left":
                    child.center = parent.center + Vector3.left * ((parent.width + child.width) * 0.5f);
                    break;
                case "back":
                    child.center = parent.center + Vector3.forward * ((parent.depth + child.depth) * 0.5f);
                    break;
                case "front":
                    child.center = parent.center + Vector3.back * ((parent.depth + child.depth) * 0.5f);
                    break;
                default:
                    child.center = parent.center + Vector3.right * ((parent.width + child.width) * 0.5f);
                    break;
            }
        }

        private static float SharedWallLength(RoomSpec a, RoomSpec b, string wall)
        {
            bool vertical = wall == "left" || wall == "right";
            float aCenter = vertical ? a.center.z : a.center.x;
            float bCenter = vertical ? b.center.z : b.center.x;
            float aSize = vertical ? a.depth : a.width;
            float bSize = vertical ? b.depth : b.width;
            float minimum = Mathf.Max(aCenter - aSize * 0.5f, bCenter - bSize * 0.5f);
            float maximum = Mathf.Min(aCenter + aSize * 0.5f, bCenter + bSize * 0.5f);
            return Mathf.Max(0f, maximum - minimum);
        }

        private static float ConnectionWidth(float sharedLength, string type)
        {
            if (type == "open") return Mathf.Clamp(sharedLength * 0.58f, 0.9f, Mathf.Max(0.9f, sharedLength - 0.5f));
            return Mathf.Clamp(sharedLength - 0.6f, 0.72f, 0.95f);
        }

        private static string OppositeWall(string wall)
        {
            switch (wall)
            {
                case "left": return "right";
                case "back": return "front";
                case "front": return "back";
                default: return "left";
            }
        }

        private static List<string> ReadRoomTypes(string prompt)
        {
            Match compact = Regex.Match(prompt, "(?<beds>[一二两三四1-4])室(?<living>[一二两三1-3])厅");
            if (!compact.Success) return PromptRules.ReadRooms(prompt);
            List<string> result = new List<string>();
            for (int i = 0; i < ParseCount(compact.Groups["living"].Value); i++) result.Add("living_room");
            for (int i = 0; i < ParseCount(compact.Groups["beds"].Value); i++)
                result.Add(i == 0 ? "master_bedroom" : "secondary_bedroom");
            return result;
        }

        private static RoomSpec CreateRoom(string type, int index)
        {
            string requestedRole = type;
            string normalizedType = type == "master_bedroom" || type == "secondary_bedroom" ? "bedroom" : type;
            RoomSpec room = new RoomSpec
            {
                id = requestedRole + "_" + index.ToString("00"),
                type = normalizedType,
                description = "auto-designed role: " + requestedRole,
                height = normalizedType == "living_room" ? 3.05f : 2.85f,
                hasWindow = normalizedType != "bathroom"
            };
            switch (requestedRole)
            {
                case "master_bedroom": room.width = 6.4f; room.depth = 5.4f; break;
                case "secondary_bedroom": room.width = 5.5f; room.depth = 4.7f; break;
                case "bedroom": room.width = 5.8f; room.depth = 4.9f; break;
                case "study": room.width = 4.6f; room.depth = 4.0f; break;
                case "dining_room": room.width = 4.8f; room.depth = 4.2f; break;
                case "kitchen": room.width = 5.2f; room.depth = 4.3f; break;
                case "bathroom": room.width = 3.8f; room.depth = 3.4f; break;
                default: room.width = 7.8f; room.depth = 6.2f; break;
            }
            return room;
        }

        internal static void AddRoomFurniture(SceneSpec spec, RoomSpec room)
        {
            switch (room.type)
            {
                case "living_room":
                    Add(spec, room, "sofa", "comfortable sofa", "against_wall");
                    string sofaId = LastId(spec);
                    Add(spec, room, "coffee_table", "coffee table", "in_front_of_sofa", "floor", sofaId);
                    Add(spec, room, "tv_stand", "TV stand", "opposite_sofa", "floor", sofaId);
                    string coffeeId = FindLastId(spec, room.id, "coffee_table");
                    Add(spec, room, "rug", "living room rug", "under_coffee_table", "floor", coffeeId);
                    Add(spec, room, "plant", "corner plant", "corner");
                    Add(spec, room, "plant", "second varied corner plant", "corner");
                    Add(spec, room, "floor_lamp", "reading lamp beside sofa", "beside_sofa", "floor", sofaId);
                    Add(spec, room, "chair", "casual accent chair beside sofa", "beside_sofa", "floor", sofaId);
                    Add(spec, room, "bookshelf", "living room display shelf", "against_wall");
                    Add(spec, room, "ceiling_light", "central ceiling light", "ceiling_center", "ceiling");
                    Add(spec, room, "flower_pot", "colorful flowers on coffee table", "on_top_of", "surface", coffeeId);
                    Add(spec, room, "decor_bowl", "small living tray on coffee table", "on_top_of", "surface", coffeeId);
                    Add(spec, room, "wall_art", "large living room artwork", "wall_above_sofa", "wall", sofaId);
                    break;
                case "bedroom":
                    bool masterBedroom = room.description.Contains("master_bedroom") || room.id.Contains("master_bedroom");
                    Add(spec, room, "bed", masterBedroom ? "warm double bed" : "compact single bed", "against_wall");
                    spec.objects[spec.objects.Count - 1].preferredAssetId = masterBedroom ? "bed_oak_double" : "bed_oak_single";
                    string bedId = LastId(spec);
                    Add(spec, room, "nightstand", "left nightstand", "beside_bed", "floor", bedId);
                    string firstNightstandId = LastId(spec);
                    if (masterBedroom) Add(spec, room, "nightstand", "right nightstand", "beside_bed", "floor", bedId);
                    Add(spec, room, "wardrobe", "wardrobe", "against_wall");
                    Add(spec, room, "rug", "soft bedroom rug", "center");
                    Add(spec, room, "ceiling_light", "central ceiling light", "ceiling_center", "ceiling");
                    Add(spec, room, "table_lamp", "lamp on nightstand", "on_top_of", "surface", firstNightstandId);
                    Add(spec, room, "clothes_rack", "open clothes rack", "against_wall");
                    Add(spec, room, "fan", "standing fan in a free corner", "corner");
                    Add(spec, room, "air_conditioner", "wall air conditioner", "wall_high", "wall");
                    Add(spec, room, "plant", "bedroom plant", "corner");
                    if (masterBedroom)
                    {
                        string secondNightstandId = FindLastId(spec, room.id, "nightstand");
                        Add(spec, room, "table_lamp", "second bedside lamp", "on_top_of", "surface", secondNightstandId);
                        Add(spec, room, "flower_pot", "fresh flowers beside bed", "on_top_of", "surface", firstNightstandId);
                    }
                    else
                    {
                        Add(spec, room, "desk", "computer desk near window", "near_window");
                        string bedroomDeskId = LastId(spec);
                        Add(spec, room, "chair", "desk chair facing computer", "in_front_of_desk", "floor", bedroomDeskId);
                        Add(spec, room, "computer_set", "monitor keyboard and mouse", "on_top_of", "surface", bedroomDeskId);
                        Add(spec, room, "books", "study books on desk", "on_top_of", "surface", bedroomDeskId);
                        Add(spec, room, "bookshelf", "book collection", "against_wall");
                    }
                    break;
                case "study":
                    Add(spec, room, "desk", "desk near window", "near_window");
                    string deskId = LastId(spec);
                    Add(spec, room, "chair", "desk chair", "in_front_of_desk", "floor", deskId);
                    Add(spec, room, "bookshelf", "bookshelf", "against_wall");
                    Add(spec, room, "ceiling_light", "central ceiling light", "ceiling_center", "ceiling");
                    Add(spec, room, "books", "books on desk", "on_top_of", "surface", deskId);
                    break;
                case "dining_room":
                    Add(spec, room, "dining_table", "dining table", "center");
                    string tableId = LastId(spec);
                    for (int i = 0; i < 4; i++) Add(spec, room, "chair", "dining chair", "around_table", "floor", tableId);
                    Add(spec, room, "ceiling_light", "central ceiling light", "ceiling_center", "ceiling");
                    Add(spec, room, "decor_bowl", "decorative bowl on table", "on_top_of", "surface", tableId);
                    break;
                case "kitchen":
                    Add(spec, room, "kitchen_counter", "main kitchen counter", "against_wall");
                    string counterId = LastId(spec);
                    Add(spec, room, "kitchen_counter", "secondary preparation counter", "against_wall");
                    Add(spec, room, "refrigerator", "family refrigerator", "against_wall");
                    Add(spec, room, "stove", "cooking stove", "against_wall");
                    Add(spec, room, "dining_table", "compact breakfast table near window", "near_window");
                    string breakfastTableId = LastId(spec);
                    Add(spec, room, "chair", "breakfast chair", "around_table", "floor", breakfastTableId);
                    Add(spec, room, "chair", "second breakfast chair", "around_table", "floor", breakfastTableId);
                    Add(spec, room, "flower_pot", "small herb pot on counter", "on_top_of", "surface", counterId);
                    Add(spec, room, "ceiling_light", "kitchen ceiling light", "ceiling_center", "ceiling");
                    break;
                case "bathroom":
                    Add(spec, room, "toilet", "porcelain toilet against wall", "against_wall");
                    Add(spec, room, "sink_vanity", "wash basin vanity against wall", "against_wall");
                    Add(spec, room, "shower", "glass shower in corner", "corner");
                    Add(spec, room, "laundry_basket", "woven laundry basket", "corner");
                    Add(spec, room, "flower_pot", "small bathroom flower pot", "on_top_of", "surface", FindLastId(spec, room.id, "sink_vanity"));
                    Add(spec, room, "ceiling_light", "bathroom ceiling light", "ceiling_center", "ceiling");
                    break;
            }
        }

        private static void Add(
            SceneSpec spec, RoomSpec room, string category, string description, string relation,
            string placement = "floor", string anchorId = "")
        {
            int ordinal = spec.objects.Count + 1;
            spec.objects.Add(new SceneObjectRequest
            {
                id = room.id + "_" + category + "_" + ordinal.ToString("00"),
                roomId = room.id,
                category = category,
                description = description,
                relation = relation,
                placement = placement,
                anchorId = anchorId
            });
        }

        private static string LastId(SceneSpec spec)
        {
            return spec.objects.Count == 0 ? string.Empty : spec.objects[spec.objects.Count - 1].id;
        }

        private static string FindLastId(SceneSpec spec, string roomId, string category)
        {
            for (int i = spec.objects.Count - 1; i >= 0; i--)
                if (spec.objects[i].roomId == roomId && spec.objects[i].category == category) return spec.objects[i].id;
            return string.Empty;
        }

        private static void AddConnectionOpening(
            RoomSpec owner, RoomSpec target, RoomConnectionSpec connection, string wall)
        {
            bool verticalWall = wall == "left" || wall == "right";
            float ownerMinimum = verticalWall
                ? owner.center.z - owner.depth * 0.5f
                : owner.center.x - owner.width * 0.5f;
            float ownerMaximum = verticalWall
                ? owner.center.z + owner.depth * 0.5f
                : owner.center.x + owner.width * 0.5f;
            float targetMinimum = verticalWall
                ? target.center.z - target.depth * 0.5f
                : target.center.x - target.width * 0.5f;
            float targetMaximum = verticalWall
                ? target.center.z + target.depth * 0.5f
                : target.center.x + target.width * 0.5f;
            float overlapMinimum = Mathf.Max(ownerMinimum, targetMinimum);
            float overlapMaximum = Mathf.Min(ownerMaximum, targetMaximum);
            float worldCenter = overlapMaximum > overlapMinimum
                ? (overlapMinimum + overlapMaximum) * 0.5f
                : ((ownerMinimum + ownerMaximum) + (targetMinimum + targetMaximum)) * 0.25f;
            float ownerAxisCenter = verticalWall ? owner.center.z : owner.center.x;
            float alongOffset = worldCenter - ownerAxisCenter;
            float wallLength = wall == "left" || wall == "right" ? owner.depth : owner.width;
            float safeHalf = Mathf.Max(0f, wallLength * 0.5f - connection.width * 0.5f - 0.2f);
            owner.openings.Add(new WallOpeningSpec
            {
                id = connection.id + "_" + owner.id,
                type = connection.type,
                wall = wall,
                center = Mathf.Clamp(alongOffset, -safeHalf, safeHalf),
                width = connection.width,
                height = connection.type == "open" ? owner.height : Mathf.Min(DoorHeight, owner.height - 0.1f),
                sillHeight = 0f,
                clearanceDepth = connection.type == "open" ? 0.8f : 1.05f,
                connectsToRoomId = target.id
            });
        }

        private static void AddExteriorOpenings(SceneSpec spec)
        {
            bool entranceAdded = false;
            for (int i = 0; i < spec.rooms.Count; i++)
            {
                RoomSpec room = spec.rooms[i];
                string exteriorWall;
                float exteriorCenter;
                if (!entranceAdded && RoomGeometry.TryExteriorSlot(room, spec.rooms, 0.95f,
                        new[] { "front", "left", "right", "back" }, out exteriorWall, out exteriorCenter))
                {
                    room.openings.Add(new WallOpeningSpec
                    {
                        id = room.id + "_entrance",
                        type = "door",
                        wall = exteriorWall,
                        center = exteriorCenter,
                        width = 0.95f,
                        height = Mathf.Min(DoorHeight, room.height - 0.1f),
                        sillHeight = 0f,
                        clearanceDepth = 1.05f
                    });
                    entranceAdded = true;
                }
                if (!room.hasWindow) continue;
                string windowWall;
                float windowCenter;
                if (!RoomGeometry.TryExteriorSlot(room, spec.rooms, 1.55f,
                        new[] { "back", "front", "right", "left" }, out windowWall, out windowCenter))
                {
                    room.hasWindow = false;
                    continue;
                }
                float wallLength = windowWall == "left" || windowWall == "right" ? room.depth : room.width;
                room.openings.Add(new WallOpeningSpec
                {
                    id = room.id + "_window_01",
                    type = "window",
                    wall = windowWall,
                    center = windowCenter,
                    width = Mathf.Min(1.55f, wallLength * 0.42f),
                    height = 1.15f,
                    sillHeight = 0.9f,
                    clearanceDepth = 0.55f
                });
            }
        }

        private static string FindUnusedWall(RoomSpec room)
        {
            string[] candidates = { "back", "front", "right", "left" };
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                bool occupied = false;
                for (int openingIndex = 0; openingIndex < room.openings.Count; openingIndex++)
                    if (room.openings[openingIndex].wall == candidates[candidateIndex]) { occupied = true; break; }
                if (!occupied) return candidates[candidateIndex];
            }
            return string.Empty;
        }

        private static int ParseCount(string token)
        {
            int numeric;
            if (int.TryParse(token, out numeric)) return numeric;
            switch (token)
            {
                case "一": return 1;
                case "二": case "两": return 2;
                case "三": return 3;
                case "四": return 4;
                default: return 1;
            }
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++) if (value.Contains(terms[i])) return true;
            return false;
        }
    }
}
