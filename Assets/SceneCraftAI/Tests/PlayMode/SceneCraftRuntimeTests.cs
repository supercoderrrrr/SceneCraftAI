using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using SceneCraftAI.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneCraftAI.Tests
{
    public sealed class SceneCraftRuntimeTests
    {
        [UnityTest]
        public IEnumerator RoomBuilderCreatesVisibleBrownDoorAssetInsideTheOpening()
        {
            RoomSpec room = new RoomSpec
            {
                id = "door_visual_room", width = 5f, depth = 4f, height = 2.8f, hasWindow = false
            };
            room.openings.Add(new WallOpeningSpec
            {
                id = "front_door", type = "door", wall = "front", center = 0.4f,
                width = 0.95f, height = 2.1f, clearanceDepth = 1.05f
            });
            GameObject parent = new GameObject("Door Visual Test");

            new RoomViewBuilder().Build(room, parent.transform);

            Transform[] children = parent.GetComponentsInChildren<Transform>();
            Transform panel = System.Array.Find(children, child => child.name.Contains("DoorAsset_Panel"));
            Assert.That(panel, Is.Not.Null);
            Color panelColor = panel.GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(panelColor.r, Is.GreaterThan(panelColor.g));
            Assert.That(panelColor.g, Is.GreaterThan(panelColor.b));
            Assert.That(panel.localScale.x, Is.GreaterThan(0.6f));
            Assert.That(panel.localScale.y, Is.GreaterThan(1.8f));

            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnequalConnectedRoomsShareOneDoorAndOneOverlapWall()
        {
            RoomSpec smallRoom = new RoomSpec
            {
                id = "a_small", width = 4f, depth = 3f, height = 2.8f,
                center = Vector3.zero, hasWindow = false
            };
            RoomSpec largeRoom = new RoomSpec
            {
                id = "b_large", width = 5f, depth = 6f, height = 2.8f,
                center = new Vector3(4.5f, 0f, 0.75f), hasWindow = false
            };
            smallRoom.openings.Add(new WallOpeningSpec
            {
                id = "a_to_b", type = "door", wall = "right", center = 0f,
                width = 0.95f, height = 2.1f, connectsToRoomId = largeRoom.id
            });
            largeRoom.openings.Add(new WallOpeningSpec
            {
                id = "b_to_a", type = "door", wall = "left", center = -0.75f,
                width = 0.95f, height = 2.1f, connectsToRoomId = smallRoom.id
            });
            List<RoomSpec> rooms = new List<RoomSpec> { smallRoom, largeRoom };
            GameObject house = new GameObject("Unequal Shared Wall Test");
            GameObject smallRoot = new GameObject("Small Room");
            GameObject largeRoot = new GameObject("Large Room");
            smallRoot.transform.SetParent(house.transform, false);
            largeRoot.transform.SetParent(house.transform, false);
            smallRoot.transform.localPosition = smallRoom.center;
            largeRoot.transform.localPosition = largeRoom.center;

            RoomViewBuilder builder = new RoomViewBuilder();
            builder.Build(smallRoom, smallRoot.transform, rooms);
            builder.Build(largeRoom, largeRoot.transform, rooms);

            Transform[] houseParts = house.GetComponentsInChildren<Transform>();
            int doorPanelCount = 0;
            for (int i = 0; i < houseParts.Length; i++)
                if (houseParts[i].name.Contains("DoorAsset_Panel")) doorPanelCount++;
            Assert.That(doorPanelCount, Is.EqualTo(1), "A paired room connection must render exactly one door asset.");

            // Keep exterior wall extensions outside the shared overlap
            const float sharedLocalStart = -2.25f;
            const float sharedLocalEnd = 0.75f;
            Transform[] largeParts = largeRoot.GetComponentsInChildren<Transform>();
            int retainedExtensionCount = 0;
            for (int i = 0; i < largeParts.Length; i++)
            {
                Transform part = largeParts[i];
                if (!part.name.StartsWith("left_WallSegment")) continue;
                retainedExtensionCount++;
                float halfLength = part.localScale.z * 0.5f;
                float start = part.localPosition.z - halfLength;
                float end = part.localPosition.z + halfLength;
                Assert.That(end <= sharedLocalStart + 0.002f || start >= sharedLocalEnd - 0.002f, Is.True,
                    "The non-owning room built a second wall across the shared doorway.");
            }
            Assert.That(retainedExtensionCount, Is.EqualTo(2),
                "The larger room must retain only the two exterior wall extensions outside the shared overlap.");

            Object.Destroy(house);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImportedWardrobeVisualMatchesItsAuthoritativeBounds()
        {
            GameObject parent = new GameObject("Wardrobe Bounds Test");
            AssetDefinition definition = new AssetCatalog().FindById("wardrobe_oak");
            PlacedObjectSpec placement = new PlacedObjectSpec
            {
                id = "wardrobe_test",
                assetId = definition.Id,
                category = definition.Category,
                size = definition.Size
            };

            GameObject created = new HybridAssetFactory().Create(definition, placement, parent.transform);
            SceneObjectView view = created.GetComponent<SceneObjectView>();
            Material shared = created.GetComponentInChildren<Renderer>().sharedMaterial;
            Color originalColor = shared.color;
            view.SetSelected(true);
            view.SetPlacementValid(false);
            Assert.That(created.GetComponentInChildren<Renderer>().sharedMaterial, Is.SameAs(shared));
            Assert.That(shared.color, Is.EqualTo(originalColor));
            Renderer[] renderers = created.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.GreaterThan(0));
            Bounds visualBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) visualBounds.Encapsulate(renderers[i].bounds);

            Assert.That(visualBounds.size.x, Is.EqualTo(placement.size.x).Within(0.03f));
            Assert.That(visualBounds.size.y, Is.EqualTo(placement.size.y).Within(0.03f));
            Assert.That(visualBounds.size.z, Is.EqualTo(placement.size.z).Within(0.03f));
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesPlayableDemoScene()
        {
            yield return null;
            yield return null;

            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            Assert.That(controller, Is.Not.Null);
            yield return WaitForBuild(controller);
            Assert.That(controller.CurrentSpec, Is.Not.Null);
            Assert.That(controller.CurrentSpec.placements.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(Object.FindObjectOfType<SceneObjectView>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
            SceneObjectView[] views = Object.FindObjectsOfType<SceneObjectView>();
            Assert.That(System.Array.Exists(views, view => view.AssetSource.StartsWith("Kenney CC0")), Is.True);
            SceneObjectView sofa = System.Array.Find(views, view => view.Spec.category == "sofa");
            Assert.That(sofa, Is.Not.Null);
            Vector3 sofaFront = Quaternion.Euler(0f, sofa.Spec.rotationY, 0f) * Vector3.back;
            Vector3 directionToRoom = -sofa.Spec.position.normalized;
            Assert.That(Vector3.Dot(sofaFront, directionToRoom), Is.GreaterThan(0.75f));
            float rotationBeforeSnap = sofa.Spec.rotationY;
            Assert.That(controller.SnapToWall(sofa), Is.True);
            Assert.That(sofa.Spec.rotationY, Is.EqualTo(rotationBeforeSnap), "Wall snap must preserve the user's rotation.");
            Assert.That(controller.CurrentQuality, Is.Not.Null);
            SceneObjectView coffeeTable = System.Array.Find(views, view => view.Spec.category == "coffee_table");
            Assert.That(coffeeTable, Is.Not.Null);
            float beforeFacing = coffeeTable.Spec.rotationY;
            bool faced = controller.FaceAnchor(coffeeTable);
            if (faced)
            {
                PlacedObjectSpec sofaSpec = controller.CurrentSpec.placements.Find(item => item.id == coffeeTable.Spec.anchorId);
                Vector3 tableFront = Quaternion.Euler(0f, coffeeTable.Spec.rotationY, 0f) * Vector3.back;
                Vector3 tableToSofa = (sofaSpec.position - coffeeTable.Spec.position).normalized;
                Assert.That(Vector3.Dot(tableFront, tableToSofa), Is.GreaterThan(0.98f));
            }
            else
            {
                Assert.That(coffeeTable.Spec.rotationY, Is.EqualTo(beforeFacing));
                Assert.That(coffeeTable.transform.rotation.eulerAngles.y, Is.EqualTo(beforeFacing).Within(0.01f));
            }
            for (int i = 0; i < views.Length; i++)
            {
                if (!views[i].AssetSource.StartsWith("Kenney CC0")) continue;
                Assert.That(views[i].AssetDisplayName, Is.Not.Empty);
                Renderer[] renderers = views[i].GetComponentsInChildren<Renderer>();
                Assert.That(renderers.Length, Is.GreaterThan(0));
                Bounds visualBounds = renderers[0].bounds;
                for (int rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++)
                {
                    visualBounds.Encapsulate(renderers[rendererIndex].bounds);
                }
                Assert.That(visualBounds.size.sqrMagnitude, Is.GreaterThan(0.01f));
                Assert.That(visualBounds.min.y, Is.EqualTo(views[i].Spec.position.y).Within(0.02f));
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Material[] materials = renderers[rendererIndex].sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        Assert.That(materials[materialIndex].shader.name, Does.StartWith("Universal Render Pipeline"));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator FaceAnchorSucceedsInAnUncrowdedControlledScene()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            Assert.That(controller, Is.Not.Null);
            controller.Generate("Create a living room. Only place one sofa and one coffee table", false);
            yield return WaitForBuild(controller);
            SceneObjectView table = System.Array.Find(Object.FindObjectsOfType<SceneObjectView>(), view => view.Spec.category == "coffee_table");
            Assert.That(table, Is.Not.Null);
            Assert.That(controller.FaceAnchor(table), Is.True);
            PlacedObjectSpec sofa = controller.CurrentSpec.placements.Find(item => item.id == table.Spec.anchorId);
            Vector3 front = Quaternion.Euler(0f, table.Spec.rotationY, 0f) * Vector3.back;
            Assert.That(Vector3.Dot(front, (sofa.position - table.Spec.position).normalized), Is.GreaterThan(0.98f));
        }

        [UnityTest]
        public IEnumerator FaceAnchorRefusesBlockedRotationWithoutChangingState()
        {
            SceneRuntimeController controller = Object.FindObjectOfType<SceneRuntimeController>();
            controller.Generate("Create a living room. Only place one sofa and one coffee table", false);
            yield return WaitForBuild(controller);
            SceneObjectView table = System.Array.Find(Object.FindObjectsOfType<SceneObjectView>(), view => view.Spec.category == "coffee_table");
            controller.CurrentSpec.placements.Add(new PlacedObjectSpec
            {
                id = "test_blocker", category = "wardrobe", roomId = table.Spec.roomId,
                position = table.Spec.position, size = new Vector3(3f, 3f, 3f)
            });
            string before = SceneSpecJson.ToJson(controller.CurrentSpec);
            Assert.That(controller.FaceAnchor(table), Is.False);
            Assert.That(SceneSpecJson.ToJson(controller.CurrentSpec), Is.EqualTo(before));
            controller.CurrentSpec.placements.RemoveAll(item => item.id == "test_blocker");
        }

        [UnityTest]
        public IEnumerator SharedWallsWithoutDoorsAreRenderedOnlyOnce()
        {
            RoomSpec a = new RoomSpec { id = "a", width = 5f, depth = 4f, hasWindow = false };
            RoomSpec b = new RoomSpec { id = "b", width = 5f, depth = 4f, center = Vector3.right * 5f, hasWindow = false };
            GameObject house = new GameObject("Shared Wall Test");
            Transform first = new GameObject("First Room").transform;
            Transform second = new GameObject("Second Room").transform;
            first.SetParent(house.transform);
            second.SetParent(house.transform);
            RoomViewBuilder builder = new RoomViewBuilder();
            List<RoomSpec> rooms = new List<RoomSpec> { a, b };
            builder.Build(a, first, rooms);
            builder.Build(b, second, rooms);
            Assert.That(System.Array.FindAll(first.GetComponentsInChildren<Transform>(), item => item.name.StartsWith("right_WallSegment")).Length, Is.EqualTo(1));
            Assert.That(System.Array.Exists(second.GetComponentsInChildren<Transform>(), item => item.name.StartsWith("left_WallSegment")), Is.False);
            Object.Destroy(house);
            yield return null;
        }
        private static IEnumerator WaitForBuild(SceneRuntimeController controller)
        {
            for (int frame = 0; frame < 60 && controller.IsBusy; frame++) yield return null;
            Assert.That(controller.IsBusy, Is.False, "Local generation must finish within the frame budget");
            yield return null;
        }
    }
}
