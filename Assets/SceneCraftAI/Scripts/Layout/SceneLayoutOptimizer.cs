using System;
using System.Collections.Generic;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using UnityEngine;

namespace SceneCraftAI.Layout
{
    public sealed class SceneAuditReport
    {
        public readonly List<string> Issues = new List<string>();
        public bool Passed { get { return Issues.Count == 0; } }
        public string Summary { get { return Passed ? "audit passed" : Issues.Count + " audit warning(s)"; } }
    }

    public sealed class LayoutAudit
    {
        public SceneAuditReport Audit(SceneSpec spec, AssetCatalog catalog)
        {
            SceneAuditReport report = new SceneAuditReport();
            if (spec == null) { report.Issues.Add("Scene is missing."); return report; }
            SceneSpecDefaults.EnsureHouse(spec);
            for (int requestIndex = 0; requestIndex < spec.objects.Count; requestIndex++)
            {
                SceneObjectRequest request = spec.objects[requestIndex];
                if (request.required && FindPlacement(spec.placements, request.id) == null)
                    report.Issues.Add(request.id + " was requested but could not be placed.");
            }

            for (int placementIndex = 0; placementIndex < spec.placements.Count; placementIndex++)
            {
                PlacedObjectSpec placement = spec.placements[placementIndex];
                RoomSpec room = FindRoom(spec.rooms, placement.roomId);
                if (room == null) { report.Issues.Add(placement.id + " has no valid room."); continue; }
                PlacedObjectSpec local = SceneLayoutEngine.Clone(placement);
                local.position -= room.center;
                if (room.openings != null && placement.category != "rug" && placement.category != "wall_art")
                {
                    for (int openingIndex = 0; openingIndex < room.openings.Count; openingIndex++)
                        if (local.Bounds.Intersects(SceneLayoutEngine.GetOpeningClearance(room.openings[openingIndex], room)))
                        {
                            report.Issues.Add(placement.id + " blocks opening " + room.openings[openingIndex].id + ".");
                            break;
                        }
                }

                AssetDefinition asset = catalog.FindById(placement.assetId);
                AssetSpatialProfile profile = asset == null
                    ? AssetSpatialProfile.ForCategory(placement.category)
                    : asset.SpatialProfile;
                if (profile.PrefersWall && DistanceToWall(local.Bounds, room) > 0.08f)
                    report.Issues.Add(placement.id + " is expected to touch a wall.");

                if (placement.category == "chair" && !string.IsNullOrWhiteSpace(placement.anchorId))
                {
                    PlacedObjectSpec anchor = FindPlacement(spec.placements, placement.anchorId);
                    if (anchor != null)
                    {
                        Vector3 front = Quaternion.Euler(0f, placement.rotationY, 0f) * Vector3.back;
                        Vector3 toward = anchor.position - placement.position;
                        toward.y = 0f;
                        if (toward.sqrMagnitude > 0.001f && Vector3.Dot(front, toward.normalized) < 0.9f)
                            report.Issues.Add(placement.id + " does not face " + anchor.id + ".");
                    }
                }
            }
            return report;
        }

        private static RoomSpec FindRoom(List<RoomSpec> rooms, string id)
        {
            for (int i = 0; i < rooms.Count; i++) if (rooms[i].id == id) return rooms[i];
            return null;
        }

        private static PlacedObjectSpec FindPlacement(List<PlacedObjectSpec> placements, string id)
        {
            for (int i = 0; i < placements.Count; i++) if (placements[i].id == id) return placements[i];
            return null;
        }

        private static float DistanceToWall(Bounds bounds, RoomSpec room)
        {
            return Mathf.Min(
                Mathf.Min(bounds.min.x + room.width * 0.5f, room.width * 0.5f - bounds.max.x),
                Mathf.Min(bounds.min.z + room.depth * 0.5f, room.depth * 0.5f - bounds.max.z));
        }
    }

    public sealed class LayoutQualityReport
    {
        public float Score;
        public float CompletionScore;
        public float RelationScore;
        public float CirculationScore;
        public float BalanceScore;
        public float ConnectivityScore;
        public float SupportScore;

