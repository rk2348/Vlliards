using UnityEngine;
using UnityEngine.InputSystem;

namespace Vlliards.Desktop
{
    public sealed partial class DesktopGame
    {
        [SerializeField, Range(.02f, .5f)] private float mouseSensitivity = .12f;
        private float lookYaw, lookPitch = 6;

        private void SetMouseCapture(bool captured)
        {
            if (Application.isBatchMode) return;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private void ReadBallViewInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!Application.isBatchMode && Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame) SetMouseCapture(true);
                return;
            }
            Vector2 delta = mouse.delta.ReadValue();
            lookYaw += delta.x * mouseSensitivity;
            lookPitch = Mathf.Clamp(lookPitch - delta.y * mouseSensitivity * (appliedPreferences != null && appliedPreferences.invertY ? -1 : 1), -50, 50);
            aim = Quaternion.Euler(0, lookYaw, 0) * Vector3.forward;
            if (mouse.leftButton.wasPressedThisFrame && player.CanLaunch) { charging = true; charge = 0; }
            if (player.Frozen) { charging = false; charge = 0; }
            if (charging) charge = Mathf.Clamp01(charge + Time.deltaTime / 1.2f);
            if (mouse.leftButton.wasReleasedThisFrame && charging)
            {
                Launch(Mathf.Lerp(5, 17, charge));
                charging = false;
            }
        }

        private Vector3 ViewRelativeMovement(Vector2 input)
        {
            return Quaternion.Euler(0, lookYaw, 0) * new Vector3(input.x, 0, input.y);
        }

        private void LateUpdate()
        {
            if (view == null || player == null) return;
            if (titleCamera) { RenderTitleCamera(); return; }
            UpdateBallView();
            UpdateShotMotion();
        }

        private void UpdateBallView()
        {
            // Follow the interpolated position, never the sphere's rolling rotation.
            view.transform.SetPositionAndRotation(player.transform.position + Vector3.up * .12f,
                Quaternion.Euler(lookPitch, lookYaw, 0));
        }

    }
}
