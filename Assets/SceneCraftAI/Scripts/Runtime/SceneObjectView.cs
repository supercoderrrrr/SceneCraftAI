using SceneCraftAI.Domain;
using UnityEngine;

namespace SceneCraftAI.Runtime
{
    public sealed class SceneObjectView : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<Renderer> renderers =
            new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<Color> baseColors =
            new System.Collections.Generic.List<Color>();
        private bool selected;
        private bool placementValid = true;
        private MaterialPropertyBlock colors;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorValue = Shader.PropertyToID("_Color");

        public PlacedObjectSpec Spec { get; private set; }
        public string AssetSource { get; private set; }
        public string AssetDisplayName { get; private set; }

        public void Initialize(
            PlacedObjectSpec spec,
            string assetSource = "Procedural fallback",
            string assetDisplayName = "")
        {
            if (colors == null) colors = new MaterialPropertyBlock();
            Spec = spec;
            AssetSource = assetSource;
            AssetDisplayName = string.IsNullOrWhiteSpace(assetDisplayName) ? spec.assetId : assetDisplayName;
            name = spec.category + " [" + spec.id + "]";
            GetComponentsInChildren(true, renderers);
            baseColors.Clear();
            for (int i = 0; i < renderers.Count; i++)
            {
                Material material = renderers[i].sharedMaterial;
                baseColors.Add(material == null ? Color.white : material.color);
            }
        }

        public void SetSelected(bool selected)
        {
            this.selected = selected;
            RefreshColors();
        }

        public void SetPlacementValid(bool valid)
        {
            placementValid = valid;
            RefreshColors();
        }

        private void RefreshColors()
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Color color = baseColors[i];
                if (!placementValid) color = Color.Lerp(color, new Color(1f, 0.12f, 0.08f), 0.68f);
                else if (selected) color = Color.Lerp(color, new Color(0.20f, 0.82f, 1f), 0.48f);
                renderers[i].GetPropertyBlock(colors, 0);
                colors.SetColor(BaseColor, color);
                colors.SetColor(ColorValue, color);
                renderers[i].SetPropertyBlock(colors, 0);
            }
        }
    }
}
