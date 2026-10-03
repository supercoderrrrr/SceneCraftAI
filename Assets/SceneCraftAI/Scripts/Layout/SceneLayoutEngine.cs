using System;
using System.Collections.Generic;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using UnityEngine;

namespace SceneCraftAI.Layout
{
    public sealed class LayoutReport
    {
        public int RequestedCount;
        public int PlacedCount;
        public int RepairCount;
        public readonly List<string> Messages = new List<string>();
    }

    public sealed class SceneLayoutEngine
    {
        // Leave a small tolerance between physical bounds and walls
        private const float WallGap = RoomGeometry.WallGap;
        private const float FurnitureGap = 0.04f;
        private const float DoorApproachDepth = 1.35f;
        private const float DoorApproachSideMargin = 0.28f;

        public LayoutReport Layout(SceneSpec spec, AssetCatalog catalog)
        {
            return Layout(spec, catalog, 0);
        }

        public LayoutReport Layout(SceneSpec spec, AssetCatalog catalog, int layoutVariant)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            SceneSpecDefaults.EnsureHouse(spec);
            SceneSpecDefaults.EnsureOpenings(spec);
            if (spec.rooms.Count > 1) return LayoutHouse(spec, catalog, layoutVariant);
            return LayoutSingleRoom(spec, catalog, layoutVariant);
        }

        private LayoutReport LayoutSingleRoom(SceneSpec spec, AssetCatalog catalog, int layoutVariant)
        {

            LayoutReport report = new LayoutReport { RequestedCount = spec.objects.Count };
            spec.placements.Clear();

            bool[] handled = new bool[spec.objects.Count];
            int handledCount = 0;
            for (int i = 0; i < spec.objects.Count; i++)
            {
                if (!spec.objects[i].locked) continue;
                PlaceRequest(spec, catalog, spec.objects[i], report, layoutVariant);
                handled[i] = true;
                handledCount++;
            }
            for (int pass = 0; pass <= spec.objects.Count && handledCount < spec.objects.Count; pass++)
            {
                bool progressed = false;
                for (int i = 0; i < spec.objects.Count; i++)
                {
                    if (handled[i]) continue;
                    SceneObjectRequest request = spec.objects[i];
                    if (pass < spec.objects.Count && IsAnchorPending(request, spec.objects, handled)) continue;
                    PlaceRequest(spec, catalog, request, report, layoutVariant);
                    handled[i] = true;
                    handledCount++;
                    progressed = true;
                }

                if (!progressed && handledCount < spec.objects.Count)
                {
                    // Place remaining requests even when their anchors form a cycle
                    for (int i = 0; i < spec.objects.Count; i++)
                    {
                        if (handled[i]) continue;
                        PlaceRequest(spec, catalog, spec.objects[i], report, layoutVariant);
                        handled[i] = true;
                        handledCount++;
                    }
                    break;
                }
            }

            report.Messages.Add(string.Format(
                "Layout complete: {0}/{1} objects placed, {2} automatic repairs.",
                report.PlacedCount,
                report.RequestedCount,
                report.RepairCount));
            return report;
        }

        private LayoutReport LayoutHouse(SceneSpec spec, AssetCatalog catalog, int layoutVariant)
        {
            spec.placements.Clear();
            LayoutReport combined = new LayoutReport { RequestedCount = spec.objects.Count };
            for (int roomIndex = 0; roomIndex < spec.rooms.Count; roomIndex++)
            {
                RoomSpec room = spec.rooms[roomIndex];
                SceneSpec roomScene = new SceneSpec
                {
                    prompt = spec.prompt,
                    roomType = room.type,
                    style = spec.style,
                    room = room
                };
                roomScene.rooms.Add(room);
                for (int i = 0; i < spec.objects.Count; i++)
                {
                    SceneObjectRequest request = spec.objects[i];
                    if (request.roomId == room.id) roomScene.objects.Add(request);
                }

                LayoutReport roomReport = LayoutSingleRoom(roomScene, catalog, layoutVariant + roomIndex * 7);
                combined.PlacedCount += roomReport.PlacedCount;
                combined.RepairCount += roomReport.RepairCount;
                for (int i = 0; i < roomReport.Messages.Count; i++)
                    combined.Messages.Add(room.id + ": " + roomReport.Messages[i]);
                for (int i = 0; i < roomScene.placements.Count; i++)
                {
                    PlacedObjectSpec placement = roomScene.placements[i];
                    placement.position += room.center;
                    placement.roomId = room.id;
                    spec.placements.Add(placement);
                }
            }

            combined.Messages.Add(string.Format(
                "House layout complete: {0} rooms connected, {1}/{2} objects placed.",
                spec.rooms.Count,
                combined.PlacedCount,
                combined.RequestedCount));
            return combined;
        }

