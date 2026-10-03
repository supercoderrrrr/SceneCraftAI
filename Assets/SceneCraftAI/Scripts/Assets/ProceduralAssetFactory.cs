using System.Collections.Generic;
using SceneCraftAI.Domain;
using SceneCraftAI.Runtime;
using UnityEngine;

namespace SceneCraftAI.Assets
{
    public sealed class ProceduralAssetFactory
    {
        private readonly Dictionary<Color32, Material> materials = new Dictionary<Color32, Material>();
        private readonly Material darkMaterial;
        private readonly Material lightMaterial;
        private readonly Material greenMaterial;

        public ProceduralAssetFactory()
        {
            darkMaterial = GetMaterial(new Color(0.12f, 0.13f, 0.15f));
            lightMaterial = GetMaterial(new Color(0.92f, 0.88f, 0.78f));
            greenMaterial = GetMaterial(new Color(0.20f, 0.48f, 0.28f));
        }

        public GameObject Create(AssetDefinition definition, PlacedObjectSpec placement, Transform parent)
        {
            GameObject root = new GameObject(placement.id);
            root.transform.SetParent(parent, false);
            root.transform.position = placement.position;
            root.transform.rotation = Quaternion.Euler(0f, placement.rotationY, 0f);
            Material primary = GetMaterial(definition.PrimaryColor);

            switch (definition.Kind)
            {
                case ProceduralAssetKind.Bed: BuildBed(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Nightstand: BuildNightstand(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Wardrobe: BuildWardrobe(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Sofa: BuildSofa(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.CoffeeTable: BuildTable(root.transform, placement.size, primary, false); break;
                case ProceduralAssetKind.DiningTable: BuildTable(root.transform, placement.size, primary, false); break;
                case ProceduralAssetKind.TvStand: BuildTvStand(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Desk: BuildTable(root.transform, placement.size, primary, true); break;
                case ProceduralAssetKind.Chair: BuildChair(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Bookshelf: BuildBookshelf(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Plant: BuildPlant(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.FloorLamp: BuildLamp(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Rug: AddBox(root.transform, "Rug", new Vector3(0f, placement.size.y * 0.5f, 0f), placement.size, primary); break;
                case ProceduralAssetKind.WallArt: BuildWallArt(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.CeilingLight: BuildCeilingLight(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.TableLamp: BuildTableLamp(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Vase: BuildVase(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Books: BuildBooks(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.DecorBowl: BuildDecorBowl(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Toilet: BuildToilet(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.SinkVanity: BuildSinkVanity(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Shower: BuildShower(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.KitchenCounter: BuildKitchenCounter(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Refrigerator: BuildRefrigerator(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Stove: BuildStove(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.ComputerSet: BuildComputerSet(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.ClothesRack: BuildClothesRack(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.Fan: BuildFan(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.AirConditioner: BuildAirConditioner(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.FlowerPot: BuildFlowerPot(root.transform, placement.size, primary); break;
                case ProceduralAssetKind.LaundryBasket: BuildLaundryBasket(root.transform, placement.size, primary); break;
            }

            BoxCollider selectionCollider = root.AddComponent<BoxCollider>();
            selectionCollider.center = definition.SpatialProfile.Surface == AssetPlacementSurface.Wall
                ? Vector3.zero
                : Vector3.up * placement.size.y * 0.5f;
            selectionCollider.size = placement.size;
            SceneObjectView view = root.AddComponent<SceneObjectView>();
            view.Initialize(placement, "Procedural fallback", definition.DisplayName);
            return root;
        }

        private void BuildBed(Transform root, Vector3 size, Material primary)
        {
            AddBox(root, "Frame", new Vector3(0f, 0.18f, 0f), new Vector3(size.x, 0.25f, size.z), primary);
            AddBox(root, "Mattress", new Vector3(0f, 0.39f, -0.04f), new Vector3(size.x * 0.94f, 0.25f, size.z * 0.90f), lightMaterial);
            AddBox(root, "Headboard", new Vector3(0f, size.y * 0.62f, size.z * 0.46f), new Vector3(size.x, size.y * 1.25f, 0.12f), primary);
            AddBox(root, "PillowLeft", new Vector3(-size.x * 0.24f, 0.56f, size.z * 0.27f), new Vector3(size.x * 0.36f, 0.12f, size.z * 0.22f), lightMaterial);
            AddBox(root, "PillowRight", new Vector3(size.x * 0.24f, 0.56f, size.z * 0.27f), new Vector3(size.x * 0.36f, 0.12f, size.z * 0.22f), lightMaterial);
        }

        private void BuildNightstand(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Body", Vector3.up * size.y * 0.5f, size, material);
            AddBox(root, "Drawer", new Vector3(0f, size.y * 0.58f, -size.z * 0.505f), new Vector3(size.x * 0.82f, size.y * 0.22f, 0.025f), lightMaterial);
            AddSphere(root, "Knob", new Vector3(0f, size.y * 0.58f, -size.z * 0.55f), Vector3.one * 0.045f, darkMaterial);
        }

        private void BuildWardrobe(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Body", Vector3.up * size.y * 0.5f, size, material);
            AddBox(root, "DoorLeft", new Vector3(-size.x * 0.25f, size.y * 0.52f, -size.z * 0.51f), new Vector3(size.x * 0.46f, size.y * 0.90f, 0.035f), lightMaterial);
            AddBox(root, "DoorRight", new Vector3(size.x * 0.25f, size.y * 0.52f, -size.z * 0.51f), new Vector3(size.x * 0.46f, size.y * 0.90f, 0.035f), lightMaterial);
            AddSphere(root, "HandleLeft", new Vector3(-0.08f, size.y * 0.52f, -size.z * 0.57f), Vector3.one * 0.045f, darkMaterial);
            AddSphere(root, "HandleRight", new Vector3(0.08f, size.y * 0.52f, -size.z * 0.57f), Vector3.one * 0.045f, darkMaterial);
        }

        private void BuildSofa(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Seat", new Vector3(0f, size.y * 0.34f, 0f), new Vector3(size.x * 0.84f, size.y * 0.34f, size.z * 0.78f), material);
            AddBox(root, "Back", new Vector3(0f, size.y * 0.68f, size.z * 0.35f), new Vector3(size.x, size.y * 0.65f, size.z * 0.22f), material);
            AddBox(root, "ArmLeft", new Vector3(-size.x * 0.46f, size.y * 0.43f, 0f), new Vector3(size.x * 0.12f, size.y * 0.55f, size.z), material);
            AddBox(root, "ArmRight", new Vector3(size.x * 0.46f, size.y * 0.43f, 0f), new Vector3(size.x * 0.12f, size.y * 0.55f, size.z), material);
            AddBox(root, "CushionLeft", new Vector3(-size.x * 0.22f, size.y * 0.56f, size.z * 0.13f), new Vector3(size.x * 0.38f, size.y * 0.25f, size.z * 0.18f), lightMaterial);
            AddBox(root, "CushionRight", new Vector3(size.x * 0.22f, size.y * 0.56f, size.z * 0.13f), new Vector3(size.x * 0.38f, size.y * 0.25f, size.z * 0.18f), lightMaterial);
        }

        private void BuildTable(Transform root, Vector3 size, Material material, bool addDrawer)
        {
            AddBox(root, "Top", new Vector3(0f, size.y - 0.08f, 0f), new Vector3(size.x, 0.16f, size.z), material);
            float legHeight = size.y - 0.16f;
            float x = size.x * 0.42f;
            float z = size.z * 0.38f;
            AddBox(root, "LegFL", new Vector3(-x, legHeight * 0.5f, -z), new Vector3(0.09f, legHeight, 0.09f), darkMaterial);
            AddBox(root, "LegFR", new Vector3(x, legHeight * 0.5f, -z), new Vector3(0.09f, legHeight, 0.09f), darkMaterial);
            AddBox(root, "LegBL", new Vector3(-x, legHeight * 0.5f, z), new Vector3(0.09f, legHeight, 0.09f), darkMaterial);
            AddBox(root, "LegBR", new Vector3(x, legHeight * 0.5f, z), new Vector3(0.09f, legHeight, 0.09f), darkMaterial);
            if (addDrawer)
            {
                AddBox(root, "Drawer", new Vector3(0f, size.y - 0.22f, -size.z * 0.38f), new Vector3(size.x * 0.55f, 0.22f, size.z * 0.14f), lightMaterial);
            }
        }

        private void BuildTvStand(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Cabinet", Vector3.up * size.y * 0.5f, size, material);
            AddBox(root, "Screen", new Vector3(0f, size.y + 0.58f, 0.04f), new Vector3(size.x * 0.78f, 0.95f, 0.08f), darkMaterial);
            AddBox(root, "ScreenBase", new Vector3(0f, size.y + 0.08f, 0.04f), new Vector3(0.42f, 0.08f, 0.22f), darkMaterial);
        }

        private void BuildChair(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Seat", new Vector3(0f, size.y * 0.48f, 0f), new Vector3(size.x * 0.84f, 0.12f, size.z * 0.82f), material);
            AddBox(root, "Back", new Vector3(0f, size.y * 0.74f, size.z * 0.37f), new Vector3(size.x * 0.84f, size.y * 0.48f, 0.10f), material);
            float x = size.x * 0.33f;
            float z = size.z * 0.32f;
            AddBox(root, "LegFL", new Vector3(-x, size.y * 0.23f, -z), new Vector3(0.07f, size.y * 0.46f, 0.07f), darkMaterial);
            AddBox(root, "LegFR", new Vector3(x, size.y * 0.23f, -z), new Vector3(0.07f, size.y * 0.46f, 0.07f), darkMaterial);
            AddBox(root, "LegBL", new Vector3(-x, size.y * 0.23f, z), new Vector3(0.07f, size.y * 0.46f, 0.07f), darkMaterial);
            AddBox(root, "LegBR", new Vector3(x, size.y * 0.23f, z), new Vector3(0.07f, size.y * 0.46f, 0.07f), darkMaterial);
        }

        private void BuildBookshelf(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Back", new Vector3(0f, size.y * 0.5f, size.z * 0.42f), new Vector3(size.x, size.y, size.z * 0.16f), material);
            AddBox(root, "SideLeft", new Vector3(-size.x * 0.46f, size.y * 0.5f, 0f), new Vector3(size.x * 0.08f, size.y, size.z), material);
            AddBox(root, "SideRight", new Vector3(size.x * 0.46f, size.y * 0.5f, 0f), new Vector3(size.x * 0.08f, size.y, size.z), material);
            for (int i = 0; i < 5; i++)
            {
                float y = 0.04f + i * (size.y - 0.08f) / 4f;
                AddBox(root, "Shelf" + i, new Vector3(0f, y, 0f), new Vector3(size.x, 0.07f, size.z), material);
            }
        }

        private void BuildPlant(Transform root, Vector3 size, Material material)
        {
            AddCylinder(root, "Pot", new Vector3(0f, size.y * 0.18f, 0f), new Vector3(size.x * 0.65f, size.y * 0.36f, size.z * 0.65f), material);
            AddCylinder(root, "Stem", new Vector3(0f, size.y * 0.58f, 0f), new Vector3(0.08f, size.y * 0.55f, 0.08f), darkMaterial);
            AddSphere(root, "Leaves", new Vector3(0f, size.y * 0.78f, 0f), new Vector3(size.x, size.y * 0.48f, size.z), greenMaterial);
        }

        private void BuildLamp(Transform root, Vector3 size, Material material)
        {
            AddCylinder(root, "Base", new Vector3(0f, 0.04f, 0f), new Vector3(size.x * 0.75f, 0.08f, size.z * 0.75f), darkMaterial);
            AddCylinder(root, "Pole", new Vector3(0f, size.y * 0.48f, 0f), new Vector3(0.055f, size.y * 0.88f, 0.055f), darkMaterial);
            AddCylinder(root, "Shade", new Vector3(0f, size.y * 0.88f, 0f), new Vector3(size.x, size.y * 0.24f, size.z), material);
        }

        private void BuildWallArt(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Frame", Vector3.zero, size, darkMaterial);
            AddBox(root, "Canvas", new Vector3(0f, 0f, -size.z * 0.58f), new Vector3(size.x * 0.90f, size.y * 0.84f, size.z * 0.22f), material);
        }

        private void BuildCeilingLight(Transform root, Vector3 size, Material material)
        {
            AddCylinder(root, "Canopy", new Vector3(0f, size.y * 0.88f, 0f), new Vector3(size.x * 0.32f, size.y * 0.12f, size.z * 0.32f), darkMaterial);
            AddCylinder(root, "Stem", new Vector3(0f, size.y * 0.58f, 0f), new Vector3(0.045f, size.y * 0.55f, 0.045f), darkMaterial);
            AddSphere(root, "Shade", new Vector3(0f, size.y * 0.20f, 0f), new Vector3(size.x, size.y * 0.42f, size.z), material);
        }

        private void BuildTableLamp(Transform root, Vector3 size, Material material)
        {
            AddCylinder(root, "Base", new Vector3(0f, size.y * 0.05f, 0f), new Vector3(size.x * 0.72f, size.y * 0.1f, size.z * 0.72f), darkMaterial);
            AddCylinder(root, "Stem", new Vector3(0f, size.y * 0.42f, 0f), new Vector3(0.035f, size.y * 0.68f, 0.035f), darkMaterial);
            AddCylinder(root, "Shade", new Vector3(0f, size.y * 0.8f, 0f), new Vector3(size.x, size.y * 0.34f, size.z), material);
        }

        private void BuildVase(Transform root, Vector3 size, Material material)
        {
            AddSphere(root, "VaseBody", new Vector3(0f, size.y * 0.42f, 0f), new Vector3(size.x, size.y * 0.68f, size.z), material);
            AddCylinder(root, "VaseNeck", new Vector3(0f, size.y * 0.76f, 0f), new Vector3(size.x * 0.42f, size.y * 0.34f, size.z * 0.42f), material);
            AddCylinder(root, "VaseRim", new Vector3(0f, size.y * 0.95f, 0f), new Vector3(size.x * 0.58f, size.y * 0.07f, size.z * 0.58f), lightMaterial);
        }

        private void BuildBooks(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "BookBottom", new Vector3(0f, size.y * 0.22f, 0f), new Vector3(size.x, size.y * 0.44f, size.z), material);
            AddBox(root, "BookTop", new Vector3(0.03f, size.y * 0.72f, -0.01f), new Vector3(size.x * 0.9f, size.y * 0.44f, size.z * 0.92f), lightMaterial);
        }

        private void BuildDecorBowl(Transform root, Vector3 size, Material material)
        {
            AddCylinder(root, "Bowl", new Vector3(0f, size.y * 0.48f, 0f), size, material);
            AddCylinder(root, "Inset", new Vector3(0f, size.y * 0.82f, 0f), new Vector3(size.x * 0.7f, size.y * 0.25f, size.z * 0.7f), darkMaterial);
        }

        private void BuildToilet(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Cistern", new Vector3(0f, size.y * 0.58f, size.z * 0.32f), new Vector3(size.x * 0.78f, size.y * 0.70f, size.z * 0.30f), material);
            AddSphere(root, "Bowl", new Vector3(0f, size.y * 0.32f, -size.z * 0.12f), new Vector3(size.x, size.y * 0.48f, size.z * 0.72f), material);
            AddCylinder(root, "Seat", new Vector3(0f, size.y * 0.56f, -size.z * 0.12f), new Vector3(size.x * 0.82f, 0.055f, size.z * 0.58f), darkMaterial);
            AddBox(root, "Flush", new Vector3(0f, size.y * 0.96f, size.z * 0.25f), new Vector3(0.16f, 0.03f, 0.10f), darkMaterial);
        }

        private void BuildSinkVanity(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Vanity", new Vector3(0f, size.y * 0.42f, 0f), new Vector3(size.x, size.y * 0.84f, size.z), material);
            AddBox(root, "Counter", new Vector3(0f, size.y * 0.88f, 0f), new Vector3(size.x * 1.04f, 0.10f, size.z * 1.04f), lightMaterial);
            AddSphere(root, "Basin", new Vector3(0f, size.y * 0.93f, -size.z * 0.05f), new Vector3(size.x * 0.52f, 0.12f, size.z * 0.62f), lightMaterial);
            AddCylinder(root, "Tap", new Vector3(0f, size.y * 1.04f, size.z * 0.22f), new Vector3(0.055f, 0.22f, 0.055f), darkMaterial);
            AddBox(root, "Mirror", new Vector3(0f, size.y * 1.55f, size.z * 0.40f), new Vector3(size.x * 0.76f, size.y * 0.80f, 0.04f), darkMaterial);
        }

        private void BuildShower(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Tray", new Vector3(0f, 0.06f, 0f), new Vector3(size.x, 0.12f, size.z), lightMaterial);
            AddBox(root, "GlassBack", new Vector3(0f, size.y * 0.5f, size.z * 0.48f), new Vector3(size.x, size.y, 0.035f), material);
            AddBox(root, "GlassSide", new Vector3(size.x * 0.48f, size.y * 0.5f, 0f), new Vector3(0.035f, size.y, size.z), material);
            AddCylinder(root, "Pipe", new Vector3(-size.x * 0.28f, size.y * 0.55f, size.z * 0.43f), new Vector3(0.04f, size.y * 0.72f, 0.04f), darkMaterial);
            AddSphere(root, "ShowerHead", new Vector3(-size.x * 0.28f, size.y * 0.88f, size.z * 0.32f), Vector3.one * 0.16f, darkMaterial);
        }

        private void BuildKitchenCounter(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Cabinet", new Vector3(0f, size.y * 0.43f, 0f), new Vector3(size.x, size.y * 0.86f, size.z), material);
            AddBox(root, "Worktop", new Vector3(0f, size.y * 0.93f, 0f), new Vector3(size.x * 1.04f, 0.10f, size.z * 1.06f), lightMaterial);
            for (int i = -1; i <= 1; i++)
                AddBox(root, "Door" + i, new Vector3(i * size.x * 0.29f, size.y * 0.48f, -size.z * 0.51f), new Vector3(size.x * 0.27f, size.y * 0.68f, 0.03f), lightMaterial);
        }

        private void BuildRefrigerator(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Body", Vector3.up * size.y * 0.5f, size, material);
            AddBox(root, "UpperDoor", new Vector3(0f, size.y * 0.68f, -size.z * 0.51f), new Vector3(size.x * 0.96f, size.y * 0.58f, 0.035f), lightMaterial);
            AddBox(root, "LowerDoor", new Vector3(0f, size.y * 0.24f, -size.z * 0.51f), new Vector3(size.x * 0.96f, size.y * 0.27f, 0.035f), lightMaterial);
            AddBox(root, "Handle", new Vector3(size.x * 0.34f, size.y * 0.62f, -size.z * 0.56f), new Vector3(0.04f, size.y * 0.38f, 0.04f), darkMaterial);
        }

        private void BuildStove(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Oven", new Vector3(0f, size.y * 0.43f, 0f), new Vector3(size.x, size.y * 0.86f, size.z), material);
            AddBox(root, "OvenGlass", new Vector3(0f, size.y * 0.42f, -size.z * 0.51f), new Vector3(size.x * 0.72f, size.y * 0.38f, 0.035f), darkMaterial);
            AddBox(root, "Cooktop", new Vector3(0f, size.y * 0.94f, 0f), new Vector3(size.x, 0.07f, size.z), darkMaterial);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                AddCylinder(root, "Hob", new Vector3(x * size.x * 0.23f, size.y * 0.99f, z * size.z * 0.23f), new Vector3(0.18f, 0.025f, 0.18f), lightMaterial);
        }

        private void BuildComputerSet(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Monitor", new Vector3(0f, size.y * 0.68f, size.z * 0.08f), new Vector3(size.x * 0.68f, size.y * 0.62f, 0.055f), darkMaterial);
            AddBox(root, "Screen", new Vector3(0f, size.y * 0.68f, size.z * 0.045f), new Vector3(size.x * 0.61f, size.y * 0.52f, 0.02f), material);
            AddBox(root, "Stand", new Vector3(0f, size.y * 0.25f, size.z * 0.09f), new Vector3(0.07f, size.y * 0.28f, 0.07f), darkMaterial);
            AddBox(root, "Keyboard", new Vector3(0f, 0.035f, -size.z * 0.28f), new Vector3(size.x * 0.60f, 0.055f, size.z * 0.36f), darkMaterial);
            AddSphere(root, "Mouse", new Vector3(size.x * 0.38f, 0.035f, -size.z * 0.25f), new Vector3(0.10f, 0.05f, 0.14f), darkMaterial);
        }

        private void BuildClothesRack(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "LeftPost", new Vector3(-size.x * 0.45f, size.y * 0.5f, 0f), new Vector3(0.07f, size.y, 0.07f), material);
            AddBox(root, "RightPost", new Vector3(size.x * 0.45f, size.y * 0.5f, 0f), new Vector3(0.07f, size.y, 0.07f), material);
            AddBox(root, "Rail", new Vector3(0f, size.y * 0.88f, 0f), new Vector3(size.x, 0.07f, 0.07f), material);
            AddBox(root, "Shelf", new Vector3(0f, size.y * 0.08f, 0f), new Vector3(size.x, 0.08f, size.z), material);
            for (int i = -1; i <= 1; i++)
                AddBox(root, "Garment" + i, new Vector3(i * size.x * 0.23f, size.y * 0.58f, 0f), new Vector3(size.x * 0.18f, size.y * 0.48f, size.z * 0.58f), lightMaterial);
        }

        private void BuildFan(Transform root, Vector3 size, Material material)
        {
            AddCylinder(root, "Base", new Vector3(0f, 0.04f, 0f), new Vector3(size.x * 0.82f, 0.08f, size.z * 0.82f), darkMaterial);
            AddCylinder(root, "Pole", new Vector3(0f, size.y * 0.45f, 0f), new Vector3(0.055f, size.y * 0.72f, 0.055f), darkMaterial);
            AddCylinder(root, "Hub", new Vector3(0f, size.y * 0.82f, -0.02f), new Vector3(0.14f, 0.13f, 0.14f), material).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                GameObject blade = AddBox(root, "Blade" + i, new Vector3(0f, size.y * 0.82f, -0.08f), new Vector3(size.x * 0.10f, size.x * 0.66f, 0.035f), material);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f + 22f);
            }
        }

        private void BuildAirConditioner(Transform root, Vector3 size, Material material)
        {
            AddBox(root, "Unit", Vector3.zero, size, material);
            AddBox(root, "Vent", new Vector3(0f, -size.y * 0.28f, -size.z * 0.52f), new Vector3(size.x * 0.82f, size.y * 0.16f, 0.025f), darkMaterial);
            AddSphere(root, "Indicator", new Vector3(size.x * 0.38f, 0f, -size.z * 0.53f), Vector3.one * 0.035f, greenMaterial);
        }

        private void BuildFlowerPot(Transform root, Vector3 size, Material material)
        {
            AddCylinder(root, "DecoratedPot", new Vector3(0f, size.y * 0.20f, 0f), new Vector3(size.x * 0.82f, size.y * 0.40f, size.z * 0.82f), material);
            for (int i = -1; i <= 1; i++)
            {
                float x = i * size.x * 0.20f;
                AddCylinder(root, "Stem" + i, new Vector3(x, size.y * 0.58f, 0f), new Vector3(0.025f, size.y * (0.48f + 0.08f * (i + 1)), 0.025f), greenMaterial);
                Material flower = GetMaterial(i == 0 ? new Color(0.95f, 0.55f, 0.60f) : new Color(0.95f, 0.78f, 0.32f));
                AddSphere(root, "Flower" + i, new Vector3(x, size.y * (0.79f + 0.05f * (i + 1)), 0f), Vector3.one * size.x * 0.30f, flower);
            }
        }

        private void BuildLaundryBasket(Transform root, Vector3 size, Material material)
        {
            AddCylinder(root, "Basket", new Vector3(0f, size.y * 0.48f, 0f), new Vector3(size.x, size.y * 0.96f, size.z), material);
            AddCylinder(root, "Rim", new Vector3(0f, size.y * 0.94f, 0f), new Vector3(size.x * 1.04f, 0.06f, size.z * 1.04f), darkMaterial);
            AddBox(root, "Cloth", new Vector3(0f, size.y * 0.90f, 0f), new Vector3(size.x * 0.72f, 0.08f, size.z * 0.72f), lightMaterial);
        }

        private GameObject AddBox(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return AddPrimitive(PrimitiveType.Cube, parent, objectName, localPosition, localScale, material);
        }

        private GameObject AddSphere(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return AddPrimitive(PrimitiveType.Sphere, parent, objectName, localPosition, localScale, material);
        }

        private GameObject AddCylinder(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return AddPrimitive(PrimitiveType.Cylinder, parent, objectName, localPosition, new Vector3(localScale.x, localScale.y * 0.5f, localScale.z), material);
        }

        private static GameObject AddPrimitive(
            PrimitiveType type,
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.material = new Material(material);
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            return primitive;
        }

        private Material GetMaterial(Color color)
        {
            Color32 key = color;
            Material existing;
            if (materials.TryGetValue(key, out existing)) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", 0.28f);
            materials.Add(key, material);
            return material;
        }
    }
}
