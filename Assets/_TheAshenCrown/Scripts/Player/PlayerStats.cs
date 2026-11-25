using System;
using UnityEngine;

namespace TheAshenCrown.Player
{
    /// <summary>
    /// Manages player vital attributes: Health and Stamina.
    /// Handles stamina consumption, exhaustion, and resting/prone bonuses.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        public float CurrentHealth { get; private set; }

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 18f;
        [SerializeField] private float proneRestMultiplier = 1.75f;
        [SerializeField] private float regenDelayAfterUse = 1.2f;
        [SerializeField] private float sprintStaminaCostPerSec = 14f;
        [SerializeField] private float dodgeStaminaCost = 22f;
        [SerializeField] private float jumpStaminaCost = 10f;

        public float CurrentStamina { get; private set; }
        public bool IsExhausted { get; private set; }

        private float lastStaminaUseTime;
        private bool isProneResting;

        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnStaminaChanged;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            CurrentStamina = maxStamina;
        }

        private void Update()
        {
            RegenerateStamina();
        }

        public void SetProneResting(bool resting)
        {
            isProneResting = resting;
        }

        public bool CanSprint()
        {
            return CurrentStamina > 2f && !IsExhausted;
        }

        public void ConsumeSprintStamina(float deltaTime)
        {
            ConsumeStamina(sprintStaminaCostPerSec * deltaTime);
        }

        public bool TryConsumeJumpStamina()
        {
            if (CurrentStamina >= jumpStaminaCost && !IsExhausted)
            {
                ConsumeStamina(jumpStaminaCost);
                return true;
            }
            return false;
        }

        public bool TryConsumeDodgeStamina()
        {
            if (CurrentStamina >= dodgeStaminaCost && !IsExhausted)
            {
                ConsumeStamina(dodgeStaminaCost);
                return true;
            }
            return false;
        }

        public void ConsumeStamina(float amount)
        {
            CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
            lastStaminaUseTime = Time.time;

            if (CurrentStamina <= 0.01f)
            {
                IsExhausted = true;
            }

            OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        private void RegenerateStamina()
        {
            if (Time.time < lastStaminaUseTime + regenDelayAfterUse) return;

            if (CurrentStamina < maxStamina)
            {
                float rate = staminaRegenRate * (isProneResting ? proneRestMultiplier : 1.0f);
                CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + (rate * Time.deltaTime));

                // Recover from exhaustion once above 25% stamina
                if (IsExhausted && CurrentStamina >= (maxStamina * 0.25f))
                {
                    IsExhausted = false;
                }

                OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
            }
        }

        public void TakeDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            Debug.Log("[PlayerStats] Protagonist has fallen.");
        }
    }
}
