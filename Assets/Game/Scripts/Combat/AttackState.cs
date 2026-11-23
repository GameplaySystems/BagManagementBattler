using BagManagementBattler.Bag;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class AttackState : IUnitState
    {
        private readonly CombatUnit unit;
        private ICombatTarget targetOnEnter;
        private AttackStyle attackStyle;
        private float nextAttackTime;

        internal AttackState(CombatUnit unit) => this.unit = unit;

        public void Enter()
        {
            if (unit.WeaponDefinition == null)
            {
                throw new System.InvalidOperationException("An attack state requires an equipped weapon.");
            }

            targetOnEnter = unit.CurrentTarget;
            attackStyle = unit.WeaponDefinition.AttackStyle;
            unit.Body.linearVelocity = Vector2.zero;
            nextAttackTime = Time.time + unit.WeaponDefinition.AttackCooldown * 0.5f;
        }

        public Vector2 FixedTick()
        {
            if (!unit.IsAlive)
            {
                return Vector2.zero;
            }

            if (unit.CurrentTarget != targetOnEnter)
            {
                unit.StateMachine.ChangeState(unit.WalkState);
                return unit.StateMachine.FixedTick();
            }

            if (unit.WeaponDefinition.AttackStyle != attackStyle ||
                !unit.HasValidTarget ||
                unit.CurrentTargetGap > unit.EngagementReleaseRange)
            {
                unit.ClearTarget();
                unit.StateMachine.ChangeState(unit.WalkState);
                unit.RefreshTargetNow();
                return unit.StateMachine.FixedTick();
            }

            if (Time.time >= nextAttackTime)
            {
                unit.PerformAttack();
                nextAttackTime += unit.WeaponDefinition.AttackCooldown;
            }

            return Vector2.zero;
        }

        public void Exit() { }
    }
}
