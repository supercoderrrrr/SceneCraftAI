using SceneCraftAI.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SceneCraftAI.Runtime
{
    public static class SceneCraftBootstrap
    {
        private static bool bootstrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            bootstrapped = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            Application.targetFrameRate = 60;

            GameObject appRoot = new GameObject("SceneCraft AI Runtime");
            SceneRuntimeController controller = appRoot.AddComponent<SceneRuntimeController>();

            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.105f);
            camera.fieldOfView = 52f;
            if (camera.GetComponent<OrbitCameraController>() == null) camera.gameObject.AddComponent<OrbitCameraController>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            EnsureLighting();

            RuntimeSelectionController selection = appRoot.AddComponent<RuntimeSelectionController>();
            selection.Initialize(camera, controller);
            GameObject hudObject = new GameObject("SceneCraft AI HUD");
            SceneCraftHud hud = hudObject.AddComponent<SceneCraftHud>();
            hud.Initialize(controller, selection);

            controller.Generate(hud.PromptText, false);
        }

        private static void EnsureLighting()
        {
            Light[] lights = Object.FindObjectsOfType<Light>();
            bool hasDirectional = false;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) hasDirectional = true;
            }

            if (!hasDirectional)
            {
                GameObject sun = new GameObject("SceneCraft Sun");
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.color = new Color(1f, 0.93f, 0.82f);
                sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }

            GameObject fillObject = new GameObject("SceneCraft Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 12f;
            fill.intensity = 4.5f;
            fill.color = new Color(0.72f, 0.84f, 1f);
            fillObject.transform.position = new Vector3(-2.5f, 3.5f, -2f);
        }
    }
}
