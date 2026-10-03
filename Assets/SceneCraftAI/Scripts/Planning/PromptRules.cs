using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SceneCraftAI.Assets;
using SceneCraftAI.Domain;
using UnityEngine;

namespace SceneCraftAI.Planning
{
    public static class PromptRules
    {
        private sealed class Term
        {
            public readonly string Id;
            public readonly string[] Names;
            public Term(string id, params string[] names) { Id = id; Names = names; }
        }

        private sealed class Mention
        {
            public string Id;
            public int Start;
            public int End;
            public int Count;
            public bool Explicit;
            public bool Negative;
        }

        private const string Numbers = @"(?<n>\d{1,2}|(?<![a-z])(?:zero|one|two|three|four|five|six|seven|eight|a|an)|零|一|二|两|三|四|五|六|七|八)";
        private static readonly Term[] RoomTerms =
        {
            new Term("living_room", "living rooms", "living room", "lounge", "客厅", "起居室"),
            new Term("master_bedroom", "primary bedrooms", "primary bedroom", "master bedrooms", "master bedroom", "主卧"),
            new Term("secondary_bedroom", "secondary bedrooms", "secondary bedroom", "guest bedrooms", "guest bedroom", "次卧", "侧卧", "客卧"),
            new Term("bedroom", "bedrooms", "bedroom", "卧室"),
            new Term("study", "studies", "study", "office", "workspace", "书房", "办公室"),
            new Term("dining_room", "dining rooms", "dining room", "餐厅"),
            new Term("kitchen", "kitchens", "kitchen", "厨房"),
            new Term("bathroom", "bathrooms", "bathroom", "restrooms", "restroom", "卫生间", "浴室", "厕所")
        };
        private static readonly Term[] ItemTerms =
        {
            new Term("bed", "double bed", "single bed", "beds", "bed", "双人床", "单人床", "床"),
            new Term("nightstand", "bedside tables", "bedside table", "nightstands", "nightstand", "床头柜"),
            new Term("wardrobe", "wardrobes", "wardrobe", "衣柜"),
            new Term("sofa", "sofas", "sofa", "couch", "沙发"),
            new Term("coffee_table", "coffee tables", "coffee table", "茶几"),
            new Term("tv_stand", "tv stands", "tv stand", "television", "电视柜", "电视机"),
            new Term("dining_table", "dining tables", "dining table", "round table", "餐桌", "饭桌", "圆桌", "圆形桌"),
            new Term("desk", "desks", "desk", "书桌", "办公桌"),
            new Term("chair", "chairs", "chair", "椅子", "座椅"),
            new Term("bookshelf", "bookshelves", "bookshelf", "bookcase", "书架", "书柜"),
            new Term("plant", "potted plants", "potted plant", "plants", "plant", "植物", "绿植", "盆栽"),
            new Term("floor_lamp", "floor lamps", "floor lamp", "落地灯"),
            new Term("rug", "rugs", "rug", "地毯"),
            new Term("wall_art", "wall art", "paintings", "painting", "挂画", "墙画"),
            new Term("ceiling_light", "ceiling lights", "ceiling light", "pendant light", "吊灯", "吸顶灯"),
            new Term("table_lamp", "table lamps", "table lamp", "台灯", "桌灯"),
            new Term("vase", "vases", "vase", "花瓶"),
            new Term("books", "book stack", "books", "书本", "摞书", "本书"),
            new Term("decor_bowl", "decorative bowl", "tray", "装饰碗", "托盘"),
            new Term("toilet", "toilets", "toilet", "马桶"),
            new Term("sink_vanity", "sink vanity", "washbasin", "洗手台", "洗手盆"),
            new Term("shower", "showers", "shower", "淋浴"),
            new Term("kitchen_counter", "kitchen counters", "kitchen counter", "厨房台面", "橱柜"),
            new Term("refrigerator", "refrigerators", "refrigerator", "fridge", "冰箱"),
            new Term("stove", "stoves", "stove", "cooktop", "灶台", "炉灶"),
            new Term("computer_set", "computer", "monitor", "电脑", "显示器"),
            new Term("clothes_rack", "clothes rack", "coat rack", "衣架"),
            new Term("fan", "fans", "fan", "风扇"),
            new Term("air_conditioner", "air conditioners", "air conditioner", "空调"),
            new Term("flower_pot", "flower pots", "flower pot", "花盆", "盆花"),
            new Term("laundry_basket", "laundry baskets", "laundry basket", "洗衣篮", "脏衣篮")
        };

