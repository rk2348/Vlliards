using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vlliards.Desktop;

public static class DesktopSceneSetup
{
    private const string ScenePath = "Assets/Scenes/DesktopGame.unity";

    public static void AssignOriginalTable()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var game in root.GetComponentsInChildren<DesktopGame>(true))
            {
                var serialized = new SerializedObject(game);
                serialized.FindProperty("tableModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modeling/Billiards.glb");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        EditorSceneManager.SaveScene(scene);
    }

    public static void InspectOriginalTable()
    {
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modeling/Billiards.glb"));
        Renderer cloth = null;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (renderer.name.ToLowerInvariant().StartsWith("green_floor")) cloth = renderer;
            if (renderer.name.StartsWith("BALL1"))
                Debug.Log($"SOURCE_BALL {renderer.name} mat={renderer.sharedMaterial.name} texture={renderer.sharedMaterial.mainTexture?.name} shader={renderer.sharedMaterial.shader.name}");
        }
        float scale = Mathf.Min(8 / cloth.bounds.size.x, 14 / cloth.bounds.size.z);
        model.transform.localScale *= scale;
        model.transform.position -= new Vector3(cloth.bounds.center.x, cloth.bounds.max.y, cloth.bounds.center.z);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            string name = renderer.name.ToLowerInvariant();
            if (name.StartsWith("ball") || name.StartsWith("stick") || name.StartsWith("taco") || name.Contains("star_balls")) continue;
            var mesh = renderer.GetComponent<MeshFilter>();
            if (mesh == null) continue;
            var collider = renderer.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.sharedMesh;
            Debug.Log($"SOURCE_RAIL {renderer.name} center={renderer.bounds.center} size={renderer.bounds.size}");
        }
        Physics.SyncTransforms();
        foreach (float y in new[] { .03f, .08f, .15f, .3f })
            foreach (Vector3 dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (Physics.Raycast(new Vector3(0, y, 0), dir, out var hit, 10)) Debug.Log($"RAIL_HIT y={y} dir={dir} point={hit.point} mesh={hit.collider.name}");
        UnityEngine.Object.DestroyImmediate(model);
    }

    [MenuItem("Vlliards/Desktop/Prepare Original Lounge")]
    public static void PrepareArtScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/BarLounge/SampleScene/BaLSample.unity", OpenSceneMode.Single);
        var sourceScene = SceneManager.GetActiveScene();
        var sourceRoots = sourceScene.GetRootGameObjects();
        var environment = new GameObject("LoungeEnvironment");
        foreach (var source in sourceRoots)
        {
            if (source.GetComponentsInChildren<Renderer>(true).Length == 0 && source.GetComponentsInChildren<Light>(true).Length == 0) continue;
            var copy = UnityEngine.Object.Instantiate(source, environment.transform, true);
            copy.name = source.name;
            foreach (var camera in copy.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(camera);
            foreach (var listener in copy.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(listener);
            foreach (var audio in copy.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(audio);
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) UnityEngine.Object.DestroyImmediate(behaviour);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                renderer.lightmapIndex = -1;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
            }
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Environment")) AssetDatabase.CreateFolder("Assets", "Environment");
        PrefabUtility.SaveAsPrefabAsset(environment, "Assets/Environment/LoungeEnvironment.prefab");
        // Discard the temporary copies; the original artist scene stays unchanged on disk.
        UnityEngine.Object.DestroyImmediate(environment);
        CreateScene();
        AssetDatabase.SaveAssets();
    }

    public static void PrepareAndVerify()
    {
        PrepareArtScene();
        VerifyPlayMode();
    }

    public static void PreviewBallView()
    {
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.EnterPlaymode();
    }

    public static void RefreshOriginalMaterialsAndPreview()
    {
        QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        // The GLB was cached with the old built-in renderer before the URP migration.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        typeof(GLTFast.RenderPipelineUtils).GetField("s_RenderPipeline", flags).SetValue(null, GLTFast.RenderPipeline.Unknown);
        typeof(GLTFast.Materials.MaterialGenerator).GetField("s_DefaultMaterialGenerator", flags).SetValue(null, null);
        AssetDatabase.ImportAsset("Assets/Modeling/Billiards.glb", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modeling/Billiards.glb");
        foreach (var renderer in asset.GetComponentsInChildren<Renderer>())
            if (renderer.name == "BALL1") Debug.Log("ORIGINAL_BALL_SHADER " + renderer.sharedMaterial.shader.name + " texture=" + renderer.sharedMaterial.mainTexture?.name);
        AssignOriginalTable();
        PreviewBallView();
    }

    public static void VerifyPlayMode()
    {
        if (!System.IO.File.Exists(ScenePath)) CreateScene();
        DesktopRulesChecks.Run();
        EditorSceneManager.OpenScene(ScenePath);
        // SessionState survives the domain reload when entering Play mode.
        SessionState.SetBool("Vlliards.Desktop.Verify", true);
        SessionState.SetFloat("Vlliards.Desktop.Deadline", (float)EditorApplication.timeSinceStartup + 120);
        System.IO.File.WriteAllText("Temp/DesktopPlayModeResult.txt", "RUNNING");
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void WatchVerification()
    {
        EditorApplication.update += () =>
        {
            if (Application.isBatchMode && SessionState.GetBool("Vlliards.Desktop.Verify", false) &&
                EditorApplication.timeSinceStartup > SessionState.GetFloat("Vlliards.Desktop.Deadline", float.MaxValue))
            {
                System.IO.File.WriteAllText("Temp/DesktopPlayModeResult.txt", "FAIL: Play mode verification timed out.");
                EditorApplication.Exit(1);
            }
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("Vlliards.Desktop.Verify", false)) return;
            SessionState.SetBool("Vlliards.Desktop.Verify", false);
            const string path = "Temp/DesktopPlayModeResult.txt";
            bool passed = System.IO.File.Exists(path) && System.IO.File.ReadAllText(path).Contains("DESKTOP_SMOKE PASS");
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        };
    }

    [MenuItem("Vlliards/Desktop/Create PC Scene")]
    public static void CreateScene()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("Vlliards Desktop");
            var game = root.AddComponent<DesktopGame>();
            var settings = new SerializedObject(game);
            settings.FindProperty("tableModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modeling/Billiards.glb");
            settings.FindProperty("loungeEnvironment").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/LoungeEnvironment.prefab");
            settings.FindProperty("music").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Billiards.mp3");
            settings.FindProperty("surfaceShader").objectReferenceValue = Shader.Find("Universal Render Pipeline/Lit");
            settings.FindProperty("guideShader").objectReferenceValue = Shader.Find("Universal Render Pipeline/Unlit");
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save PC scene.");
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
        }
    }

}
