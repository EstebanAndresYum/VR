using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VR.CrowbarPuzzle.Editor
{
    [InitializeOnLoad]
    public static class CrowbarProjectionPuzzleBuilder
    {
        private const string SourceModelPath = "Assets/Art/Crowbar/Models/crowbar_1.fbx";
        private const string SourcePrefabPath = "Assets/Art/Crowbar/Prefabs/crowbar_1.prefab";
        private const string GeneratedRoot = "Assets/Generated/CrowbarPuzzle";
        private const string GeneratedMeshRoot = GeneratedRoot + "/Meshes";
        private const string GeneratedMaterialRoot = GeneratedRoot + "/Materials";
        private const string DemoScenePath = "Assets/Scenes/CrowbarProjectionDemo.unity";

        static CrowbarProjectionPuzzleBuilder()
        {
            if (Application.isBatchMode || AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) != null)
            {
                return;
            }

            EditorApplication.delayCall += BuildMissingDemoScene;
        }

        private static void BuildMissingDemoScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += BuildMissingDemoScene;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) != null)
            {
                return;
            }

            try
            {
                BuildDemoScene();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Crowbar Puzzle/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            EnsureFolder(GeneratedMeshRoot);
            EnsureFolder(GeneratedMaterialRoot);

            Mesh sourceMesh = LoadSourceMesh();
            if (sourceMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "Crowbar Puzzle",
                    $"Could not load a mesh from {SourceModelPath}.",
                    "OK");
                return;
            }

            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            Renderer sourceRenderer = sourcePrefab != null ? sourcePrefab.GetComponentInChildren<Renderer>() : null;
            Material[] sourceMaterials = sourceRenderer != null && sourceRenderer.sharedMaterials.Length > 0
                ? sourceRenderer.sharedMaterials
                : new[] { AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat") };

            int splitAxis = LongestAxis(sourceMesh.bounds.size);
            float axisMinimum = Component(sourceMesh.bounds.min, splitAxis);
            float axisMaximum = Component(sourceMesh.bounds.max, splitAxis);
            float firstCut = Mathf.Lerp(axisMinimum, axisMaximum, 0.34f);
            float secondCut = Mathf.Lerp(axisMinimum, axisMaximum, 0.68f);

            Mesh[] pieces =
            {
                CreateMeshAsset(sourceMesh, splitAxis, float.NegativeInfinity, firstCut, "CrowbarPiece01"),
                CreateMeshAsset(sourceMesh, splitAxis, firstCut, secondCut, "CrowbarPiece02"),
                CreateMeshAsset(sourceMesh, splitAxis, secondCut, float.PositiveInfinity, "CrowbarPiece03")
            };

            Material joint1FirstMaterial = CreateMarkerMaterial("Joint1_First", new Color(0.1f, 0.9f, 1f));
            Material joint1SecondMaterial = CreateMarkerMaterial("Joint1_Second", new Color(1f, 0.15f, 0.75f));
            Material joint2FirstMaterial = CreateMarkerMaterial("Joint2_First", new Color(1f, 0.85f, 0.1f));
            Material joint2SecondMaterial = CreateMarkerMaterial("Joint2_Second", new Color(0.35f, 0.45f, 1f));
            Material floorMaterial = CreateLitMaterial("DemoFloor", new Color(0.16f, 0.18f, 0.22f));
            Material targetMaterial = CreateMarkerMaterial("ViewpointTarget", new Color(0.2f, 1f, 0.35f));

            Scene previousScene = SceneManager.GetActiveScene();
            Scene demoScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(demoScene);

            try
            {
                BuildScene(
                    sourceMesh,
                    pieces,
                    sourceMaterials,
                    splitAxis,
                    firstCut,
                    secondCut,
                    joint1FirstMaterial,
                    joint1SecondMaterial,
                    joint2FirstMaterial,
                    joint2SecondMaterial,
                    floorMaterial,
                    targetMaterial);

                EditorSceneManager.SaveScene(demoScene, DemoScenePath);
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousScene);
                }

                EditorSceneManager.CloseScene(demoScene, true);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath);
            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            Debug.Log($"Crowbar projection demo created at {DemoScenePath}");
        }

        private static Mesh LoadSourceMesh()
        {
            ModelImporter importer = AssetImporter.GetAtPath(SourceModelPath) as ModelImporter;
            if (importer == null)
            {
                return null;
            }

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAllAssetsAtPath(SourceModelPath)
                .OfType<Mesh>()
                .OrderByDescending(mesh => mesh.vertexCount)
                .FirstOrDefault();
        }

        private static void BuildScene(
            Mesh sourceMesh,
            IReadOnlyList<Mesh> pieces,
            Material[] sourceMaterials,
            int splitAxis,
            float firstCut,
            float secondCut,
            Material joint1FirstMaterial,
            Material joint1SecondMaterial,
            Material joint2FirstMaterial,
            Material joint2SecondMaterial,
            Material floorMaterial,
            Material targetMaterial)
        {
            Vector3 targetViewpoint = new Vector3(0f, 1.65f, 0f);
            Vector3 assembledPosition = targetViewpoint + Vector3.forward * 3.5f;
            Vector3 localLongAxis = AxisVector(splitAxis);
            Quaternion alignLongAxisToUp = Quaternion.FromToRotation(localLongAxis, Vector3.up);
            Quaternion assembledRotation = Quaternion.AngleAxis(-24f, Vector3.forward) * alignLongAxisToUp;
            float assembledScale = 1.55f / Component(sourceMesh.bounds.size, splitAxis);

            Camera camera = CreateCamera(targetViewpoint + Vector3.left * 0.8f);
            CreateLighting();
            CreateFloor(floorMaterial);
            CreateViewpointTarget(targetViewpoint, targetMaterial);

            GameObject root = new GameObject("Crowbar Projection Puzzle");
            GameObject fragmentsRoot = new GameObject("Fragments");
            fragmentsRoot.transform.SetParent(root.transform, false);

            float[] depthScales = { 0.78f, 1f, 1.24f };
            GameObject[] fragmentObjects = new GameObject[3];

            for (int index = 0; index < fragmentObjects.Length; index++)
            {
                float depthScale = depthScales[index];
                GameObject fragment = new GameObject($"Crowbar Piece {index + 1}");
                fragment.transform.SetParent(fragmentsRoot.transform, false);
                fragment.transform.position = targetViewpoint + (assembledPosition - targetViewpoint) * depthScale;
                fragment.transform.rotation = assembledRotation;
                fragment.transform.localScale = Vector3.one * (assembledScale * depthScale);
                AddMeshComponents(fragment, pieces[index], sourceMaterials);
                fragmentObjects[index] = fragment;
            }

            float markerRadius = SmallestPerpendicularExtent(sourceMesh.bounds.size, splitAxis) * 0.42f;
            float markerThickness = Component(sourceMesh.bounds.size, splitAxis) * 0.008f;

            ProjectionAnchorSet firstA = CreateAnchorSet(
                fragmentObjects[0].transform,
                "Joint 1 - Piece 1",
                sourceMesh.bounds,
                splitAxis,
                firstCut,
                markerRadius,
                markerThickness,
                joint1FirstMaterial);
            ProjectionAnchorSet firstB = CreateAnchorSet(
                fragmentObjects[1].transform,
                "Joint 1 - Piece 2",
                sourceMesh.bounds,
                splitAxis,
                firstCut,
                markerRadius,
                markerThickness,
                joint1SecondMaterial);
            ProjectionAnchorSet secondA = CreateAnchorSet(
                fragmentObjects[1].transform,
                "Joint 2 - Piece 2",
                sourceMesh.bounds,
                splitAxis,
                secondCut,
                markerRadius,
                markerThickness,
                joint2FirstMaterial);
            ProjectionAnchorSet secondB = CreateAnchorSet(
                fragmentObjects[2].transform,
                "Joint 2 - Piece 3",
                sourceMesh.bounds,
                splitAxis,
                secondCut,
                markerRadius,
                markerThickness,
                joint2SecondMaterial);

            GameObject completedObject = new GameObject("Completed Crowbar");
            completedObject.transform.SetParent(root.transform, false);
            completedObject.transform.position = assembledPosition;
            completedObject.transform.rotation = assembledRotation;
            completedObject.transform.localScale = Vector3.one * assembledScale;
            AddMeshComponents(completedObject, sourceMesh, sourceMaterials);
            completedObject.SetActive(false);

            CrowbarProjectionPuzzle puzzle = root.AddComponent<CrowbarProjectionPuzzle>();
            puzzle.Configure(
                camera,
                firstA,
                firstB,
                secondA,
                secondB,
                fragmentObjects,
                completedObject);
        }

        private static Camera CreateCamera(Vector3 startPosition)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = startPosition;
            cameraObject.transform.rotation = Quaternion.identity;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CrowbarDemoCameraController>();
            return camera;
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
        }

        private static void CreateFloor(Material material)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, 0f, 3.5f);
            floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            floor.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateViewpointTarget(Vector3 targetViewpoint, Material material)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = "Target HMD Position";
            target.transform.position = new Vector3(targetViewpoint.x, 0.02f, targetViewpoint.z);
            target.transform.localScale = new Vector3(0.32f, 0.01f, 0.32f);
            target.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(target.GetComponent<Collider>());
        }

        private static void AddMeshComponents(GameObject target, Mesh mesh, Material[] materials)
        {
            MeshFilter filter = target.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;

            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;
        }

        private static ProjectionAnchorSet CreateAnchorSet(
            Transform parent,
            string name,
            Bounds sourceBounds,
            int splitAxis,
            float cutCoordinate,
            float markerRadius,
            float markerThickness,
            Material markerMaterial)
        {
            GameObject anchorObject = new GameObject(name);
            anchorObject.transform.SetParent(parent, false);

            Vector3 cutCenter = sourceBounds.center;
            SetComponent(ref cutCenter, splitAxis, cutCoordinate);
            anchorObject.transform.localPosition = cutCenter;

            Vector3[] perpendicularAxes = PerpendicularAxes(splitAxis);
            Transform center = CreateAnchor(anchorObject.transform, "Center", Vector3.zero);
            Transform horizontal = CreateAnchor(
                anchorObject.transform,
                "Horizontal",
                perpendicularAxes[0] * markerRadius);
            Transform vertical = CreateAnchor(
                anchorObject.transform,
                "Vertical",
                perpendicularAxes[1] * markerRadius);

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Visible Alignment Circle";
            disc.transform.SetParent(anchorObject.transform, false);
            disc.transform.localPosition = Vector3.zero;
            disc.transform.localRotation = Quaternion.FromToRotation(Vector3.up, AxisVector(splitAxis));
            disc.transform.localScale = new Vector3(
                markerRadius * 2f,
                markerThickness * 0.5f,
                markerRadius * 2f);
            disc.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());

            ProjectionAnchorSet set = anchorObject.AddComponent<ProjectionAnchorSet>();
            set.Configure(center, horizontal, vertical);
            return set;
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 localPosition)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            return anchor.transform;
        }

        private static Mesh CreateMeshAsset(
            Mesh source,
            int axis,
            float minimum,
            float maximum,
            string assetName)
        {
            Mesh result = ClipMesh(source, axis, minimum, maximum, assetName);
            string path = $"{GeneratedMeshRoot}/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(result, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Mesh ClipMesh(Mesh source, int axis, float minimum, float maximum, string meshName)
        {
            Vector3[] sourcePositions = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector4[] sourceTangents = source.tangents;
            Vector2[] sourceUv = source.uv;
            Color[] sourceColors = source.colors;

            bool hasNormals = sourceNormals.Length == sourcePositions.Length;
            bool hasTangents = sourceTangents.Length == sourcePositions.Length;
            bool hasUv = sourceUv.Length == sourcePositions.Length;
            bool hasColors = sourceColors.Length == sourcePositions.Length;

            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>();
            List<Vector2> uv = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int>[] submeshTriangles = new List<int>[source.subMeshCount];

            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                submeshTriangles[submesh] = new List<int>();
                int[] triangles = source.GetTriangles(submesh);

                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    List<VertexData> polygon = new List<VertexData>(3)
                    {
                        ReadVertex(triangles[triangle], sourcePositions, sourceNormals, sourceTangents, sourceUv, sourceColors, hasNormals, hasTangents, hasUv, hasColors),
                        ReadVertex(triangles[triangle + 1], sourcePositions, sourceNormals, sourceTangents, sourceUv, sourceColors, hasNormals, hasTangents, hasUv, hasColors),
                        ReadVertex(triangles[triangle + 2], sourcePositions, sourceNormals, sourceTangents, sourceUv, sourceColors, hasNormals, hasTangents, hasUv, hasColors)
                    };

                    if (!float.IsNegativeInfinity(minimum))
                    {
                        polygon = ClipPolygon(polygon, axis, minimum, true);
                    }

                    if (!float.IsPositiveInfinity(maximum))
                    {
                        polygon = ClipPolygon(polygon, axis, maximum, false);
                    }

                    if (polygon.Count < 3)
                    {
                        continue;
                    }

                    for (int fan = 1; fan < polygon.Count - 1; fan++)
                    {
                        AppendVertex(polygon[0], positions, normals, tangents, uv, colors, hasNormals, hasTangents, hasUv, hasColors, submeshTriangles[submesh]);
                        AppendVertex(polygon[fan], positions, normals, tangents, uv, colors, hasNormals, hasTangents, hasUv, hasColors, submeshTriangles[submesh]);
                        AppendVertex(polygon[fan + 1], positions, normals, tangents, uv, colors, hasNormals, hasTangents, hasUv, hasColors, submeshTriangles[submesh]);
                    }
                }
            }

            Mesh result = new Mesh
            {
                name = meshName,
                indexFormat = positions.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            result.SetVertices(positions);
            if (hasNormals) result.SetNormals(normals);
            if (hasTangents) result.SetTangents(tangents);
            if (hasUv) result.SetUVs(0, uv);
            if (hasColors) result.SetColors(colors);

            result.subMeshCount = submeshTriangles.Length;
            for (int submesh = 0; submesh < submeshTriangles.Length; submesh++)
            {
                result.SetTriangles(submeshTriangles[submesh], submesh, false);
            }

            if (!hasNormals)
            {
                result.RecalculateNormals();
            }

            if (!hasTangents && hasUv)
            {
                result.RecalculateTangents();
            }

            result.RecalculateBounds();
            return result;
        }

        private static VertexData ReadVertex(
            int index,
            Vector3[] positions,
            Vector3[] normals,
            Vector4[] tangents,
            Vector2[] uv,
            Color[] colors,
            bool hasNormals,
            bool hasTangents,
            bool hasUv,
            bool hasColors)
        {
            return new VertexData
            {
                Position = positions[index],
                Normal = hasNormals ? normals[index] : Vector3.zero,
                Tangent = hasTangents ? tangents[index] : Vector4.zero,
                Uv = hasUv ? uv[index] : Vector2.zero,
                Color = hasColors ? colors[index] : Color.white
            };
        }

        private static List<VertexData> ClipPolygon(
            IReadOnlyList<VertexData> input,
            int axis,
            float boundary,
            bool keepGreater)
        {
            List<VertexData> output = new List<VertexData>();
            if (input.Count == 0)
            {
                return output;
            }

            VertexData previous = input[input.Count - 1];
            bool previousInside = IsInside(previous.Position, axis, boundary, keepGreater);

            for (int index = 0; index < input.Count; index++)
            {
                VertexData current = input[index];
                bool currentInside = IsInside(current.Position, axis, boundary, keepGreater);

                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output.Add(Intersect(previous, current, axis, boundary));
                    }

                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(Intersect(previous, current, axis, boundary));
                }

                previous = current;
                previousInside = currentInside;
            }

            return output;
        }

        private static bool IsInside(Vector3 position, int axis, float boundary, bool keepGreater)
        {
            float coordinate = Component(position, axis);
            return keepGreater ? coordinate >= boundary : coordinate <= boundary;
        }

        private static VertexData Intersect(VertexData first, VertexData second, int axis, float boundary)
        {
            float firstCoordinate = Component(first.Position, axis);
            float secondCoordinate = Component(second.Position, axis);
            float denominator = secondCoordinate - firstCoordinate;
            float t = Mathf.Abs(denominator) < 0.000001f
                ? 0f
                : Mathf.Clamp01((boundary - firstCoordinate) / denominator);
            return VertexData.Lerp(first, second, t);
        }

        private static void AppendVertex(
            VertexData vertex,
            ICollection<Vector3> positions,
            ICollection<Vector3> normals,
            ICollection<Vector4> tangents,
            ICollection<Vector2> uv,
            ICollection<Color> colors,
            bool hasNormals,
            bool hasTangents,
            bool hasUv,
            bool hasColors,
            ICollection<int> triangleIndices)
        {
            int index = positions.Count;
            positions.Add(vertex.Position);
            if (hasNormals) normals.Add(vertex.Normal.normalized);
            if (hasTangents) tangents.Add(vertex.Tangent);
            if (hasUv) uv.Add(vertex.Uv);
            if (hasColors) colors.Add(vertex.Color);
            triangleIndices.Add(index);
        }

        private static Material CreateMarkerMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return SaveMaterial(material, $"{GeneratedMaterialRoot}/{name}.mat");
        }

        private static Material CreateLitMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return SaveMaterial(material, $"{GeneratedMaterialRoot}/{name}.mat");
        }

        private static Material SaveMaterial(Material material, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void EnsureFolder(string fullPath)
        {
            string[] parts = fullPath.Split('/');
            string current = parts[0];

            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static int LongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z) return 0;
            if (size.y >= size.x && size.y >= size.z) return 1;
            return 2;
        }

        private static float SmallestPerpendicularExtent(Vector3 size, int axis)
        {
            switch (axis)
            {
                case 0: return Mathf.Min(size.y, size.z);
                case 1: return Mathf.Min(size.x, size.z);
                default: return Mathf.Min(size.x, size.y);
            }
        }

        private static Vector3[] PerpendicularAxes(int axis)
        {
            switch (axis)
            {
                case 0: return new[] { Vector3.up, Vector3.forward };
                case 1: return new[] { Vector3.right, Vector3.forward };
                default: return new[] { Vector3.right, Vector3.up };
            }
        }

        private static Vector3 AxisVector(int axis)
        {
            switch (axis)
            {
                case 0: return Vector3.right;
                case 1: return Vector3.up;
                default: return Vector3.forward;
            }
        }

        private static float Component(Vector3 value, int axis)
        {
            switch (axis)
            {
                case 0: return value.x;
                case 1: return value.y;
                default: return value.z;
            }
        }

        private static void SetComponent(ref Vector3 value, int axis, float component)
        {
            if (axis == 0) value.x = component;
            else if (axis == 1) value.y = component;
            else value.z = component;
        }

        private struct VertexData
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Tangent;
            public Vector2 Uv;
            public Color Color;

            public static VertexData Lerp(VertexData first, VertexData second, float t)
            {
                Vector3 normal = Vector3.Lerp(first.Normal, second.Normal, t);
                Vector4 tangent = Vector4.Lerp(first.Tangent, second.Tangent, t);
                if (normal.sqrMagnitude > 0f) normal.Normalize();

                Vector3 tangentDirection = new Vector3(tangent.x, tangent.y, tangent.z);
                if (tangentDirection.sqrMagnitude > 0f)
                {
                    tangentDirection.Normalize();
                    tangent.x = tangentDirection.x;
                    tangent.y = tangentDirection.y;
                    tangent.z = tangentDirection.z;
                }

                return new VertexData
                {
                    Position = Vector3.Lerp(first.Position, second.Position, t),
                    Normal = normal,
                    Tangent = tangent,
                    Uv = Vector2.Lerp(first.Uv, second.Uv, t),
                    Color = Color.Lerp(first.Color, second.Color, t)
                };
            }
        }
    }
}
