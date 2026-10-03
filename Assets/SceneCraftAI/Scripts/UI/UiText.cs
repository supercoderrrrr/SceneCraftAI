using System;
using System.Collections.Generic;
using System.Globalization;
using SceneCraftAI.Domain;
using SceneCraftAI.Runtime;

namespace SceneCraftAI.UI
{
    public enum UiLanguage { English, Chinese }

    public static class UiText
    {
        public const string PreferenceKey = "SceneCraftAI.UiLanguage";
        public const string ChinesePrompt = "一套生活化住宅：客厅、主卧、次卧、厨房和卫生间";
        public const string EnglishPrompt = "A lived-in apartment: living room, primary bedroom, guest bedroom, kitchen and bathroom";

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "住宅空间设计演示", "Residential scene design" },
            { "界面语言", "Language" },
            { "空间需求", "Space brief" },
            { "选好房间，家具和细节由系统补充", "Choose your rooms; we will fill in the details" },
            { "本地生成", "Build locally" },
            { "DeepSeek 生成", "Build with DeepSeek" },
            { "保存场景", "Save scene" },
            { "载入场景", "Load scene" },
            { "换个布局", "Try another layout" },
            { "清空", "Clear" },
            { "尚未生成场景", "No scene yet" },
            { "房间", "Rooms" },
            { "物件", "Objects" },
            { "布局评分", "Layout score" },
            { "准备就绪", "Ready" },
            { "生成流程", "Build workflow" },
            { "等待开始", "Not started" },
            { "解析需求", "Read the brief" },
            { "整理物件", "Prepare inventory" },
            { "设计布局", "Design layout" },
            { "检查方案", "Review constraints" },
            { "搭建场景", "Build the scene" },
            { "确认房间类型与设计偏好", "Identify rooms and design preferences" },
            { "核对数量、禁用项与资源", "Check quantities, exclusions and assets" },
            { "比较候选布局与空间关系", "Compare layouts and spatial relations" },
            { "检查门口、碰撞与支撑面", "Check doors, collisions and support" },
            { "创建房间、门窗与家具", "Create rooms, openings and furniture" },
            { "查看运行记录", "Show run details" },
            { "收起运行记录", "Hide run details" },
            { "运行记录保留原始技术信息", "Diagnostics retain their original wording" },
            { "右键旋转   中键平移   滚轮缩放\n左键拖动家具   Shift 加大移动步长", "Right drag: orbit   Middle drag: pan   Scroll: zoom\nLeft drag: move furniture   Shift: larger nudges" },
            { "已选择对象", "Selected object" },
            { "点击场景中的家具查看信息", "Select furniture to inspect or adjust it" },
            { "左转 45°", "Rotate -45°" },
            { "右转 45°", "Rotate +45°" },
            { "吸附最近墙", "Snap to wall" },
            { "自动找空位", "Find free space" },
            { "朝向语义锚点", "Face anchor" },
            { "朝向并吸附锚点", "Face and snap" },
            { "锁定位置", "Lock position" },
            { "锁定位置（换布局时保留）", "Lock for layout changes" },
            { "解除位置锁定", "Unlock position" },
            { "已锁定", "Locked" },
            { "未锁定", "Unlocked" },
            { "删除对象", "Delete object" },
            { "撤销上一步", "Undo" },
            { "程序化模型", "Procedural" },
            { "例如：客厅、主卧、次卧、厨房、卫生间", "Try: living room, bedrooms, kitchen, bathroom" },
            { "填写需求后即可生成", "Enter a brief to build a scene" },
            { "场景可调整，有 {0} 项摆放提醒", "Scene ready with {0} placement notes" },
            { "场景已就绪，可直接调整家具", "Scene ready; furniture can be adjusted" },
            { "正在规划新的空间", "Planning a new space" },
            { "正在生成，完成后可继续调整", "Building; editing resumes when ready" },
            { "完成", "Done" },
            { "进行中", "Running" },
            { "沿用", "Reused" },
            { "未完成", "Failed" },
            { "等待", "Waiting" },
            { "已中止", "Stopped" },
            { "生成未完成，请查看运行记录", "Build failed; see run details" },
            { "正在执行：{0}", "In progress: {0}" },
            { "DeepSeek 正在解析房间与偏好", "DeepSeek is reading the brief" },
            { "正在解析房间与偏好", "Reading rooms and preferences" },
            { "云端不可用，改用本地解析", "Cloud unavailable; trying local planning" },
            { "云端不可用，已使用本地解析", "Cloud unavailable; local plan used" },
            { "已确认房间与设计需求", "Rooms and preferences confirmed" },
            { "正在核对数量、禁用项和资源偏好", "Checking quantities and asset preferences" },
            { "已整理 {0} 个物件请求", "Prepared {0} object requests" },
            { "正在比较 {0} 个候选布局", "Comparing {0} candidate layouts" },
            { "已选出布局，评分 {1:0.0}", "Layout selected, score {1:0.0}" },
            { "DeepSeek 正在复查空间关系", "DeepSeek is reviewing spatial relations" },
            { "正在检查门口、碰撞与支撑面", "Checking doors, collisions and support" },
            { "云端复查未完成，保留本地检查结果", "Cloud review skipped; local checks kept" },
            { "复查建议更改了物件清单，保留原方案", "Review changed inventory; original kept" },
            { "复查建议应用失败，保留原方案", "Review could not be applied; original kept" },
            { "正在创建房间、门窗与家具", "Creating rooms, openings and furniture" },
            { "已创建 {0} 个物件，可自由调整", "Created {0} objects, ready to edit" },
            { "沿用已有空间需求", "Reusing the existing brief" },
            { "保留物件清单与位置锁定", "Keeping inventory and position locks" },
            { "正在比较新的候选布局", "Comparing new candidate layouts" },
            { "正在检查新布局的空间约束", "Checking the new layout constraints" },
            { "正在更新场景", "Updating the scene" },
            { "布局已更新，可继续调整", "Layout updated, ready to edit" },
            { "本地检查未发现约束问题", "No issues found by local checks" },
            { "{0} 项约束提醒，请检查摆放", "{0} constraint notes; inspect placement" },
            { "未能完成，详情见运行记录", "Not completed; see run details" },
            { "客厅", "Living room" },
            { "主卧", "Primary bedroom" },
            { "次卧", "Guest bedroom" },
            { "卧室", "Bedroom" },
            { "书房", "Study" },
            { "餐厅", "Dining room" },
            { "厨房", "Kitchen" },
            { "卫生间", "Bathroom" },
            { "沙发", "Sofa" },
            { "床", "Bed" },
            { "床头柜", "Nightstand" },
            { "衣柜", "Wardrobe" },
            { "茶几", "Coffee table" },
            { "电视柜", "TV stand" },
            { "书桌", "Desk" },
            { "餐桌", "Dining table" },
            { "椅子", "Chair" },
            { "书柜", "Bookshelf" },
            { "植物", "Plant" },
            { "落地灯", "Floor lamp" },
            { "地毯", "Rug" },
            { "挂画", "Wall art" },
            { "吊灯", "Ceiling light" },
            { "台灯", "Table lamp" },
            { "花瓶", "Vase" },
            { "书籍", "Books" },
            { "装饰碗", "Decorative bowl" },
            { "马桶", "Toilet" },
            { "洗手台", "Sink vanity" },
            { "淋浴", "Shower" },
            { "厨柜", "Kitchen counter" },
            { "冰箱", "Refrigerator" },
            { "炉灶", "Stove" },
            { "电脑", "Computer setup" },
            { "衣架", "Clothes rack" },
            { "风扇", "Fan" },
            { "空调", "Air conditioner" },
            { "花盆", "Flower pot" },
            { "洗衣篮", "Laundry basket" }
        };

        public static UiLanguage FromPreference(int value)
        {
            return value == (int)UiLanguage.Chinese ? UiLanguage.Chinese : UiLanguage.English;
        }

        public static bool HasTranslation(string key) { return English.ContainsKey(key); }

        public static string Get(UiLanguage language, string key, params object[] values)
        {
            string translated;
            string text = language == UiLanguage.English && English.TryGetValue(key, out translated) ? translated : key;
            return values.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, values);
        }

        public static string ProgressNote(UiLanguage language, BuildProgress step)
        {
            if (string.IsNullOrEmpty(step.Note)) return string.Empty;
            string note = Get(language, step.Note, step.Count, step.Score);
            return string.IsNullOrEmpty(step.Detail) ? note : Get(language, step.Detail) + " / " + note;
        }

        public static string RoomName(UiLanguage language, RoomSpec room)
        {
            string label;
            switch (room.type)
            {
                case "living_room": label = "客厅"; break;
                case "master_bedroom": label = "主卧"; break;
                case "secondary_bedroom": label = "次卧"; break;
                case "bedroom": label = "卧室"; break;
                case "study": label = "书房"; break;
                case "dining_room": label = "餐厅"; break;
                case "kitchen": label = "厨房"; break;
                case "bathroom": label = "卫生间"; break;
                default: label = "房间"; break;
            }
            if (room.type == "bedroom")
            {
                string role = (room.id ?? string.Empty) + " " + (room.description ?? string.Empty);
                if (role.Contains("master_bedroom")) label = "主卧";
                else if (role.Contains("secondary_bedroom")) label = "次卧";
            }
            return Get(language, label);
        }

        public static string CategoryName(UiLanguage language, string category)
        {
            string label;
            switch (category)
            {
                case "bed": label = "床"; break;
                case "nightstand": label = "床头柜"; break;
                case "wardrobe": label = "衣柜"; break;
                case "sofa": label = "沙发"; break;
                case "coffee_table": label = "茶几"; break;
                case "tv_stand": label = "电视柜"; break;
                case "desk": label = "书桌"; break;
                case "dining_table": label = "餐桌"; break;
                case "chair": label = "椅子"; break;
                case "bookshelf": label = "书柜"; break;
                case "plant": label = "植物"; break;
                case "floor_lamp": label = "落地灯"; break;
                case "rug": label = "地毯"; break;
                case "wall_art": label = "挂画"; break;
                case "ceiling_light": label = "吊灯"; break;
                case "table_lamp": label = "台灯"; break;
                case "vase": label = "花瓶"; break;
                case "books": label = "书籍"; break;
                case "decor_bowl": label = "装饰碗"; break;
                case "toilet": label = "马桶"; break;
                case "sink_vanity": label = "洗手台"; break;
                case "shower": label = "淋浴"; break;
                case "kitchen_counter": label = "厨柜"; break;
                case "refrigerator": label = "冰箱"; break;
                case "stove": label = "炉灶"; break;
                case "computer_set": label = "电脑"; break;
                case "clothes_rack": label = "衣架"; break;
                case "fan": label = "风扇"; break;
                case "air_conditioner": label = "空调"; break;
                case "flower_pot": label = "花盆"; break;
                case "laundry_basket": label = "洗衣篮"; break;
                default: return category ?? string.Empty;
            }
            return Get(language, label);
        }
    }
}
