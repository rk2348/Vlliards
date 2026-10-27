using System.Collections.Generic;
using UnityEngine;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame
    {
        private readonly List<MeshCollider> tableCollisionMeshes = new List<MeshCollider>();
        private readonly List<BoxCollider> cushionWalls = new List<BoxCollider>();

        private void CreateOriginalTableColliders(GameObject table)
        {
            foreach (var renderer in table.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                var mesh = renderer.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null || renderer.bounds.size.sqrMagnitude < .00001f) continue;
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                collider.sharedMaterial = bounce;
                collider.contactOffset = .001f;
                tableCollisionMeshes.Add(collider);
            }
            Physics.SyncTransforms();
        }

        private bool FindOriginalCushion(Vector3 origin, Vector3 direction, out RaycastHit closest)
        {
            closest = default;
            float distance = float.MaxValue;
            foreach (var collider in tableCollisionMeshes)
            {
                if (!collider.Raycast(new Ray(origin, direction), out var hit, 9) || hit.distance >= distance) continue;
                closest = hit;
                distance = hit.distance;
            }
            return distance < float.MaxValue;
        }

        private void CreateCushion(Vector3 position, Vector3 size, Material wood)
        {
            bool side = Mathf.Abs(position.x) > 1;
            Vector3 direction = side ? Vector3.right * Mathf.Sign(position.x) : Vector3.forward * Mathf.Sign(position.z);
            Vector3 origin = side ? new Vector3(0, .08f, position.z) : new Vector3(position.x, .08f, 0);
            RaycastHit hit = default;
            bool found = false;
            float nearest = float.MaxValue;
            foreach (float height in new[] { .03f, .08f, .15f, .21f })
            {
                origin.y = height;
                if (FindOriginalCushion(origin, direction, out var sample) && sample.distance < nearest)
                { found = true; nearest = sample.distance; hit = sample; }
            }
            if (found)
            {
                // The visible cushion is lower than the sphere's centre. Extend its
                // inward face vertically, so a constrained ball cannot cut into its lip.
                if (side) { size.x = .16f; position.x = hit.point.x + direction.x * size.x * .5f; }
                else { size.z = .16f; position.z = hit.point.z + direction.z * size.z * .5f; }
                size.y = 1.2f;
                position.y = .35f;
            }
            var wall = Box(side ? "Side cushion" : "End cushion", position, size, wood);
            wall.GetComponent<Renderer>().enabled = !originalCloth;
            var box = wall.GetComponent<BoxCollider>();
            box.contactOffset = .001f;
            cushionWalls.Add(box);
        }
    }
}
