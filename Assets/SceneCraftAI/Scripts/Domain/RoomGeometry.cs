using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneCraftAI.Domain
{
    public static class RoomGeometry
    {
        public const float WallGap = 0.025f;
        public const float Tolerance = 0.01f;

        public static bool Normalize(RoomSpec room)
        {
            float width = SafeDimension(room.width, 2.8f, 9f, 6f);
            float depth = SafeDimension(room.depth, 2.8f, 9f, 5f);
            float height = SafeDimension(room.height, 2.4f, 4f, 2.8f);
            bool changed = width != room.width || depth != room.depth || height != room.height;
            room.width = width;
            room.depth = depth;
            room.height = height;
            return changed;
        }

        private static float SafeDimension(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }

        public static float Plane(RoomSpec room, string wall)
        {
            switch (wall)
            {
                case "left": return room.center.x - room.width * 0.5f;
                case "right": return room.center.x + room.width * 0.5f;
                case "front": return room.center.z - room.depth * 0.5f;
                default: return room.center.z + room.depth * 0.5f;
            }
        }

        public static string Opposite(string wall)
        {
            switch (wall)
            {
                case "left": return "right";
                case "right": return "left";
                case "front": return "back";
                default: return "front";
            }
        }

        public static float Along(RoomSpec room, string wall)
        {
            return wall == "left" || wall == "right" ? room.center.z : room.center.x;
        }

        public static bool TrySharedRange(RoomSpec room, RoomSpec target, string wall, out float start, out float end)
        {
            bool vertical = wall == "left" || wall == "right";
            float a = Along(room, wall);
            float b = Along(target, wall);
            float aSize = vertical ? room.depth : room.width;
            float bSize = vertical ? target.depth : target.width;
            start = Mathf.Max(a - aSize * 0.5f, b - bSize * 0.5f);
            end = Mathf.Min(a + aSize * 0.5f, b + bSize * 0.5f);
            return Mathf.Abs(Plane(room, wall) - Plane(target, Opposite(wall))) <= Tolerance && end - start > Tolerance;
        }

        public static bool Overlaps(RoomSpec a, RoomSpec b)
        {
            return Mathf.Abs(a.center.x - b.center.x) < (a.width + b.width) * 0.5f - Tolerance &&
                Mathf.Abs(a.center.z - b.center.z) < (a.depth + b.depth) * 0.5f - Tolerance;
        }

        public static bool TryExteriorSlot(RoomSpec room, IReadOnlyList<RoomSpec> rooms, float width,
            string[] walls, out string wall, out float center)
        {
            wall = string.Empty;
            center = 0f;
            foreach (string side in walls)
            {
                float origin = Along(room, side);
                float length = side == "left" || side == "right" ? room.depth : room.width;
                List<Vector2> ranges = new List<Vector2> { new Vector2(-length * 0.5f + 0.2f, length * 0.5f - 0.2f) };
                foreach (RoomSpec target in rooms)
                {
                    float start, end;
                    if (target == room || !TrySharedRange(room, target, side, out start, out end)) continue;
                    Subtract(ranges, start - origin - 0.2f, end - origin + 0.2f);
                }
                foreach (WallOpeningSpec opening in room.openings)
                    if (opening.wall == side) Subtract(ranges, opening.center - opening.width * 0.5f - 0.2f,
                        opening.center + opening.width * 0.5f + 0.2f);
                foreach (Vector2 range in ranges)
                {
                    if (range.y - range.x < width) continue;
                    wall = side;
                    center = Mathf.Clamp(0f, range.x + width * 0.5f, range.y - width * 0.5f);
                    return true;
                }
            }
            return false;
        }

        private static void Subtract(List<Vector2> ranges, float start, float end)
        {
            for (int i = ranges.Count - 1; i >= 0; i--)
            {
                Vector2 range = ranges[i];
                if (end <= range.x || start >= range.y) continue;
                ranges.RemoveAt(i);
                if (start > range.x) ranges.Insert(i, new Vector2(range.x, start));
                if (end < range.y) ranges.Insert(i, new Vector2(end, range.y));
            }
        }

        public static bool HasPassage(RoomSpec a, RoomSpec b, RoomConnectionSpec connection)
        {
            if (a == null || b == null || Overlaps(a, b)) return false;
            foreach (WallOpeningSpec opening in a.openings)
            {
                if (opening.connectsToRoomId != b.id || opening.type != connection.type || opening.sillHeight > Tolerance) continue;
                float start, end;
                if (!TrySharedRange(a, b, opening.wall, out start, out end)) continue;
                float center = Along(a, opening.wall) + opening.center;
                if (opening.width < 0.65f || opening.height < 1.8f ||
                    center - opening.width * 0.5f < start + 0.09f || center + opening.width * 0.5f > end - 0.09f) continue;
                foreach (WallOpeningSpec paired in b.openings)
                {
                    if (paired.connectsToRoomId != a.id || paired.wall != Opposite(opening.wall) ||
                        paired.type != opening.type || paired.sillHeight > Tolerance || paired.height < 1.8f) continue;
                    if (Mathf.Abs(Along(b, paired.wall) + paired.center - center) <= Tolerance &&
                        Mathf.Abs(paired.width - opening.width) <= Tolerance &&
                        Mathf.Abs(connection.width - opening.width) <= Tolerance) return true;
                }
            }
            return false;
        }
    }
}