        public string Summary
        {
            get
            {
                return string.Format(
                    "quality {0:0.0}/100 | relations {1:0}% | circulation {2:0}% | connectivity {3:0}% | support {4:0}%",
                    Score,
                    RelationScore * 100f,
                    CirculationScore * 100f,
                    ConnectivityScore * 100f,
                    SupportScore * 100f);
            }
        }
    }

    public sealed class LayoutResult
    {
        public SceneSpec Spec;
        public LayoutReport LayoutReport;
        public LayoutQualityReport Quality;
        public int SelectedVariant;
        public int CandidateCount;
        public float NoveltyScore;
    }

    public sealed class SceneLayoutOptimizer
    {
        private readonly SceneLayoutEngine layout;
        private readonly LayoutEvaluator evaluator;

        public SceneLayoutOptimizer(SceneLayoutEngine layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            evaluator = new LayoutEvaluator();
        }

        public LayoutResult Optimize(
            SceneSpec plannedSpec,
            AssetCatalog catalog,
            int firstVariant,
            int candidateCount,
            SceneSpec referenceSpec = null)
        {
            if (plannedSpec == null) throw new ArgumentNullException(nameof(plannedSpec));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            candidateCount = Mathf.Clamp(candidateCount, 1, 8);

            LayoutResult best = null;
            float bestSelectionScore = float.MinValue;
            for (int i = 0; i < candidateCount; i++)
            {
                int variant = Mathf.Max(0, firstVariant + i);
                SceneSpec candidate = SceneSpecJson.FromJson(SceneSpecJson.ToJson(plannedSpec, false));
                LayoutReport layoutReport = layout.Layout(candidate, catalog, variant);
                LayoutQualityReport quality = evaluator.Evaluate(candidate, catalog);
                float novelty = EvaluateNovelty(candidate, referenceSpec);
                float selectionScore = quality.Score + novelty * 6f;
                if (best == null || selectionScore > bestSelectionScore + 0.001f)
                {
                    bestSelectionScore = selectionScore;
                    best = new LayoutResult
                    {
                        Spec = candidate,
                        LayoutReport = layoutReport,
                        Quality = quality,
                        SelectedVariant = variant,
                        CandidateCount = candidateCount,
                        NoveltyScore = novelty
                    };
                }
            }

            return best;
        }

        private static float EvaluateNovelty(SceneSpec candidate, SceneSpec reference)
        {
            if (reference == null || reference.placements == null || reference.placements.Count == 0) return 0f;
            float total = 0f;
            int count = 0;
            float roomScale = Mathf.Max(1f, new Vector2(candidate.room.width, candidate.room.depth).magnitude * 0.35f);
            for (int i = 0; i < candidate.placements.Count; i++)
            {
                PlacedObjectSpec current = candidate.placements[i];
                PlacedObjectSpec previous = null;
                for (int j = 0; j < reference.placements.Count; j++)
                {
                    if (reference.placements[j].id != current.id) continue;
                    previous = reference.placements[j];
                    break;
                }
                if (previous == null) continue;
                float positionChange = Vector3.Distance(current.position, previous.position) / roomScale;
                float rotationChange = Mathf.Abs(Mathf.DeltaAngle(current.rotationY, previous.rotationY)) / 180f;
                total += Mathf.Clamp01(positionChange * 0.75f + rotationChange * 0.25f);
                count++;
            }

            return count == 0 ? 0f : total / count;
        }
    }

    public sealed class LayoutEvaluator
    {
        private const float WalkCellSize = 0.4f;

        public LayoutQualityReport Evaluate(SceneSpec spec, AssetCatalog catalog)
        {
            SceneSpecDefaults.EnsureHouse(spec);
            if (spec.rooms.Count > 1) return EvaluateHouse(spec, catalog);
            return EvaluateSingleRoom(spec, catalog);
        }

