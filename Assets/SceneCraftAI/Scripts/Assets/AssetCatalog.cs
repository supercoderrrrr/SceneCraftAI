using System;
using System.Collections.Generic;
using SceneCraftAI.Domain;
using UnityEngine;

namespace SceneCraftAI.Assets
{
    public enum ProceduralAssetKind
    {
        Bed,
        Nightstand,
        Wardrobe,
        Sofa,
        CoffeeTable,
        DiningTable,
        TvStand,
        Desk,
        Chair,
        Bookshelf,
        Plant,
        FloorLamp,
        Rug,
        WallArt,
        CeilingLight,
        TableLamp,
        Vase,
        Books,
        DecorBowl,
        Toilet,
        SinkVanity,
        Shower,
        KitchenCounter,
        Refrigerator,
        Stove,
        ComputerSet,
        ClothesRack,
        Fan,
        AirConditioner,
        FlowerPot,
        LaundryBasket
    }

    public enum AssetPlacementSurface
    {
        Floor,
        Wall,
        Ceiling,
        Support
    }

    public sealed class AssetSpatialProfile
    {
        public AssetPlacementSurface Surface { get; private set; }
        public float FrontClearance { get; private set; }
        public float SideClearance { get; private set; }
        public bool PrefersWall { get; private set; }
        public bool Repeatable { get; private set; }

        private AssetSpatialProfile(
            AssetPlacementSurface surface,
            float frontClearance,
            float sideClearance,
            bool prefersWall,
            bool repeatable)
        {
            Surface = surface;
            FrontClearance = frontClearance;
            SideClearance = sideClearance;
            PrefersWall = prefersWall;
            Repeatable = repeatable;
        }

