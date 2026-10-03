using System;
using System.Collections.Generic;
using System.IO;
using SceneCraftAI.Assets;
using UnityEditor;
using UnityEngine;

namespace SceneCraftAI.Editor
{
    public static class KenneyFurnitureAssetBuilder
    {
        private const string AutoRebuildSessionKey = "SceneCraftAI.KenneyRegistryAutoRebuilt";
        private const string ModelFolder = "Assets/SceneCraftAI/Art/Models/KenneyFurnitureKit";
        private const string PrefabFolder = "Assets/SceneCraftAI/Art/Prefabs/KenneyFurnitureKit";
        private const string ResourceFolder = "Assets/SceneCraftAI/Resources";
        private const string LibraryPath = ResourceFolder + "/SceneCraftAssetLibrary.asset";

        [InitializeOnLoadMethod]
        private static void ScheduleRegistrySynchronization()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += SynchronizeRegistryIfNeeded;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(AutoRebuildSessionKey, false);
            EditorApplication.delayCall += SynchronizeRegistryIfNeeded;
        }

        private static void SynchronizeRegistryIfNeeded()
        {
            if (SessionState.GetBool(AutoRebuildSessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            bool roundModelAvailable = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/tableRound.fbx") != null;
            bool sourceNamedRoundPrefabMissing =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/tableRound.prefab") == null;
            bool legacyGeneratedNamesRemain =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/dining_table_oak.prefab") != null;
            PrefabAssetLibrary library = AssetDatabase.LoadAssetAtPath<PrefabAssetLibrary>(LibraryPath);
            bool roundMappingMissing = library == null || library.FindPrefab("dining_table_round") == null;
            if (!roundModelAvailable || (!sourceNamedRoundPrefabMissing && !legacyGeneratedNamesRemain && !roundMappingMissing))
                return;

            SessionState.SetBool(AutoRebuildSessionKey, true);
            Build();
        }

        [MenuItem("Tools/SceneCraft AI/Rebuild Kenney Prefab Library")]
        public static void Build()
        {
            EnsureFolder("Assets/SceneCraftAI/Art/Prefabs", "KenneyFurnitureKit");
            EnsureFolder("Assets/SceneCraftAI", "Resources");

            AssetCatalog catalog = new AssetCatalog();
            HashSet<string> validModelNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                string sourceModelName = catalog.Definitions[i].SourceModelName;
                if (!string.IsNullOrWhiteSpace(sourceModelName)) validModelNames.Add(sourceModelName);
            }
            RemoveStaleGeneratedPrefabs(validModelNames);

            List<PrefabAssetEntry> entries = new List<PrefabAssetEntry>();
            Dictionary<string, GameObject> prefabsByModel = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                AssetDefinition definition = catalog.Definitions[i];
                if (string.IsNullOrWhiteSpace(definition.SourceModelName)) continue;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/" + definition.SourceModelName + ".fbx");
                if (model == null)
                {
                    Debug.LogWarning("[SceneCraftAI] Missing Kenney model: " + definition.SourceModelName);
                    continue;
                }

                GameObject prefab;
                if (!prefabsByModel.TryGetValue(definition.SourceModelName, out prefab))
                {
                    prefab = CreatePrefab(definition.SourceModelName, model);
                    prefabsByModel.Add(definition.SourceModelName, prefab);
                }
                PrefabAssetEntry entry = new PrefabAssetEntry();
                entry.Configure(definition.Id, prefab);
                entries.Add(entry);
            }

            PrefabAssetLibrary library = AssetDatabase.LoadAssetAtPath<PrefabAssetLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<PrefabAssetLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.ReplaceEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneCraftAI] Built " + entries.Count + " Kenney CC0 prefab mappings.");
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static GameObject CreatePrefab(string sourceModelName, GameObject model)
        {
            GameObject root = new GameObject(sourceModelName);
            GameObject visual = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (visual == null) visual = UnityEngine.Object.Instantiate(model);
            visual.name = sourceModelName;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            string path = PrefabFolder + "/" + sourceModelName + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void RemoveStaleGeneratedPrefabs(HashSet<string> validModelNames)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!validModelNames.Contains(fileName)) AssetDatabase.DeleteAsset(path);
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

    }

    public sealed class KenneyFurnitureModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/SceneCraftAI/Art/Models/KenneyFurnitureKit/", StringComparison.Ordinal)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
        }
    }
}