        private static LayoutQualityReport EvaluateSingleRoom(SceneSpec spec, AssetCatalog catalog)
        {
            int requested = spec.objects == null ? 0 : spec.objects.Count;
            int placed = spec.placements == null ? 0 : spec.placements.Count;
            float completion = requested == 0 ? 1f : Mathf.Clamp01((float)placed / requested);
            float relations = EvaluateRelations(spec);
            float circulation = EvaluateCirculation(spec, catalog);
            float balance = EvaluateBalance(spec);
            float support = EvaluateSupport(spec);
            return new LayoutQualityReport
            {
                CompletionScore = completion,
                RelationScore = relations,
                CirculationScore = circulation,
                BalanceScore = balance,
                ConnectivityScore = 1f,
                SupportScore = support,
                Score = completion * 35f + relations * 25f + circulation * 15f + balance * 10f + 10f + support * 5f
            };
        }

        private static LayoutQualityReport EvaluateHouse(SceneSpec spec, AssetCatalog catalog)
        {
            float relation = 0f;
            float circulation = 0f;
            float balance = 0f;
            float support = 0f;
            int roomCount = 0;
            for (int roomIndex = 0; roomIndex < spec.rooms.Count; roomIndex++)
            {
                RoomSpec room = spec.rooms[roomIndex];
                SceneSpec local = new SceneSpec { room = room, roomType = room.type, style = spec.style };
                local.rooms.Add(room);
                for (int i = 0; i < spec.objects.Count; i++)
                    if (spec.objects[i].roomId == room.id) local.objects.Add(spec.objects[i]);
                for (int i = 0; i < spec.placements.Count; i++)
                {
                    if (spec.placements[i].roomId != room.id) continue;
                    PlacedObjectSpec placement = SceneLayoutEngine.Clone(spec.placements[i]);
                    placement.position -= room.center;
                    local.placements.Add(placement);
                }
                LayoutQualityReport roomQuality = EvaluateSingleRoom(local, catalog);
                relation += roomQuality.RelationScore;
                circulation += roomQuality.CirculationScore;
                balance += roomQuality.BalanceScore;
                support += roomQuality.SupportScore;
                roomCount++;
            }

            float completion = spec.objects.Count == 0
                ? 1f
                : Mathf.Clamp01((float)spec.placements.Count / spec.objects.Count);
            float divisor = Mathf.Max(1, roomCount);
            relation /= divisor;
            circulation /= divisor;
            balance /= divisor;
            support /= divisor;
            float connectivity = EvaluateConnectivity(spec);
            return new LayoutQualityReport
            {
                CompletionScore = completion,
                RelationScore = relation,
                CirculationScore = circulation,
                BalanceScore = balance,
                ConnectivityScore = connectivity,
                SupportScore = support,
                Score = completion * 35f + relation * 25f + circulation * 15f + balance * 10f + connectivity * 10f + support * 5f
            };
        }

        private static float EvaluateConnectivity(SceneSpec spec)
        {
            if (spec.rooms.Count <= 1) return 1f;
            Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>();
            for (int i = 0; i < spec.rooms.Count; i++) graph[spec.rooms[i].id] = new List<string>();
            for (int i = 0; i < spec.connections.Count; i++)
            {
                RoomConnectionSpec connection = spec.connections[i];
                if (!graph.ContainsKey(connection.roomAId) || !graph.ContainsKey(connection.roomBId)) continue;
                RoomSpec a = spec.rooms.Find(room => room.id == connection.roomAId);
                RoomSpec b = spec.rooms.Find(room => room.id == connection.roomBId);
                if (!RoomGeometry.HasPassage(a, b, connection)) continue;
                graph[connection.roomAId].Add(connection.roomBId);
                graph[connection.roomBId].Add(connection.roomAId);
            }
            HashSet<string> visited = new HashSet<string>();
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(spec.rooms[0].id);
            visited.Add(spec.rooms[0].id);
            while (queue.Count > 0)
            {
                List<string> neighbors = graph[queue.Dequeue()];
                for (int i = 0; i < neighbors.Count; i++)
                    if (visited.Add(neighbors[i])) queue.Enqueue(neighbors[i]);
            }
            return Mathf.Clamp01((float)visited.Count / spec.rooms.Count);
        }

