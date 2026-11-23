using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class WaitingState : IUnitState
    {
        private readonly CombatUnit unit;
        private ICombatTarget targetOnEnter;

        internal WaitingState(CombatUnit unit) => this.unit = unit;
        public void Enter() => targetOnEnter = unit.CurrentTarget;

        public Vector2 FixedTick()
        {
            if (!unit.HasValidTarget || unit.CurrentTarget != targetOnEnter)
            {
                unit.StateMachine.ChangeState(unit.WalkState);
                return unit.StateMachine.FixedTick();
            }

            if (unit.CurrentTargetGap <= unit.EngagementRange)
            {
                unit.StateMachine.ChangeState(unit.AttackState);
                return Vector2.zero;
            }

            Vector2 slide = unit.ResolveChaseSlide();
            if (unit.IsTouchingRestingTeammate)
            {
                return Vector2.zero;
            }

            unit.StateMachine.ChangeState(unit.WalkState);
            return slide;
        }

        public void Exit() { }
    }
}
