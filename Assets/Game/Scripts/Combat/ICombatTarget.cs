using UnityEngine;

namespace BagManagementBattler.Combat
{
    public interface ICombatTarget
    {
        Vector2 Position { get; }
        Collider2D Collider { get; }
        CombatTeam Team { get; }
        bool IsTargetable { get; }
        void TakeDamage(float amount);
    }
}
