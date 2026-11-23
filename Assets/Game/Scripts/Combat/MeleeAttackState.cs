using BagManagementBattler.Bag;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class MeleeAttackState : IUnitState
    {
        private readonly CombatUnit unit;

        internal MeleeAttackState(CombatUnit unit) => this.unit = unit;
        public void Enter() => unit.EnterAttackState(AttackStyle.Melee);
        public Vector2 FixedTick() => unit.TickAttackState(AttackStyle.Melee);
        public void Exit() { }
    }
}
