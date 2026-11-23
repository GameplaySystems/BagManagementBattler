using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class DeathState : IUnitState
    {
        private const float ReleaseDelay = 0.5f;

        private readonly CombatUnit unit;
        private float releaseTime;

        internal DeathState(CombatUnit unit) => this.unit = unit;

        public void Enter()
        {
            unit.StopForDeath();
            releaseTime = Time.time + ReleaseDelay;
            unit.PlayDeathFeedback(ReleaseDelay);
        }

        public Vector2 FixedTick()
        {
            if (Time.time >= releaseTime)
            {
                unit.ReleaseToOwner();
            }

            return Vector2.zero;
        }

        public void Exit() { }
    }
}
