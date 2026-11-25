using UnityEngine;

namespace TheAshenCrown.Player
{
    /// <summary>
    /// Lightweight immediate-mode debug HUD for testing Health, Stamina, and Stances
    /// without needing complex Canvas setup. Runs with zero overhead on Intel UHD 620.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerLocomotion))]
    public class PlayerDebugHUD : MonoBehaviour
    {
        private PlayerStats stats;
        private PlayerLocomotion locomotion;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private Texture2D staminaTex;
        private Texture2D healthTex;
        private Texture2D bgTex;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            locomotion = GetComponent<PlayerLocomotion>();
        }

        private void Start()
        {
            bgTex = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.65f));
            healthTex = MakeTex(2, 2, new Color(0.75f, 0.15f, 0.15f, 0.9f));
            staminaTex = MakeTex(2, 2, new Color(0.2f, 0.65f, 0.25f, 0.9f));
        }

        private void OnGUI()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
                };
            }

            float padding = 20f;
            float barWidth = 230f;
            float barHeight = 16f;

            // Background box
            GUI.DrawTexture(new Rect(padding - 10, padding - 10, barWidth + 20, 115), bgTex);

            // Title
            GUI.Label(new Rect(padding, padding - 5, barWidth, 20), "THE ASHEN CROWN (Alpha)", headerStyle);

            // Movement Stance tag
            string stanceTag = "";
            if (locomotion.CurrentStance == BodyStance.Prone) stanceTag = "<color=cyan>[PRONE / SLEEP]</color> ";
            else if (locomotion.CurrentStance == BodyStance.Crouching) stanceTag = "<color=yellow>[CROUCH]</color> ";
            else stanceTag = "<color=green>[STAND]</color> ";

            string stateText = $"{stanceTag}{locomotion.CurrentState} | {(stats.IsExhausted ? "<color=red>EXHAUSTED</color>" : "Ready")}";
            GUI.Label(new Rect(padding, padding + 18, barWidth, 20), stateText, labelStyle);

            // Health Bar
            float hpPct = stats.CurrentHealth / 100f;
            GUI.DrawTexture(new Rect(padding, padding + 42, barWidth, barHeight), bgTex);
            GUI.DrawTexture(new Rect(padding, padding + 42, barWidth * hpPct, barHeight), healthTex);
            GUI.Label(new Rect(padding + 5, padding + 40, barWidth, barHeight), $"HP: {Mathf.CeilToInt(stats.CurrentHealth)} / 100", labelStyle);

            // Stamina Bar
            float stamPct = stats.CurrentStamina / 100f;
            GUI.DrawTexture(new Rect(padding, padding + 66, barWidth, barHeight), bgTex);
            GUI.DrawTexture(new Rect(padding, padding + 66, barWidth * stamPct, barHeight), stats.IsExhausted ? healthTex : staminaTex);
            GUI.Label(new Rect(padding + 5, padding + 64, barWidth, barHeight), $"Stamina: {Mathf.CeilToInt(stats.CurrentStamina)} / 100", labelStyle);

            // Controls hint at bottom left
            GUI.Label(new Rect(padding, Screen.height - 40, 700, 30), "WASD: Move | Space: Jump / Stand | C: Crouch | Z: Prone / Sleep | Alt: Dodge | Shift: Sprint", labelStyle);
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnDestroy()
        {
            if (bgTex != null) Destroy(bgTex);
            if (healthTex != null) Destroy(healthTex);
            if (staminaTex != null) Destroy(staminaTex);
        }
    }
}
