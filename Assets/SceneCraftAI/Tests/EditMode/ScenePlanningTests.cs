using NUnit.Framework;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using SceneCraftAI.Layout;
using SceneCraftAI.Planning;
using UnityEngine;

namespace SceneCraftAI.Tests
{
    public sealed class ScenePlanningTests
    {
        [Test]
        public void BedroomPromptCreatesRequiredBedroomObjects()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("温馨卧室，带书桌和椅子");

            Assert.That(spec.roomType, Is.EqualTo("bedroom"));
            Assert.That(spec.objects.Exists(item => item.category == "bed"), Is.True);
            Assert.That(spec.objects.Exists(item => item.category == "desk"), Is.True);
            Assert.That(spec.objects.Exists(item => item.category == "chair"), Is.True);
        }

        [Test]
        public void AssetCatalogUsesCategoryAsHardFilter()
        {
            AssetCatalog catalog = new AssetCatalog();
            AssetDefinition result = catalog.FindBest(
                new SceneObjectRequest { category = "sofa", description = "natural sage modern sofa" },
                "natural");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Category, Is.EqualTo("sofa"));
            Assert.That(result.Id, Is.EqualTo("sofa_sage"));
        }

        [Test]
        public void AssetCatalogCarriesSpatialMetadataAndStyleVariants()
        {
            AssetCatalog catalog = new AssetCatalog();
            AssetDefinition table = catalog.FindBest(
                new SceneObjectRequest { category = "dining_table", description = "黑色工业风餐桌" },
                "industrial black");
            AssetDefinition wardrobe = catalog.FindById("wardrobe_oak");

            Assert.That(catalog.Definitions.Count, Is.GreaterThanOrEqualTo(31));
            Assert.That(table.Id, Is.EqualTo("dining_table_charcoal"));
            Assert.That(table.SpatialProfile.SideClearance, Is.GreaterThanOrEqualTo(0.8f));
            Assert.That(wardrobe.SpatialProfile.PrefersWall, Is.True);
            Assert.That(wardrobe.SpatialProfile.FrontClearance, Is.GreaterThan(0.5f));
        }

        [Test]
        public void LivingRoomLayoutStaysInsideRoomAndAvoidsSolidIntersections()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("温馨现代客厅，有植物和落地灯");
            LayoutReport report = new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            Assert.That(report.PlacedCount, Is.GreaterThanOrEqualTo(5));
            for (int i = 0; i < spec.placements.Count; i++)
            {
                PlacedObjectSpec current = spec.placements[i];
                if (current.category == "rug" || current.category == "wall_art") continue;
                Assert.That(current.Bounds.min.x, Is.GreaterThanOrEqualTo(-spec.room.width * 0.5f));
                Assert.That(current.Bounds.max.x, Is.LessThanOrEqualTo(spec.room.width * 0.5f));
                Assert.That(current.Bounds.min.z, Is.GreaterThanOrEqualTo(-spec.room.depth * 0.5f));
                Assert.That(current.Bounds.max.z, Is.LessThanOrEqualTo(spec.room.depth * 0.5f));

                for (int j = i + 1; j < spec.placements.Count; j++)
                {
                    PlacedObjectSpec other = spec.placements[j];
                    if (other.category == "rug" || other.category == "wall_art") continue;
                    Assert.That(current.Bounds.Intersects(other.Bounds), Is.False,
                        current.id + " overlaps " + other.id);
                }
            }
        }

        [Test]
        public void SceneSpecRoundTripsThroughJson()
        {
            SceneSpec original = new OfflineScenePlanner().Build("现代书房");
            new SceneLayoutEngine().Layout(original, new AssetCatalog());

            SceneSpec restored = SceneSpecJson.FromJson(SceneSpecJson.ToJson(original));

            Assert.That(restored.roomType, Is.EqualTo(original.roomType));
            Assert.That(restored.objects.Count, Is.EqualTo(original.objects.Count));
            Assert.That(restored.placements.Count, Is.EqualTo(original.placements.Count));
            Assert.That(restored.room.openings.Count, Is.GreaterThan(0));
        }

        [Test]
        public void DefaultRoomHasOffsetDoorAndWindowOpenings()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("现代客厅");

            Assert.That(spec.room.openings.Exists(item => item.type == "door" && item.wall == "front"), Is.True);
            Assert.That(spec.room.openings.Exists(item => item.type == "window" && item.wall == "right"), Is.True);
            Assert.That(spec.room.openings.Find(item => item.type == "door").center, Is.Not.EqualTo(0f));
        }

        [Test]
        public void DoorAndWindowOnTheSameWallAreSeparatedBeforeRendering()
        {
            RoomSpec room = new RoomSpec { id = "overlap_room", width = 6f, depth = 5f, height = 2.8f };
            room.openings.Add(new WallOpeningSpec
            {
                id = "door", type = "door", wall = "front", center = 0f,
                width = 1f, height = 2.1f, clearanceDepth = 1.05f
            });
            room.openings.Add(new WallOpeningSpec
            {
                id = "window", type = "window", wall = "front", center = 0.2f,
                width = 1.5f, height = 1.1f, sillHeight = 0.9f, clearanceDepth = 0.55f
            });

            SceneSpecDefaults.EnsureRoomOpenings(room, false);

            WallOpeningSpec door = room.openings.Find(item => item.id == "door");
            WallOpeningSpec window = room.openings.Find(item => item.id == "window");
            Assert.That(window, Is.Not.Null);
            float doorLeft = door.center - door.width * 0.5f;
            float doorRight = door.center + door.width * 0.5f;
            float windowLeft = window.center - window.width * 0.5f;
            float windowRight = window.center + window.width * 0.5f;
            Assert.That(windowRight + 0.13f <= doorLeft || windowLeft - 0.13f >= doorRight, Is.True);
        }

        [Test]
        public void PairedRoomDoorOpeningsShareOneWorldCoordinate()
        {
            SceneSpec spec = new OfflineScenePlanner().Build(
                "一套生活化住宅：客厅、主卧、次卧、厨房和卫生间");

            for (int i = 0; i < spec.connections.Count; i++)
            {
                RoomConnectionSpec connection = spec.connections[i];
                RoomSpec roomA = spec.rooms.Find(room => room.id == connection.roomAId);
                RoomSpec roomB = spec.rooms.Find(room => room.id == connection.roomBId);
                WallOpeningSpec openingA = roomA.openings.Find(opening => opening.connectsToRoomId == roomB.id);
                WallOpeningSpec openingB = roomB.openings.Find(opening => opening.connectsToRoomId == roomA.id);
                float worldA = openingA.wall == "left" || openingA.wall == "right"
                    ? roomA.center.z + openingA.center
                    : roomA.center.x + openingA.center;
                float worldB = openingB.wall == "left" || openingB.wall == "right"
                    ? roomB.center.z + openingB.center
                    : roomB.center.x + openingB.center;
                Assert.That(worldA, Is.EqualTo(worldB).Within(0.001f), connection.id);
            }
        }

        [Test]
        public void FurnitureCannotBlockDoorClearance()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("现代客厅");
            WallOpeningSpec door = spec.room.openings.Find(item => item.type == "door");
            Bounds clearance = SceneLayoutEngine.GetOpeningClearance(door, spec.room);
            PlacedObjectSpec candidate = new PlacedObjectSpec
            {
                id = "probe",
                category = "cabinet",
                position = new Vector3(clearance.center.x, 0f, clearance.center.z),
                size = new Vector3(0.6f, 1f, 0.4f)
            };
            string reason;

            bool valid = new SceneLayoutEngine().IsPlacementValid(
                candidate, spec.room, spec.placements, null, out reason);

            Assert.That(valid, Is.False);
            StringAssert.Contains("门或窗", reason);
        }

        [Test]
        public void DoorClearanceIncludesAUsableInteriorApproachPocket()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("现代客厅");
            WallOpeningSpec door = spec.room.openings.Find(item => item.type == "door");
            Bounds clearance = SceneLayoutEngine.GetOpeningClearance(door, spec.room);

            Assert.That(clearance.size.z, Is.GreaterThanOrEqualTo(1.35f));
            Assert.That(clearance.size.x, Is.GreaterThanOrEqualTo(door.width + 0.55f));

            PlacedObjectSpec sideBlocker = new PlacedObjectSpec
            {
                id = "side_blocker",
                category = "nightstand",
                position = new Vector3(
                    door.center + door.width * 0.5f + 0.16f,
                    0f,
                    clearance.center.z),
                size = new Vector3(0.18f, 0.5f, 0.25f)
            };

            bool valid = new SceneLayoutEngine().IsPlacementValid(
                sideBlocker, spec.room, spec.placements, null, out _);

            Assert.That(valid, Is.False, "Furniture beside the door leaf must not pinch the entrance path.");
        }

        [Test]
        public void FurnitureUsesSmallPhysicalGapInsteadOfInflatedCollisionBox()
        {
            PlacedObjectSpec existing = new PlacedObjectSpec
            {
                id = "bed",
                category = "bed",
                position = Vector3.zero,
                size = Vector3.one
            };
            PlacedObjectSpec nearby = new PlacedObjectSpec
            {
                id = "nightstand",
                category = "nightstand",
                position = new Vector3(1.06f, 0f, 0f),
                size = Vector3.one
            };
            RoomSpec room = new RoomSpec { width = 6f, depth = 5f, height = 2.8f };

            bool valid = new SceneLayoutEngine().IsPlacementValid(
                nearby, room, new System.Collections.Generic.List<PlacedObjectSpec> { existing }, null, out _);

            Assert.That(valid, Is.True, "A 6 cm visible gap should not be rejected by hidden padding.");
        }

        [Test]
        public void AgainstWallFurnitureCanSitFlushWithoutEnteringTheWall()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("卧室里只放一张床");
            spec.objects.RemoveAll(item => item.category != "bed");

            new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            PlacedObjectSpec bed = spec.placements.Find(item => item.category == "bed");
            float wallDistance = spec.room.depth * 0.5f - bed.Bounds.max.z;
            Assert.That(wallDistance, Is.InRange(0.02f, 0.031f));
            Assert.That(bed.Bounds.max.z, Is.LessThan(spec.room.depth * 0.5f));
        }

        [Test]
        public void RotatedBoundsSwapRectangularFootprint()
        {
            PlacedObjectSpec placement = new PlacedObjectSpec
            {
                size = new Vector3(2f, 1f, 0.5f),
                rotationY = 90f
            };

            Assert.That(placement.Bounds.size.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(placement.Bounds.size.z, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void CuratedPrefabLibraryContainsExpectedMappings()
        {
            PrefabAssetLibrary library = Resources.Load<PrefabAssetLibrary>("SceneCraftAssetLibrary");

            Assert.That(library, Is.Not.Null);
            Assert.That(library.Entries.Count, Is.EqualTo(29));
            Assert.That(library.FindPrefab("sofa_cream"), Is.Not.Null);
            Assert.That(library.FindPrefab("dining_table_oak"), Is.Not.Null);
            Assert.That(library.FindPrefab("dining_table_round"), Is.Not.Null);
            Assert.That(library.FindPrefab("dining_table_round").name, Is.EqualTo("tableRound"));
            Assert.That(library.FindPrefab("wall_art_sage"), Is.Null,
                "Wall art intentionally exercises the procedural fallback.");
        }

        [Test]
        public void LivingRoomSofaFacesIntoRoomAndRugCentersUnderTable()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("温馨客厅，有沙发、茶几和地毯");
            new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            PlacedObjectSpec sofa = spec.placements.Find(item => item.category == "sofa");
            PlacedObjectSpec table = spec.placements.Find(item => item.category == "coffee_table");
            PlacedObjectSpec rug = spec.placements.Find(item => item.category == "rug");
            Assert.That(sofa.position.z, Is.GreaterThan(0f));
            Assert.That(sofa.rotationY, Is.EqualTo(0f));
            Assert.That(rug.position.x, Is.EqualTo(table.position.x).Within(0.001f));
            Assert.That(rug.position.z, Is.EqualTo(table.position.z).Within(0.001f));
        }

        [Test]
        public void OfflinePromptUnderstandsTwoPlants()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("现代客厅，要两盆植物和一个沙发");
            new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            Assert.That(spec.objects.FindAll(item => item.category == "plant").Count, Is.EqualTo(2));
            Assert.That(spec.placements.FindAll(item => item.category == "plant").Count, Is.EqualTo(2));
        }

        [Test]
        public void OfflinePromptBuildsDiningGroupWithExplicitAnchor()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("窗户旁放一张餐桌，餐桌两边放两把椅子");

            SceneObjectRequest table = spec.objects.Find(item => item.category == "dining_table");
            var chairs = spec.objects.FindAll(item => item.category == "chair");
            Assert.That(table, Is.Not.Null);
            Assert.That(chairs.Count, Is.EqualTo(2));
            Assert.That(chairs.TrueForAll(item => item.relation == "around_table"), Is.True);
            Assert.That(chairs.TrueForAll(item => item.anchorId == table.id), Is.True);
        }

        [Test]
        public void RoundTablePromptLocksTheActualRoundPrefab()
        {
            const string prompt = "窗边只放一张圆桌";
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            AssetCatalog catalog = new AssetCatalog();

            int locks = catalog.ApplyAssetHints(spec, prompt);
            new SceneLayoutEngine().Layout(spec, catalog);

            SceneObjectRequest tableRequest = spec.objects.Find(item => item.category == "dining_table");
            PlacedObjectSpec table = spec.placements.Find(item => item.category == "dining_table");
            Assert.That(locks, Is.EqualTo(1));
            Assert.That(tableRequest.preferredAssetId, Is.EqualTo("dining_table_round"));
            Assert.That(table.assetId, Is.EqualTo("dining_table_round"));
            Assert.That(table.size.x, Is.EqualTo(table.size.z).Within(0.001f));
        }

        [Test]
        public void ExplicitFbxNameBypassesSemanticRanking()
        {
            SceneSpec spec = new SceneSpec();
            AssetCatalog catalog = new AssetCatalog();

            int locks = catalog.ApplyAssetHints(spec, "请使用 tableRound.fbx");

            Assert.That(locks, Is.EqualTo(1));
            Assert.That(spec.objects.Count, Is.EqualTo(1));
            Assert.That(spec.objects[0].category, Is.EqualTo("dining_table"));
            Assert.That(spec.objects[0].preferredAssetId, Is.EqualTo("dining_table_round"));
        }

        [Test]
        public void ExplicitAssetLockingIsIdempotentAcrossCriticPasses()
        {
            SceneSpec spec = new SceneSpec { prompt = "桌上放一个花瓶" };
            spec.objects.Add(new SceneObjectRequest
            {
                id = "vase_01", category = "vase", placement = "surface", relation = "on_top_of"
            });
            AssetCatalog catalog = new AssetCatalog();

            int first = catalog.ApplyAssetHints(spec, spec.prompt);
            int second = catalog.ApplyAssetHints(spec, spec.prompt);

            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(0));
            Assert.That(spec.objects.FindAll(item => item.category == "vase").Count, Is.EqualTo(1));
            Assert.That(spec.objects[0].preferredAssetId, Is.EqualTo("vase_ceramic"));
        }

        [Test]
        public void ExplicitPromptQuantitiesOverrideModelOverGeneration()
        {
            SceneSpec spec = new SceneSpec
            {
                prompt = "客厅桌上放一个花瓶，卧室有一个床头柜"
            };
            spec.objects.Add(new SceneObjectRequest { id = "vase_01", category = "vase", roomId = "living_01" });
            spec.objects.Add(new SceneObjectRequest { id = "vase_02", category = "vase", roomId = "living_01" });
            spec.objects.Add(new SceneObjectRequest { id = "nightstand_01", category = "nightstand", roomId = "bedroom_01" });
            spec.objects.Add(new SceneObjectRequest { id = "nightstand_02", category = "nightstand", roomId = "bedroom_01" });

            int changes = OfflineScenePlanner.ApplyPromptRules(spec, spec.prompt);

            Assert.That(changes, Is.EqualTo(2));
            Assert.That(spec.objects.FindAll(item => item.category == "vase").Count, Is.EqualTo(1));
            Assert.That(spec.objects.FindAll(item => item.category == "nightstand").Count, Is.EqualTo(1));
        }

        [Test]
        public void PreferredAssetIdOverridesStyleTagsWithinCategory()
        {
            AssetDefinition result = new AssetCatalog().FindBest(new SceneObjectRequest
            {
                category = "dining_table",
                description = "black industrial rectangular table",
                preferredAssetId = "dining_table_round"
            }, "industrial");

            Assert.That(result.Id, Is.EqualTo("dining_table_round"));
            Assert.That(result.SourceModelName, Is.EqualTo("tableRound"));
            Assert.That(result.DisplayName, Is.EqualTo("圆形餐桌"));
        }

        [Test]
        public void DiningGroupUsesWindowCoordinateAndKeepsChairsFacingTable()
        {
            SceneSpec spec = new SceneSpec();
            SceneSpecDefaults.EnsureOpenings(spec);
            spec.objects.Add(new SceneObjectRequest
            {
                id = "chair_01", category = "chair", relation = "around_table", anchorId = "table_01"
            });
            spec.objects.Add(new SceneObjectRequest
            {
                id = "chair_02", category = "chair", relation = "around_table", anchorId = "table_01"
            });
            spec.objects.Add(new SceneObjectRequest
            {
                id = "chair_03", category = "chair", relation = "around_table", anchorId = "table_01"
            });
            spec.objects.Add(new SceneObjectRequest
            {
                id = "chair_04", category = "chair", relation = "around_table", anchorId = "table_01"
            });
            spec.objects.Add(new SceneObjectRequest
            {
                id = "table_01", category = "dining_table", relation = "near_window"
            });

            LayoutReport report = new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            PlacedObjectSpec table = spec.placements.Find(item => item.id == "table_01");
            var chairs = spec.placements.FindAll(item => item.category == "chair");
            WallOpeningSpec window = spec.room.openings.Find(item => item.type == "window");
            Assert.That(report.PlacedCount, Is.EqualTo(5));
            Assert.That(table.position.z, Is.EqualTo(window.center).Within(0.001f));
            Assert.That(table.rotationY, Is.EqualTo(90f));
            Assert.That(chairs.Count, Is.EqualTo(4));
            Assert.That(Vector3.Distance(chairs[0].position, chairs[1].position), Is.GreaterThan(1f));
            for (int i = 0; i < chairs.Count; i++)
            {
                Vector3 chairFront = Quaternion.Euler(0f, chairs[i].rotationY, 0f) * Vector3.back;
                Vector3 directionToTable = (table.position - chairs[i].position).normalized;
                Assert.That(Vector3.Dot(chairFront, directionToTable), Is.GreaterThan(0.98f));
                Assert.That(chairs[i].anchorId, Is.EqualTo(table.id));
            }
        }

        [Test]
        public void NegativeAdditionListDoesNotCreateForbiddenFurniture()
        {
            const string prompt = "创建一个现代客厅，只放一张沙发、一张茶几、一个电视柜、两盆植物和一个花瓶。" +
                "花瓶放在茶几上。不要生成第二个花瓶，不要增加餐桌、椅子、书桌、床或其他家具。";
            SceneSpec spec = new SceneSpec { prompt = prompt };
            spec.objects.Add(new SceneObjectRequest { id = "sofa_01", category = "sofa" });
            spec.objects.Add(new SceneObjectRequest { id = "coffee_01", category = "coffee_table" });
            spec.objects.Add(new SceneObjectRequest { id = "tv_01", category = "tv_stand" });
            spec.objects.Add(new SceneObjectRequest { id = "plant_01", category = "plant" });
            spec.objects.Add(new SceneObjectRequest { id = "vase_01", category = "vase" });
            spec.objects.Add(new SceneObjectRequest { id = "bed_wrong", category = "bed" });
            spec.objects.Add(new SceneObjectRequest { id = "table_wrong", category = "dining_table" });
            spec.objects.Add(new SceneObjectRequest { id = "chair_wrong", category = "chair" });
            spec.objects.Add(new SceneObjectRequest { id = "desk_wrong", category = "desk" });

            OfflineScenePlanner.ApplyPromptRules(spec, prompt);

            Assert.That(spec.objects.FindAll(item => item.category == "sofa").Count, Is.EqualTo(1));
            Assert.That(spec.objects.FindAll(item => item.category == "coffee_table").Count, Is.EqualTo(1));
            Assert.That(spec.objects.FindAll(item => item.category == "tv_stand").Count, Is.EqualTo(1));
            Assert.That(spec.objects.FindAll(item => item.category == "plant").Count, Is.EqualTo(2));
            Assert.That(spec.objects.FindAll(item => item.category == "vase").Count, Is.EqualTo(1));
            Assert.That(spec.objects.Exists(item => item.category == "bed"), Is.False);
            Assert.That(spec.objects.Exists(item => item.category == "dining_table"), Is.False);
            Assert.That(spec.objects.Exists(item => item.category == "chair"), Is.False);
            Assert.That(spec.objects.Exists(item => item.category == "desk"), Is.False);
        }

        [Test]
        public void RestrictiveOnlyPromptRemovesContextualDefaults()
        {
            const string prompt = "现代客厅，只放一张沙发和两盆植物，不要其他家具";
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            OfflineScenePlanner.ApplyPromptRules(spec, prompt);

            Assert.That(spec.objects.FindAll(item => item.category == "sofa").Count, Is.EqualTo(1));
            Assert.That(spec.objects.FindAll(item => item.category == "plant").Count, Is.EqualTo(2));
            Assert.That(spec.objects.Count, Is.EqualTo(3));
        }

        [Test]
        public void LockedFurnitureSurvivesAlternativeLayoutOptimization()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("现代客厅，有沙发、茶几和电视柜");
            AssetCatalog catalog = new AssetCatalog();
            SceneLayoutEngine engine = new SceneLayoutEngine();
            engine.Layout(spec, catalog);
            PlacedObjectSpec sofa = spec.placements.Find(item => item.category == "sofa");
            SceneObjectRequest sofaRequest = spec.objects.Find(item => item.id == sofa.id);
            sofaRequest.locked = true;
            sofaRequest.lockedPosition = sofa.position;
            sofaRequest.lockedRotationY = sofa.rotationY;
            spec.placements.Clear();

            LayoutResult result = new SceneLayoutOptimizer(engine).Optimize(spec, catalog, 4, 4);
            PlacedObjectSpec relaidSofa = result.Spec.placements.Find(item => item.id == sofa.id);

            Assert.That(relaidSofa.locked, Is.True);
            Assert.That(relaidSofa.position, Is.EqualTo(sofaRequest.lockedPosition));
            Assert.That(relaidSofa.rotationY, Is.EqualTo(sofaRequest.lockedRotationY));
        }

        [Test]
        public void CriticRequestContainsDeterministicMultiViewObservation()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("现代客厅，有沙发和茶几");
            AssetCatalog catalog = new AssetCatalog();
            new SceneLayoutEngine().Layout(spec, catalog);

            string body = DeepSeekProtocol.BuildReviewRequest(
                spec, new LayoutEvaluator().Evaluate(spec, catalog), null);

            StringAssert.Contains("TOP(x,z)", body);
            StringAssert.Contains("FRONT(x,y|depth=z)", body);
            StringAssert.Contains("LEFT(z,y|depth=x)", body);
            StringAssert.Contains("RIGHT(-z,y|depth=-x)", body);
        }

        [Test]
        public void SceneAuditDetectsAChairTurnedAwayFromItsTable()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("餐厅只放一张餐桌和四把椅子");
            AssetCatalog catalog = new AssetCatalog();
            new SceneLayoutEngine().Layout(spec, catalog);
            PlacedObjectSpec chair = spec.placements.Find(item =>
                item.category == "chair" && !string.IsNullOrWhiteSpace(item.anchorId));
            Assert.That(chair, Is.Not.Null);

            chair.rotationY += 180f;
            SceneAuditReport report = new LayoutAudit().Audit(spec, catalog);

            Assert.That(report.Issues.Exists(issue =>
                issue.Contains(chair.id) && issue.Contains("does not face")), Is.True);
        }

        [TestCase("现代客厅，只放一张沙发、一张茶几和一个电视柜")]
        [TestCase("温馨卧室，一张床、一个床头柜和一个衣柜")]
        [TestCase("现代书房，一张书桌、一把椅子和一个书架")]
        [TestCase("明亮餐厅，一张圆桌和四把椅子")]
        [TestCase("客厅放一张沙发、两盆植物和一盏落地灯")]
        [TestCase("卧室放一张床、两个床头柜和一盏台灯")]
        [TestCase("书房只放一张书桌、一把椅子和一摞书")]
        [TestCase("餐厅只放一张餐桌、两把椅子和一个花瓶")]
        [TestCase("无窗客厅，只放一张沙发和一张茶几")]
        [TestCase("空客厅，不要家具")]
        [TestCase("两室一厅，客厅有沙发，卧室有床")]
        [TestCase("一个客厅、一个卧室和一个书房")]
        [TestCase("开放式客厅和餐厅，餐桌周围四把椅子")]
        [TestCase("现代卧室，床头靠墙，衣柜靠另一面墙")]
        [TestCase("客厅不要植物，只放沙发、茶几和电视柜")]
        [TestCase("客厅只放两盆植物，不要床和书桌")]
        [TestCase("书房靠窗放书桌，椅子朝向书桌")]
        [TestCase("餐桌靠窗，桌面放一个花瓶，周围放四把椅子")]
        [TestCase("小卧室只放一张床和一个床头柜")]
        [TestCase("现代客厅，有沙发、茶几、电视柜、地毯和挂画")]
        public void PortfolioPromptRegressionMatrixBuildsValidScene(string prompt)
        {
            SceneSpec spec = new OfflineScenePlanner().Build(prompt);
            OfflineScenePlanner.ApplyPromptRules(spec, prompt);
            AssetCatalog catalog = new AssetCatalog();
            LayoutReport report = new SceneLayoutEngine().Layout(spec, catalog);
            LayoutQualityReport quality = new LayoutEvaluator().Evaluate(spec, catalog);

            Assert.That(report.PlacedCount, Is.LessThanOrEqualTo(report.RequestedCount));
            Assert.That(quality.ConnectivityScore, Is.EqualTo(1f));
            Assert.That(quality.Score, Is.GreaterThanOrEqualTo(45f));
        }

        [Test]
        public void LayoutOptimizerScoresCandidatesAndCanPreferNovelAlternative()
        {
            SceneSpec planned = new OfflineScenePlanner().Build("现代客厅，有沙发、茶几、电视柜、植物和落地灯");
            AssetCatalog catalog = new AssetCatalog();
            SceneLayoutOptimizer optimizer = new SceneLayoutOptimizer(new SceneLayoutEngine());

            LayoutResult first = optimizer.Optimize(planned, catalog, 0, 4);
            LayoutResult alternative = optimizer.Optimize(planned, catalog, 4, 4, first.Spec);

            Assert.That(first.Quality.Score, Is.InRange(0f, 100f));
            Assert.That(first.LayoutReport.PlacedCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(alternative.NoveltyScore, Is.GreaterThan(0.05f));
            Assert.That(alternative.Quality.CompletionScore, Is.EqualTo(1f));
        }

        [Test]
        public void OfflinePromptSupportsRemovingFurnitureAndWindow()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("无窗客厅，不要植物");

            Assert.That(spec.room.hasWindow, Is.False);
            Assert.That(spec.room.openings.Exists(item => item.type == "window"), Is.False);
            Assert.That(spec.objects.Exists(item => item.category == "plant"), Is.False);
        }

        [Test]
        public void NightstandCountDoesNotChangeBedCount()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("温馨卧室，要两个床头柜");

            Assert.That(spec.objects.FindAll(item => item.category == "bed").Count, Is.EqualTo(1));
            Assert.That(spec.objects.FindAll(item => item.category == "nightstand").Count, Is.EqualTo(2));
        }

        [Test]
        public void OfflineEmptyRoomDoesNotRestoreDefaultFurniture()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("一个温馨现代的空客厅");

            Assert.That(spec.roomType, Is.EqualTo("living_room"));
            Assert.That(spec.objects, Is.Empty);
        }

        [Test]
        public void DeepSeekRequestUsesJsonModeWithoutMinimumFurnitureRule()
        {
            string body = DeepSeekProtocol.BuildPlanRequest("一个温馨现代的空客厅", null);

            StringAssert.Contains("\"model\":\"deepseek-v4-flash\"", body);
            StringAssert.Contains("\"response_format\":{\"type\":\"json_object\"}", body);
            StringAssert.Contains("\"thinking\":{\"type\":\"disabled\"}", body);
            StringAssert.Contains("一个温馨现代的空客厅", body);
            StringAssert.Contains("preferredAssetId", body);
            StringAssert.Contains("tableRound=dining_table_round", body);
            StringAssert.DoesNotContain("Keep 5 to 12 objects", body);
        }

        [Test]
        public void DeepSeekCriticRequestIsBoundedToSemanticCorrections()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("现代客厅");
            new SceneLayoutEngine().Layout(spec, new AssetCatalog());
            string body = DeepSeekProtocol.BuildReviewRequest(
                spec,
                new LayoutEvaluator().Evaluate(spec, new AssetCatalog()),
                null);

            StringAssert.Contains("critic pass", body);
            StringAssert.Contains("Do not add or remove objects", body);
            StringAssert.Contains("\"response_format\":{\"type\":\"json_object\"}", body);
        }

        [Test]
        public void DeepSeekEmptyRoomResponseKeepsFurnitureListEmpty()
        {
            string response = "{\"choices\":[{\"message\":{\"content\":\"{\\\"prompt\\\":\\\"ignored\\\"," +
                "\\\"roomType\\\":\\\"living_room\\\",\\\"style\\\":\\\"warm modern\\\"," +
                "\\\"room\\\":{\\\"width\\\":6,\\\"depth\\\":5,\\\"height\\\":2.8," +
                "\\\"hasWindow\\\":false},\\\"objects\\\":[]}\"}}]}";

            string sceneJson = DeepSeekProtocol.ReadSceneJson(response);
            SceneSpec spec = DeepSeekProtocol.ParseAndNormalize(sceneJson, "一个空客厅");

            Assert.That(spec.prompt, Is.EqualTo("一个空客厅"));
            Assert.That(spec.objects, Is.Empty);
            Assert.That(spec.room.hasWindow, Is.False);
        }

        [Test]
        public void DeepSeekResponseDropsUnsupportedAssetsAndRepairsRelations()
        {
            string sceneJson = "{\"prompt\":\"ignored\",\"roomType\":\"lounge\",\"style\":\"modern\"," +
                "\"room\":{\"width\":6,\"depth\":5,\"height\":2.8,\"hasWindow\":true},\"objects\":[" +
                "{\"id\":\"sofa_a\",\"category\":\"sofa\",\"description\":\"cream sofa\"," +
                "\"placement\":\"ceiling\",\"relation\":\"invented_relation\",\"required\":true}," +
                "{\"id\":\"dragon_01\",\"category\":\"dragon\",\"description\":\"unsupported\"," +
                "\"placement\":\"floor\",\"relation\":\"center\",\"required\":true}]}";

            SceneSpec spec = DeepSeekProtocol.ParseAndNormalize(sceneJson, "一个沙发");

            Assert.That(spec.roomType, Is.EqualTo("living_room"));
            Assert.That(spec.objects.Count, Is.EqualTo(1));
            Assert.That(spec.objects[0].category, Is.EqualTo("sofa"));
            Assert.That(spec.objects[0].placement, Is.EqualTo("floor"));
            Assert.That(spec.objects[0].relation, Is.EqualTo("auto"));
        }

        [Test]
        public void DeepSeekDiningAliasesNormalizeAndRepairChairAnchor()
        {
            string sceneJson = "{\"prompt\":\"ignored\",\"roomType\":\"living_room\",\"style\":\"modern\"," +
                "\"room\":{\"width\":6,\"depth\":5,\"height\":2.8,\"hasWindow\":true},\"objects\":[" +
                "{\"id\":\"chair_01\",\"category\":\"chair\",\"relation\":\"beside_table\"}," +
                "{\"id\":\"table_01\",\"category\":\"table\",\"relation\":\"by_window\"}]}";

            SceneSpec spec = DeepSeekProtocol.ParseAndNormalize(sceneJson, "窗边餐桌旁放椅子");

            SceneObjectRequest chair = spec.objects.Find(item => item.category == "chair");
            SceneObjectRequest table = spec.objects.Find(item => item.category == "dining_table");
            Assert.That(table, Is.Not.Null);
            Assert.That(table.relation, Is.EqualTo("near_window"));
            Assert.That(chair.relation, Is.EqualTo("around_table"));
            Assert.That(chair.anchorId, Is.EqualTo(table.id));
        }

        [Test]
        public void DeepSeekRejectsHallucinatedOrWrongCategoryAssetLocks()
        {
            string sceneJson = "{\"roomType\":\"living_room\",\"style\":\"modern\"," +
                "\"room\":{\"width\":6,\"depth\":5,\"height\":2.8,\"hasWindow\":true},\"objects\":[" +
                "{\"id\":\"table_01\",\"category\":\"dining_table\",\"preferredAssetId\":\"dining_table_round\"}," +
                "{\"id\":\"sofa_01\",\"category\":\"sofa\",\"preferredAssetId\":\"dining_table_round\"}," +
                "{\"id\":\"chair_01\",\"category\":\"chair\",\"preferredAssetId\":\"invented_prefab\"}]}";

            SceneSpec spec = DeepSeekProtocol.ParseAndNormalize(sceneJson, "使用圆桌");

            Assert.That(spec.objects[0].preferredAssetId, Is.EqualTo("dining_table_round"));
            Assert.That(spec.objects[1].preferredAssetId, Is.Empty);
            Assert.That(spec.objects[2].preferredAssetId, Is.Empty);
        }

        [Test]
        public void TwoBedroomOneLivingPromptBuildsConnectedHouseTopology()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("两室一厅，客厅和第一间卧室用门连接");

            Assert.That(spec.roomType, Is.EqualTo("house"));
            Assert.That(spec.rooms.Count, Is.EqualTo(3));
            Assert.That(spec.rooms.FindAll(room => room.type == "bedroom").Count, Is.EqualTo(2));
            Assert.That(spec.connections.Count, Is.EqualTo(2));
            Assert.That(spec.objects.TrueForAll(item => !string.IsNullOrWhiteSpace(item.roomId)), Is.True);
            for (int i = 0; i < spec.rooms.Count; i++)
            for (int j = i + 1; j < spec.rooms.Count; j++)
            {
                RoomSpec a = spec.rooms[i];
                RoomSpec b = spec.rooms[j];
                bool separatedX = Mathf.Abs(a.center.x - b.center.x) >= (a.width + b.width) * 0.5f - 0.001f;
                bool separatedZ = Mathf.Abs(a.center.z - b.center.z) >= (a.depth + b.depth) * 0.5f - 0.001f;
                Assert.That(separatedX || separatedZ, Is.True, a.id + " overlaps " + b.id);
            }
        }

        [Test]
        public void SimpleRoomSelectionExpandsIntoDenseStaggeredLifestyleHouse()
        {
            SceneSpec spec = new OfflineScenePlanner().Build(
                "一套生活化住宅：客厅、主卧、次卧、厨房和卫生间");

            Assert.That(spec.roomType, Is.EqualTo("house"));
            Assert.That(spec.rooms.Count, Is.EqualTo(5));
            Assert.That(spec.connections.Count, Is.EqualTo(4));
            Assert.That(spec.objects.Count, Is.GreaterThanOrEqualTo(50));
            Assert.That(spec.objects.Exists(item => item.category == "toilet"), Is.True);
            Assert.That(spec.objects.Exists(item => item.category == "computer_set"), Is.True);
            Assert.That(spec.objects.Exists(item => item.category == "air_conditioner"), Is.True);
            Assert.That(spec.objects.Exists(item => item.category == "flower_pot"), Is.True);

            float minimumX = float.MaxValue;
            float maximumX = float.MinValue;
            float minimumZ = float.MaxValue;
            float maximumZ = float.MinValue;
            float occupiedArea = 0f;
            for (int i = 0; i < spec.rooms.Count; i++)
            {
                RoomSpec room = spec.rooms[i];
                minimumX = Mathf.Min(minimumX, room.center.x - room.width * 0.5f);
                maximumX = Mathf.Max(maximumX, room.center.x + room.width * 0.5f);
                minimumZ = Mathf.Min(minimumZ, room.center.z - room.depth * 0.5f);
                maximumZ = Mathf.Max(maximumZ, room.center.z + room.depth * 0.5f);
                occupiedArea += room.width * room.depth;
            }
            float envelopeArea = (maximumX - minimumX) * (maximumZ - minimumZ);
            Assert.That(occupiedArea / envelopeArea, Is.LessThan(0.78f),
                "The portfolio house should have a visibly non-rectangular multi-wing footprint.");
        }

        [Test]
        public void LifestyleHouseUsesExpandedProceduralAssetCatalog()
        {
            AssetCatalog catalog = new AssetCatalog();
            string[] ids =
            {
                "toilet_porcelain", "sink_vanity_oak", "shower_glass", "kitchen_counter_oak",
                "refrigerator_modern", "computer_desktop_set", "clothes_rack_oak",
                "standing_fan", "air_conditioner_wall", "flower_pot_colorful"
            };

            for (int i = 0; i < ids.Length; i++) Assert.That(catalog.FindById(ids[i]), Is.Not.Null, ids[i]);
            Assert.That(catalog.Definitions.Count, Is.GreaterThanOrEqualTo(48));
        }

        [Test]
        public void RoomOnlyPromptDoesNotRequireManualFurnitureList()
        {
            SceneSpec spec = new OfflineScenePlanner().Build(
                "只要一个客厅、两个卧室、一个厨房和一个卫生间");
            OfflineScenePlanner.ApplyPromptRules(spec, spec.prompt);

            Assert.That(spec.rooms.Count, Is.EqualTo(5));
            Assert.That(spec.rooms.FindAll(room => room.type == "bedroom").Count, Is.EqualTo(2));
            Assert.That(spec.objects.Count, Is.GreaterThanOrEqualTo(50));
            Assert.That(spec.objects.Exists(item => item.category == "bed"), Is.True);
            Assert.That(spec.objects.Exists(item => item.category == "toilet"), Is.True);
        }

        [Test]
        public void LifestyleHouseLayoutPlacesMostRequestedObjectsWithoutBlockingDoors()
        {
            SceneSpec spec = new OfflineScenePlanner().Build(
                "一套生活化住宅：客厅、主卧、次卧、厨房和卫生间");
            AssetCatalog catalog = new AssetCatalog();

            LayoutReport report = new SceneLayoutEngine().Layout(spec, catalog, 2);

            Assert.That(report.RequestedCount, Is.GreaterThanOrEqualTo(50));
            Assert.That(report.PlacedCount, Is.GreaterThanOrEqualTo(44));
            SceneAuditReport audit = new LayoutAudit().Audit(spec, catalog);
            Assert.That(audit.Issues.Exists(issue => issue.Contains("blocks opening")), Is.False);
        }

        [Test]
        public void MultiRoomLayoutKeepsObjectsInsideTheirAssignedRoom()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("一个客厅、一个卧室和一个书房");
            LayoutReport report = new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            Assert.That(report.PlacedCount, Is.GreaterThanOrEqualTo(12));
            for (int i = 0; i < spec.placements.Count; i++)
            {
                PlacedObjectSpec placement = spec.placements[i];
                RoomSpec room = spec.rooms.Find(item => item.id == placement.roomId);
                Assert.That(room, Is.Not.Null, placement.id + " has no valid room assignment");
                Bounds localBounds = placement.Bounds;
                localBounds.center -= room.center;
                Assert.That(localBounds.min.x, Is.GreaterThanOrEqualTo(-room.width * 0.5f - 0.001f));
                Assert.That(localBounds.max.x, Is.LessThanOrEqualTo(room.width * 0.5f + 0.001f));
                Assert.That(localBounds.min.z, Is.GreaterThanOrEqualTo(-room.depth * 0.5f - 0.001f));
                Assert.That(localBounds.max.z, Is.LessThanOrEqualTo(room.depth * 0.5f + 0.001f));
            }
        }

        [Test]
        public void MultiRoomLayoutKeepsEveryDoorApproachClear()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("一个客厅、一个卧室和一个书房");
            new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            for (int roomIndex = 0; roomIndex < spec.rooms.Count; roomIndex++)
            {
                RoomSpec room = spec.rooms[roomIndex];
                for (int openingIndex = 0; openingIndex < room.openings.Count; openingIndex++)
                {
                    WallOpeningSpec opening = room.openings[openingIndex];
                    if (opening.type != "door" && opening.type != "open") continue;
                    Bounds clearance = SceneLayoutEngine.GetOpeningClearance(opening, room);
                    for (int placementIndex = 0; placementIndex < spec.placements.Count; placementIndex++)
                    {
                        PlacedObjectSpec placement = spec.placements[placementIndex];
                        if (placement.roomId != room.id || placement.category == "rug" ||
                            placement.category == "wall_art") continue;
                        PlacedObjectSpec local = SceneLayoutEngine.Clone(placement);
                        local.position -= room.center;
                        Assert.That(local.Bounds.Intersects(clearance), Is.False,
                            placement.id + " blocks " + opening.id);
                    }
                }
            }
        }

        [Test]
        public void BedroomKeepsBedAndWardrobeOnSeparateSolidWalls()
        {
            SceneSpec spec = new SceneSpec { roomType = "bedroom" };
            spec.room.id = "bedroom_01";
            spec.room.type = "bedroom";
            spec.room.width = 5.2f;
            spec.room.depth = 4.4f;
            spec.room.openings.Clear();
            spec.room.openings.Add(new WallOpeningSpec
            {
                id = "bedroom_door", type = "door", wall = "left", center = -1f,
                width = 0.95f, height = 2.1f, clearanceDepth = 1.05f
            });
            spec.room.openings.Add(new WallOpeningSpec
            {
                id = "bedroom_window", type = "window", wall = "back", center = 0f,
                width = 1.5f, height = 1.15f, sillHeight = 0.9f, clearanceDepth = 0.55f
            });
            spec.objects.Add(new SceneObjectRequest
            {
                id = "bed_01", roomId = spec.room.id, category = "bed", relation = "against_wall"
            });
            spec.objects.Add(new SceneObjectRequest
            {
                id = "wardrobe_01", roomId = spec.room.id, category = "wardrobe", relation = "against_wall"
            });

            new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            PlacedObjectSpec bed = spec.placements.Find(item => item.category == "bed");
            PlacedObjectSpec wardrobe = spec.placements.Find(item => item.category == "wardrobe");
            Assert.That(bed, Is.Not.Null);
            Assert.That(wardrobe, Is.Not.Null);
            Assert.That(NearestWall(bed.Bounds, spec.room), Is.EqualTo("right"));
            Assert.That(NearestWall(wardrobe.Bounds, spec.room), Is.EqualTo("front"));
        }

        [Test]
        public void SurfaceAndCeilingStagesProducePhysicallySupportedObjects()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("现代客厅");
            new SceneLayoutEngine().Layout(spec, new AssetCatalog());

            PlacedObjectSpec ceilingLight = spec.placements.Find(item => item.category == "ceiling_light");
            PlacedObjectSpec vase = spec.placements.Find(item => item.category == "vase");
            Assert.That(ceilingLight, Is.Not.Null);
            Assert.That(ceilingLight.Bounds.max.y, Is.EqualTo(spec.room.height).Within(0.01f));
            Assert.That(vase, Is.Not.Null);
            PlacedObjectSpec table = spec.placements.Find(item => item.id == vase.anchorId);
            Assert.That(table, Is.Not.Null);
            Assert.That(vase.Bounds.min.y, Is.EqualTo(table.Bounds.max.y).Within(0.01f));
            Assert.That(vase.Bounds.min.x, Is.GreaterThanOrEqualTo(table.Bounds.min.x - 0.025f));
            Assert.That(vase.Bounds.max.x, Is.LessThanOrEqualTo(table.Bounds.max.x + 0.025f));
        }

        [Test]
        public void HouseJsonRoundTripPreservesRoomsConnectionsAndAssignments()
        {
            SceneSpec original = new OfflineScenePlanner().Build("开放式客厅和餐厅，外加一间卧室");
            SceneSpec restored = SceneSpecJson.FromJson(SceneSpecJson.ToJson(original));

            Assert.That(restored.rooms.Count, Is.EqualTo(original.rooms.Count));
            Assert.That(restored.connections.Count, Is.EqualTo(original.connections.Count));
            Assert.That(restored.connections[0].type, Is.EqualTo("open"));
            Assert.That(restored.objects[0].roomId, Is.EqualTo(original.objects[0].roomId));
        }

        [Test]
        public void HouseQualityIncludesConnectivityAndSupportValidation()
        {
            SceneSpec spec = new OfflineScenePlanner().Build("一个客厅、一个卧室和一个书房");
            AssetCatalog catalog = new AssetCatalog();
            new SceneLayoutEngine().Layout(spec, catalog);

            LayoutQualityReport quality = new LayoutEvaluator().Evaluate(spec, catalog);

            Assert.That(quality.ConnectivityScore, Is.EqualTo(1f));
            Assert.That(quality.SupportScore, Is.EqualTo(1f));
            Assert.That(quality.Score, Is.GreaterThan(70f));
        }

        private static string NearestWall(Bounds bounds, RoomSpec room)
        {
            float back = room.depth * 0.5f - bounds.max.z;
            float left = bounds.min.x + room.width * 0.5f;
            float right = room.width * 0.5f - bounds.max.x;
            float front = bounds.min.z + room.depth * 0.5f;
            float minimum = Mathf.Min(Mathf.Min(back, left), Mathf.Min(right, front));
            if (Mathf.Approximately(minimum, left)) return "left";
            if (Mathf.Approximately(minimum, right)) return "right";
            if (Mathf.Approximately(minimum, front)) return "front";
            return "back";
        }
    }
}