        private void PlaceRequest(
            SceneSpec spec,
            AssetCatalog catalog,
            SceneObjectRequest request,
            LayoutReport report,
            int layoutVariant)
        {
            AssetDefinition asset = catalog.FindBest(request, spec.style);
            if (asset == null)
            {
                report.Messages.Add("No local asset found for " + request.category + ".");
                return;
            }

            PlacedObjectSpec placement = CreatePreferredPlacement(spec, request, asset, layoutVariant);
            if (request.locked)
            {
                spec.placements.Add(placement);
                report.PlacedCount++;
                return;
            }
            bool requiresAnchor = RequiresAnchor(request.relation);
            if (requiresAnchor && string.IsNullOrWhiteSpace(placement.anchorId))
            {
                report.Messages.Add("Skipped " + request.category + ": semantic anchor was not found.");
                return;
            }

            if (request.placement == "surface" && !IsSupportedByAnchor(placement, spec.placements))
            {
                report.Messages.Add("Skipped " + request.category + ": support surface is missing or too small.");
                return;
            }

            bool decorative = request.placement == "wall" || request.placement == "ceiling" || request.category == "rug";
            if (!decorative && !IsValid(placement, spec.room, spec.placements, null))
            {
                Vector3 repaired = Vector3.zero;
                bool semanticRelation = requiresAnchor || request.relation == "near_window";
                bool repairedNearby = request.relation == "against_wall" &&
                    TryFindValidWallPosition(placement, spec.room, spec.placements, null, out repaired);
                if (!repairedNearby)
                {
                    repairedNearby = TryFindNearbyPosition(
                        placement, spec.room, spec.placements, null, semanticRelation ? 0.9f : 1.4f, out repaired);
                }
                if (!repairedNearby && !semanticRelation)
                {
                    repairedNearby = TryFindOpenPosition(placement, spec.room, spec.placements, null, out repaired);
                }
                if (!repairedNearby)
                {
                    report.Messages.Add("Skipped " + request.category + ": no relation-preserving collision-free position.");
                    return;
                }

                placement.position = repaired;
                placement.rationale += semanticRelation
                    ? " Repaired near its semantic target."
                    : " Auto-repaired to the nearest valid free area.";
                report.RepairCount++;
            }

            spec.placements.Add(placement);
            report.PlacedCount++;
        }

