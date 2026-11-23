using UnityEngine;

namespace BagManagementBattler.Combat
{
    public interface IUnitState
    {
        void Enter();
        Vector2 FixedTick();
        void Exit();
    }
}
