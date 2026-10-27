using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vlliards.Desktop;

public static class DesktopAssetChecks
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void VerifyAndSmoke()
    {
        Run();
        DesktopPocketChecks.Run();
        DesktopSceneSetup.AssignOriginalTable();
        DesktopSceneSetup.VerifyPlayMode();
    }

    public static void VerifyAndPreview()
    {
        Run();
        DesktopSceneSetup.PreviewBallView();
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run these physics checks in an isolated batch project.");
        var root = new GameObject("Asset and cushion checks");
        var game = root.AddComponent<DesktopGame>();
        var serialized = new SerializedObject(game);
        serialized.FindProperty("tableModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modeling/Billiards.glb");
        serialized.FindProperty("surfaceShader").objectReferenceValue = Shader.Find("Universal Render Pipeline/Lit");
        serialized.FindProperty("guideShader").objectReferenceValue = Shader.Find("Universal Render Pipeline/Unlit");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var previousMode = Physics.simulationMode;
        try
        {
            typeof(DesktopGame).GetMethod("BuildArena", Fields).Invoke(game, null);
            var balls = (List<DesktopBall>)typeof(DesktopGame).GetField("balls", Fields).GetValue(game);
            var sources = (Dictionary<string, MeshFilter>)typeof(DesktopGame).GetField("artworkBalls", Fields).GetValue(game);
            var walls = (List<BoxCollider>)typeof(DesktopGame).GetField("cushionWalls", Fields).GetValue(game);
            int preserved = 0;
            foreach (var ball in balls)
            {
                string name = ball.Number <= 0 ? "BALL_WHITE" : ball.Number == 1 ? "BALL_1" : ball.Number == 8 ? "BALL_BLACK" : "BALL" + ball.Number;
                var actual = ball.transform.Find("Original numbered ball").GetComponent<MeshRenderer>();
                var expected = sources[name].GetComponent<Renderer>();
                if (actual.sharedMaterial != expected.sharedMaterial) throw new InvalidOperationException("Original material replaced on " + name);
                if (actual.GetComponent<MeshFilter>().sharedMesh != sources[name].sharedMesh) throw new InvalidOperationException("Wrong numbered mesh on " + name);
                if (AssetDatabase.GetAssetPath(actual.sharedMaterial) != "Assets/Modeling/Billiards.glb") throw new InvalidOperationException("Material is not from the original GLB: " + name);
                preserved++;
                ball.gameObject.SetActive(false);
            }
            var probe = balls[0];
            probe.gameObject.SetActive(true);
            float radius = probe.GetComponent<SphereCollider>().radius * probe.transform.lossyScale.x;
            Physics.simulationMode = SimulationMode.Script;
            Physics.SyncTransforms();
            float worstPenetration = 0;
            int rebounds = 0;
            foreach (var wall in walls)
            {
                bool side = wall.name.StartsWith("Side");
                Vector3 normal = side ? Vector3.right * Mathf.Sign(wall.bounds.center.x) : Vector3.forward * Mathf.Sign(wall.bounds.center.z);
                Vector3 surface = wall.bounds.center - normal * (side ? wall.bounds.extents.x : wall.bounds.extents.z);
                surface.y = .3f;
                foreach (float tangentSpeed in new[] { 0f, 3f, -3f })
                {
                    probe.ResetForMatch();
                    probe.ResetAt(surface - normal * (radius + 1.5f));
                    Physics.SyncTransforms();
                    probe.TryLaunch(normal * 17 + Vector3.Cross(Vector3.up, normal) * tangentSpeed, 17, 0);
                    bool reflected = false;
                    for (int step = 0; step < 22; step++)
                    {
                        Physics.Simulate(.02f);
                        float penetration = Vector3.Dot(probe.Body.position - surface, normal) + radius;
                        worstPenetration = Mathf.Max(worstPenetration, penetration);
                        if (penetration > .004f) throw new InvalidOperationException($"Wall penetration {penetration:F5} at {wall.name}, tangent={tangentSpeed}");
                        if (Vector3.Dot(probe.Body.linearVelocity, normal) < -1) reflected = true;
                    }
                    if (!reflected) throw new InvalidOperationException("No rebound from " + wall.name);
                    rebounds++;
                }
            }
            string report = $"DESKTOP_ASSETS PASS: originalGlbMaterials={preserved}, highSpeedRebounds={rebounds}, worstWallPenetration={worstPenetration:F5}";
            Debug.Log(report);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "../Temp"));
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Temp/DesktopAssetCheckResult.txt")), report);
        }
        finally
        {
            Physics.simulationMode = previousMode;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    internal static DesktopGame CreatePocketTestGame(out GameObject root)
    {
        root = new GameObject("Pocket trajectory checks");
        var game = root.AddComponent<DesktopGame>();
        var serialized = new SerializedObject(game);
        serialized.FindProperty("tableModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modeling/Billiards.glb");
        serialized.FindProperty("surfaceShader").objectReferenceValue = Shader.Find("Universal Render Pipeline/Lit");
        serialized.FindProperty("guideShader").objectReferenceValue = Shader.Find("Universal Render Pipeline/Unlit");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        typeof(DesktopGame).GetMethod("BuildArena", Fields).Invoke(game, null);
        return game;
    }
}