        private static PlacedObjectSpec CreatePreferredPlacement(
            SceneSpec spec,
            SceneObjectRequest request,
            AssetDefinition asset,
            int layoutVariant)
        {
            RoomSpec room = spec.room;
            Vector3 size = asset.Size * GetLifestyleScale(request, layoutVariant);
            if (request.locked)
            {
                return new PlacedObjectSpec
                {
                    id = request.id,
                    assetId = asset.Id,
                    category = request.category,
                    roomId = request.roomId,
                    position = request.lockedPosition,
                    rotationY = request.lockedRotationY,
                    size = size,
                    rationale = "Preserved user-locked transform.",
                    relation = request.relation,
                    anchorId = request.anchorId,
                    locked = true
                };
            }
            float backZ = room.depth * 0.5f - size.z * 0.5f - WallGap;
            float frontZ = -room.depth * 0.5f + size.z * 0.5f + WallGap;
            float leftX = -room.width * 0.5f + size.x * 0.5f + WallGap;
            float rightX = room.width * 0.5f - size.x * 0.5f - WallGap;
            Vector3 position = Vector3.zero;
            float rotation = 0f;
            string rationale = "Placed from relation: " + request.relation + ".";
            PlacedObjectSpec anchor = FindById(spec.placements, request.anchorId);
            if (anchor == null) anchor = InferAnchor(spec.placements, request);
            string resolvedAnchorId = anchor == null ? request.anchorId : anchor.id;

            switch (request.relation)
            {
                case "against_wall":
                    int wallIndex;
                    if (room.type == "bedroom" && request.category == "bed")
                    {
                        wallIndex = ChooseSolidWall(room, -1, 0, 1, 2, 3);
                    }
                    else if (room.type == "bedroom" && request.category == "wardrobe")
                    {
                        PlacedObjectSpec bedroomBed = FindFirst(spec.placements, "bed");
                        int bedWall = bedroomBed == null ? -1 : GetNearestWallIndex(bedroomBed.Bounds, room);
                        wallIndex = ChooseSolidWall(room, bedWall, 1, 2, 3, 0);
                    }
                    else
                    {
                        wallIndex = layoutVariant == 0
                            ? 0
                            : Mathf.Abs(layoutVariant + StableIndex(request.id)) % 3;
                    }
                    GetAgainstWallPlacement(room, size, wallIndex, out position, out rotation);
                    if (layoutVariant > 0 && room.type != "bedroom")
                    {
                        int offsetBand = layoutVariant / 3 % 3 - 1;
                        if (wallIndex == 0)
                            position.x = Mathf.Clamp(offsetBand * room.width * 0.16f, leftX, rightX);
                        else
                            position.z = Mathf.Clamp(offsetBand * room.depth * 0.16f, frontZ, backZ);
                    }
                    if (wallIndex == 0 && (request.category == "wardrobe" || request.category == "bookshelf"))
                        position.x = leftX + 0.2f;
                    break;
                case "beside_bed":
                    PlacedObjectSpec bed = anchor ?? FindFirst(spec.placements, "bed");
                    float side = CountCategory(spec.placements, "nightstand") == 0 ? -1f : 1f;
                    position = bed != null
                        ? bed.position + RotatedDirection(bed.rotationY, Vector3.right) *
                            (side * ((bed.size.x + size.x) * 0.5f + FurnitureGap)) +
                            RotatedDirection(bed.rotationY, Vector3.forward) * 0.15f
                        : new Vector3(side * 1.4f, 0f, backZ);
                    rotation = bed != null ? bed.rotationY : 0f;
                    break;
                case "in_front_of_sofa":
                    PlacedObjectSpec sofa = anchor ?? FindFirst(spec.placements, "sofa");
                    position = sofa != null
                        ? sofa.position + RotatedDirection(sofa.rotationY, Vector3.back) * 1.45f
                        : Vector3.zero;
                    rotation = sofa != null ? sofa.rotationY : 0f;
                    resolvedAnchorId = sofa == null ? resolvedAnchorId : sofa.id;
                    break;
                case "opposite_sofa":
                    PlacedObjectSpec oppositeSofa = anchor ?? FindFirst(spec.placements, "sofa");
                    if (oppositeSofa != null)
                    {
                        GetOppositeWallPlacement(room, size, oppositeSofa, out position, out rotation);
                        resolvedAnchorId = oppositeSofa.id;
                    }
                    else
                    {
                        position = new Vector3(0f, 0f, frontZ);
                        rotation = 180f;
                    }
                    break;
                case "beside_sofa":
                    PlacedObjectSpec adjacentSofa = anchor ?? FindFirst(spec.placements, "sofa");
                    position = adjacentSofa != null
                        ? adjacentSofa.position + RotatedDirection(adjacentSofa.rotationY, Vector3.right) *
                            ((adjacentSofa.size.x + size.x) * 0.5f + 0.15f)
                        : new Vector3(rightX, 0f, backZ);
                    rotation = adjacentSofa != null ? adjacentSofa.rotationY : 0f;
                    resolvedAnchorId = adjacentSofa == null ? resolvedAnchorId : adjacentSofa.id;
                    break;
                case "near_window":
                    WallOpeningSpec window = FindOpening(room, "window");
                    if (window != null)
                    {
                        GetNearOpeningPlacement(room, window, size, request.category == "dining_table", out position, out rotation);
                    }
                    else
                    {
                        position = new Vector3(rightX - 0.15f, 0f, backZ);
                        rotation = 0f;
                    }
                    break;
                case "in_front_of_desk":
                    PlacedObjectSpec desk = anchor ?? FindFirst(spec.placements, "desk");
                    position = desk != null ? desk.position + RotatedDirection(desk.rotationY, Vector3.back) * 0.85f : new Vector3(0f, 0f, 0.5f);
                    rotation = desk != null ? desk.rotationY + 180f : 0f;
                    resolvedAnchorId = desk == null ? resolvedAnchorId : desk.id;
                    break;
                case "around_table":
                    PlacedObjectSpec table = anchor ?? FindFirst(spec.placements, "dining_table");
                    if (table != null)
                    {
                        int slot = CountAnchored(spec.placements, table.id);
                        GetAnchorSlotPlacement(table, size, slot, out position, out rotation);
                        resolvedAnchorId = table.id;
                    }
                    break;
                case "left_of":
                    GetAnchorSlotPlacement(anchor, size, 2, out position, out rotation);
                    break;
                case "right_of":
                    GetAnchorSlotPlacement(anchor, size, 3, out position, out rotation);
                    break;
                case "in_front_of":
                    GetAnchorSlotPlacement(anchor, size, 0, out position, out rotation);
                    break;
                case "behind":
                    GetAnchorSlotPlacement(anchor, size, 1, out position, out rotation);
                    break;
                case "corner":
                    int corner = Mathf.Abs(layoutVariant + CountCategory(spec.placements, request.category)) % 4;
                    position = new Vector3(
                        corner == 0 || corner == 3 ? rightX : leftX,
                        0f,
                        corner < 2 ? frontZ : backZ);
                    break;
                case "wall_above_sofa":
                    PlacedObjectSpec wallSofa = FindFirst(spec.placements, "sofa");
                    float artX = wallSofa != null ? wallSofa.position.x : 0f;
                    position = new Vector3(artX, room.height * 0.58f, room.depth * 0.5f - size.z * 0.5f - 0.025f);
                    rotation = 0f;
                    break;
                case "wall_high":
                    int highWall = ChooseSolidWall(room, -1,
                        Mathf.Abs(StableIndex(request.id) + layoutVariant) % 4, 0, 1, 2, 3);
                    GetAgainstWallPlacement(room, size, highWall, out position, out rotation);
                    position.y = room.height - size.y * 0.72f;
                    break;
                case "center":
                    position = Vector3.zero;
                    break;
                case "under_coffee_table":
                    PlacedObjectSpec coffeeTable = anchor ?? FindFirst(spec.placements, "coffee_table");
                    position = coffeeTable != null
                        ? new Vector3(coffeeTable.position.x, 0f, coffeeTable.position.z)
                        : Vector3.zero;
                    rotation = coffeeTable != null ? coffeeTable.rotationY : 0f;
                    resolvedAnchorId = coffeeTable == null ? resolvedAnchorId : coffeeTable.id;
                    break;
                case "ceiling_center":
                    position = new Vector3(0f, Mathf.Max(0f, room.height - size.y), 0f);
                    rotation = 0f;
                    break;
                case "on_top_of":
                case "on_shelf":
                    if (anchor != null)
                    {
                        int siblingIndex = CountAnchored(spec.placements, anchor.id);
                        float offset = siblingIndex == 0 ? 0f : (siblingIndex % 2 == 0 ? -1f : 1f) *
                            Mathf.Min(anchor.size.x * 0.22f, 0.28f);
                        position = new Vector3(
                            anchor.position.x + offset,
                            anchor.position.y + anchor.size.y,
                            anchor.position.z);
                        rotation = anchor.rotationY;
                        resolvedAnchorId = anchor.id;
                    }
                    break;
                default:
                    float randomX = Mathf.Lerp(leftX, rightX, StableUnit(request.id, layoutVariant * 17 + 3));
                    float randomZ = Mathf.Lerp(frontZ, backZ, StableUnit(request.id, layoutVariant * 29 + 11));
                    position = new Vector3(randomX, 0f, randomZ);
                    rotation = 0f;
                    break;
            }

            return new PlacedObjectSpec
            {
                id = request.id,
                assetId = asset.Id,
                category = request.category,
                roomId = request.roomId,
                position = position,
                rotationY = rotation,
                size = size,
                rationale = rationale,
                relation = request.relation,
                anchorId = resolvedAnchorId,
                locked = request.locked
            };
        }

