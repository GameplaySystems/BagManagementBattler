using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class WaitingState : IUnitState
    {
        private readonly CombatUnit unit;

        internal WaitingState(CombatUnit unit) => this.unit = unit;
        public void Enter() => unit.EnterWaitingState();
        public Vector2 FixedTick() => unit.TickWaitingState();
        public void Exit() { }
    }
}
