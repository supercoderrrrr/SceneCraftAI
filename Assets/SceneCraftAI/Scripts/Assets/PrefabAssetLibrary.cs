using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneCraftAI.Assets
{
    [Serializable]
    public sealed class PrefabAssetEntry
    {
        [SerializeField] private string assetId;
        [SerializeField] private GameObject prefab;

        public string AssetId { get { return assetId; } }
        public GameObject Prefab { get { return prefab; } }

#if UNITY_EDITOR
        public void Configure(string id, GameObject prefabAsset)
        {
            assetId = id;
            prefab = prefabAsset;
        }
#endif
    }

    [CreateAssetMenu(menuName = "SceneCraft AI/Prefab Asset Library", fileName = "SceneCraftAssetLibrary")]
    public sealed class PrefabAssetLibrary : ScriptableObject
    {
        [SerializeField] private List<PrefabAssetEntry> entries = new List<PrefabAssetEntry>();

        public IReadOnlyList<PrefabAssetEntry> Entries { get { return entries; } }

        public GameObject FindPrefab(string assetId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                PrefabAssetEntry entry = entries[i];
                if (entry != null && string.Equals(entry.AssetId, assetId, StringComparison.Ordinal))
                {
                    return entry.Prefab;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void ReplaceEntries(List<PrefabAssetEntry> replacements)
        {
            entries = replacements ?? new List<PrefabAssetEntry>();
        }
#endif
    }
}
