using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Vlliards.Desktop;

public static class DesktopPocketChecks
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch session for physics checks.");
        var game = DesktopAssetChecks.CreatePocketTestGame(out var root);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var balls = (List<DesktopBall>)typeof(DesktopGame).GetField("balls", flags).GetValue(game);
        var pockets = (List<Vector3>)typeof(DesktopGame).GetField("pockets", flags).GetValue(game);
        var checkPockets = typeof(DesktopGame).GetMethod("CheckPockets", flags);
        var target = balls.Find(ball => ball.Number == 1);
        var previous = Physics.simulationMode;
        int passed = 0;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            for (int i = 0; i < pockets.Count; i++)
                foreach (float speed in new[] { 2f, 8f, 17f })
                    foreach (float offset in new[] { 0f, -.1f, .1f })
                    {
                        game.Restart();
                        foreach (var ball in balls) if (ball != target) ball.gameObject.SetActive(false);
                        Vector3 direction = i % 3 == 1 ? Vector3.right * Mathf.Sign(pockets[i].x) : new Vector3(pockets[i].x, 0, pockets[i].z - .119f).normalized;
                        Vector3 start = pockets[i] - direction * 1.5f + Vector3.Cross(Vector3.up, direction) * offset;
                        start.y = .3f;
                        target.ResetAt(start);
                        Physics.SyncTransforms();
                        target.TryLaunch(pockets[i] - start, speed, 0);
                        for (int step = 0; step < 150 && !target.Potted; step++)
                        {
                            Physics.Simulate(.02f);
                            checkPockets.Invoke(game, null);
                        }
                        if (!target.Potted) throw new InvalidOperationException($"Pocket {i + 1} blocked: speed={speed}, offset={offset}, stoppedAt={target.Body.position}");
                        passed++;
                    }
            Debug.Log($"DESKTOP_POCKETS PASS: {passed} real trajectories, six pockets, three speeds, three approaches.");
        }
        finally
        {
            Physics.simulationMode = previous;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
