using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class DeathState : IUnitState
    {
        private readonly CombatUnit unit;

        internal DeathState(CombatUnit unit) => this.unit = unit;
        public void Enter() => unit.EnterDeathState();
        public Vector2 FixedTick() => unit.TickDeathState();
        public void Exit() { }
    }
}
