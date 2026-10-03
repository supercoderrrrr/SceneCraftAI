using System.Collections.Generic;
using SceneCraftAI.Domain;
using SceneCraftAI.Runtime;
using UnityEngine;

namespace SceneCraftAI.Assets
{
    public sealed class HybridAssetFactory
    {
        private const string LibraryResourceName = "SceneCraftAssetLibrary";
        private readonly ProceduralAssetFactory proceduralFactory = new ProceduralAssetFactory();
        private readonly PrefabAssetLibrary prefabLibrary;
        private readonly Dictionary<Material, Material> compatibleMaterials = new Dictionary<Material, Material>();

        public int RealAssetCount { get { return prefabLibrary == null ? 0 : prefabLibrary.Entries.Count; } }

        public HybridAssetFactory()
        {
            prefabLibrary = Resources.Load<PrefabAssetLibrary>(LibraryResourceName);
        }

        public GameObject Create(AssetDefinition definition, PlacedObjectSpec placement, Transform parent)
        {
            GameObject prefab = prefabLibrary == null ? null : prefabLibrary.FindPrefab(definition.Id);
            if (prefab == null) return proceduralFactory.Create(definition, placement, parent);

            GameObject root = new GameObject(placement.id);
            root.transform.SetParent(parent, false);
            GameObject visual = Object.Instantiate(prefab, root.transform, false);
            visual.name = prefab.name + " Visual";
            EnsureCompatibleMaterials(visual);
            ApplyCatalogPalette(visual, definition);
            NormalizeVisual(visual.transform, placement.size);
            root.transform.localPosition = placement.position;
            root.transform.localRotation = Quaternion.Euler(0f, placement.rotationY, 0f);

            BoxCollider selectionCollider = root.AddComponent<BoxCollider>();
            selectionCollider.center = definition.SpatialProfile.Surface == AssetPlacementSurface.Wall
                ? Vector3.zero
                : Vector3.up * placement.size.y * 0.5f;
            selectionCollider.size = placement.size;

            SceneObjectView view = root.AddComponent<SceneObjectView>();
            view.Initialize(
                placement,
                "Kenney CC0 • " + definition.SourceModelName,
                definition.DisplayName);
            return root;
        }

        private void EnsureCompatibleMaterials(GameObject visual)
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) return;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] assigned = renderers[rendererIndex].sharedMaterials;
                bool changed = false;
                for (int materialIndex = 0; materialIndex < assigned.Length; materialIndex++)
                {
                    Material source = assigned[materialIndex];
                    if (source == null || (source.shader != null && source.shader.name.StartsWith("Universal Render Pipeline"))) continue;
                    Material replacement;
                    if (!compatibleMaterials.TryGetValue(source, out replacement))
                    {
                        replacement = new Material(urpShader)
                        {
                            name = source.name + " (URP Runtime)",
                            color = source.HasProperty("_Color") ? source.color : Color.white
                        };
                        replacement.SetFloat("_Smoothness", 0.22f);
                        compatibleMaterials.Add(source, replacement);
                    }
                    assigned[materialIndex] = replacement;
                    changed = true;
                }
                if (changed) renderers[rendererIndex].sharedMaterials = assigned;
            }
        }

        private static void ApplyCatalogPalette(GameObject visual, AssetDefinition definition)
        {
            // Apply the catalog palette without changing the source mesh
            // Keep the authored leaf and pot colors for plants
            if (definition.Category == "plant") return;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] instances = renderers[rendererIndex].materials;
                for (int materialIndex = 0; materialIndex < instances.Length; materialIndex++)
                {
                    Material material = instances[materialIndex];
                    if (material == null || !material.HasProperty("_Color")) continue;
                    Color authored = material.color;
                    float value = Mathf.Lerp(0.72f, 1.12f, authored.grayscale);
                    Color palette = new Color(
                        Mathf.Clamp01(definition.PrimaryColor.r * value),
                        Mathf.Clamp01(definition.PrimaryColor.g * value),
                        Mathf.Clamp01(definition.PrimaryColor.b * value),
                        authored.a);
                    material.color = Color.Lerp(authored, palette, 0.68f);
                }
            }
        }

        private static void NormalizeVisual(Transform visual, Vector3 targetSize)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float scaleX = targetSize.x / Mathf.Max(0.001f, bounds.size.x);
            float scaleY = targetSize.y / Mathf.Max(0.001f, bounds.size.y);
            float scaleZ = targetSize.z / Mathf.Max(0.001f, bounds.size.z);
            // Fit each mesh axis to the bounds used by placement and selection
            visual.localScale = Vector3.Scale(visual.localScale, new Vector3(scaleX, scaleY, scaleZ));

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            visual.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        }
    }
}