        public static List<string> ReadRooms(string prompt)
        {
            prompt = prompt ?? string.Empty;
            List<string> result = new List<string>();
            List<Mention> mentions = ReadMentions(prompt ?? string.Empty, RoomTerms);
            bool bedroomRoles = mentions.Exists(mention => !mention.Negative &&
                (mention.Id == "master_bedroom" || mention.Id == "secondary_bedroom"));
            foreach (Term term in RoomTerms)
            {
                int count = 0;
                foreach (Mention mention in mentions)
                {
                    if (mention.Id != term.Id || mention.Negative) continue;
                    if (mention.Id == "bedroom" && bedroomRoles && !mention.Explicit) continue;
                    string prefix = prompt.Substring(0, mention.Start);
                    if (Regex.IsMatch(prefix, @"(?:\b(?:each|every)\s+|每间|每个|各个)$", RegexOptions.IgnoreCase)) continue;
                    count = Mathf.Max(count, mention.Count);
                }
                for (int i = 0; i < Mathf.Min(count, 6); i++) result.Add(term.Id);
            }
            bool hasMaster = result.Contains("master_bedroom");
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i] != "bedroom") continue;
                result[i] = hasMaster ? "secondary_bedroom" : "master_bedroom";
                hasMaster = true;
            }
            return result;
        }

        public static bool HasPositive(string prompt, string term)
        {
            foreach (Match match in Regex.Matches(prompt ?? string.Empty, Pattern(term), RegexOptions.IgnoreCase))
                if (!IsNegative(prompt, match.Index)) return true;
            return false;
        }

        public static bool IsNegative(string prompt, int index)
        {
            int start = SentenceStart(prompt, index);
            string prefix = prompt.Substring(start, index - start);
            MatchCollection negatives = Regex.Matches(prefix,
                @"\bno\b|\bwithout\b|\bexclude\b|\bdo\s+not\b|\bdon't\b|不要|不需要|没有|禁止|不得|避免|无", RegexOptions.IgnoreCase);
            if (negatives.Count == 0) return false;
            Match last = negatives[negatives.Count - 1];
            string tail = prefix.Substring(last.Index + last.Length);
            return !Regex.IsMatch(tail, @"(?:[,，]\s*(?:put|place|include|add|放|摆放|添加)|\bbut\s+(?:with|put|place|include|add))\b", RegexOptions.IgnoreCase);
        }

        public static int Apply(SceneSpec spec, string prompt)
        {
            if (spec == null || string.IsNullOrWhiteSpace(prompt)) return 0;
            Dictionary<string, int> before = Inventory(spec);
            SceneSpecDefaults.EnsureHouse(spec);
            foreach (SceneObjectRequest item in spec.objects)
                if (!spec.rooms.Exists(room => room.id == item.roomId)) item.roomId = DefaultRoom(spec, item.category);
            List<Mention> mentions = ReadMentions(prompt, ItemTerms);
            AssetCatalog catalog = new AssetCatalog();
            foreach (AssetDefinition asset in catalog.Definitions)
            {
                AddMentions(mentions, prompt, asset.Category, asset.Id);
                if (!string.IsNullOrWhiteSpace(asset.SourceModelName) && asset.SourceModelName.Length >= 6)
                    AddMentions(mentions, prompt, asset.Category, asset.SourceModelName);
                foreach (string alias in asset.Aliases) AddMentions(mentions, prompt, asset.Category, alias);
            }
            RemoveOverlaps(mentions);

            if (Regex.IsMatch(prompt, @"空客厅|空房间|不要家具|不放家具|\bempty\b.*\broom\b|\b(?:no|without)\s+furniture\b", RegexOptions.IgnoreCase))
                spec.objects.Clear();
            else
            {
                HashSet<string> applied = new HashSet<string>();
                foreach (Mention mention in mentions)
                {
                    if (mention.Negative) continue;
                    List<string> scope = GetScope(spec, prompt, mention);
                    foreach (string roomId in scope)
                    {
                        string key = mention.Id + ":" + roomId;
                        if (!mention.Explicit && applied.Contains(key)) continue;
                        SetCount(spec, mention.Id, roomId, mention.Count);
                        applied.Add(key);
                    }
                }
                foreach (Mention mention in mentions)
                {
                    if (!mention.Negative) continue;
                    foreach (string roomId in GetScope(spec, prompt, mention))
                    {
                        string prefix = prompt.Substring(SentenceStart(prompt, mention.Start), mention.Start - SentenceStart(prompt, mention.Start));
                        bool extra = Regex.IsMatch(prefix, @"第二|额外|\b(?:additional|extra|another|second)\b", RegexOptions.IgnoreCase);
                        bool positive = applied.Contains(mention.Id + ":") || applied.Contains(mention.Id + ":" + roomId);
                        if (string.IsNullOrEmpty(roomId))
                            foreach (string key in applied) if (key.StartsWith(mention.Id + ":", StringComparison.Ordinal)) positive = true;
                        if (extra && positive) continue;
                        SetCount(spec, mention.Id, roomId, 0);
                    }
                }
                if (Regex.IsMatch(prompt, @"只放|只要|只需要|仅放|仅需要|\bonly\s+(?:place|include|add|use|with)\b", RegexOptions.IgnoreCase))
                {
                    HashSet<string> allowed = new HashSet<string>();
                    foreach (Mention mention in mentions) if (!mention.Negative) allowed.Add(mention.Id);
                    if (allowed.Count > 0) spec.objects.RemoveAll(item => !allowed.Contains(item.category));
                }
                RepairAnchors(spec);
                ApplyRelations(spec, prompt);
            }
            Dictionary<string, int> after = Inventory(spec);
            HashSet<string> keys = new HashSet<string>(before.Keys);
            keys.UnionWith(after.Keys);
            int changes = 0;
            foreach (string key in keys)
            {
                int oldCount, newCount;
                before.TryGetValue(key, out oldCount);
                after.TryGetValue(key, out newCount);
                changes += Mathf.Abs(oldCount - newCount);
            }
            return changes;
        }

        private static Dictionary<string, int> Inventory(SceneSpec spec)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (SceneObjectRequest item in spec.objects)
            {
                int count;
                counts.TryGetValue(item.category, out count);
                counts[item.category] = count + 1;
            }
            return counts;
        }

        private static List<Mention> ReadMentions(string prompt, Term[] terms)
        {
            List<Mention> result = new List<Mention>();
            foreach (Term term in terms)
                foreach (string name in term.Names) AddMentions(result, prompt, term.Id, name);
            RemoveOverlaps(result);
            return result;
        }

        private static void AddMentions(List<Mention> result, string prompt, string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            foreach (Match match in Regex.Matches(prompt, Pattern(name), RegexOptions.IgnoreCase))
            {
                string prefix = prompt.Substring(0, match.Index);
                Match count = Regex.Match(prefix, Numbers + @"\s*(?:间|个|件|张|把|盆|株|盏|套|摞)?\s*$", RegexOptions.IgnoreCase);
                result.Add(new Mention
                {
                    Id = id, Start = match.Index, End = match.Index + match.Length,
                    Count = count.Success ? ParseNumber(count.Groups["n"].Value) : 1,
                    Explicit = count.Success, Negative = IsNegative(prompt, match.Index)
                });
            }
        }

        private static void RemoveOverlaps(List<Mention> mentions)
        {
            mentions.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.End.CompareTo(a.End));
            for (int i = 0; i < mentions.Count; i++)
                for (int j = mentions.Count - 1; j > i; j--)
                    if (mentions[j].Start < mentions[i].End) mentions.RemoveAt(j);
        }

        private static string Pattern(string term)
        {
            if (term == "床") return "床(?!头柜)";
            bool latin = Regex.IsMatch(term, @"^[\w -]+$", RegexOptions.CultureInvariant) && term[0] < 128;
            return latin ? @"(?<![a-zA-Z0-9_])" + Regex.Escape(term) + @"(?![a-zA-Z0-9_])" : Regex.Escape(term);
        }

        private static int ParseNumber(string token)
        {
            int number;
            if (int.TryParse(token, out number)) return Mathf.Clamp(number, 0, 12);
            string[] english = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight" };
            string[] chinese = { "零", "一", "二", "三", "四", "五", "六", "七", "八" };
            for (int i = 0; i < english.Length; i++)
                if (token.Equals(english[i], StringComparison.OrdinalIgnoreCase) || token == chinese[i]) return i;
            return token == "两" ? 2 : 1;
        }

        private static int SentenceStart(string prompt, int index)
        {
            for (int i = index - 1; i >= 0; i--)
                if ("。；;.!?\n".IndexOf(prompt[i]) >= 0) return i + 1;
            return 0;
        }

        private static List<string> GetScope(SceneSpec spec, string prompt, Mention item)
        {
            List<string> result = new List<string>();
            int start = SentenceStart(prompt, item.Start);
            int end = item.End;
            while (end < prompt.Length && "。；;.!?\n，,".IndexOf(prompt[end]) < 0) end++;
            string clause = prompt.Substring(start, end - start);
            List<Mention> rooms = ReadMentions(clause, RoomTerms);
            Mention selected = null;
            foreach (Mention room in rooms)
            {
                if (room.Negative) continue;
                if (room.Start <= item.Start - start || selected == null) selected = room;
            }
            foreach (Mention room in rooms)
            {
                if (room.Negative || room.Start < item.End - start) continue;
                string qualifier = clause.Substring(item.End - start, room.Start - (item.End - start));
                if (!Regex.IsMatch(qualifier, @"^\s+(?:in|inside|for)\s+(?:(?:the|each|every|a|an)\s+)?$", RegexOptions.IgnoreCase)) continue;
                selected = room;
                break;
            }
            if (selected != null)
            {
                bool each = Regex.IsMatch(clause, @"\beach\b|\bevery\b|每间|每个|各个|各卧室", RegexOptions.IgnoreCase);
                foreach (RoomSpec room in spec.rooms)
                {
                    bool matches = selected.Id == room.type || room.id.StartsWith(selected.Id + "_", StringComparison.OrdinalIgnoreCase) ||
                        room.description.IndexOf(selected.Id, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matches) continue;
                    result.Add(room.id);
                    if (!each) break;
                }
            }
            if (result.Count == 0) result.Add(string.Empty);
            return result;
        }

        private static void SetCount(SceneSpec spec, string category, string roomId, int count)
        {
            List<SceneObjectRequest> matching = spec.objects.FindAll(item => item.category == category &&
                (string.IsNullOrEmpty(roomId) || item.roomId == roomId));
            // Preserve exact locks ahead of default variants when reducing a category
            matching.Sort((a, b) => string.IsNullOrWhiteSpace(a.preferredAssetId).CompareTo(string.IsNullOrWhiteSpace(b.preferredAssetId)));
            for (int i = count; i < matching.Count; i++) spec.objects.Remove(matching[i]);
            for (int i = matching.Count; i < count; i++)
            {
                int index = 1;
                while (spec.objects.Exists(item => item.id == category + "_prompt_" + index)) index++;
                spec.objects.Add(new SceneObjectRequest
                {
                    id = category + "_prompt_" + index, category = category,
                    roomId = string.IsNullOrEmpty(roomId) ? DefaultRoom(spec, category) : roomId,
                    description = "prompt requested " + category,
                    placement = Placement(category), relation = Relation(category)
                });
            }
        }

        private static string DefaultRoom(SceneSpec spec, string category)
        {
            string type = category == "bed" || category == "wardrobe" || category == "nightstand" ? "bedroom"
                : category == "toilet" || category == "sink_vanity" || category == "shower" || category == "laundry_basket" ? "bathroom"
                : category == "stove" || category == "refrigerator" || category == "kitchen_counter" ? "kitchen"
                : category == "desk" || category == "bookshelf" ? "study"
                : category == "dining_table" || category == "chair" ? "dining_room" : "living_room";
            RoomSpec room = spec.rooms.Find(candidate => candidate.type == type);
            return room == null ? spec.room.id : room.id;
        }

        private static string Placement(string category)
        {
            if (category == "wall_art" || category == "air_conditioner") return "wall";
            if (category == "ceiling_light") return "ceiling";
            if (category == "vase" || category == "table_lamp" || category == "books" || category == "decor_bowl" || category == "flower_pot" || category == "computer_set") return "surface";
            return "floor";
        }

        private static string Relation(string category)
        {
            if (Placement(category) == "surface") return "on_top_of";
            if (Placement(category) == "wall") return category == "wall_art" ? "wall_above_sofa" : "wall_high";
            if (Placement(category) == "ceiling") return "ceiling_center";
            if (category == "nightstand") return "beside_bed";
            if (category == "coffee_table") return "in_front_of_sofa";
            if (category == "tv_stand") return "opposite_sofa";
            if (category == "chair") return "around_table";
            if (category == "dining_table" || category == "desk") return "near_window";
            if (category == "plant" || category == "fan") return "corner";
            if (category == "rug") return "under_coffee_table";
            if (category == "floor_lamp") return "beside_sofa";
            return "against_wall";
        }

        private static void RepairAnchors(SceneSpec spec)
        {
            foreach (SceneObjectRequest item in spec.objects)
            {
                SceneObjectRequest current = spec.objects.Find(anchor => anchor.id == item.anchorId && anchor.roomId == item.roomId);
                if (current != null) continue;
                item.anchorId = string.Empty;
                string[] candidates = item.category == "chair" ? new[] { "dining_table", "desk" }
                    : item.category == "nightstand" ? new[] { "bed" }
                    : item.category == "table_lamp" ? new[] { "nightstand", "desk", "coffee_table" }
                    : item.category == "computer_set" || item.category == "books" ? new[] { "desk", "bookshelf", "coffee_table" }
                    : item.placement == "surface" ? new[] { "coffee_table", "dining_table", "nightstand", "desk", "kitchen_counter" }
                    : item.category == "coffee_table" || item.category == "tv_stand" || item.category == "floor_lamp" ? new[] { "sofa" }
                    : item.category == "rug" ? new[] { "coffee_table" } : Array.Empty<string>();
                foreach (string category in candidates)
                {
                    SceneObjectRequest anchor = spec.objects.Find(candidate => candidate.category == category && candidate.roomId == item.roomId);
                    if (anchor == null) continue;
                    item.anchorId = anchor.id;
                    if (item.category == "chair") item.relation = category == "desk" ? "in_front_of_desk" : "around_table";
                    break;
                }
            }
        }

        private static void ApplyRelations(SceneSpec spec, string prompt)
        {
            foreach (string sentence in Regex.Split(prompt, @"[。；;.!?\n]"))
            {
                List<Mention> items = ReadMentions(sentence, ItemTerms);
                if (items.Count < 2) continue;
                string relation = Regex.IsMatch(sentence, @"\bbehind\b|后面|后方", RegexOptions.IgnoreCase) ? "behind"
                    : Regex.IsMatch(sentence, @"\bleft\s+of\b|左侧|左边", RegexOptions.IgnoreCase) ? "left_of"
                    : Regex.IsMatch(sentence, @"\bright\s+of\b|右侧|右边", RegexOptions.IgnoreCase) ? "right_of"
                    : Regex.IsMatch(sentence, @"\bin\s+front\s+of\b|前面|前方", RegexOptions.IgnoreCase) ? "in_front_of" : string.Empty;
                if (string.IsNullOrEmpty(relation)) continue;
                Mention subject = items[0];
                Mention target = items[1];
                if (subject.Negative || target.Negative) continue;
                foreach (SceneObjectRequest item in spec.objects)
                {
                    if (item.category != subject.Id) continue;
                    SceneObjectRequest anchor = spec.objects.Find(candidate => candidate.category == target.Id && candidate.roomId == item.roomId);
                    if (anchor == null || anchor == item) continue;
                    item.relation = relation;
                    item.anchorId = anchor.id;
                }
            }
        }
    }
}
