using System;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerBase : MonoBehaviour, ICombatTarget
    {
        public const float DefaultMaxHealth = 100f;

        [SerializeField, Min(0.01f)] private float maxHealth = DefaultMaxHealth;

        private Collider2D targetCollider;

        public event Action Depleted;
        public Health Health { get; private set; }
        public Vector2 Position => transform.position;
        public Collider2D Collider => targetCollider;
        public CombatTeam Team => CombatTeam.Friendly;
        public bool IsTargetable =>
            isActiveAndEnabled && gameObject.activeInHierarchy &&
            Health != null && !Health.IsDepleted;

        private void Awake()
        {
            targetCollider = GetComponent<Collider2D>();
            Health = new Health(maxHealth);
            Health.Depleted += HandleDepleted;
        }

        private void OnDestroy()
        {
            if (Health != null)
            {
                Health.Depleted -= HandleDepleted;
            }
        }

        public void TakeDamage(float amount)
        {
            Health.TakeDamage(amount);
        }

        private void HandleDepleted()
        {
            Depleted?.Invoke();
        }
    }
}