        public static AssetSpatialProfile ForCategory(string category)
        {
            switch (category)
            {
                case "wall_art": return new AssetSpatialProfile(AssetPlacementSurface.Wall, 0f, 0.05f, true, true);
                case "air_conditioner": return new AssetSpatialProfile(AssetPlacementSurface.Wall, 0f, 0.08f, true, true);
                case "ceiling_light": return new AssetSpatialProfile(AssetPlacementSurface.Ceiling, 0f, 0f, false, true);
                case "table_lamp":
                case "vase":
                case "books":
                case "decor_bowl":
                case "computer_set":
                case "flower_pot":
                    return new AssetSpatialProfile(AssetPlacementSurface.Support, 0f, 0.03f, false, true);
                case "wardrobe": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.85f, 0.15f, true, false);
                case "bookshelf": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.65f, 0.12f, true, false);
                case "bed": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.75f, 0.45f, true, false);
                case "sofa": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.9f, 0.35f, true, false);
                case "desk": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.8f, 0.25f, true, false);
                case "dining_table": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.85f, 0.85f, false, false);
                case "chair": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.55f, 0.18f, false, true);
                case "coffee_table": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.45f, 0.3f, false, false);
                case "tv_stand": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.65f, 0.18f, true, false);
                case "toilet": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.65f, 0.18f, true, false);
                case "sink_vanity": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.75f, 0.18f, true, false);
                case "shower": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.7f, 0.12f, true, false);
                case "kitchen_counter": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.9f, 0.1f, true, true);
                case "refrigerator": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.8f, 0.12f, true, false);
                case "stove": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.9f, 0.1f, true, false);
                case "clothes_rack": return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.55f, 0.15f, true, false);
                default: return new AssetSpatialProfile(AssetPlacementSurface.Floor, 0.35f, 0.15f, false, true);
            }
        }
    }

    public sealed class AssetDefinition
    {
        public string Id { get; private set; }
        public string Category { get; private set; }
        public string[] Tags { get; private set; }
        public Vector3 Size { get; private set; }
        public ProceduralAssetKind Kind { get; private set; }
        public Color PrimaryColor { get; private set; }
        public AssetSpatialProfile SpatialProfile { get; private set; }
        public string SourceModelName { get; private set; }
        public string DisplayName { get; private set; }
        public string[] Aliases { get; private set; }

        public AssetDefinition(
            string id,
            string category,
            Vector3 size,
            ProceduralAssetKind kind,
            Color color,
            params string[] tags)
        {
            Id = id;
            Category = category;
            Size = size;
            Kind = kind;
            PrimaryColor = color;
            Tags = tags ?? Array.Empty<string>();
            SpatialProfile = AssetSpatialProfile.ForCategory(category);
            SourceModelName = string.Empty;
            DisplayName = id;
            Aliases = Array.Empty<string>();
        }

        public void ConfigureIdentity(string sourceModelName, string displayName, params string[] aliases)
        {
            SourceModelName = sourceModelName ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            Aliases = aliases ?? Array.Empty<string>();
        }
    }

    public sealed class AssetCatalog
    {
        private readonly List<AssetDefinition> definitions = new List<AssetDefinition>();

        public IReadOnlyList<AssetDefinition> Definitions { get { return definitions; } }

        public AssetCatalog()
        {
            Color oak = new Color(0.62f, 0.38f, 0.19f);
            Color cream = new Color(0.82f, 0.76f, 0.65f);
            Color sage = new Color(0.32f, 0.50f, 0.39f);
            Color blue = new Color(0.20f, 0.38f, 0.58f);
            Color charcoal = new Color(0.18f, 0.19f, 0.22f);

            Add("bed_oak_double", "bed", new Vector3(2.05f, 0.58f, 1.65f), ProceduralAssetKind.Bed, oak, "wood", "warm", "double", "木质", "温馨");
            Add("bed_blue_double", "bed", new Vector3(2.05f, 0.58f, 1.65f), ProceduralAssetKind.Bed, blue, "modern", "blue", "double", "现代", "蓝色");
            Add("bed_oak_single", "bed", new Vector3(1.95f, 0.55f, 1.05f), ProceduralAssetKind.Bed, oak, "wood", "single", "compact", "木质", "单人", "小户型");
            Add("nightstand_oak", "nightstand", new Vector3(0.48f, 0.52f, 0.42f), ProceduralAssetKind.Nightstand, oak, "wood", "warm", "木质");
            Add("nightstand_minimal", "nightstand", new Vector3(0.45f, 0.48f, 0.4f), ProceduralAssetKind.Nightstand, cream, "minimal", "open", "compact", "极简", "小户型");
            Add("wardrobe_oak", "wardrobe", new Vector3(1.45f, 2.15f, 0.58f), ProceduralAssetKind.Wardrobe, oak, "wood", "large", "木质");
            Add("sofa_cream", "sofa", new Vector3(2.25f, 0.92f, 0.92f), ProceduralAssetKind.Sofa, cream, "warm", "modern", "温馨", "现代");
            Add("sofa_sage", "sofa", new Vector3(2.25f, 0.92f, 0.92f), ProceduralAssetKind.Sofa, sage, "natural", "modern", "自然", "现代");
            Add("sofa_blue_compact", "sofa", new Vector3(1.85f, 0.88f, 0.86f), ProceduralAssetKind.Sofa, blue, "blue", "cool", "compact", "蓝色", "冷色", "小户型");
            Add("coffee_table_oak", "coffee_table", new Vector3(1.18f, 0.42f, 0.65f), ProceduralAssetKind.CoffeeTable, oak, "wood", "warm", "木质");
            Add("coffee_table_charcoal", "coffee_table", new Vector3(1.05f, 0.4f, 0.6f), ProceduralAssetKind.CoffeeTable, charcoal, "industrial", "black", "minimal", "工业", "黑色", "极简");
            Add("coffee_table_square", "coffee_table", new Vector3(0.82f, 0.42f, 0.82f), ProceduralAssetKind.CoffeeTable, oak, "square", "compact", "wood", "方形", "小户型", "木质");
            Add("dining_table_oak", "dining_table", new Vector3(1.55f, 0.76f, 0.85f), ProceduralAssetKind.DiningTable, oak, "wood", "warm", "dining", "木质", "餐桌");
            Add("dining_table_charcoal", "dining_table", new Vector3(1.4f, 0.75f, 0.8f), ProceduralAssetKind.DiningTable, charcoal, "industrial", "black", "minimal", "工业", "黑色", "极简");
            Add("dining_table_round", "dining_table", new Vector3(1.15f, 0.75f, 1.15f), ProceduralAssetKind.DiningTable, oak, "round", "wood", "compact", "圆形", "圆桌", "木质");
            Add("tv_stand_charcoal", "tv_stand", new Vector3(1.65f, 0.58f, 0.42f), ProceduralAssetKind.TvStand, charcoal, "modern", "dark", "现代");
            Add("tv_stand_oak_doors", "tv_stand", new Vector3(1.55f, 0.62f, 0.45f), ProceduralAssetKind.TvStand, oak, "wood", "doors", "storage", "木质", "带门", "收纳");
            Add("desk_oak", "desk", new Vector3(1.35f, 0.76f, 0.65f), ProceduralAssetKind.Desk, oak, "wood", "study", "木质", "书房");
            Add("desk_charcoal", "desk", new Vector3(1.25f, 0.75f, 0.62f), ProceduralAssetKind.Desk, charcoal, "industrial", "black", "minimal", "工业", "黑色", "极简");
            Add("chair_cream", "chair", new Vector3(0.55f, 0.92f, 0.58f), ProceduralAssetKind.Chair, cream, "modern", "soft", "现代");
            Add("chair_sage", "chair", new Vector3(0.52f, 0.88f, 0.55f), ProceduralAssetKind.Chair, sage, "natural", "sage", "compact", "自然", "鼠尾草绿");
            Add("chair_blue", "chair", new Vector3(0.52f, 0.9f, 0.56f), ProceduralAssetKind.Chair, blue, "blue", "cool", "modern", "蓝色", "冷色");
            Add("bookshelf_oak", "bookshelf", new Vector3(1.05f, 1.95f, 0.36f), ProceduralAssetKind.Bookshelf, oak, "wood", "study", "木质", "书房");
            Add("bookshelf_charcoal", "bookshelf", new Vector3(0.9f, 1.85f, 0.34f), ProceduralAssetKind.Bookshelf, charcoal, "industrial", "black", "compact", "工业", "黑色");
            Add("plant_sage", "plant", new Vector3(0.58f, 1.15f, 0.58f), ProceduralAssetKind.Plant, sage, "green", "natural", "植物", "自然");
            Add("plant_tall", "plant", new Vector3(0.5f, 1.55f, 0.5f), ProceduralAssetKind.Plant, sage, "tall", "green", "tropical", "高大", "绿植", "热带");
            Add("floor_lamp_warm", "floor_lamp", new Vector3(0.42f, 1.65f, 0.42f), ProceduralAssetKind.FloorLamp, cream, "warm", "light", "温馨", "灯");
            Add("floor_lamp_charcoal", "floor_lamp", new Vector3(0.38f, 1.58f, 0.38f), ProceduralAssetKind.FloorLamp, charcoal, "industrial", "black", "minimal", "工业", "黑色", "极简");
            Add("rug_cream", "rug", new Vector3(2.5f, 0.025f, 1.75f), ProceduralAssetKind.Rug, cream, "warm", "soft", "温馨", "地毯");
            Add("rug_blue", "rug", new Vector3(1.85f, 0.025f, 1.85f), ProceduralAssetKind.Rug, blue, "blue", "cool", "round", "蓝色", "冷色", "圆形");
            Add("wall_art_sage", "wall_art", new Vector3(1.15f, 0.72f, 0.045f), ProceduralAssetKind.WallArt, sage, "modern", "decor", "挂画", "现代");
            Add("ceiling_light_warm", "ceiling_light", new Vector3(0.65f, 0.32f, 0.65f), ProceduralAssetKind.CeilingLight, cream, "ceiling", "warm", "吊灯", "吸顶灯");
            Add("table_lamp_warm", "table_lamp", new Vector3(0.28f, 0.48f, 0.28f), ProceduralAssetKind.TableLamp, cream, "surface", "warm", "台灯", "桌灯");
            Add("vase_ceramic", "vase", new Vector3(0.22f, 0.34f, 0.22f), ProceduralAssetKind.Vase, cream, "surface", "decor", "花瓶", "陶瓷");
            Add("books_stack", "books", new Vector3(0.38f, 0.12f, 0.28f), ProceduralAssetKind.Books, blue, "surface", "stack", "书", "书本");
            Add("decor_bowl", "decor_bowl", new Vector3(0.32f, 0.11f, 0.32f), ProceduralAssetKind.DecorBowl, sage, "surface", "decor", "装饰碗", "托盘");
            Add("toilet_porcelain", "toilet", new Vector3(0.72f, 0.82f, 1.02f), ProceduralAssetKind.Toilet, cream, "bathroom", "porcelain", "马桶", "坐便器");
            Add("sink_vanity_oak", "sink_vanity", new Vector3(1.05f, 0.92f, 0.56f), ProceduralAssetKind.SinkVanity, oak, "bathroom", "sink", "vanity", "洗手台", "洗手盆");
            Add("shower_glass", "shower", new Vector3(1.05f, 2.10f, 1.05f), ProceduralAssetKind.Shower, blue, "bathroom", "glass", "淋浴间", "淋浴房");
            Add("kitchen_counter_oak", "kitchen_counter", new Vector3(1.80f, 0.92f, 0.64f), ProceduralAssetKind.KitchenCounter, oak, "kitchen", "counter", "cabinet", "橱柜", "操作台");
            Add("refrigerator_modern", "refrigerator", new Vector3(0.82f, 1.92f, 0.74f), ProceduralAssetKind.Refrigerator, cream, "kitchen", "appliance", "冰箱", "双门冰箱");
            Add("stove_black", "stove", new Vector3(0.74f, 0.91f, 0.64f), ProceduralAssetKind.Stove, charcoal, "kitchen", "appliance", "灶台", "炉灶");
            Add("computer_desktop_set", "computer_set", new Vector3(0.76f, 0.52f, 0.30f), ProceduralAssetKind.ComputerSet, charcoal, "surface", "computer", "monitor", "电脑", "显示器", "键盘");
            Add("clothes_rack_oak", "clothes_rack", new Vector3(1.05f, 1.72f, 0.46f), ProceduralAssetKind.ClothesRack, oak, "bedroom", "storage", "衣架", "挂衣架");
            Add("standing_fan", "fan", new Vector3(0.48f, 1.28f, 0.48f), ProceduralAssetKind.Fan, cream, "appliance", "fan", "风扇", "落地扇");
            Add("air_conditioner_wall", "air_conditioner", new Vector3(1.08f, 0.34f, 0.28f), ProceduralAssetKind.AirConditioner, cream, "wall", "appliance", "空调", "壁挂空调");
            Add("flower_pot_colorful", "flower_pot", new Vector3(0.30f, 0.48f, 0.30f), ProceduralAssetKind.FlowerPot, sage, "surface", "flowers", "花盆", "鲜花", "盆花");
            Add("laundry_basket_woven", "laundry_basket", new Vector3(0.52f, 0.62f, 0.52f), ProceduralAssetKind.LaundryBasket, cream, "bathroom", "storage", "洗衣篮", "脏衣篮");

            ConfigureIdentities();
        }

        private void ConfigureIdentities()
        {
            Configure("bed_oak_double", "bedDouble", "Kenney 双人床", "double bed prefab", "双人床模型");
            Configure("bed_blue_double", "bedDouble", "Kenney 双人床（材质变体）");
            Configure("bed_oak_single", "bedSingle", "Kenney 单人床", "single bed prefab", "单人床模型");
            Configure("nightstand_oak", "sideTableDrawers", "抽屉床头柜", "drawer side table");
            Configure("nightstand_minimal", "sideTable", "开放式边桌", "open side table");
            Configure("wardrobe_oak", "bookcaseClosedWide", "宽型封闭储物柜", "closed wide cabinet");
            Configure("sofa_cream", "loungeDesignSofa", "设计款双人沙发", "design sofa");
            Configure("sofa_sage", "loungeSofaLong", "长款休闲沙发", "long lounge sofa");
            Configure("sofa_blue_compact", "loungeSofa", "紧凑休闲沙发", "compact lounge sofa");
            Configure("coffee_table_oak", "tableCoffee", "长方形茶几", "rectangular coffee table");
            Configure("coffee_table_charcoal", "tableCoffeeGlass", "玻璃茶几", "glass coffee table");
            Configure("coffee_table_square", "tableCoffeeSquare", "方形茶几", "square coffee table", "方茶几");
            Configure("dining_table_oak", "table", "长方形餐桌", "rectangular dining table", "长桌");
            Configure("dining_table_charcoal", "tableCross", "交叉桌腿方形餐桌", "cross leg dining table", "方形餐桌", "方桌");
            Configure("dining_table_round", "tableRound", "圆形餐桌", "round dining table", "圆桌");
            Configure("tv_stand_charcoal", "cabinetTelevision", "开放式电视柜", "open tv cabinet");
            Configure("tv_stand_oak_doors", "cabinetTelevisionDoors", "带门电视柜", "tv cabinet with doors");
            Configure("desk_oak", "desk", "标准书桌", "standard desk prefab");
            Configure("desk_charcoal", "deskCorner", "转角书桌", "corner desk");
            Configure("chair_cream", "chairModernCushion", "现代软垫椅", "modern cushion chair");
            Configure("chair_sage", "chairCushion", "木质软垫椅", "cushion chair");
            Configure("chair_blue", "chairRounded", "圆背木椅", "rounded chair", "圆背椅");
            Configure("bookshelf_oak", "bookcaseOpen", "高开放式书架", "open bookcase");
            Configure("bookshelf_charcoal", "bookcaseOpenLow", "矮开放式书架", "low bookcase");
            Configure("plant_sage", "pottedPlant", "大型盆栽", "potted plant prefab");
            Configure("plant_tall", string.Empty, "程序化高大绿植", "tall procedural plant");
            Configure("floor_lamp_warm", "lampRoundFloor", "圆罩落地灯", "round floor lamp");
            Configure("floor_lamp_charcoal", "lampSquareFloor", "方罩落地灯", "square floor lamp");
            Configure("rug_cream", "rugRectangle", "长方形地毯", "rectangular rug");
            Configure("rug_blue", "rugRound", "圆形地毯", "round rug", "圆地毯");
            Configure("wall_art_sage", string.Empty, "程序化墙面挂画", "procedural wall art");
            Configure("ceiling_light_warm", string.Empty, "暖色吊灯", "ceiling lamp", "吊灯");
            Configure("table_lamp_warm", string.Empty, "桌面台灯", "table lamp", "台灯");
            Configure("vase_ceramic", string.Empty, "陶瓷花瓶", "ceramic vase", "花瓶");
            Configure("books_stack", string.Empty, "叠放书本", "stack of books", "书本");
            Configure("decor_bowl", string.Empty, "桌面装饰碗", "decorative bowl", "装饰碗");
            Configure("toilet_porcelain", string.Empty, "程序化陶瓷马桶", "toilet");
            Configure("sink_vanity_oak", string.Empty, "木质洗手台", "bathroom vanity");
            Configure("shower_glass", string.Empty, "玻璃淋浴间", "glass shower");
            Configure("kitchen_counter_oak", string.Empty, "木质厨房操作台", "kitchen counter");
            Configure("refrigerator_modern", string.Empty, "现代双门冰箱", "refrigerator");
            Configure("stove_black", string.Empty, "黑色炉灶", "stove");
            Configure("computer_desktop_set", string.Empty, "台式电脑组合", "desktop computer set");
            Configure("clothes_rack_oak", string.Empty, "开放式挂衣架", "clothes rack");
            Configure("standing_fan", string.Empty, "落地风扇", "standing fan");
            Configure("air_conditioner_wall", string.Empty, "壁挂空调", "wall air conditioner");
            Configure("flower_pot_colorful", string.Empty, "桌面盆花", "flower pot");
            Configure("laundry_basket_woven", string.Empty, "编织洗衣篮", "laundry basket");
        }

        private void Configure(string id, string sourceModelName, string displayName, params string[] aliases)
        {
            AssetDefinition definition = FindById(id);
            if (definition != null) definition.ConfigureIdentity(sourceModelName, displayName, aliases);
        }

        private void Add(string id, string category, Vector3 size, ProceduralAssetKind kind, Color color, params string[] tags)
        {
            definitions.Add(new AssetDefinition(id, category, size, kind, color, tags));
        }

        public AssetDefinition FindBest(SceneObjectRequest request, string sceneStyle)
        {
            if (!string.IsNullOrWhiteSpace(request.preferredAssetId))
            {
                AssetDefinition preferred = FindById(request.preferredAssetId.Trim());
                if (preferred != null && string.Equals(preferred.Category, request.category, StringComparison.OrdinalIgnoreCase))
                    return preferred;
            }

            AssetDefinition best = null;
            int bestScore = int.MinValue;
            string query = ((request.description ?? string.Empty) + " " + (sceneStyle ?? string.Empty)).ToLowerInvariant();

            for (int i = 0; i < definitions.Count; i++)
            {
                AssetDefinition candidate = definitions[i];
                if (!string.Equals(candidate.Category, request.category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int score = 100;
                if (query.Contains(candidate.Id.ToLowerInvariant())) score += 1000;
                if (!string.IsNullOrWhiteSpace(candidate.SourceModelName) &&
                    query.Contains(candidate.SourceModelName.ToLowerInvariant())) score += 800;
                if (!string.IsNullOrWhiteSpace(candidate.DisplayName) &&
                    query.Contains(candidate.DisplayName.ToLowerInvariant())) score += 600;
                for (int tagIndex = 0; tagIndex < candidate.Tags.Length; tagIndex++)
                {
                    if (query.Contains(candidate.Tags[tagIndex].ToLowerInvariant()))
                    {
                        score += 10;
                    }
                }
                for (int aliasIndex = 0; aliasIndex < candidate.Aliases.Length; aliasIndex++)
                {
                    if (query.Contains(candidate.Aliases[aliasIndex].ToLowerInvariant())) score += 120;
                }

                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        public int ApplyAssetHints(SceneSpec spec, string prompt)
        {
            if (spec == null || string.IsNullOrWhiteSpace(prompt)) return 0;
            int applied = 0;
            string normalizedPrompt = prompt.ToLowerInvariant();
            HashSet<string> appliedSourceModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definitions.Count; i++)
            {
                AssetDefinition definition = definitions[i];
                if (!MatchesExplicitReference(definition, prompt)) continue;
                bool exactInternalId = ContainsToken(normalizedPrompt, definition.Id.ToLowerInvariant());
                if (!exactInternalId && !string.IsNullOrWhiteSpace(definition.SourceModelName) &&
                    !appliedSourceModels.Add(definition.SourceModelName)) continue;
                SceneObjectRequest request = null;
                SceneObjectRequest alreadyLocked = null;
                for (int objectIndex = 0; objectIndex < spec.objects.Count; objectIndex++)
                {
                    SceneObjectRequest candidate = spec.objects[objectIndex];
                    if (candidate.category != definition.Category) continue;
                    if (string.Equals(candidate.preferredAssetId, definition.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyLocked = candidate;
                        break;
                    }
                    if (!string.IsNullOrWhiteSpace(candidate.preferredAssetId)) continue;
                    request = candidate;
                    break;
                }

                // Keep exact asset locks when the critic runs again
                if (alreadyLocked != null) continue;

                if (request == null)
                {
                    request = new SceneObjectRequest
                    {
                        id = CreateUniqueRequestId(spec, definition.Category),
                        category = definition.Category,
                        roomId = spec.room == null ? string.Empty : spec.room.id,
                        description = "explicit asset reference: " + definition.DisplayName,
                        relation = DefaultRelation(definition.Category),
                        placement = PlacementName(definition.SpatialProfile.Surface)
                    };
                    spec.objects.Add(request);
                }

                request.preferredAssetId = definition.Id;
                applied++;
            }

            return applied;
        }

        private static bool MatchesExplicitReference(AssetDefinition definition, string prompt)
        {
            string lower = prompt.ToLowerInvariant();
            if (SceneCraftAI.Planning.PromptRules.HasPositive(prompt, definition.Id)) return true;
            if (!string.IsNullOrWhiteSpace(definition.SourceModelName))
            {
                string source = definition.SourceModelName.ToLowerInvariant();
                if ((source.Length >= 6 && SceneCraftAI.Planning.PromptRules.HasPositive(prompt, source)) || lower.Trim() == source) return true;
            }
            if (!string.IsNullOrWhiteSpace(definition.DisplayName) && SceneCraftAI.Planning.PromptRules.HasPositive(prompt, definition.DisplayName)) return true;
            for (int i = 0; i < definition.Aliases.Length; i++)
                if (SceneCraftAI.Planning.PromptRules.HasPositive(prompt, definition.Aliases[i])) return true;
            return false;
        }

        private static bool ContainsToken(string value, string token)
        {
            int index = value.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                bool left = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
                int end = index + token.Length;
                bool right = end >= value.Length || !char.IsLetterOrDigit(value[end]);
                if (left && right) return true;
                index = value.IndexOf(token, index + 1, StringComparison.Ordinal);
            }
            return false;
        }

        private static string CreateUniqueRequestId(SceneSpec spec, string category)
        {
            int index = 1;
            while (true)
            {
                string id = category + "_explicit_" + index.ToString("00");
                bool exists = false;
                for (int i = 0; i < spec.objects.Count; i++)
                    if (spec.objects[i].id == id) { exists = true; break; }
                if (!exists) return id;
                index++;
            }
        }

        private static string DefaultRelation(string category)
        {
            switch (category)
            {
                case "bed":
                case "sofa":
                case "wardrobe":
                case "bookshelf": return "against_wall";
                case "toilet":
                case "sink_vanity":
                case "kitchen_counter":
                case "refrigerator":
                case "stove":
                case "clothes_rack": return "against_wall";
                case "desk":
                case "dining_table": return "near_window";
                case "plant": return "corner";
                case "wall_art": return "wall_above_sofa";
                case "air_conditioner": return "wall_high";
                case "ceiling_light": return "ceiling_center";
                case "table_lamp":
                case "vase":
                case "books":
                case "decor_bowl": return "on_top_of";
                case "computer_set":
                case "flower_pot": return "on_top_of";
                case "shower":
                case "fan":
                case "laundry_basket": return "corner";
                default: return "auto";
            }
        }

        private static string PlacementName(AssetPlacementSurface surface)
        {
            switch (surface)
            {
                case AssetPlacementSurface.Wall: return "wall";
                case AssetPlacementSurface.Ceiling: return "ceiling";
                case AssetPlacementSurface.Support: return "surface";
                default: return "floor";
            }
        }

        public AssetDefinition FindById(string id)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].Id, id, StringComparison.Ordinal))
                {
                    return definitions[i];
                }
            }

            return null;
        }
    }
}
