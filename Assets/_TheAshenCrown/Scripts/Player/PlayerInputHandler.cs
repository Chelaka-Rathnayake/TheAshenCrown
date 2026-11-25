using UnityEngine;
using UnityEngine.InputSystem;

namespace TheAshenCrown.Player
{
    /// <summary>
    /// Centralized input reader using Unity's New Input System.
    /// Handles Movement, Sprint, Jump, Crouch (C), Prone/Sleep (Z), Dodge (Alt/V), and Combat.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        public static PlayerInputHandler Instance { get; private set; }

        [Header("Movement Inputs")]
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprintPressed { get; private set; }
        public bool IsWalkToggled { get; private set; }
        public bool JumpTriggered { get; private set; }
        public bool CrouchTriggered { get; private set; }
        public bool ProneTriggered { get; private set; }
        public bool DodgeTriggered { get; private set; }

        [Header("Action Inputs")]
        public bool LightAttackTriggered { get; private set; }
        public bool HeavyAttackTriggered { get; private set; }
        public bool BlockPressed { get; private set; }
        public bool InteractTriggered { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            ReadMovementInputs();
            ReadActionInputs();
        }

        private void ReadMovementInputs()
        {
            Vector2 move = Vector2.zero;
            Keyboard kb = Keyboard.current;

            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;

                IsSprintPressed = kb.leftShiftKey.isPressed;

                // Space for Jump (or stand up from prone/crouch)
                if (kb.spaceKey.wasPressedThisFrame)
                {
                    JumpTriggered = true;
                }

                // C exclusively for Crouch toggle
                if (kb.cKey.wasPressedThisFrame)
                {
                    CrouchTriggered = true;
                }

                // Z exclusively for Prone / Sleep-Prone toggle
                if (kb.zKey.wasPressedThisFrame)
                {
                    ProneTriggered = true;
                }

                // Left Alt or V for Dodge / Roll
                if (kb.leftAltKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame)
                {
                    DodgeTriggered = true;
                }

                // X for slow walk toggle
                if (kb.xKey.wasPressedThisFrame)
                {
                    IsWalkToggled = !IsWalkToggled;
                }

                InteractTriggered = kb.eKey.wasPressedThisFrame;
            }

            MoveInput = move.normalized;

            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                LookInput = mouse.delta.ReadValue();
            }
            else
            {
                LookInput = Vector2.zero;
            }
        }

        private void ReadActionInputs()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                LightAttackTriggered = mouse.leftButton.wasPressedThisFrame;
                BlockPressed = mouse.rightButton.isPressed;
            }
            else
            {
                LightAttackTriggered = false;
                BlockPressed = false;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                HeavyAttackTriggered = kb.fKey.wasPressedThisFrame;
            }
        }

        public void ConsumeJump() => JumpTriggered = false;
        public void ConsumeCrouch() => CrouchTriggered = false;
        public void ConsumeProne() => ProneTriggered = false;
        public void ConsumeDodge() => DodgeTriggered = false;
        public void ConsumeLightAttack() => LightAttackTriggered = false;
        public void ConsumeHeavyAttack() => HeavyAttackTriggered = false;
        public void ConsumeInteract() => InteractTriggered = false;
    }
}
