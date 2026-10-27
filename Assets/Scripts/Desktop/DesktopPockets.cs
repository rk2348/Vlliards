using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame
    {
        private readonly List<float> pocketRadii = new List<float>();
        private int matchRevision;

        private void CreatePocketLayout(Material dark)
        {
            // Centres measured from the circular cut-outs in Billiards.glb's cloth.
            pockets.AddRange(new[]
            {
                new Vector3(-3.373f, 0, -6.54f), new Vector3(-3.481f, 0, .119f), new Vector3(-3.373f, 0, 6.78f),
                new Vector3(3.421f, 0, -6.54f), new Vector3(3.530f, 0, .119f), new Vector3(3.421f, 0, 6.78f)
            });
            for (int i = 0; i < pockets.Count; i++)
            {
                bool side = i % 3 == 1;
                pocketRadii.Add(side ? .34f : .38f);
                var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hole.name = "Pocket opening " + (i + 1);
                hole.transform.SetParent(transform);
                hole.transform.position = pockets[i] - Vector3.up * .025f;
                float diameter = side ? .78f : .88f;
                hole.transform.localScale = new Vector3(diameter, .02f, diameter);
                hole.GetComponent<Collider>().enabled = false;
                hole.GetComponent<Renderer>().sharedMaterial = dark;
            }
        }

        private void CreateOpenCushions(Material wood)
        {
            // Square proxy ends must stop before the curved pocket jaws, rather
            // than bridging the entrances with the old full-length boxes.
            const float cornerOpening = .56f, sideOpening = .43f;
            float leftEnd = pockets[0].x + cornerOpening, rightEnd = pockets[3].x - cornerOpening;
            foreach (int end in new[] { 0, 2 })
                CreateCushion(new Vector3((leftEnd + rightEnd) * .5f, .23f, pockets[end].z),
                    new Vector3(rightEnd - leftEnd, .5f, .16f), wood);
            foreach (int start in new[] { 0, 3 })
            {
                float x = pockets[start + 1].x;
                float south = pockets[start].z + cornerOpening, north = pockets[start + 2].z - cornerOpening;
                float lowerMouth = pockets[start + 1].z - sideOpening, upperMouth = pockets[start + 1].z + sideOpening;
                CreateCushion(new Vector3(x, .23f, (south + lowerMouth) * .5f), new Vector3(.16f, .5f, lowerMouth - south), wood);
                CreateCushion(new Vector3(x, .23f, (north + upperMouth) * .5f), new Vector3(.16f, .5f, north - upperMouth), wood);
            }
            // The decorative table shell contains faces across/below the pockets.
            // Use it for measurements, then let the open gameplay cushions govern collisions.
            foreach (var collider in tableCollisionMeshes) collider.enabled = false;
        }

        private bool IsInPocket(Vector3 position, out Vector3 centre)
        {
            for (int i = 0; i < pockets.Count; i++)
            {
                Vector3 delta = position - pockets[i];
                delta.y = 0;
                if (delta.sqrMagnitude <= pocketRadii[i] * pocketRadii[i]) { centre = pockets[i]; return true; }
            }
            centre = default;
            return false;
        }

        private IEnumerator ReturnScratchedCue(DesktopBall cue, Vector3 preferred, int revision)
        {
            yield return new WaitForSeconds(.32f);
            if (revision == matchRevision) Respawn(cue, preferred);
        }
    }
}