        private static float EvaluateSupport(SceneSpec spec)
        {
            int supportedCount = 0;
            int validCount = 0;
            for (int i = 0; i < spec.placements.Count; i++)
            {
                PlacedObjectSpec item = spec.placements[i];
                if (item.relation != "on_top_of" && item.relation != "on_shelf") continue;
                supportedCount++;
                PlacedObjectSpec anchor = FindPlacement(spec.placements, item.anchorId);
                if (anchor == null) continue;
                Bounds itemBounds = item.Bounds;
                Bounds anchorBounds = anchor.Bounds;
                if (itemBounds.min.x >= anchorBounds.min.x - 0.025f && itemBounds.max.x <= anchorBounds.max.x + 0.025f &&
                    itemBounds.min.z >= anchorBounds.min.z - 0.025f && itemBounds.max.z <= anchorBounds.max.z + 0.025f &&
                    Mathf.Abs(itemBounds.min.y - anchorBounds.max.y) <= 0.04f) validCount++;
            }
            return supportedCount == 0 ? 1f : (float)validCount / supportedCount;
        }

        private static float EvaluateRelations(SceneSpec spec)
        {
            if (spec.placements == null || spec.placements.Count == 0) return 1f;
            float total = 0f;
            int count = 0;
            for (int i = 0; i < spec.placements.Count; i++)
            {
                PlacedObjectSpec placement = spec.placements[i];
                if (placement.category == "rug" || placement.category == "wall_art") continue;
                total += EvaluateRelation(placement, spec);
                count++;
            }

            return count == 0 ? 1f : total / count;
        }

        private static float EvaluateRelation(PlacedObjectSpec placement, SceneSpec spec)
        {
            PlacedObjectSpec anchor = FindPlacement(spec.placements, placement.anchorId);
            switch (placement.relation)
            {
                case "against_wall":
                    return 1f - Mathf.Clamp01(DistanceToNearestWall(placement.Bounds, spec.room) / 0.75f);
                case "near_window":
                    WallOpeningSpec window = FindOpening(spec.room, "window");
                    if (window == null) return 0.4f;
                    float axisDistance = window.wall == "left" || window.wall == "right"
                        ? Mathf.Abs(placement.position.z - window.center)
                        : Mathf.Abs(placement.position.x - window.center);
                    return 1f - Mathf.Clamp01(axisDistance / 1.5f);
                case "around_table":
                case "in_front_of_desk":
                    return anchor == null ? 0f : FacingAndDistanceScore(placement, anchor, 0.65f, 2f);
                case "in_front_of_sofa":
                    return anchor == null ? 0.5f : DirectionScore(anchor, placement, Vector3.back);
                case "opposite_sofa":
                    return anchor == null ? 0.5f : FacingAndDistanceScore(placement, anchor, 1.5f, 8f);
                case "beside_sofa":
                case "beside_bed":
                    return anchor == null ? 0.5f : DistanceBandScore(placement, anchor, 0.1f, 1.4f);
                case "left_of":
                    return anchor == null ? 0f : DirectionScore(anchor, placement, Vector3.left);
                case "right_of":
                    return anchor == null ? 0f : DirectionScore(anchor, placement, Vector3.right);
                case "in_front_of":
                    return anchor == null ? 0f : DirectionScore(anchor, placement, Vector3.back);
                case "behind":
                    return anchor == null ? 0f : DirectionScore(anchor, placement, Vector3.forward);
                case "corner":
                    float normalizedX = Mathf.Abs(placement.position.x) / Mathf.Max(0.01f, spec.room.width * 0.5f);
                    float normalizedZ = Mathf.Abs(placement.position.z) / Mathf.Max(0.01f, spec.room.depth * 0.5f);
                    return Mathf.Clamp01((normalizedX + normalizedZ) * 0.5f);
                case "center":
                    return 1f - Mathf.Clamp01(placement.position.magnitude / 2f);
                case "under_coffee_table":
                    return anchor == null ? 0.5f : 1f - Mathf.Clamp01(
                        Vector3.Distance(placement.position, anchor.position) / 0.5f);
                case "ceiling_center":
                    return Mathf.Clamp01(1f - new Vector2(placement.position.x, placement.position.z).magnitude / 1.5f) *
                        Mathf.Clamp01(1f - Mathf.Abs(placement.Bounds.max.y - spec.room.height) / 0.2f);
                case "on_top_of":
                case "on_shelf":
                    if (anchor == null) return 0f;
                    return Mathf.Abs(placement.Bounds.min.y - anchor.Bounds.max.y) <= 0.04f ? 1f : 0f;
                default:
                    return 0.8f;
            }
        }

