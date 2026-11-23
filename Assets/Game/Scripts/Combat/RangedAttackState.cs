using BagManagementBattler.Bag;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class RangedAttackState : IUnitState
    {
        private readonly CombatUnit unit;

        internal RangedAttackState(CombatUnit unit) => this.unit = unit;
        public void Enter() => unit.EnterAttackState(AttackStyle.Ranged);
        public Vector2 FixedTick() => unit.TickAttackState(AttackStyle.Ranged);
        public void Exit() { }
    }
}
