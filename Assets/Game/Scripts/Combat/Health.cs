using System;

namespace BagManagementBattler.Combat
{
    public sealed class Health
    {
        private bool depletedRaised;

        public Health(float max)
        {
            if (!IsFinite(max) || max <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(max), "Maximum health must be finite and greater than zero.");
            }

            Max = max;
            Current = max;
        }

        public event Action Depleted;

        public float Current { get; private set; }
        public float Max { get; }
        public bool IsDepleted => Current <= 0f;

        public void TakeDamage(float amount)
        {
            if (!IsFinite(amount) || amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Damage must be finite and non-negative.");
            }

            if (amount == 0f || depletedRaised)
            {
                return;
            }

            Current = Math.Max(0f, Current - amount);
            if (Current > 0f)
            {
                return;
            }

            depletedRaised = true;
            Depleted?.Invoke();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
