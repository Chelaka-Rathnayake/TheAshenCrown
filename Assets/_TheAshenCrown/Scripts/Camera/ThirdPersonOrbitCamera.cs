using UnityEngine;
using UnityEngine.InputSystem;
using TheAshenCrown.Player;

namespace TheAshenCrown.CameraSystem
{
    /// <summary>
    /// Lightweight, high-performance third-person orbit camera with collision detection.
    /// Perfect for low-spec laptops (Intel UHD 620) without overhead.
    /// </summary>
    public class ThirdPersonOrbitCamera : MonoBehaviour
    {
        [Header("Target Follow")]
        [Tooltip("The transform the camera pivots around (e.g., Player's head/chest pivot).")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private float followSharpness = 15f;

        [Header("Orbit Settings")]
        [SerializeField] private float mouseSensitivityX = 1.8f;
        [SerializeField] private float mouseSensitivityY = 1.5f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 65f;

        [Header("Distance & Collision")]
        [SerializeField] private float defaultDistance = 3.5f;
        [SerializeField] private float minDistance = 0.8f;
        [SerializeField] private float maxDistance = 5.0f;
        [SerializeField] private float zoomSpeed = 2.0f;
        [SerializeField] private float cameraRadius = 0.2f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        private float currentYaw;
        private float currentPitch = 15f;
        private float targetDistance;
        private float currentDistance;
        private Vector3 currentFollowPosition;

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        private void Start()
        {
            targetDistance = defaultDistance;
            currentDistance = defaultDistance;

            if (followTarget != null)
            {
                currentFollowPosition = followTarget.position + targetOffset;
                currentYaw = followTarget.eulerAngles.y;
            }

            LockCursor();
        }

        private void LateUpdate()
        {
            HandleCursorToggle();

            if (followTarget == null) return;

            HandleOrbitRotation();
            HandleZoom();
            FollowTargetPosition();
            HandleCameraCollision();
        }

        private void HandleOrbitRotation()
        {
            Vector2 look = PlayerInputHandler.Instance != null 
                ? PlayerInputHandler.Instance.LookInput 
                : Vector2.zero;

            currentYaw += look.x * mouseSensitivityX * 0.1f;
            currentPitch -= look.y * mouseSensitivityY * 0.1f;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        private void HandleZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    targetDistance = Mathf.Clamp(targetDistance - (scroll * 0.002f * zoomSpeed), minDistance, maxDistance);
                }
            }
        }

        private void FollowTargetPosition()
        {
            Vector3 targetPivot = followTarget.position + targetOffset;
            currentFollowPosition = Vector3.Lerp(currentFollowPosition, targetPivot, Time.deltaTime * followSharpness);
        }

        private void HandleCameraCollision()
        {
            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 desiredDirection = rotation * -Vector3.forward;

            // Check for environmental collision (walls, rocks, terrain)
            Ray ray = new Ray(currentFollowPosition, desiredDirection);
            float targetDist = targetDistance;

            if (Physics.SphereCast(ray, cameraRadius, out RaycastHit hit, targetDistance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                // Don't clip through obstacles
                targetDist = Mathf.Clamp(hit.distance - 0.05f, minDistance, targetDistance);
            }

            currentDistance = Mathf.Lerp(currentDistance, targetDist, Time.deltaTime * 20f);

            transform.position = currentFollowPosition + (desiredDirection * currentDistance);
            transform.LookAt(currentFollowPosition);
        }

        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleCursorToggle()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.Locked) UnlockCursor();
                else LockCursor();
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }
        }
    }
}
