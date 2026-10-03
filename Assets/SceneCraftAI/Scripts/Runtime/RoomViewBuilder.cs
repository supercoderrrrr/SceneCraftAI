using System.Collections.Generic;
using SceneCraftAI.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace SceneCraftAI.Runtime
{
    public sealed class RoomViewBuilder
    {
        private struct WallRange
        {
            public float Start;
            public float End;

            public WallRange(float start, float end)
            {
                Start = start;
                End = end;
            }
        }

        private readonly Material floorMaterial;
        private readonly Material bedroomFloorMaterial;
        private readonly Material kitchenFloorMaterial;
        private readonly Material bathroomFloorMaterial;
        private readonly Material wallMaterial;
        private readonly Material windowGlassMaterial;
        private readonly Material windowFrameMaterial;
        private readonly Material doorMaterial;
        private readonly Material doorFrameMaterial;
        private readonly Material doorHandleMaterial;

        public RoomViewBuilder()
        {
            floorMaterial = CreateMaterial(new Color(0.38f, 0.24f, 0.14f), 0.18f);
            bedroomFloorMaterial = CreateMaterial(new Color(0.48f, 0.32f, 0.21f), 0.16f);
            kitchenFloorMaterial = CreateMaterial(new Color(0.55f, 0.50f, 0.42f), 0.24f);
            bathroomFloorMaterial = CreateMaterial(new Color(0.62f, 0.68f, 0.70f), 0.38f);
            wallMaterial = CreateMaterial(new Color(0.88f, 0.88f, 0.84f), 0.05f);
            windowGlassMaterial = CreateGlassMaterial(new Color(0.58f, 0.82f, 0.94f, 0.24f));
            windowFrameMaterial = CreateMaterial(new Color(0.72f, 0.76f, 0.78f), 0.32f);
            doorMaterial = CreateMaterial(new Color(0.42f, 0.20f, 0.075f), 0.22f);
            doorFrameMaterial = CreateMaterial(new Color(0.24f, 0.105f, 0.035f), 0.18f);
            doorHandleMaterial = CreateMaterial(new Color(0.76f, 0.58f, 0.20f), 0.52f);
        }

        public void Build(RoomSpec room, Transform parent)
        {
            Build(room, parent, null);
        }

        public void Build(RoomSpec room, Transform parent, IReadOnlyList<RoomSpec> allRooms)
        {
            // Only standalone rooms need a fallback exterior entrance
            bool standaloneRoom = allRooms == null || allRooms.Count <= 1;
            SceneSpecDefaults.EnsureRoomOpenings(room, standaloneRoom);
            AddCube(parent, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(room.width, 0.10f, room.depth), SelectFloorMaterial(room.type));
            BuildWall(room, "front", parent, allRooms);
            BuildWall(room, "back", parent, allRooms);
            BuildWall(room, "left", parent, allRooms);
            BuildWall(room, "right", parent, allRooms);
        }

        private Material SelectFloorMaterial(string roomType)
        {
            switch (roomType)
            {
                case "bedroom": return bedroomFloorMaterial;
                case "kitchen": return kitchenFloorMaterial;
                case "bathroom": return bathroomFloorMaterial;
                default: return floorMaterial;
            }
        }

        private void BuildWall(
            RoomSpec room, string wall, Transform parent, IReadOnlyList<RoomSpec> allRooms)
        {
            bool horizontal = wall == "front" || wall == "back";
            float length = horizontal ? room.width : room.depth;
            List<WallRange> ownedRanges = GetOwnedWallRanges(room, wall, length, allRooms);
            List<WallOpeningSpec> openings = new List<WallOpeningSpec>();
            for (int i = 0; i < room.openings.Count; i++)
            {
                WallOpeningSpec opening = room.openings[i];
                if (opening.wall != wall) continue;
                if (!string.IsNullOrWhiteSpace(opening.connectsToRoomId) &&
                    string.CompareOrdinal(room.id, opening.connectsToRoomId) > 0) continue;
                if (IsInsideAnyRange(opening.center, ownedRanges)) openings.Add(opening);
            }
            openings.Sort((a, b) => a.center.CompareTo(b.center));

            for (int rangeIndex = 0; rangeIndex < ownedRanges.Count; rangeIndex++)
                BuildWallRange(room, wall, parent, ownedRanges[rangeIndex], openings);
        }

        private void BuildWallRange(
            RoomSpec room, string wall, Transform parent, WallRange range, List<WallOpeningSpec> openings)
        {
            float cursor = range.Start;
            for (int i = 0; i < openings.Count; i++)
            {
                WallOpeningSpec opening = openings[i];
                float left = opening.center - opening.width * 0.5f;
                float right = opening.center + opening.width * 0.5f;
                if (right <= range.Start + 0.001f || left >= range.End - 0.001f) continue;
                left = Mathf.Clamp(left, range.Start, range.End);
                right = Mathf.Clamp(right, range.Start, range.End);
                AddWallBlock(room, wall, cursor, left, 0f, room.height, parent, wallMaterial, "WallSegment");
                AddWallBlock(room, wall, left, right, 0f, opening.sillHeight, parent, wallMaterial, "OpeningSill");
                float openingTop = Mathf.Min(room.height, opening.sillHeight + opening.height);
                AddWallBlock(room, wall, left, right, openingTop, room.height, parent, wallMaterial, "OpeningHeader");
                if (opening.type == "window")
                {
                    const float frame = 0.075f;
                    AddWallBlock(room, wall, left + frame, right - frame, opening.sillHeight + frame,
                        openingTop - frame, parent, windowGlassMaterial, "WindowGlass", 0.035f);
                    AddWallBlock(room, wall, left, left + frame, opening.sillHeight, openingTop,
                        parent, windowFrameMaterial, "WindowFrameLeft");
                    AddWallBlock(room, wall, right - frame, right, opening.sillHeight, openingTop,
                        parent, windowFrameMaterial, "WindowFrameRight");
                    AddWallBlock(room, wall, left, right, opening.sillHeight, opening.sillHeight + frame,
                        parent, windowFrameMaterial, "WindowFrameBottom");
                    AddWallBlock(room, wall, left, right, openingTop - frame, openingTop,
                        parent, windowFrameMaterial, "WindowFrameTop");
                    float middle = (left + right) * 0.5f;
                    AddWallBlock(room, wall, middle - frame * 0.35f, middle + frame * 0.35f,
                        opening.sillHeight + frame, openingTop - frame,
                        parent, windowFrameMaterial, "WindowMullion");
                }
                else if (opening.type == "door" && ShouldRenderDoor(room, opening))
                {
                    BuildDoorAsset(room, wall, left, right, openingTop, parent);
                }
                cursor = Mathf.Max(cursor, right);
            }

            AddWallBlock(room, wall, cursor, range.End, 0f, room.height, parent, wallMaterial, "WallSegment");
        }

        private static List<WallRange> GetOwnedWallRanges(
            RoomSpec room, string wall, float length, IReadOnlyList<RoomSpec> allRooms)
        {
            List<WallRange> ranges = new List<WallRange> { new WallRange(-length * 0.5f, length * 0.5f) };
            if (allRooms == null) return ranges;

            for (int i = 0; i < allRooms.Count; i++)
            {
                RoomSpec target = allRooms[i];
                if (target == room || string.CompareOrdinal(room.id, target.id) < 0) continue;
                float start, end;
                if (!RoomGeometry.TrySharedRange(room, target, wall, out start, out end)) continue;
                float origin = RoomGeometry.Along(room, wall);
                SubtractRange(ranges, new WallRange(start - origin, end - origin));
            }
            return ranges;
        }

        private static RoomSpec FindRoom(IReadOnlyList<RoomSpec> rooms, string id)
        {
            for (int i = 0; i < rooms.Count; i++) if (rooms[i].id == id) return rooms[i];
            return null;
        }

        private static WallRange GetSharedLocalRange(RoomSpec room, RoomSpec target, string wall)
        {
            bool vertical = wall == "left" || wall == "right";
            float roomCenter = vertical ? room.center.z : room.center.x;
            float roomSize = vertical ? room.depth : room.width;
            float targetCenter = vertical ? target.center.z : target.center.x;
            float targetSize = vertical ? target.depth : target.width;
            float worldStart = Mathf.Max(roomCenter - roomSize * 0.5f, targetCenter - targetSize * 0.5f);
            float worldEnd = Mathf.Min(roomCenter + roomSize * 0.5f, targetCenter + targetSize * 0.5f);
            return new WallRange(worldStart - roomCenter, worldEnd - roomCenter);
        }

        private static void SubtractRange(List<WallRange> ranges, WallRange removed)
        {
            for (int i = ranges.Count - 1; i >= 0; i--)
            {
                WallRange current = ranges[i];
                float overlapStart = Mathf.Max(current.Start, removed.Start);
                float overlapEnd = Mathf.Min(current.End, removed.End);
                if (overlapEnd <= overlapStart + 0.001f) continue;
                ranges.RemoveAt(i);
                if (current.Start < overlapStart - 0.001f)
                    ranges.Insert(i, new WallRange(current.Start, overlapStart));
                if (overlapEnd < current.End - 0.001f)
                    ranges.Insert(i + (current.Start < overlapStart - 0.001f ? 1 : 0),
                        new WallRange(overlapEnd, current.End));
            }
        }

        private static bool IsInsideAnyRange(float center, List<WallRange> ranges)
        {
            for (int i = 0; i < ranges.Count; i++)
                if (center >= ranges[i].Start - 0.001f && center <= ranges[i].End + 0.001f) return true;
            return false;
        }

        private static bool ShouldRenderDoor(RoomSpec room, WallOpeningSpec opening)
        {
            return string.IsNullOrWhiteSpace(opening.connectsToRoomId) ||
                string.CompareOrdinal(room.id, opening.connectsToRoomId) < 0;
        }

        private void BuildDoorAsset(
            RoomSpec room, string wall, float left, float right, float openingTop, Transform parent)
        {
            const float frameWidth = 0.085f;
            const float panelThickness = 0.075f;
            float clearWidth = Mathf.Max(0.20f, right - left - frameWidth * 2f);
            float panelHeight = Mathf.Max(0.30f, openingTop - 0.10f);
            bool horizontal = wall == "front" || wall == "back";
            float wallCoordinate = horizontal
                ? (wall == "front" ? -room.depth * 0.5f : room.depth * 0.5f)
                : (wall == "left" ? -room.width * 0.5f : room.width * 0.5f);
            float inwardOffset = wall == "front" || wall == "left" ? 0.025f : -0.025f;
            float alongCenter = (left + right) * 0.5f;

            Vector3 panelPosition = horizontal
                ? new Vector3(alongCenter, panelHeight * 0.5f, wallCoordinate + inwardOffset)
                : new Vector3(wallCoordinate + inwardOffset, panelHeight * 0.5f, alongCenter);
            Vector3 panelScale = horizontal
                ? new Vector3(clearWidth, panelHeight, panelThickness)
                : new Vector3(panelThickness, panelHeight, clearWidth);
            AddDoorCube(parent, wall + "_DoorAsset_Panel", panelPosition, panelScale, doorMaterial);

            AddDoorFrameBlock(room, wall, left, left + frameWidth, 0f, openingTop, parent, "FrameLeft");
            AddDoorFrameBlock(room, wall, right - frameWidth, right, 0f, openingTop, parent, "FrameRight");
            AddDoorFrameBlock(room, wall, left, right, openingTop - frameWidth, openingTop, parent, "FrameTop");

            float handleAlong = right - frameWidth - 0.16f;
            Vector3 handlePosition = horizontal
                ? new Vector3(handleAlong, Mathf.Min(1.02f, panelHeight * 0.54f), wallCoordinate + inwardOffset * 2.4f)
                : new Vector3(wallCoordinate + inwardOffset * 2.4f, Mathf.Min(1.02f, panelHeight * 0.54f), handleAlong);
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = wall + "_DoorAsset_Handle";
            handle.transform.SetParent(parent, false);
            handle.transform.localPosition = handlePosition;
            handle.transform.localScale = Vector3.one * 0.075f;
            handle.GetComponent<Renderer>().sharedMaterial = doorHandleMaterial;
            Collider handleCollider = handle.GetComponent<Collider>();
            if (handleCollider != null) handleCollider.enabled = false;
        }

        private void AddDoorFrameBlock(
            RoomSpec room, string wall, float start, float end, float bottom, float top,
            Transform parent, string label)
        {
            bool horizontal = wall == "front" || wall == "back";
            float along = (start + end) * 0.5f;
            float y = (bottom + top) * 0.5f;
            Vector3 position;
            Vector3 scale;
            if (horizontal)
            {
                float z = wall == "front" ? -room.depth * 0.5f : room.depth * 0.5f;
                position = new Vector3(along, y, z);
                scale = new Vector3(end - start, top - bottom, 0.15f);
            }
            else
            {
                float x = wall == "left" ? -room.width * 0.5f : room.width * 0.5f;
                position = new Vector3(x, y, along);
                scale = new Vector3(0.15f, top - bottom, end - start);
            }
            AddDoorCube(parent, wall + "_DoorAsset_" + label, position, scale, doorFrameMaterial);
        }

        private static GameObject AddDoorCube(
            Transform parent, string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = AddCube(parent, objectName, position, scale, material);
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            return cube;
        }

        private static void AddWallBlock(
            RoomSpec room, string wall, float start, float end, float bottom, float top,
            Transform parent, Material material, string label, float thickness = 0.12f)
        {
            if (end - start <= 0.01f || top - bottom <= 0.01f) return;
            float along = (start + end) * 0.5f;
            float y = (bottom + top) * 0.5f;
            Vector3 position;
            Vector3 scale;
            if (wall == "front" || wall == "back")
            {
                float z = wall == "front" ? -room.depth * 0.5f : room.depth * 0.5f;
                position = new Vector3(along, y, z);
                scale = new Vector3(end - start, top - bottom, thickness);
            }
            else
            {
                float x = wall == "left" ? -room.width * 0.5f : room.width * 0.5f;
                position = new Vector3(x, y, along);
                scale = new Vector3(thickness, top - bottom, end - start);
            }

            AddCube(parent, wall + "_" + label, position, scale, material);
        }

        private static GameObject AddCube(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Material CreateMaterial(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Material CreateGlassMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.78f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }
}
