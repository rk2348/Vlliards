using UnityEngine;

namespace Vlliards.Desktop
{
    // Presentation only: the cue and hands never participate in ball physics.
    public sealed class CueStrokeVisual
    {
        private readonly GameObject root;
        private readonly Transform shaft, grip, bridge;
        private readonly LineRenderer ring, directionLine;
        private readonly bool localView;
        private float strikeAt = -100;
        private Vector3 strikeOrigin, strikeDirection;
        public bool Visible => root.activeSelf;
        public bool Preparing { get; private set; }
        public float Charge { get; private set; }
        public Vector3 TipPosition { get; private set; }
        public Vector3 AimDirection { get; private set; }
        public bool UsesOriginalCue { get; }

        public CueStrokeVisual(Transform parent, bool local, MeshFilter original, Material wood, Material glove, Material sleeve, Material signal)
        {
            localView = local;
            root = new GameObject(local ? "Player shot cue and hands" : "CPU shot cue and hands");
            root.transform.SetParent(parent, false);
            shaft = new GameObject("Animated cue and rear hand").transform;
            shaft.SetParent(root.transform, false);
            float length = local ? 1.15f : 2;
            if (original != null && original.sharedMesh != null)
            {
                var art = new GameObject("Original STICK1 cue", typeof(MeshFilter), typeof(MeshRenderer));
                art.transform.SetParent(shaft, false);
                art.GetComponent<MeshFilter>().sharedMesh = original.sharedMesh;
                art.GetComponent<MeshRenderer>().sharedMaterials = original.GetComponent<Renderer>().sharedMaterials;
                // Principal axis and centre of the author's diagonally modelled STICK1.
                Quaternion rotation = Quaternion.FromToRotation(new Vector3(.21433787f, -.97675958f, 0), Vector3.forward);
                float scale = length / 17.690962f;
                art.transform.localRotation = rotation;
                art.transform.localScale = Vector3.one * scale;
                art.transform.localPosition = Vector3.back * (.3f + length * .5f) - rotation * new Vector3(2.765167f, .452818f, -6.963936f) * scale;
                UsesOriginalCue = true;
            }
            else
                Part(shaft, "Cue shaft", PrimitiveType.Cylinder, new Vector3(0, 0, -.3f - length * .5f), new Vector3(.025f, length * .5f, .025f), wood, Quaternion.Euler(90, 0, 0));
            grip = Hand(shaft, "Rear grip", new Vector3(0, -.025f, local ? -.62f : -1.15f), glove);
            bridge = Hand(root.transform, "Bridge hand", new Vector3(.075f, -.055f, -.38f), glove);
            Part(grip, "Sleeve", PrimitiveType.Capsule, new Vector3(.06f, -.11f, -.20f), new Vector3(.10f, .20f, .10f), sleeve, Quaternion.Euler(65, -12, 0));
            Part(bridge, "Sleeve", PrimitiveType.Capsule, new Vector3(.22f, -.10f, -.13f), new Vector3(.10f, .20f, .10f), sleeve, Quaternion.Euler(55, -55, 0));
            ring = Line(root.transform, "Charge ring", signal, .025f);
            directionLine = Line(root.transform, "Shot intention", signal, .015f);
            root.SetActive(false);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            return part;
        }

        private static Transform Hand(Transform parent, string name, Vector3 position, Material glove)
        {
            var hand = new GameObject(name).transform;
            hand.SetParent(parent, false);
            hand.localPosition = position;
            Part(hand, "Palm", PrimitiveType.Sphere, Vector3.zero, new Vector3(.12f, .065f, .16f), glove, Quaternion.identity);
            for (int finger = 0; finger < 4; finger++)
                Part(hand, "Finger", PrimitiveType.Capsule, new Vector3((finger - 1.5f) * .026f, .012f, .065f), new Vector3(.026f, .055f, .026f), glove, Quaternion.Euler(65, 0, 0));
            Part(hand, "Thumb", PrimitiveType.Capsule, new Vector3(-.065f, .015f, .015f), new Vector3(.032f, .052f, .032f), glove, Quaternion.Euler(65, 0, -40));
            return hand;
        }

        private static LineRenderer Line(Transform parent, string name, Material material, float width)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.startWidth = line.endWidth = width;
            line.enabled = false;
            return line;
        }

        public void Reset()
        {
            strikeAt = -100;
            Preparing = false;
            Charge = 0;
            root.SetActive(false);
        }

        public void Strike(Vector3 origin, Vector3 direction, float now)
        {
            strikeAt = now;
            strikeOrigin = origin;
            strikeDirection = direction.normalized;
        }

        public void Render(DesktopBall ball, Vector3 direction, bool preparing, float charge, float now, Camera camera)
        {
            float age = now - strikeAt;
            bool striking = age >= 0 && age < .34f;
            bool visible = ball != null && ball.gameObject.activeInHierarchy && !ball.Potted && !ball.Frozen && (preparing || striking);
            root.SetActive(visible);
            Preparing = visible && preparing && !striking;
            Charge = Preparing ? Mathf.Clamp01(charge) : 0;
            if (!visible) return;
            AimDirection = striking ? strikeDirection : direction.normalized;
            if (localView)
                root.transform.SetPositionAndRotation(camera.transform.position + camera.transform.right * .24f - camera.transform.up * .14f + camera.transform.forward * 1.3f, camera.transform.rotation);
            else
                root.transform.SetPositionAndRotation(striking ? strikeOrigin : ball.transform.position, Quaternion.LookRotation(AimDirection, Vector3.up));

            float pull;
            if (striking)
            {
                pull = age < .085f ? Mathf.Lerp(-.48f, .06f, Mathf.SmoothStep(0, 1, age / .085f)) : Mathf.Lerp(.06f, -.15f, (age - .085f) / .255f);
                shaft.localPosition = new Vector3(0, -Mathf.Max(0, age - .18f) * 2, pull);
            }
            else
            {
                pull = -.06f - Mathf.SmoothStep(0, 1, Charge) * .38f + Mathf.Sin(now * 10) * .018f * (1 - Charge);
                shaft.localPosition = new Vector3(0, 0, pull);
            }
            bridge.localRotation = Quaternion.Euler(0, 0, Preparing ? -Charge * 8 : 0);
            TipPosition = root.transform.TransformPoint(shaft.localPosition + Vector3.back * .3f);
            ring.enabled = directionLine.enabled = Preparing && !localView;
            if (ring.enabled)
            {
                int count = Mathf.Max(2, Mathf.CeilToInt(Charge * 40) + 1);
                ring.positionCount = count;
                for (int i = 0; i < count; i++)
                {
                    float angle = (float)i / (count - 1) * Charge * Mathf.PI * 2;
                    ring.SetPosition(i, new Vector3(Mathf.Sin(angle) * .45f, -.265f, Mathf.Cos(angle) * .45f));
                }
                directionLine.positionCount = 2;
                directionLine.SetPosition(0, new Vector3(0, -.20f, .35f));
                directionLine.SetPosition(1, new Vector3(0, -.20f, 1.9f));
            }
        }
    }
}
