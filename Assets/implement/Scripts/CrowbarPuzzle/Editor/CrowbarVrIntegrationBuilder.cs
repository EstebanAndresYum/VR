using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VR.CrowbarPuzzle.Editor
{
    public static class CrowbarVrIntegrationBuilder
    {
        private const string DesktopScenePath = "Assets/Scenes/CrowbarProjectionDemo.unity";
        private const string VrScenePath = "Assets/Scenes/CrowbarProjectionVR.unity";
        private const string CameraRigPrefabPath =
            "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string InteractionRigPrefabPath =
            "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRComprehensiveInteractionRig.prefab";

        [MenuItem("Tools/VR Puzzle/Build Crowbar VR Integration Scene")]
        public static void Build()
        {
            GameObject cameraRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefabPath);
            GameObject interactionRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionRigPrefabPath);
            if (cameraRigPrefab == null || interactionRigPrefab == null)
            {
                throw new System.InvalidOperationException(
                    "Meta camera or interaction rig prefab was not found. " +
                    "Wait for Package Manager to finish importing Meta XR SDK, then try again.");
            }

            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(VrScenePath))
            {
                Scene sourceScene = EditorSceneManager.OpenScene(DesktopScenePath, OpenSceneMode.Single);
                if (!EditorSceneManager.SaveScene(sourceScene, VrScenePath, true))
                {
                    throw new System.InvalidOperationException($"Could not create '{VrScenePath}'.");
                }
            }

            Scene scene = EditorSceneManager.OpenScene(VrScenePath, OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "OVRComprehensiveInteractionRig" || root.name == "OVRCameraRig")
                {
                    Object.DestroyImmediate(root);
                }
            }

            CrowbarDemoCameraController desktopController =
                Object.FindFirstObjectByType<CrowbarDemoCameraController>(FindObjectsInactive.Include);
            if (desktopController != null)
            {
                Object.DestroyImmediate(desktopController.gameObject);
            }
            else
            {
                Camera desktopCamera = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(camera => camera.CompareTag("MainCamera"));

                if (desktopCamera != null)
                {
                    Object.DestroyImmediate(desktopCamera.gameObject);
                }
            }

            GameObject cameraRig = (GameObject)PrefabUtility.InstantiatePrefab(cameraRigPrefab, scene);
            cameraRig.name = "OVRCameraRig";
            cameraRig.transform.SetPositionAndRotation(new Vector3(-0.8f, 0f, 0f), Quaternion.identity);

            GameObject interactionRig =
                (GameObject)PrefabUtility.InstantiatePrefab(interactionRigPrefab, scene);
            interactionRig.name = "OVRComprehensiveInteractionRig";
            interactionRig.transform.SetParent(cameraRig.transform, false);

            Camera hmdCamera = cameraRig.GetComponentsInChildren<Camera>(true)
                .FirstOrDefault(camera => camera.name == "CenterEyeAnchor")
                ?? cameraRig.GetComponentsInChildren<Camera>(true).FirstOrDefault();

            if (hmdCamera == null)
            {
                Object.DestroyImmediate(cameraRig);
                throw new System.InvalidOperationException("OVRCameraRig contains no Camera component.");
            }

            hmdCamera.tag = "MainCamera";
            hmdCamera.nearClipPlane = 0.05f;

            CrowbarProjectionPuzzle puzzle =
                Object.FindFirstObjectByType<CrowbarProjectionPuzzle>(FindObjectsInactive.Include);
            if (puzzle == null)
            {
                Object.DestroyImmediate(cameraRig);
                throw new System.InvalidOperationException("CrowbarProjectionPuzzle was not found in the VR scene.");
            }

            SerializedObject serializedPuzzle = new SerializedObject(puzzle);
            SerializedProperty hmdCameraProperty = serializedPuzzle.FindProperty("hmdCamera");
            hmdCameraProperty.objectReferenceValue = hmdCamera;
            serializedPuzzle.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(puzzle);
            EditorUtility.SetDirty(cameraRig);
            EditorUtility.SetDirty(interactionRig);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new System.InvalidOperationException($"Could not save '{VrScenePath}'.");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(VrScenePath, true)
            };
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = cameraRig;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log(
                $"Created Meta Quest crowbar integration scene at '{VrScenePath}'. " +
                $"HMD camera: '{hmdCamera.name}'.",
                cameraRig);
        }
    }
}