        private static float FacingAndDistanceScore(
            PlacedObjectSpec placement,
            PlacedObjectSpec anchor,
            float minimumDistance,
            float maximumDistance)
        {
            Vector3 delta = anchor.position - placement.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f) return 0f;
            Vector3 front = Quaternion.Euler(0f, placement.rotationY, 0f) * Vector3.back;
            float facing = Mathf.Clamp01((Vector3.Dot(front, delta.normalized) + 1f) * 0.5f);
            float distance = delta.magnitude;
            float distanceScore = distance < minimumDistance
                ? Mathf.Clamp01(distance / minimumDistance)
                : 1f - Mathf.Clamp01((distance - minimumDistance) / Mathf.Max(0.01f, maximumDistance - minimumDistance));
            return facing * 0.65f + distanceScore * 0.35f;
        }

        private static float DirectionScore(
            PlacedObjectSpec anchor,
            PlacedObjectSpec placement,
            Vector3 expectedLocalDirection)
        {
            Vector3 expected = Quaternion.Euler(0f, anchor.rotationY, 0f) * expectedLocalDirection;
            Vector3 actual = placement.position - anchor.position;
            actual.y = 0f;
            if (actual.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Clamp01((Vector3.Dot(expected.normalized, actual.normalized) + 1f) * 0.5f);
        }

        private static float DistanceBandScore(
            PlacedObjectSpec placement,
            PlacedObjectSpec anchor,
            float minimumDistance,
            float maximumDistance)
        {
            float distance = Vector3.Distance(placement.position, anchor.position);
            if (distance < minimumDistance) return Mathf.Clamp01(distance / minimumDistance);
            return 1f - Mathf.Clamp01((distance - minimumDistance) / Mathf.Max(0.01f, maximumDistance - minimumDistance));
        }

        private static float EvaluateBalance(SceneSpec spec)
        {
            Vector3 weightedCenter = Vector3.zero;
            float totalWeight = 0f;
            for (int i = 0; i < spec.placements.Count; i++)
            {
                PlacedObjectSpec placement = spec.placements[i];
                if (placement.category == "rug" || placement.category == "wall_art" ||
                    placement.category == "ceiling_light" || placement.category == "table_lamp" ||
                    placement.category == "vase" || placement.category == "books" || placement.category == "decor_bowl" ||
                    placement.category == "computer_set" || placement.category == "flower_pot" ||
                    placement.category == "air_conditioner") continue;
                float weight = Mathf.Max(0.1f, placement.size.x * placement.size.z);
                weightedCenter += placement.position * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f) return 1f;
            weightedCenter /= totalWeight;
            float halfDiagonal = new Vector2(spec.room.width, spec.room.depth).magnitude * 0.5f;
            return 1f - Mathf.Clamp01(new Vector2(weightedCenter.x, weightedCenter.z).magnitude / halfDiagonal);
        }

        private static float EvaluateCirculation(SceneSpec spec, AssetCatalog catalog)
        {
            WallOpeningSpec door = FindOpening(spec.room, "door");
            if (door == null) return 1f;
            int columns = Mathf.Max(3, Mathf.FloorToInt(spec.room.width / WalkCellSize));
            int rows = Mathf.Max(3, Mathf.FloorToInt(spec.room.depth / WalkCellSize));
            bool[,] walkable = new bool[columns, rows];
            int walkableCount = 0;
            for (int x = 0; x < columns; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    Vector2 point = CellToPoint(x, z, columns, rows, spec.room);
                    walkable[x, z] = IsWalkable(point, spec.placements, catalog);
                    if (walkable[x, z]) walkableCount++;
                }
            }

            if (walkableCount == 0) return 0f;
            Vector2 doorPoint = GetDoorInteriorPoint(door, spec.room);
            Vector2Int start = FindNearestWalkable(doorPoint, walkable, columns, rows, spec.room);
            if (start.x < 0) return 0f;

            bool[,] visited = new bool[columns, rows];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited[start.x, start.y] = true;
            int reached = 0;
            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                reached++;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nextX = current.x + dx[direction];
                    int nextZ = current.y + dz[direction];
                    if (nextX < 0 || nextX >= columns || nextZ < 0 || nextZ >= rows) continue;
                    if (!walkable[nextX, nextZ] || visited[nextX, nextZ]) continue;
                    visited[nextX, nextZ] = true;
                    queue.Enqueue(new Vector2Int(nextX, nextZ));
                }
            }

            return Mathf.Clamp01((float)reached / walkableCount);
        }

        private static bool IsWalkable(
            Vector2 point,
            List<PlacedObjectSpec> placements,
            AssetCatalog catalog)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                PlacedObjectSpec placement = placements[i];
                if (placement.category == "rug" || placement.category == "wall_art" ||
                    placement.category == "ceiling_light" || placement.category == "table_lamp" ||
                    placement.category == "vase" || placement.category == "books" || placement.category == "decor_bowl" ||
                    placement.category == "computer_set" || placement.category == "flower_pot" ||
                    placement.category == "air_conditioner") continue;
                AssetDefinition asset = catalog.FindById(placement.assetId);
                AssetSpatialProfile profile = asset == null
                    ? AssetSpatialProfile.ForCategory(placement.category)
                    : asset.SpatialProfile;
                float sidePadding = Mathf.Clamp(profile.SideClearance, 0.12f, 0.5f);
                float frontClearance = Mathf.Clamp(profile.FrontClearance, 0.12f, 1.0f);
                Vector3 worldOffset = new Vector3(point.x - placement.position.x, 0f, point.y - placement.position.z);
                Vector3 local = Quaternion.Euler(0f, -placement.rotationY, 0f) * worldOffset;
                float halfWidth = placement.size.x * 0.5f + sidePadding;
                float halfDepth = placement.size.z * 0.5f;
                // Furniture faces local -Z and needs free space in front
                if (Mathf.Abs(local.x) <= halfWidth &&
                    local.z <= halfDepth + sidePadding && local.z >= -halfDepth - frontClearance)
                    return false;
            }

            return true;
        }

        private static Vector2 CellToPoint(int x, int z, int columns, int rows, RoomSpec room)
        {
            return new Vector2(
                -room.width * 0.5f + (x + 0.5f) * room.width / columns,
                -room.depth * 0.5f + (z + 0.5f) * room.depth / rows);
        }

        private static Vector2Int FindNearestWalkable(
            Vector2 point,
            bool[,] walkable,
            int columns,
            int rows,
            RoomSpec room)
        {
            Vector2Int best = new Vector2Int(-1, -1);
            float bestDistance = float.MaxValue;
            for (int x = 0; x < columns; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    if (!walkable[x, z]) continue;
                    float distance = (CellToPoint(x, z, columns, rows, room) - point).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = new Vector2Int(x, z);
                }
            }

            return best;
        }

        private static Vector2 GetDoorInteriorPoint(WallOpeningSpec door, RoomSpec room)
        {
            const float inset = 0.45f;
            switch (door.wall)
            {
                case "back": return new Vector2(door.center, room.depth * 0.5f - inset);
                case "left": return new Vector2(-room.width * 0.5f + inset, door.center);
                case "right": return new Vector2(room.width * 0.5f - inset, door.center);
                default: return new Vector2(door.center, -room.depth * 0.5f + inset);
            }
        }

        private static float DistanceToNearestWall(Bounds bounds, RoomSpec room)
        {
            float halfWidth = room.width * 0.5f;
            float halfDepth = room.depth * 0.5f;
            return Mathf.Min(
                Mathf.Min(bounds.min.x + halfWidth, halfWidth - bounds.max.x),
                Mathf.Min(bounds.min.z + halfDepth, halfDepth - bounds.max.z));
        }

        private static PlacedObjectSpec FindPlacement(List<PlacedObjectSpec> placements, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < placements.Count; i++)
                if (placements[i].id == id) return placements[i];
            return null;
        }

        private static WallOpeningSpec FindOpening(RoomSpec room, string type)
        {
            if (room.openings == null) return null;
            for (int i = 0; i < room.openings.Count; i++)
                if (room.openings[i].type == type) return room.openings[i];
            return null;
        }
    }
}
