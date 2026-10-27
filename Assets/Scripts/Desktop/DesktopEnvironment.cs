using System.Collections.Generic;
using UnityEngine;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame
    {
        [SerializeField] private GameObject loungeEnvironment;
        private readonly Dictionary<string, MeshFilter> artworkBalls = new Dictionary<string, MeshFilter>();
        private bool originalCloth;
        private float loungeFloor = -4.2f;

        private void CreateTableArtwork()
        {
            if (tableModel == null) return;
            var model = Instantiate(tableModel, transform);
            model.name = "Original billiard table";
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            Renderer cloth = null;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                string lower = renderer.name.ToLowerInvariant();
                if (lower == "stick1") cueArtwork = renderer.GetComponent<MeshFilter>();
                if (lower == "green_floor") cloth = renderer;
                if (lower.StartsWith("ball"))
                {
                    var mesh = renderer.GetComponent<MeshFilter>();
                    if (mesh != null) artworkBalls[renderer.name.ToUpperInvariant()] = mesh;
                    renderer.enabled = false;
                }
                if (lower.StartsWith("stick") || lower.StartsWith("taco") || lower.Contains("star_balls")) renderer.enabled = false;
            }
            var visible = model.GetComponentsInChildren<Renderer>();
            if (cloth == null || visible.Length == 0) return;
            float scale = Mathf.Min(8 / cloth.bounds.size.x, 14 / cloth.bounds.size.z);
            model.transform.localScale *= scale;
            Vector3 center = cloth.bounds.center;
            model.transform.position += new Vector3(-center.x, -cloth.bounds.max.y, -center.z);
            var bounds = cloth.bounds;
            foreach (var renderer in visible) if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
            loungeFloor = bounds.min.y;
            originalCloth = true;
            CreateOriginalTableColliders(model);
        }

        private void CreateLounge()
        {
            if (loungeEnvironment == null) return;
            var lounge = Instantiate(loungeEnvironment, transform);
            lounge.name = "Original bar lounge - architecture and furniture";
            lounge.transform.position = new Vector3(32.42f, loungeFloor, 22.07f);
            FixImportedMaterials(lounge);
            foreach (var collider in lounge.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var light in lounge.GetComponentsInChildren<Light>())
            {
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.intensity = Mathf.Min(light.intensity, 2.5f);
                light.shadows = LightShadows.None;
            }
            var tableLight = new GameObject("Warm billiard pendant light", typeof(Light)).GetComponent<Light>();
            tableLight.transform.SetParent(transform);
            tableLight.transform.position = new Vector3(0, 4, 0);
            tableLight.type = LightType.Point;
            tableLight.color = new Color(1, .85f, .65f);
            tableLight.intensity = 8;
            tableLight.range = 22;
        }

        private void ApplyBallArtwork(GameObject sphere, int number)
        {
            // The author named the 1 mesh BALL_1; BALL1 is actually the striped 11.
            string name = number <= 0 ? "BALL_WHITE" : number == 1 ? "BALL_1" : number == 8 ? "BALL_BLACK" : "BALL" + number;
            if (!artworkBalls.TryGetValue(name, out var source) || source.sharedMesh == null) return;
            var art = new GameObject("Original numbered ball", typeof(MeshFilter), typeof(MeshRenderer));
            art.transform.SetParent(sphere.transform, false);
            art.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var renderer = art.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
            Bounds bounds = source.sharedMesh.bounds;
            float scale = 1 / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            art.transform.localScale = Vector3.one * scale;
            art.transform.localPosition = -bounds.center * scale;
            sphere.GetComponent<Renderer>().sharedMaterials = renderer.sharedMaterials;
            sphere.GetComponent<Renderer>().enabled = false;
        }

        private void FixImportedMaterials(GameObject root)
        {
            var converted = new Dictionary<Material, Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null || source.shader == null ||
                        (!source.shader.name.StartsWith("Standard") && source.shader.name != "Hidden/InternalErrorShader" && !source.shader.name.StartsWith("Legacy Shaders"))) continue;
                    if (!converted.TryGetValue(source, out var target))
                    {
                        target = Material(source.HasProperty("_Color") ? source.color : Color.white);
                        if (source.HasProperty("_MainTex")) target.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                        converted[source] = target;
                    }
                    materials[i] = target;
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
