using UnityEngine;

namespace BagManagementBattler.Combat
{
    public static class UnitVisualLayout
    {
        public static readonly Vector2 BodyPositionInCells = new Vector2(0f, -0.38f);
        public static readonly Vector2 HeadPositionInCells = new Vector2(0f, 0.43f);
        public const float ScaleInCells = 0.34f;

        // Field parts are laid out at this fixed size (weapon hand poses were authored against it);
        // on-field size comes from the CombatUnit "Visual" root scale, not the bag cell size.
        public const float FieldLayoutCellSize = 0.65f;
    }
}
