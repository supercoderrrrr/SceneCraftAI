using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneCraftAI.Domain
{
    [Serializable]
    public sealed class SceneSpec
    {
        public string prompt;
        public string roomType = "living_room";
        public string style = "warm modern";
        public RoomSpec room = new RoomSpec();
        public List<RoomSpec> rooms = new List<RoomSpec>();
        public List<RoomConnectionSpec> connections = new List<RoomConnectionSpec>();
        public List<SceneObjectRequest> objects = new List<SceneObjectRequest>();
        public List<PlacedObjectSpec> placements = new List<PlacedObjectSpec>();
    }

    [Serializable]
    public sealed class RoomSpec
    {
        public string id = "room_01";
        public string type = "living_room";
        public string description = "";
        public Vector3 center = Vector3.zero;
        public float width = 6f;
        public float depth = 5f;
        public float height = 2.8f;
        public bool hasWindow = true;
        public List<WallOpeningSpec> openings = new List<WallOpeningSpec>();
    }

    [Serializable]
    public sealed class WallOpeningSpec
    {
        public string id;
        public string type;
        public string wall;
        public float center;
        public float width;
        public float height;
        public float sillHeight;
        public float clearanceDepth;
        public string connectsToRoomId;
    }

    [Serializable]
    public sealed class RoomConnectionSpec
    {
        public string id;
        public string roomAId;
        public string roomBId;
        public string type = "door";
        public float width = 0.95f;
    }

    [Serializable]
    public sealed class SceneObjectRequest
    {
        public string id;
        public string category;
        public string description;
        public string preferredAssetId;
        public string roomId;
        public string placement = "floor";
        public string relation = "auto";
        public string anchorId;
        public bool required = true;
        public bool locked;
        public Vector3 lockedPosition;
        public float lockedRotationY;
    }

    [Serializable]
    public sealed class PlacedObjectSpec
    {
        public string id;
        public string assetId;
        public string category;
        public string roomId;
        public Vector3 position;
        public float rotationY;
        public Vector3 size;
        public string rationale;
        public string relation;
        public string anchorId;
        public bool locked;

        public Bounds Bounds
        {
            get
            {
                float radians = rotationY * Mathf.Deg2Rad;
                float cosine = Mathf.Abs(Mathf.Cos(radians));
                float sine = Mathf.Abs(Mathf.Sin(radians));
                Vector3 rotatedSize = new Vector3(
                    size.x * cosine + size.z * sine,
                    size.y,
                    size.x * sine + size.z * cosine);
                return new Bounds(position + Vector3.up * size.y * 0.5f, rotatedSize);
            }
        }
    }

    public static class SceneSpecJson
    {
        public static string ToJson(SceneSpec spec, bool pretty = true)
        {
            return JsonUtility.ToJson(spec, pretty);
        }

        public static SceneSpec FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Scene JSON is empty.", nameof(json));
            }

            SceneSpec spec = JsonUtility.FromJson<SceneSpec>(json);
            if (spec == null || spec.room == null)
            {
                throw new InvalidOperationException("Scene JSON does not contain a valid room.");
            }

            spec.objects = spec.objects ?? new List<SceneObjectRequest>();
            spec.placements = spec.placements ?? new List<PlacedObjectSpec>();
            spec.rooms = spec.rooms ?? new List<RoomSpec>();
            spec.connections = spec.connections ?? new List<RoomConnectionSpec>();
            spec.room.openings = spec.room.openings ?? new List<WallOpeningSpec>();
            SceneSpecDefaults.EnsureHouse(spec);
            SceneSpecDefaults.EnsureOpenings(spec);
            return spec;
        }
    }

    public static class SceneSpecDefaults
    {
        public static void EnsureHouse(SceneSpec spec)
        {
            if (spec == null) return;
            if (spec.room == null) spec.room = new RoomSpec();
            if (spec.rooms == null) spec.rooms = new List<RoomSpec>();
            if (spec.connections == null) spec.connections = new List<RoomConnectionSpec>();
            if (spec.rooms.Count == 0) spec.rooms.Add(spec.room);
            for (int i = 0; i < spec.rooms.Count; i++)
            {
                RoomSpec room = spec.rooms[i] ?? new RoomSpec();
                spec.rooms[i] = room;
                if (string.IsNullOrWhiteSpace(room.id)) room.id = "room_" + (i + 1).ToString("00");
                if (string.IsNullOrWhiteSpace(room.type)) room.type = i == 0 ? spec.roomType : "room";
                if (room.openings == null) room.openings = new List<WallOpeningSpec>();
            }
            spec.room = spec.rooms[0];
            if (string.IsNullOrWhiteSpace(spec.roomType)) spec.roomType = spec.room.type;

            for (int i = 0; i < spec.objects.Count; i++)
                if (string.IsNullOrWhiteSpace(spec.objects[i].roomId)) spec.objects[i].roomId = spec.room.id;
            for (int i = 0; i < spec.placements.Count; i++)
                if (string.IsNullOrWhiteSpace(spec.placements[i].roomId)) spec.placements[i].roomId = spec.room.id;
        }

        public static void EnsureOpenings(SceneSpec spec)
        {
            if (spec == null) return;
            EnsureHouse(spec);
            for (int i = 0; i < spec.rooms.Count; i++) EnsureRoomOpenings(spec.rooms[i], spec.rooms.Count == 1);
        }

        public static void EnsureRoomOpenings(RoomSpec room, bool includeExteriorDoor)
        {
            if (room == null) return;
            if (room.openings == null) room.openings = new List<WallOpeningSpec>();
            bool hasExteriorDoor = false;
            bool hasWindowOpening = false;
            for (int i = 0; i < room.openings.Count; i++)
            {
                WallOpeningSpec opening = room.openings[i];
                if (opening.type == "window") hasWindowOpening = true;
                if (opening.type == "door" && string.IsNullOrWhiteSpace(opening.connectsToRoomId)) hasExteriorDoor = true;
            }

            if (includeExteriorDoor && !hasExteriorDoor) room.openings.Add(new WallOpeningSpec
            {
                id = room.id + "_entrance",
                type = "door",
                wall = "front",
                center = -room.width * 0.28f,
                width = 0.95f,
                height = Mathf.Min(2.15f, room.height - 0.15f),
                sillHeight = 0f,
                clearanceDepth = 1.05f
            });

            if (room.hasWindow && !hasWindowOpening)
            {
                room.openings.Add(new WallOpeningSpec
                {
                    id = room.id + "_window_01",
                    type = "window",
                    wall = "right",
                    center = 0.35f,
                    width = 1.55f,
                    height = 1.15f,
                    sillHeight = 0.9f,
                    clearanceDepth = 0.55f
                });
            }

            SanitizeRoomOpenings(room);
        }

        public static void SanitizeRoomOpenings(RoomSpec room)
        {
            if (room == null || room.openings == null) return;
            string[] walls = { "front", "back", "left", "right" };
            for (int wallIndex = 0; wallIndex < walls.Length; wallIndex++)
            {
                string wall = walls[wallIndex];
                float length = wall == "left" || wall == "right" ? room.depth : room.width;
                List<WallOpeningSpec> wallOpenings = new List<WallOpeningSpec>();
                for (int i = 0; i < room.openings.Count; i++)
                    if (room.openings[i] != null && room.openings[i].wall == wall)
                        wallOpenings.Add(room.openings[i]);
                wallOpenings.Sort(CompareOpeningPriority);

                List<Vector2> occupied = new List<Vector2>();
                for (int i = 0; i < wallOpenings.Count; i++)
                {
                    WallOpeningSpec opening = wallOpenings[i];
                    opening.width = Mathf.Clamp(opening.width, 0.25f, Mathf.Max(0.25f, length - 0.28f));
                    opening.height = Mathf.Clamp(opening.height, 0.25f, room.height);
                    opening.sillHeight = Mathf.Clamp(opening.sillHeight, 0f, Mathf.Max(0f, room.height - opening.height));
                    float placedCenter;
                    if (TryFindNonOverlappingCenter(opening.center, opening.width, length, occupied, out placedCenter))
                    {
                        opening.center = placedCenter;
                        occupied.Add(new Vector2(
                            placedCenter - opening.width * 0.5f,
                            placedCenter + opening.width * 0.5f));
                    }
                    else if (opening.type == "window")
                    {
                        room.openings.Remove(opening);
                    }
                }
            }

            room.hasWindow = false;
            for (int i = 0; i < room.openings.Count; i++)
                if (room.openings[i] != null && room.openings[i].type == "window")
                {
                    room.hasWindow = true;
                    break;
                }
        }

        private static int CompareOpeningPriority(WallOpeningSpec a, WallOpeningSpec b)
        {
            int aPriority = a.type == "door" || a.type == "open" ? 0 : 1;
            int bPriority = b.type == "door" || b.type == "open" ? 0 : 1;
            if (aPriority != bPriority) return aPriority.CompareTo(bPriority);
            return a.center.CompareTo(b.center);
        }

        private static bool TryFindNonOverlappingCenter(
            float requestedCenter, float width, float wallLength, List<Vector2> occupied, out float result)
        {
            const float edgeMargin = 0.14f;
            const float openingGap = 0.14f;
            float minimumCenter = -wallLength * 0.5f + edgeMargin + width * 0.5f;
            float maximumCenter = wallLength * 0.5f - edgeMargin - width * 0.5f;
            if (minimumCenter > maximumCenter)
            {
                result = 0f;
                return false;
            }

            List<float> candidates = new List<float>
            {
                Mathf.Clamp(requestedCenter, minimumCenter, maximumCenter),
                minimumCenter,
                maximumCenter
            };
            for (int i = 0; i < occupied.Count; i++)
            {
                candidates.Add(occupied[i].x - openingGap - width * 0.5f);
                candidates.Add(occupied[i].y + openingGap + width * 0.5f);
            }

            bool found = false;
            float bestDistance = float.MaxValue;
            result = 0f;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                float center = candidates[candidateIndex];
                if (center < minimumCenter - 0.001f || center > maximumCenter + 0.001f) continue;
                float left = center - width * 0.5f;
                float right = center + width * 0.5f;
                bool overlaps = false;
                for (int occupiedIndex = 0; occupiedIndex < occupied.Count; occupiedIndex++)
                    if (right + openingGap > occupied[occupiedIndex].x &&
                        left - openingGap < occupied[occupiedIndex].y)
                    {
                        overlaps = true;
                        break;
                    }
                if (overlaps) continue;
                float distance = Mathf.Abs(center - requestedCenter);
                if (found && distance >= bestDistance) continue;
                found = true;
                bestDistance = distance;
                result = center;
            }
            return found;
        }
    }
}
