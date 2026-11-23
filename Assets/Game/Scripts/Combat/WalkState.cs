using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class WalkState : IUnitState
    {
        private readonly CombatUnit unit;

        internal WalkState(CombatUnit unit) => this.unit = unit;
        public void Enter() { }

        public Vector2 FixedTick()
        {
            if (!unit.HasValidTarget)
            {
                return unit.AdvanceVelocity();
            }

            if (unit.CurrentTargetGap <= unit.EngagementRange)
            {
                unit.StateMachine.ChangeState(unit.AttackState);
                return Vector2.zero;
            }

            Vector2 slide = unit.ResolveChaseSlide();
            if (unit.IsTouchingRestingTeammate &&
                slide.magnitude < unit.WaitSlideSpeedThreshold)
            {
                unit.StateMachine.ChangeState(unit.WaitingState);
                return Vector2.zero;
            }

            return slide;
        }

        public void Exit() { }
    }
}
