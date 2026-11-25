using UnityEngine;

namespace TheAshenCrown.Player
{
    public enum BodyStance
    {
        Standing,
        Crouching,
        Prone
    }

    public enum MovementState
    {
        Idle,
        Walking,
        Jogging,
        Sprinting,
        Crouching,
        ProneCrawling,
        InAir,
        Dodging
    }

    /// <summary>
    /// Handles grounded, camera-relative third-person locomotion with CharacterController.
    /// Grounded, deliberate movement matching medieval realism.
    /// Supports:
    /// - Standing (Walk, Jog, Sprint, Jump)
    /// - Crouch (C key - sneak, 1.2m height)
    /// - Sleep/Prone (Z key - crawl/rest, 0.5m height, flat horizontal posture, stamina boost)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerLocomotion : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float jogSpeed = 4.8f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float crouchSpeed = 2.0f;
        [SerializeField] private float proneCrawlSpeed = 1.1f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float speedSmoothTime = 0.15f;

        [Header("Jumping & Gravity")]
        [SerializeField] private float jumpHeight = 1.3f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedGravity = -3f;
        [SerializeField] private float terminalFallVelocity = -35f;

        [Header("Stance Dimensions")]
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private float proneHeight = 0.5f;
        [SerializeField] private float transitionSpeed = 12f;
        [SerializeField] private Transform visualModel;

        [Header("Dodge / Roll")]
        [SerializeField] private float dodgeSpeed = 9.0f;
        [SerializeField] private float dodgeDuration = 0.45f;

        // Dependencies
        private CharacterController characterController;
        private PlayerInputHandler input;
        private PlayerStats stats;
        private Transform cameraTransform;

        // Runtime state
        private Vector3 verticalVelocity;
        private float currentSpeed;
        private float targetSpeed;
        private float speedSmoothVelocity;
        private bool isGrounded;
        private BodyStance currentStance = BodyStance.Standing;
        private MovementState currentState = MovementState.Idle;
        private float dodgeTimer;
        private Vector3 dodgeDirection;
        private float jumpCooldownTimer;
        private float currentControllerHeight;

        public BodyStance CurrentStance => currentStance;
        public MovementState CurrentState => currentState;
        public float NormalizedSpeed => currentSpeed / sprintSpeed;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputHandler>();
            stats = GetComponent<PlayerStats>();

            CapsuleCollider redundantCollider = GetComponent<CapsuleCollider>();
            if (redundantCollider != null)
            {
                Destroy(redundantCollider);
            }

            SetupVisualAlignment();

            currentControllerHeight = standingHeight;
            characterController.height = standingHeight;
            characterController.center = new Vector3(0f, standingHeight * 0.5f, 0f);

