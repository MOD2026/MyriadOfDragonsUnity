using UnityEngine;
using UnityEngine.Events;

namespace MyriadOfDragons.Combat
{
    /// <summary>Payload sent to the visual layer when a card's passive triggers.</summary>
    [System.Serializable]
    public struct PassiveAuraData
    {
        public string Element;
        public bool IsPlayerOwned;
        public Color AuraColor;
    }

    /// <summary>UnityEvent variant carrying a PassiveAuraData payload - shows up in the Inspector.</summary>
    [System.Serializable]
    public class PassiveAuraEvent : UnityEvent<PassiveAuraData> { }

    /// <summary>
    /// Drives turn-based combat animation state triggers. Artists hook particle systems,
    /// camera moves, etc. onto <see cref="OnPassiveAuraTriggered"/> directly in the Inspector -
    /// no code changes needed to attach new visuals.
    /// </summary>
    public class CardCombatAnimator : MonoBehaviour
    {
        [Header("Aura Colors")]
        [SerializeField] private Color _playerAuraColor = Color.blue;
        [SerializeField] private Color _enemyAuraColor = Color.red;

        [Header("Events (attach particle systems / VFX here in the Inspector)")]
        [SerializeField] private PassiveAuraEvent _onPassiveAuraTriggered = new PassiveAuraEvent();
        public PassiveAuraEvent OnPassiveAuraTriggered => _onPassiveAuraTriggered;

        /// <summary>
        /// Fires the passive-activation aura for a card. Aura color is determined by
        /// ownership - blue for the player's card, red for the enemy's - matching the
        /// original design doc's "blue backdrop when player's card active, red when enemy's"
        /// rule. It is not gated by element, so every element can trigger it; `element` is
        /// still passed through in the event payload in case a per-element particle style is
        /// wanted later (e.g. a different preset for Ktini vs. Pnevmas).
        /// </summary>
        public void TriggerPassiveAura(string element, bool isPlayerOwned)
        {
            Color auraColor = isPlayerOwned ? _playerAuraColor : _enemyAuraColor;

            var data = new PassiveAuraData
            {
                Element = element,
                IsPlayerOwned = isPlayerOwned,
                AuraColor = auraColor,
            };

            _onPassiveAuraTriggered.Invoke(data);
        }
    }
}
