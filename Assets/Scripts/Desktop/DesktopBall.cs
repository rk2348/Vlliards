using UnityEngine;

namespace Vlliards.Desktop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DesktopBall : MonoBehaviour
    {
        public int Number { get; private set; }
        public Rigidbody Body { get; private set; }
        public bool Potted { get; private set; }
        public DesktopShot Shot { get; set; }
        public Vector3 IncomingVelocity { get; private set; }
        public bool IsCue => Number <= 0;
        private float frozenUntil;
        public float FreezeRemaining => Mathf.Max(0, frozenUntil - Time.time);
        public bool Frozen => FreezeRemaining > 0;
        public System.Action<DesktopBall, Collision> Contact;
        private float nextLaunchTime;
        public float CooldownRemaining => Mathf.Max(0, nextLaunchTime - Time.time);
        public bool CanLaunch => !Potted && !Frozen && gameObject.activeInHierarchy && CooldownRemaining <= 0;

        public void Freeze(float seconds)
        {
            frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.isKinematic = true;
        }

        public void ResetForMatch()
        {
            StopAllCoroutines();
            frozenUntil = nextLaunchTime = Time.time;
            Body.isKinematic = false;
            Shot = null;
        }

        private void FixedUpdate()
        {
            if (Body == null) return;
            if (!Frozen && Body.isKinematic) Body.isKinematic = false;
            IncomingVelocity = Body.linearVelocity;
        }

        private void OnCollisionEnter(Collision collision) => Contact?.Invoke(this, collision);

        public bool TryLaunch(Vector3 direction, float speed, float cooldownSeconds)
        {
            direction.y = 0;
            if (!CanLaunch || direction.sqrMagnitude < .0001f || speed <= 0) return false;
            if (Body.isKinematic) Body.isKinematic = false;
            Body.linearVelocity = direction.normalized * speed;
            ConsumeCooldown(cooldownSeconds);
            return true;
        }

        internal void ConsumeCooldown(float seconds) => nextLaunchTime = Time.time + Mathf.Max(0, seconds);

        public void ResetCooldown() => nextLaunchTime = Time.time;

        public void Initialize(int number)
        {
            Number = number;
            Body = GetComponent<Rigidbody>();
            Body.mass = 1;
            Body.useGravity = false;
            Body.constraints = RigidbodyConstraints.FreezePositionY;
            Body.linearDamping = .45f;
            Body.angularDamping = .8f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.solverIterations = 12;
            Body.solverVelocityIterations = 6;
        }

        public void ResetAt(Vector3 position)
        {
            StopAllCoroutines();
            gameObject.SetActive(true);
            Potted = false;
            GetComponent<SphereCollider>().enabled = true;
            Body.isKinematic = Frozen;
            Body.position = position;
            transform.position = position;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
        }

        public void Pot()
        {
            if (Potted) return;
            Potted = true;
            Body.linearVelocity = Vector3.zero;
            gameObject.SetActive(false);
        }

        public void Pot(Vector3 pocket)
        {
            if (Potted) return;
            Potted = true;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.isKinematic = true;
            GetComponent<SphereCollider>().enabled = false;
            if (Application.isPlaying) StartCoroutine(FallIntoPocket(pocket));
            else gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator FallIntoPocket(Vector3 pocket)
        {
            Vector3 start = transform.position;
            float elapsed = 0;
            while (elapsed < .28f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / .28f);
                Vector3 position = Vector3.Lerp(start, pocket, t);
                position.y = Mathf.Lerp(start.y, -.8f, t * t);
                Body.position = position;
                transform.position = position;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