            if (UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }
        }

        private void SetupVisualAlignment()
        {
            if (visualModel != null) return;

            if (transform.childCount > 0)
            {
                visualModel = transform.GetChild(0);
                return;
            }

            MeshFilter rootFilter = GetComponent<MeshFilter>();
            MeshRenderer rootRenderer = GetComponent<MeshRenderer>();

            if (rootFilter != null && rootRenderer != null)
            {
                GameObject child = new GameObject("VisualModel");
                child.transform.SetParent(transform);
                child.transform.localPosition = new Vector3(0f, standingHeight * 0.5f, 0f);
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one;

                MeshFilter childFilter = child.AddComponent<MeshFilter>();
                childFilter.sharedMesh = rootFilter.sharedMesh;

                MeshRenderer childRenderer = child.AddComponent<MeshRenderer>();
                childRenderer.sharedMaterials = rootRenderer.sharedMaterials;

                visualModel = child.transform;

                Destroy(rootRenderer);
                Destroy(rootFilter);
            }
        }

        private void Update()
        {
            if (cameraTransform == null && UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }

            if (jumpCooldownTimer > 0f)
            {
                jumpCooldownTimer -= Time.deltaTime;
            }

            isGrounded = characterController.isGrounded;

            HandleStanceInputs();
            HandleJump();
            HandleDodge();

            if (currentState == MovementState.Dodging)
            {
                ExecuteDodgeMovement();
            }
            else
            {
                HandleLocomotion();
            }

            ApplyGravity();
            UpdateStanceGeometry();
        }

        private void HandleStanceInputs()
        {
            if (currentState == MovementState.Dodging) return;

            // 'C' exclusively toggles Crouch
            if (input.CrouchTriggered)
            {
                input.ConsumeCrouch();

                if (currentStance == BodyStance.Crouching)
                {
                    if (HasHeadClearance(standingHeight))
                    {
                        currentStance = BodyStance.Standing;
                    }
                }
                else if (currentStance == BodyStance.Prone)
                {
                    if (HasHeadClearance(crouchHeight))
                    {
                        currentStance = BodyStance.Crouching;
                    }
                }
                else // Standing
                {
                    currentStance = BodyStance.Crouching;
                }
            }

            // 'Z' exclusively toggles Prone / Sleep
            if (input.ProneTriggered)
            {
                input.ConsumeProne();

                if (currentStance == BodyStance.Prone)
                {
                    if (HasHeadClearance(standingHeight))
                    {
                        currentStance = BodyStance.Standing;
                    }
                    else if (HasHeadClearance(crouchHeight))
                    {
                        currentStance = BodyStance.Crouching;
                    }
                }
                else
                {
                    currentStance = BodyStance.Prone;
                }
            }

            // Sprinting automatically stands you up if you have ceiling clearance
            if (input.IsSprintPressed && currentStance != BodyStance.Standing && HasHeadClearance(standingHeight))
            {
                currentStance = BodyStance.Standing;
            }

            // Tell PlayerStats if we are resting in prone for stamina regen boost
            stats.SetProneResting(currentStance == BodyStance.Prone);
        }

        private bool HasHeadClearance(float desiredHeight)
        {
            if (desiredHeight <= currentControllerHeight) return true;

            Vector3 castStart = transform.position + new Vector3(0f, currentControllerHeight, 0f);
            float castDistance = desiredHeight - currentControllerHeight;
            int layerMask = ~(1 << gameObject.layer);
            return !Physics.SphereCast(castStart, characterController.radius * 0.8f, Vector3.up, out _, castDistance, layerMask, QueryTriggerInteraction.Ignore);
        }

        private void UpdateStanceGeometry()
        {
            float targetHeight = standingHeight;
            if (currentStance == BodyStance.Crouching) targetHeight = crouchHeight;
            else if (currentStance == BodyStance.Prone) targetHeight = proneHeight;

            currentControllerHeight = Mathf.MoveTowards(currentControllerHeight, targetHeight, Time.deltaTime * transitionSpeed);

            // Anchor collider base strictly at ground level Y = 0
            characterController.height = currentControllerHeight;
            characterController.center = new Vector3(0f, currentControllerHeight * 0.5f, 0f);

            // Visually transform the model
            if (visualModel != null)
            {
                if (currentStance == BodyStance.Prone)
                {
                    // Lie flat horizontally on the ground
                    Quaternion targetRot = Quaternion.Euler(90f, 0f, 0f);
                    visualModel.localRotation = Quaternion.Slerp(visualModel.localRotation, targetRot, Time.deltaTime * transitionSpeed);
                    visualModel.localPosition = Vector3.Lerp(visualModel.localPosition, new Vector3(0f, 0.25f, 0f), Time.deltaTime * transitionSpeed);
                    visualModel.localScale = Vector3.Lerp(visualModel.localScale, new Vector3(1f, 1f, 1f), Time.deltaTime * transitionSpeed);
                }
                else
                {
                    // Vertical stance (Standing or Crouching)
                    Quaternion targetRot = Quaternion.identity;
                    visualModel.localRotation = Quaternion.Slerp(visualModel.localRotation, targetRot, Time.deltaTime * transitionSpeed);

                    float normalizedScaleY = currentControllerHeight / standingHeight;
                    visualModel.localScale = Vector3.Lerp(visualModel.localScale, new Vector3(1f, normalizedScaleY, 1f), Time.deltaTime * transitionSpeed);
                    visualModel.localPosition = Vector3.Lerp(visualModel.localPosition, new Vector3(0f, currentControllerHeight * 0.5f, 0f), Time.deltaTime * transitionSpeed);
                }
            }
        }

        private void HandleJump()
        {
            if (input.JumpTriggered)
            {
                input.ConsumeJump();

                // If in Prone or Crouch, Space stands you up first!
                if (currentStance != BodyStance.Standing)
                {
                    if (HasHeadClearance(standingHeight))
                    {
                        currentStance = BodyStance.Standing;
                    }
                    return;
                }

                // If already standing, perform standard jump
                if (isGrounded && currentState != MovementState.Dodging && jumpCooldownTimer <= 0f)
                {
                    if (stats.TryConsumeJumpStamina())
                    {
                        verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                        currentState = MovementState.InAir;
                        jumpCooldownTimer = 0.2f;
                    }
                }
            }
        }

        private void HandleDodge()
        {
            if (input.DodgeTriggered && isGrounded && currentState != MovementState.Dodging)
            {
                input.ConsumeDodge();

                // Dodge stands you up
                if (currentStance != BodyStance.Standing && HasHeadClearance(standingHeight))
                {
                    currentStance = BodyStance.Standing;
                }

                if (stats.TryConsumeDodgeStamina())
                {
                    currentState = MovementState.Dodging;
                    dodgeTimer = dodgeDuration;

                    Vector2 moveInput = input.MoveInput;
                    if (moveInput.sqrMagnitude > 0.05f && cameraTransform != null)
                    {
                        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                        dodgeDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
                    }
                    else
                    {
                        dodgeDirection = -transform.forward;
                    }

                    transform.rotation = Quaternion.LookRotation(dodgeDirection);
                }
            }
        }

        private void ExecuteDodgeMovement()
        {
            dodgeTimer -= Time.deltaTime;
            characterController.Move(dodgeDirection * dodgeSpeed * Time.deltaTime);

            if (dodgeTimer <= 0f)
            {
                currentState = isGrounded ? MovementState.Idle : MovementState.InAir;
            }
        }

        private void HandleLocomotion()
        {
            Vector2 moveInput = input.MoveInput;
            bool hasMovement = moveInput.sqrMagnitude > 0.05f;

            if (!isGrounded && verticalVelocity.y < 0f)
            {
                currentState = MovementState.InAir;
            }
            else if (!hasMovement)
            {
                targetSpeed = 0f;
                if (currentStance == BodyStance.Prone) currentState = MovementState.Idle;
                else if (currentStance == BodyStance.Crouching) currentState = MovementState.Crouching;
                else currentState = MovementState.Idle;
            }
            else
            {
                if (currentStance == BodyStance.Prone)
                {
                    targetSpeed = proneCrawlSpeed;
                    currentState = MovementState.ProneCrawling;
                }
                else if (currentStance == BodyStance.Crouching)
                {
                    targetSpeed = crouchSpeed;
                    currentState = MovementState.Crouching;
                }
                else if (input.IsSprintPressed && stats.CanSprint())
                {
                    targetSpeed = sprintSpeed;
                    currentState = MovementState.Sprinting;
                    stats.ConsumeSprintStamina(Time.deltaTime);
                }
                else if (input.IsWalkToggled)
                {
                    targetSpeed = walkSpeed;
                    currentState = MovementState.Walking;
                }
                else
                {
                    targetSpeed = jogSpeed;
                    currentState = MovementState.Jogging;
                }
            }

            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

            if (hasMovement && cameraTransform != null)
            {
                Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

                characterController.Move(moveDirection * currentSpeed * Time.deltaTime);
            }
        }

        private void ApplyGravity()
        {
            if (isGrounded && verticalVelocity.y < 0f)
            {
                verticalVelocity.y = groundedGravity;
            }
            else
            {
                verticalVelocity.y += gravity * Time.deltaTime;
                verticalVelocity.y = Mathf.Max(verticalVelocity.y, terminalFallVelocity);
            }

            characterController.Move(verticalVelocity * Time.deltaTime);
        }
    }
}
