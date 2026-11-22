using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace VR.DualDialHapticPuzzle.Editor
{
    public static class DualDialPuzzleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/DualDialHapticPuzzle.unity";
        private const string CameraRigPrefabPath =
            "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string InteractionRigPrefabPath =
            "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRComprehensiveInteractionRig.prefab";
        private const string GeneratedMaterialsPath = "Assets/Generated/DualDialHapticPuzzle/Materials";

        [MenuItem("Tools/VR Puzzle/Build Dual Dial Haptic Scene")]
        public static void Build()
        {
            GameObject cameraRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefabPath);
            GameObject interactionRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionRigPrefabPath);
            if (cameraRigPrefab == null || interactionRigPrefab == null)
            {
                throw new System.InvalidOperationException(
                    "Meta XR camera or interaction rig prefab was not found. " +
                    "Wait for Package Manager to finish importing Meta XR SDK, then try again.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            CreateFloor();

            GameObject cameraRig = (GameObject)PrefabUtility.InstantiatePrefab(cameraRigPrefab, scene);
            cameraRig.name = "OVRCameraRig";
            cameraRig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            GameObject interactionRig =
                (GameObject)PrefabUtility.InstantiatePrefab(interactionRigPrefab, scene);
            interactionRig.name = "OVRComprehensiveInteractionRig";
            interactionRig.transform.SetParent(cameraRig.transform, false);

            Camera hmdCamera = FindHmdCamera(cameraRig);
            if (hmdCamera == null)
            {
                throw new System.InvalidOperationException("No camera was found under OVRCameraRig.");
            }

            hmdCamera.tag = "MainCamera";
            hmdCamera.nearClipPlane = 0.05f;

            Material dialMaterial = CreateMaterial("DialFace.mat", new Color(0.22f, 0.23f, 0.29f));
            Material pointerMaterial = CreateMaterial("DialPointer.mat", new Color(0.83f, 0.82f, 0.78f));
            DialInteraction leftDial = CreateDial(
                "Left Dial", new Vector3(-0.34f, 1.25f, 1.1f),
                cameraRig.transform, InputDeviceCharacteristics.Left, dialMaterial, pointerMaterial);
            DialInteraction rightDial = CreateDial(
                "Right Dial", new Vector3(0.34f, 1.25f, 1.1f),
                cameraRig.transform, InputDeviceCharacteristics.Right, dialMaterial, pointerMaterial);

            GameObject managerObject = new GameObject("Dual Dial Puzzle Manager");
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            DualDialPuzzleManager manager = managerObject.AddComponent<DualDialPuzzleManager>();
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("leftDial").objectReferenceValue = leftDial;
            serializedManager.FindProperty("rightDial").objectReferenceValue = rightDial;
            serializedManager.FindProperty("leftTargetAngle").floatValue = 70f;
            serializedManager.FindProperty("rightTargetAngle").floatValue = 120f;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            Debug.Log($"Dual-dial haptic puzzle scene created at {ScenePath}");
        }

        private static Camera FindHmdCamera(GameObject cameraRig)
        {
            Camera[] cameras = cameraRig.GetComponentsInChildren<Camera>(true);
            foreach (Camera candidate in cameras)
            {
                if (candidate.name == "CenterEyeAnchor")
                {
                    return candidate;
                }
            }

            return cameras.Length > 0 ? cameras[0] : null;
        }

        private static DialInteraction CreateDial(
            string objectName,
            Vector3 position,
            Transform trackingOrigin,
            InputDeviceCharacteristics hand,
            Material faceMaterial,
            Material pointerMaterial)
        {
            GameObject pivot = new GameObject(objectName);
            pivot.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));

            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            face.name = "Dial Face";
            face.transform.SetParent(pivot.transform, false);
            face.transform.localScale = new Vector3(0.34f, 0.34f, 0.055f);
            face.GetComponent<Renderer>().sharedMaterial = faceMaterial;

            GameObject visual = new GameObject("Rotating Pointer");
            visual.transform.SetParent(pivot.transform, false);

            GameObject pointer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pointer.name = "Pointer";
            pointer.transform.SetParent(visual.transform, false);
            pointer.transform.localPosition = new Vector3(0f, 0.11f, -0.045f);
            pointer.transform.localScale = new Vector3(0.035f, 0.19f, 0.025f);
            pointer.GetComponent<Renderer>().sharedMaterial = pointerMaterial;

            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hub.name = "Pointer Hub";
            hub.transform.SetParent(visual.transform, false);
            hub.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            hub.transform.localScale = Vector3.one * 0.075f;
            hub.GetComponent<Renderer>().sharedMaterial = pointerMaterial;

            DialInteraction dial = pivot.AddComponent<DialInteraction>();
            SerializedObject serializedDial = new SerializedObject(dial);
            serializedDial.FindProperty("trackingOrigin").objectReferenceValue = trackingOrigin;
            serializedDial.FindProperty("hand").intValue = (int)(
                InputDeviceCharacteristics.Controller | hand);
            serializedDial.FindProperty("dialVisual").objectReferenceValue = visual.transform;
            serializedDial.FindProperty("statusRenderer").objectReferenceValue = face.GetComponent<Renderer>();
            serializedDial.FindProperty("initialAngle").floatValue = 0f;
            serializedDial.ApplyModifiedPropertiesWithoutUndo();
            return dial;
        }

        private static void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.06f, 1.5f);
            floor.transform.localScale = new Vector3(5f, 0.12f, 5f);
            floor.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial("Floor.mat", new Color(0.16f, 0.17f, 0.2f));
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static Material CreateMaterial(string fileName, Color color)
        {
            EnsureFolder("Assets/Generated");
            EnsureFolder("Assets/Generated/DualDialHapticPuzzle");
            EnsureFolder(GeneratedMaterialsPath);

            string assetPath = $"{GeneratedMaterialsPath}/{fileName}";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                existing.color = color;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int separator = folderPath.LastIndexOf('/');
            string parent = folderPath.Substring(0, separator);
            string folderName = folderPath.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != scenePath)
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
