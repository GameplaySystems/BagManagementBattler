using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class WalkState : IUnitState
    {
        private readonly CombatUnit unit;

        internal WalkState(CombatUnit unit) => this.unit = unit;
        public void Enter() => unit.EnterWalkState();
        public Vector2 FixedTick() => unit.TickWalkState();
        public void Exit() { }
    }
}
