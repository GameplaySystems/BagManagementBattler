using System;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class UnitStateMachine
    {
        public IUnitState CurrentState { get; private set; }

        public void ChangeState(IUnitState nextState)
        {
            if (nextState == null)
            {
                throw new ArgumentNullException(nameof(nextState));
            }

            if (ReferenceEquals(CurrentState, nextState))
            {
                return;
            }

            CurrentState?.Exit();
            CurrentState = nextState;
            CurrentState.Enter();
        }

        /// <summary>Leaves the current state and holds none (used when a unit returns to its pool).</summary>
        public void Reset()
        {
            CurrentState?.Exit();
            CurrentState = null;
        }

        public Vector2 FixedTick()
        {
            return CurrentState != null ? CurrentState.FixedTick() : Vector2.zero;
        }
    }
}
