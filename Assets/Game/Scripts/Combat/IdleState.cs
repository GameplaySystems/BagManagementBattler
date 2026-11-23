using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class IdleState : IUnitState
    {
        private readonly CombatUnit unit;

        internal IdleState(CombatUnit unit) => this.unit = unit;
        public void Enter() => unit.EnterIdleState();
        public Vector2 FixedTick() => unit.TickIdleState();
        public void Exit() { }
    }
}
