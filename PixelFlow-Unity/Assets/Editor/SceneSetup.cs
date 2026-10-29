using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PixelFlow.Core;
using PixelFlow.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelFlow.Editor
{
    [InitializeOnLoad]
    public static class SceneSetup
    {
        public const string ScenePath = "Assets/Scenes/PixelFlow.unity";
        private const string GeneratedPath = "Assets/Generated";
        private const string ReadyPath = GeneratedPath + "/SceneReady.txt";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        static SceneSetup() { EditorApplication.delayCall += EnsureFirstScene; }

        private static void EnsureFirstScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
                EditorApplication.delayCall += EnsureFirstScene;
                return;
            }
            if (!File.Exists(ReadyPath)) PrepareScene();
        }

        [MenuItem("PixelFlow/Oyun sahnesini hazırla", false, 0)]
        public static void PrepareScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(GeneratedPath + "/Meshes");
            Directory.CreateDirectory(GeneratedPath + "/Materials");
            AssetDatabase.Refresh();
            ConfigureProject();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("PixelFlow");
            GameController controller = root.AddComponent<GameController>();
            string requested = EditorPrefs.GetString("PixelFlow.EditorTestLevelId", "1");
            controller.StartupLevelId = requested;
            controller.BuildEditorPreview(FindLevelIndex(requested));
            PersistGeneratedAssets(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Oyun sahnesi kaydedilemedi: " + ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            File.WriteAllText(ReadyPath, "PixelFlow Unity 6.6 — sahne ve hacimli parçalar hazır.\n", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReadyPath);
            AssetDatabase.SaveAssets();
            EditorPrefs.DeleteKey("PixelFlow.EditorTestLevelId");
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) {
                Camera preview = root.GetComponentInChildren<Camera>();
                if (preview != null) SceneView.lastActiveSceneView.LookAt(preview.transform.position + preview.transform.forward * 65f,
                    preview.transform.rotation, preview.orthographicSize, true);
                SceneView.lastActiveSceneView.Repaint();
            }
            Debug.Log("PixelFlow hazır. Game sekmesini açıp üstteki Play üçgenine bas. Bölüm çizmek için PixelFlow > Bölüm tasarla.");
        }

        private static int FindLevelIndex(string id)
        {
            int offset = 0;
            foreach (string name in new[] { "Levels/reference-levels", "Levels/custom-levels" }) {
                TextAsset data = Resources.Load<TextAsset>(name);
                if (data == null) continue;
                LevelCollection catalog = JsonUtility.FromJson<LevelCollection>(data.text);
                if (catalog == null || catalog.levels == null) continue;
                foreach (LevelDefinition level in catalog.levels) {
                    if (level != null && level.id == id) return offset;
                    offset++;
                }
            }
            return 0;
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "PixelFlow";
            PlayerSettings.productName = "PixelFlow";
            PlayerSettings.defaultScreenWidth = 390;
            PlayerSettings.defaultScreenHeight = 844;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = null;
            QualitySettings.renderPipeline = null;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 100f;
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 1;
        }

        // Editor-generated geometry becomes normal project assets. It remains
        // visible after saving, closing and reopening Unity, before Play.
        private static void PersistGeneratedAssets(Transform root)
        {
            var savedMeshes = new Dictionary<int, Mesh>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true)) {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh)) continue;
                Mesh persisted;
                if (!savedMeshes.TryGetValue(mesh.GetInstanceID(), out persisted)) {
                    var signature = new StringBuilder();
                    foreach (Vector3 vertex in mesh.vertices)
                        signature.Append(vertex.x.ToString("R", Invariant)).Append(',').Append(vertex.y.ToString("R", Invariant)).Append(',').Append(vertex.z.ToString("R", Invariant)).Append(';');
                    foreach (int index in mesh.triangles) signature.Append(index).Append(',');
                    string path = GeneratedPath + "/Meshes/mesh-" + Hash(signature.ToString()) + ".asset";
                    persisted = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (persisted == null) { AssetDatabase.CreateAsset(mesh, path); persisted = mesh; }
                    savedMeshes[mesh.GetInstanceID()] = persisted;
                }
                filter.sharedMesh = persisted;
            }
            foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true)) {
                Mesh persisted;
                if (collider.sharedMesh != null && savedMeshes.TryGetValue(collider.sharedMesh.GetInstanceID(), out persisted)) collider.sharedMesh = persisted;
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
                Material[] list = renderer.sharedMaterials;
                for (int i = 0; i < list.Length; i++) {
                    Material mat = list[i];
                    if (mat == null || AssetDatabase.Contains(mat)) continue;
                    // Built-in font atlases retain their own Unity references.
                    if (mat.shader != null && mat.shader.name.StartsWith("GUI/", StringComparison.Ordinal)) continue;
                    string signature = (mat.shader == null ? "" : mat.shader.name) + "|" + ColorUtility.ToHtmlStringRGBA(mat.color);
                    foreach (string property in new[] { "_Glossiness", "_Metallic", "_Smoothness", "_Mode" })
                        if (mat.HasProperty(property)) signature += "|" + property + "=" + mat.GetFloat(property).ToString("R", Invariant);
                    if (mat.HasProperty("_EmissionColor")) signature += "|em=" + ColorUtility.ToHtmlStringRGBA(mat.GetColor("_EmissionColor"));
                    string[] keywords = mat.shaderKeywords;
                    Array.Sort(keywords, StringComparer.Ordinal);
                    signature += "|" + String.Join(",", keywords);
                    string path = GeneratedPath + "/Materials/material-" + Hash(signature) + ".mat";
                    Material persisted = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (persisted == null) { AssetDatabase.CreateAsset(mat, path); persisted = mat; }
                    list[i] = persisted;
                }
                renderer.sharedMaterials = list;
            }
        }

        private static string Hash(string value)
        {
            using (MD5 hash = MD5.Create()) {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
                var text = new StringBuilder(32);
                foreach (byte item in bytes) text.Append(item.ToString("x2", Invariant));
                return text.ToString();
            }
        }

        [MenuItem("PixelFlow/Oyunu çalıştır", false, 1)]
        public static void PlayGame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) PrepareScene();
            else {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            if (File.Exists(ScenePath)) EditorApplication.isPlaying = true;
        }

        [MenuItem("PixelFlow/Bilgisayar için oyun çıkar", false, 50)]
        public static void BuildDesktop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) PrepareScene();
            if (!File.Exists(ScenePath)) return;
            BuildTarget target = Application.platform == RuntimePlatform.WindowsEditor ? BuildTarget.StandaloneWindows64
                : Application.platform == RuntimePlatform.OSXEditor ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneLinux64;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target)) {
                EditorUtility.DisplayDialog("Unity kurulumuna bir parça eklemek gerekiyor", "Unity Hub > Installs > Unity 6.6 yanındaki menü > Add modules bölümünden bilgisayarına uygun Build Support seçeneğini ekle.", "Tamam");
                return;
            }
            string folder = EditorUtility.SaveFolderPanel("Oyunu nereye kaydedelim?", "", "PixelFlow-Oyun");
            if (String.IsNullOrEmpty(folder)) return;
            string name = target == BuildTarget.StandaloneWindows64 ? "PixelFlow.exe" : target == BuildTarget.StandaloneOSX ? "PixelFlow.app" : "PixelFlow";
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = target,
                locationPathName = Path.Combine(folder, name), options = BuildOptions.None });
            if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(Path.Combine(folder, name));
            else Debug.LogError("Oyun dosyası hazırlanamadı. Console bölümündeki ilk kırmızı mesaja bak.");
        }
    }
}