        public bool TryFindOpenPosition(
            PlacedObjectSpec candidate,
            RoomSpec room,
            List<PlacedObjectSpec> existing,
            string ignoredId,
            out Vector3 position)
        {
            Bounds candidateBounds = candidate.Bounds;
            float minX = -room.width * 0.5f + candidateBounds.extents.x + WallGap;
            float maxX = room.width * 0.5f - candidateBounds.extents.x - WallGap;
            float minZ = -room.depth * 0.5f + candidateBounds.extents.z + WallGap;
            float maxZ = room.depth * 0.5f - candidateBounds.extents.z - WallGap;

            for (float z = maxZ; z >= minZ; z -= 0.35f)
            {
                for (float x = minX; x <= maxX; x += 0.35f)
                {
                    PlacedObjectSpec probe = Clone(candidate);
                    probe.position = new Vector3(x, candidate.position.y, z);
                    if (IsValid(probe, room, existing, ignoredId))
                    {
                        position = probe.position;
                        return true;
                    }
                }
            }

            position = Vector3.zero;
            return false;
        }

        private bool TryFindNearbyPosition(
            PlacedObjectSpec candidate,
            RoomSpec room,
            List<PlacedObjectSpec> existing,
            string ignoredId,
            float maxRadius,
            out Vector3 position)
        {
            const int DirectionCount = 16;
            for (float radius = 0.2f; radius <= maxRadius + 0.01f; radius += 0.2f)
            {
                for (int i = 0; i < DirectionCount; i++)
                {
                    float angle = Mathf.PI * 2f * i / DirectionCount;
                    PlacedObjectSpec probe = Clone(candidate);
                    probe.position = candidate.position + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f,
                        Mathf.Sin(angle) * radius);
                    if (IsValid(probe, room, existing, ignoredId))
                    {
                        position = probe.position;
                        return true;
                    }
                }
            }

            position = Vector3.zero;
            return false;
        }

        private bool TryFindValidWallPosition(
            PlacedObjectSpec candidate,
            RoomSpec room,
            List<PlacedObjectSpec> existing,
            string ignoredId,
            out Vector3 position)
        {
            int preferredWall = GetNearestWallIndex(candidate.Bounds, room);
            for (int wallPass = 0; wallPass < 4; wallPass++)
            {
                int wall = (preferredWall + wallPass) % 4;
                if ((candidate.category == "bed" || candidate.category == "wardrobe") &&
                    HasOpeningOnWall(room, WallName(wall))) continue;

                Vector3 basePosition;
                float rotation;
                GetAgainstWallPlacement(room, candidate.size, wall, out basePosition, out rotation);
                Vector3 extents = GetRotatedFootprintExtents(candidate.size, rotation);
                float maxOffset = wall == 0 || wall == 3
                    ? room.width * 0.5f - extents.x - WallGap
                    : room.depth * 0.5f - extents.z - WallGap;
                int steps = Mathf.Max(0, Mathf.FloorToInt(maxOffset / 0.25f));
                for (int step = 0; step <= steps; step++)
                {
                    int signs = step == 0 ? 1 : 2;
                    for (int signIndex = 0; signIndex < signs; signIndex++)
                    {
                        float offset = step * 0.25f * (signIndex == 0 ? 1f : -1f);
                        PlacedObjectSpec probe = Clone(candidate);
                        probe.rotationY = rotation;
                        probe.position = basePosition;
                        if (wall == 0 || wall == 3) probe.position.x = offset;
                        else probe.position.z = offset;
                        if (!IsValid(probe, room, existing, ignoredId)) continue;
                        candidate.rotationY = probe.rotationY;
                        position = probe.position;
                        return true;
                    }
                }
            }

            position = Vector3.zero;
            return false;
        }

        public bool IsPlacementValid(
            PlacedObjectSpec candidate,
            RoomSpec room,
            List<PlacedObjectSpec> existing,
            string ignoredId,
            out string reason)
        {
            if (!IsInsideRoom(candidate, room))
            {
                reason = "家具超出了房间边界";
                return false;
            }

            if (IntersectsOpeningClearance(candidate, room))
            {
                reason = "该位置会挡住门或窗的使用区域";
                return false;
            }

            Bounds padded = candidate.Bounds;
            padded.Expand(new Vector3(FurnitureGap * 2f, 0f, FurnitureGap * 2f));
            for (int i = 0; i < existing.Count; i++)
            {
                PlacedObjectSpec other = existing[i];
                if (other.id == ignoredId || other.category == "rug" || other.category == "wall_art") continue;
                if ((candidate.relation == "on_top_of" || candidate.relation == "on_shelf") &&
                    other.id == candidate.anchorId) continue;
                if (padded.Intersects(other.Bounds))
                {
                    reason = "该位置会与其他家具重叠";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public bool IsValid(PlacedObjectSpec candidate, RoomSpec room, List<PlacedObjectSpec> existing, string ignoredId)
        {
            string reason;
            return IsPlacementValid(candidate, room, existing, ignoredId, out reason);
        }

        private static bool IsInsideRoom(PlacedObjectSpec candidate, RoomSpec room)
        {
            Bounds bounds = candidate.Bounds;
            float halfWidth = room.width * 0.5f;
            float halfDepth = room.depth * 0.5f;
            if (bounds.min.x < -halfWidth || bounds.max.x > halfWidth ||
                bounds.min.z < -halfDepth || bounds.max.z > halfDepth ||
                bounds.min.y < -0.01f || bounds.max.y > room.height + 0.01f)
            {
                return false;
            }

            return true;
        }

        private static bool IntersectsOpeningClearance(PlacedObjectSpec candidate, RoomSpec room)
        {
            if (candidate.category == "rug" || candidate.category == "wall_art" || room.openings == null) return false;
            for (int i = 0; i < room.openings.Count; i++)
            {
                Bounds clearance = GetOpeningClearance(room.openings[i], room);
                if (candidate.Bounds.Intersects(clearance)) return true;
            }

            return false;
        }

        public static Bounds GetOpeningClearance(WallOpeningSpec opening, RoomSpec room)
        {
            bool passage = opening.type == "door" || opening.type == "open";
            float minimumPassageDepth = opening.type == "open" ? 1.1f : DoorApproachDepth;
            float depth = passage
                ? Mathf.Max(minimumPassageDepth, opening.clearanceDepth)
                : Mathf.Max(0.2f, opening.clearanceDepth);
            float sideMargin = passage ? DoorApproachSideMargin : 0.06f;
            float width = Mathf.Max(0.2f, opening.width + sideMargin * 2f);
            Vector3 center;
            Vector3 size;
            switch (opening.wall)
            {
                case "back":
                    center = new Vector3(opening.center, room.height * 0.5f, room.depth * 0.5f - depth * 0.5f);
                    size = new Vector3(width, room.height, depth);
                    break;
                case "left":
                    center = new Vector3(-room.width * 0.5f + depth * 0.5f, room.height * 0.5f, opening.center);
                    size = new Vector3(depth, room.height, width);
                    break;
                case "right":
                    center = new Vector3(room.width * 0.5f - depth * 0.5f, room.height * 0.5f, opening.center);
                    size = new Vector3(depth, room.height, width);
                    break;
                default:
                    center = new Vector3(opening.center, room.height * 0.5f, -room.depth * 0.5f + depth * 0.5f);
                    size = new Vector3(width, room.height, depth);
                    break;
            }

            return new Bounds(center, size);
        }

        public static PlacedObjectSpec Clone(PlacedObjectSpec source)
        {
            return new PlacedObjectSpec
            {
                id = source.id,
                assetId = source.assetId,
                category = source.category,
                roomId = source.roomId,
                position = source.position,
                rotationY = source.rotationY,
                size = source.size,
                rationale = source.rationale,
                relation = source.relation,
                anchorId = source.anchorId,
                locked = source.locked
            };
        }

        private static bool IsAnchorPending(
            SceneObjectRequest request,
            List<SceneObjectRequest> requests,
            bool[] handled)
        {
            if (string.IsNullOrWhiteSpace(request.anchorId)) return false;
            for (int i = 0; i < requests.Count; i++)
            {
                if (!handled[i] && requests[i].id == request.anchorId) return true;
            }

            return false;
        }

        private static bool RequiresAnchor(string relation)
        {
            switch (relation)
            {
                case "around_table":
                case "left_of":
                case "right_of":
                case "in_front_of":
                case "behind":
                case "on_top_of":
                case "on_shelf":
                    return true;
                default:
                    return false;
            }
        }

        private static PlacedObjectSpec FindById(List<PlacedObjectSpec> placements, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].id == id) return placements[i];
            }

            return null;
        }

        private static PlacedObjectSpec InferAnchor(
            List<PlacedObjectSpec> placements,
            SceneObjectRequest request)
        {
            switch (request.relation)
            {
                case "around_table":
                    return FindFirst(placements, "dining_table") ?? FindFirst(placements, "desk");
                case "beside_bed":
                    return FindFirst(placements, "bed");
                case "in_front_of_sofa":
                case "beside_sofa":
                case "opposite_sofa":
                case "wall_above_sofa":
                    return FindFirst(placements, "sofa");
                case "in_front_of_desk":
                    return FindFirst(placements, "desk");
                case "under_coffee_table":
                    return FindFirst(placements, "coffee_table");
                case "on_top_of":
                case "on_shelf":
                    return FindFirst(placements, "coffee_table") ?? FindFirst(placements, "dining_table") ??
                        FindFirst(placements, "desk") ?? FindFirst(placements, "nightstand") ??
                        FindFirst(placements, "bookshelf");
                default:
                    return null;
            }
        }

        private static bool IsSupportedByAnchor(PlacedObjectSpec placement, List<PlacedObjectSpec> existing)
        {
            PlacedObjectSpec anchor = FindById(existing, placement.anchorId);
            if (anchor == null) return false;
            Bounds support = anchor.Bounds;
            Bounds item = placement.Bounds;
            const float tolerance = 0.025f;
            return item.min.x >= support.min.x - tolerance && item.max.x <= support.max.x + tolerance &&
                item.min.z >= support.min.z - tolerance && item.max.z <= support.max.z + tolerance &&
                Mathf.Abs(item.min.y - support.max.y) <= 0.04f;
        }

        private static WallOpeningSpec FindOpening(RoomSpec room, string kind)
        {
            if (room.openings == null) return null;
            for (int i = 0; i < room.openings.Count; i++)
            {
                if (room.openings[i].type == kind) return room.openings[i];
            }

            return null;
        }

        private static void GetAgainstWallPlacement(
            RoomSpec room,
            Vector3 objectSize,
            int wallIndex,
            out Vector3 position,
            out float rotation)
        {
            rotation = wallIndex == 1 ? -90f : wallIndex == 2 ? 90f : wallIndex == 3 ? 180f : 0f;
            Vector3 extents = GetRotatedFootprintExtents(objectSize, rotation);
            if (wallIndex == 1)
                position = new Vector3(-room.width * 0.5f + extents.x + WallGap, 0f, 0f);
            else if (wallIndex == 2)
                position = new Vector3(room.width * 0.5f - extents.x - WallGap, 0f, 0f);
            else if (wallIndex == 3)
                position = new Vector3(0f, 0f, -room.depth * 0.5f + extents.z + WallGap);
            else
                position = new Vector3(0f, 0f, room.depth * 0.5f - extents.z - WallGap);
        }

        private static int ChooseSolidWall(RoomSpec room, int excludedWall, params int[] preferredWalls)
        {
            for (int i = 0; i < preferredWalls.Length; i++)
            {
                int wall = preferredWalls[i];
                if (wall == excludedWall || HasOpeningOnWall(room, WallName(wall))) continue;
                return wall;
            }
            for (int i = 0; i < preferredWalls.Length; i++)
                if (preferredWalls[i] != excludedWall) return preferredWalls[i];
            return 0;
        }

        private static bool HasOpeningOnWall(RoomSpec room, string wall)
        {
            if (room.openings == null) return false;
            for (int i = 0; i < room.openings.Count; i++)
                if (room.openings[i].wall == wall) return true;
            return false;
        }

        private static string WallName(int wallIndex)
        {
            switch (wallIndex)
            {
                case 1: return "left";
                case 2: return "right";
                case 3: return "front";
                default: return "back";
            }
        }

        private static int GetNearestWallIndex(Bounds bounds, RoomSpec room)
        {
            float[] distances =
            {
                room.depth * 0.5f - bounds.max.z,
                bounds.min.x + room.width * 0.5f,
                room.width * 0.5f - bounds.max.x,
                bounds.min.z + room.depth * 0.5f
            };
            int nearest = 0;
            for (int i = 1; i < distances.Length; i++)
                if (distances[i] < distances[nearest]) nearest = i;
            return nearest;
        }

        private static void GetOppositeWallPlacement(
            RoomSpec room,
            Vector3 objectSize,
            PlacedObjectSpec target,
            out Vector3 position,
            out float rotation)
        {
            float halfWidth = room.width * 0.5f;
            float halfDepth = room.depth * 0.5f;
            float toBack = halfDepth - target.Bounds.max.z;
            float toLeft = target.Bounds.min.x + halfWidth;
            float toRight = halfWidth - target.Bounds.max.x;

            if (toLeft <= toBack && toLeft <= toRight)
            {
                rotation = 90f;
                Vector3 extents = GetRotatedFootprintExtents(objectSize, rotation);
                position = new Vector3(halfWidth - extents.x - WallGap, 0f, target.position.z);
            }
            else if (toRight <= toBack && toRight <= toLeft)
            {
                rotation = -90f;
                Vector3 extents = GetRotatedFootprintExtents(objectSize, rotation);
                position = new Vector3(-halfWidth + extents.x + WallGap, 0f, target.position.z);
            }
            else
            {
                rotation = 180f;
                Vector3 extents = GetRotatedFootprintExtents(objectSize, rotation);
                position = new Vector3(target.position.x, 0f, -halfDepth + extents.z + WallGap);
            }
        }

        private static int StableIndex(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int hash = 17;
            for (int i = 0; i < value.Length; i++) hash = unchecked(hash * 31 + value[i]);
            return hash & int.MaxValue;
        }

        private static float StableUnit(string value, int salt)
        {
            int hash = StableIndex((value ?? string.Empty) + "#" + salt);
            return (hash % 10001) / 10000f;
        }

        private static float GetLifestyleScale(SceneObjectRequest request, int layoutVariant)
        {
            string category = request == null ? string.Empty : request.category;
            float range = category == "flower_pot" || category == "plant" || category == "vase" ||
                category == "books" || category == "decor_bowl" ? 0.15f : 0.055f;
            float unit = StableUnit(request == null ? string.Empty : request.id, layoutVariant * 7 + 41);
            return Mathf.Lerp(1f - range, 1f + range, unit);
        }

        private static void GetNearOpeningPlacement(
            RoomSpec room,
            WallOpeningSpec opening,
            Vector3 objectSize,
            bool needsSeatingClearance,
            out Vector3 position,
            out float rotation)
        {
            float seatingGap = needsSeatingClearance ? 0.9f : 0.18f;
            rotation = opening.wall == "left" || opening.wall == "right" ? 90f : 0f;
            Vector3 extents = GetRotatedFootprintExtents(objectSize, rotation);

            switch (opening.wall)
            {
                case "back":
                    position = new Vector3(
                        Mathf.Clamp(opening.center, -room.width * 0.5f + extents.x + WallGap, room.width * 0.5f - extents.x - WallGap),
                        0f,
                        room.depth * 0.5f - opening.clearanceDepth - extents.z - seatingGap);
                    break;
                case "left":
                    position = new Vector3(
                        -room.width * 0.5f + opening.clearanceDepth + extents.x + seatingGap,
                        0f,
                        Mathf.Clamp(opening.center, -room.depth * 0.5f + extents.z + WallGap, room.depth * 0.5f - extents.z - WallGap));
                    break;
                case "right":
                    position = new Vector3(
                        room.width * 0.5f - opening.clearanceDepth - extents.x - seatingGap,
                        0f,
                        Mathf.Clamp(opening.center, -room.depth * 0.5f + extents.z + WallGap, room.depth * 0.5f - extents.z - WallGap));
                    break;
                default:
                    position = new Vector3(
                        Mathf.Clamp(opening.center, -room.width * 0.5f + extents.x + WallGap, room.width * 0.5f - extents.x - WallGap),
                        0f,
                        -room.depth * 0.5f + opening.clearanceDepth + extents.z + seatingGap);
                    break;
            }
        }

        private static Vector3 GetRotatedFootprintExtents(Vector3 size, float rotationY)
        {
            float radians = rotationY * Mathf.Deg2Rad;
            float cosine = Mathf.Abs(Mathf.Cos(radians));
            float sine = Mathf.Abs(Mathf.Sin(radians));
            return new Vector3(
                (size.x * cosine + size.z * sine) * 0.5f,
                size.y * 0.5f,
                (size.x * sine + size.z * cosine) * 0.5f);
        }

        private static Vector3 RotatedDirection(float rotationY, Vector3 localDirection)
        {
            return Quaternion.Euler(0f, rotationY, 0f) * localDirection;
        }

        private static void GetAnchorSlotPlacement(
            PlacedObjectSpec anchor,
            Vector3 objectSize,
            int slot,
            out Vector3 position,
            out float rotation)
        {
            if (anchor == null)
            {
                position = Vector3.zero;
                rotation = 0f;
                return;
            }

            int side = slot % 4;
            int ring = slot / 4;
            float extraGap = 0.28f + ring * 0.42f;
            Vector3 localDirection;
            float distance;
            float localRotation;
            switch (side)
            {
                case 0:
                    localDirection = Vector3.back;
                    distance = anchor.size.z * 0.5f + objectSize.z * 0.5f + extraGap;
                    localRotation = 180f;
                    break;
                case 1:
                    localDirection = Vector3.forward;
                    distance = anchor.size.z * 0.5f + objectSize.z * 0.5f + extraGap;
                    localRotation = 0f;
                    break;
                case 2:
                    localDirection = Vector3.left;
                    distance = anchor.size.x * 0.5f + objectSize.z * 0.5f + extraGap;
                    localRotation = -90f;
                    break;
                default:
                    localDirection = Vector3.right;
                    distance = anchor.size.x * 0.5f + objectSize.z * 0.5f + extraGap;
                    localRotation = 90f;
                    break;
            }

            position = anchor.position + RotatedDirection(anchor.rotationY, localDirection) * distance;
            rotation = anchor.rotationY + localRotation;
        }

        private static int CountAnchored(List<PlacedObjectSpec> placements, string anchorId)
        {
            int count = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].anchorId == anchorId) count++;
            }

            return count;
        }

        private static PlacedObjectSpec FindFirst(List<PlacedObjectSpec> placements, string category)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].category == category) return placements[i];
            }

            return null;
        }

        private static int CountCategory(List<PlacedObjectSpec> placements, string category)
        {
            int count = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].category == category) count++;
            }

            return count;
        }
    }
}
