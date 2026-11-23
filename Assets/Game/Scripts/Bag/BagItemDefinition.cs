using System;
using System.Collections.Generic;
using BagManagementBattler.Combat;
using UnityEngine;

namespace BagManagementBattler.Bag
{
    public enum BagItemRole
    {
        Soldier,
        Weapon
    }

    public enum AttackStyle
    {
        Melee,
        Ranged
    }

    [Serializable]
    public sealed class BagItemVisualPart
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private Vector2 localPositionInCells;
        [SerializeField] private float scaleInCells = 1f;

        public Sprite Sprite => sprite;
        public Vector2 LocalPositionInCells => localPositionInCells;
        public float ScaleInCells => scaleInCells;
    }

    [CreateAssetMenu(fileName = "BagItemDefinition", menuName = "Bag Game/Bag Item Definition")]
    public sealed class BagItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemTypeId = "item";
        [SerializeField] private int level = 1;
        [SerializeField] private BagItemDefinition nextLevelDefinition;
        [SerializeField] private BagItemRole role = BagItemRole.Soldier;
        [SerializeField, Tooltip("Used only when Role is Weapon.")]
        private float productionDurationSeconds;
        [SerializeField, Tooltip("Used only when Role is Soldier.")]
        private UnitDefinition unitDefinition;
        [SerializeField, Tooltip("Used only when Role is Weapon.")]
        private float movementSpeedMultiplier = 1f;
        [SerializeField, Tooltip("Used only when Role is Weapon.")]
        private float engagementRange = 0.75f;
        [SerializeField, Tooltip("Used only when Role is Weapon."), Min(0.01f)]
        private float damage = 1f;
        [SerializeField, Tooltip("Used only when Role is Weapon."), Min(0.01f)]
        private float attackCooldown = 1f;
        [SerializeField, Tooltip("Used only when Role is Weapon.")]
        private AttackStyle attackStyle;
        [SerializeField, Tooltip("Used only when Role is Weapon."), Min(0f)]
        private float knockbackDistance;
        [SerializeField, Tooltip("Used only when Role is Weapon. Zero damages only the target."), Min(0f)]
        private float areaHitRadius;
        [SerializeField] private List<Vector2Int> footprint = new List<Vector2Int> { Vector2Int.zero };
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Color itemColor = Color.white;
        [SerializeField] private List<BagItemVisualPart> visualParts = new List<BagItemVisualPart>();
        [SerializeField, Tooltip("Weapon visual position relative to a soldier HandPoint.")]
        private Vector2 equippedLocalPosition;
        [SerializeField, Tooltip("Weapon visual Z rotation relative to a soldier HandPoint.")]
        private float equippedLocalRotationDegrees;
        [SerializeField, Tooltip("Weapon visual scale relative to a soldier HandPoint.")]
        private Vector2 equippedLocalScale = Vector2.one;

        public string ItemTypeId => itemTypeId;
        public int Level => level;
        public BagItemDefinition NextLevelDefinition => nextLevelDefinition;
        public BagItemRole Role => role;
        public float ProductionDurationSeconds => productionDurationSeconds;
        public UnitDefinition UnitDefinition => unitDefinition;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public float EngagementRange => engagementRange;
        public float Damage => damage;
        public float AttackCooldown => attackCooldown;
        public AttackStyle AttackStyle => attackStyle;
        public float KnockbackDistance => knockbackDistance;
        public float AreaHitRadius => areaHitRadius;
        public IReadOnlyList<Vector2Int> Footprint => footprint;
        public Sprite IconSprite => iconSprite;
        public Color ItemColor => itemColor;
        public IReadOnlyList<BagItemVisualPart> VisualParts => visualParts;
        public Vector2 EquippedLocalPosition => equippedLocalPosition;
        public float EquippedLocalRotationDegrees => equippedLocalRotationDegrees;
        public Vector2 EquippedLocalScale => equippedLocalScale;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(itemTypeId))
            {
                throw new InvalidOperationException("An item type identifier is required.");
            }

            if (level < 1)
            {
                throw new InvalidOperationException("An item definition level must be at least one.");
            }

            if (!Enum.IsDefined(typeof(BagItemRole), role))
            {
                throw new InvalidOperationException($"Unknown bag item role {role}.");
            }

            if (role == BagItemRole.Weapon &&
                (float.IsNaN(productionDurationSeconds) ||
                 float.IsInfinity(productionDurationSeconds) ||
                 productionDurationSeconds <= 0f))
            {
                throw new InvalidOperationException("A weapon production duration must be finite and greater than zero.");
            }

            if (role == BagItemRole.Soldier)
            {
                if (unitDefinition == null)
                {
                    throw new InvalidOperationException(
                        $"Soldier item '{itemTypeId}' must reference a friendly unit definition.");
                }

                unitDefinition.Validate();
                if (unitDefinition.Team != CombatTeam.Friendly)
                {
                    throw new InvalidOperationException(
                        $"Soldier item '{itemTypeId}' must reference a friendly unit definition, not {unitDefinition.Team}.");
                }
            }

            if (role == BagItemRole.Weapon &&
                (!IsFinite(movementSpeedMultiplier) || movementSpeedMultiplier <= 0f ||
                 !IsFinite(engagementRange) || engagementRange <= 0f))
            {
                throw new InvalidOperationException(
                    "A weapon requires finite positive movement multiplier and engagement range values.");
            }

            if (role == BagItemRole.Weapon &&
                (!IsFinite(damage) || damage <= 0f ||
                 !IsFinite(attackCooldown) || attackCooldown <= 0f ||
                 !Enum.IsDefined(typeof(AttackStyle), attackStyle) ||
                 !IsFinite(knockbackDistance) || knockbackDistance < 0f ||
                 !IsFinite(areaHitRadius) || areaHitRadius < 0f))
            {
                throw new InvalidOperationException(
                    "A weapon requires finite positive damage/cooldown, a known attack style, and finite non-negative knockback/area values.");
            }

            if (role == BagItemRole.Weapon &&
                (!IsFinite(equippedLocalPosition.x) ||
                 !IsFinite(equippedLocalPosition.y) ||
                 !IsFinite(equippedLocalRotationDegrees) ||
                 !IsFinite(equippedLocalScale.x) ||
                 !IsFinite(equippedLocalScale.y) ||
                 equippedLocalScale.x <= 0f ||
                 equippedLocalScale.y <= 0f))
            {
                throw new InvalidOperationException(
                    "A weapon equipped pose requires finite position/rotation and positive scale.");
            }

            if (footprint == null || footprint.Count == 0)
            {
                throw new InvalidOperationException("An item footprint must contain at least one cell.");
            }

            HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in footprint)
            {
                if (!uniqueCells.Add(cell))
                {
                    throw new InvalidOperationException($"Footprint contains duplicate cell {cell}.");
                }
            }
        }

        /// <summary>Test cooldown long enough that a fixture weapon never lands a hit (first hit at half of it).</summary>
        public const float NonLethalTestAttackCooldown = 1000f;

        public static BagItemDefinition CreateTransient(
            string itemTypeId,
            IEnumerable<Vector2Int> footprint)
        {
            return CreateTransient(itemTypeId, 1, footprint, null);
        }

        public static BagItemDefinition CreateTransient(
            string itemTypeId,
            int level,
            IEnumerable<Vector2Int> footprint,
            BagItemDefinition nextLevelDefinition = null)
        {
            return CreateTransientConfigured(
                itemTypeId,
                level,
                BagItemRole.Soldier,
                0f,
                CreateFixtureFriendlyUnit(),
                1f,
                0.75f,
                1f,
                1f,
                AttackStyle.Melee,
                0f,
                0f,
                footprint,
                nextLevelDefinition);
        }

        public static BagItemDefinition CreateTransient(
            string itemTypeId,
            int level,
            BagItemRole role,
            float productionDurationSeconds,
            IEnumerable<Vector2Int> footprint,
            BagItemDefinition nextLevelDefinition = null)
        {
            return CreateTransientConfigured(
                itemTypeId,
                level,
                role,
                productionDurationSeconds,
                role == BagItemRole.Soldier ? CreateFixtureFriendlyUnit() : null,
                1f,
                0.75f,
                1f,
                1f,
                AttackStyle.Melee,
                0f,
                0f,
                footprint,
                nextLevelDefinition);
        }

        // Generic transient items default to the Soldier role; give them a valid
        // friendly unit so grid/merge fixtures satisfy soldier validation.
        private static UnitDefinition CreateFixtureFriendlyUnit()
        {
            return UnitDefinition.CreateTransient(
                CombatTeam.Friendly,
                UnitArchetype.Basic,
                1,
                1f);
        }

        public static BagItemDefinition CreateTransientSoldier(
            string itemTypeId,
            int level,
            UnitDefinition configuredUnitDefinition,
            IEnumerable<Vector2Int> footprint,
            BagItemDefinition nextLevelDefinition = null)
        {
            return CreateTransientConfigured(
                itemTypeId,
                level,
                BagItemRole.Soldier,
                0f,
                configuredUnitDefinition,
                1f,
                0.75f,
                1f,
                1f,
                AttackStyle.Melee,
                0f,
                0f,
                footprint,
                nextLevelDefinition);
        }

        public static BagItemDefinition CreateTransientWeapon(
            string itemTypeId,
            int level,
            float productionDurationSeconds,
            float movementSpeedMultiplier,
            float engagementRange,
            IEnumerable<Vector2Int> footprint,
            BagItemDefinition nextLevelDefinition = null,
            float damage = 1f,
            float attackCooldown = 1f,
            AttackStyle attackStyle = AttackStyle.Melee,
            float knockbackDistance = 0f,
            float areaHitRadius = 0f)
        {
            return CreateTransientConfigured(
                itemTypeId,
                level,
                BagItemRole.Weapon,
                productionDurationSeconds,
                null,
                movementSpeedMultiplier,
                engagementRange,
                damage,
                attackCooldown,
                attackStyle,
                knockbackDistance,
                areaHitRadius,
                footprint,
                nextLevelDefinition);
        }

        private static BagItemDefinition CreateTransientConfigured(
            string itemTypeId,
            int level,
            BagItemRole role,
            float productionDurationSeconds,
            UnitDefinition unitDefinition,
            float movementSpeedMultiplier,
            float engagementRange,
            float damage,
            float attackCooldown,
            AttackStyle attackStyle,
            float knockbackDistance,
            float areaHitRadius,
            IEnumerable<Vector2Int> footprint,
            BagItemDefinition nextLevelDefinition)
        {
            if (footprint == null)
            {
                throw new ArgumentNullException(nameof(footprint));
            }

            BagItemDefinition definition = CreateInstance<BagItemDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.Configure(
                itemTypeId,
                level,
                role,
                productionDurationSeconds,
                unitDefinition,
                movementSpeedMultiplier,
                engagementRange,
                damage,
                attackCooldown,
                attackStyle,
                knockbackDistance,
                areaHitRadius,
                footprint,
                nextLevelDefinition);
            return definition;
        }

        private void Configure(
            string configuredItemTypeId,
            int configuredLevel,
            BagItemRole configuredRole,
            float configuredProductionDurationSeconds,
            UnitDefinition configuredUnitDefinition,
            float configuredMovementSpeedMultiplier,
            float configuredEngagementRange,
            float configuredDamage,
            float configuredAttackCooldown,
            AttackStyle configuredAttackStyle,
            float configuredKnockbackDistance,
            float configuredAreaHitRadius,
            IEnumerable<Vector2Int> configuredFootprint,
            BagItemDefinition configuredNextLevelDefinition)
        {
            if (string.IsNullOrWhiteSpace(configuredItemTypeId))
            {
                throw new ArgumentException("An item type identifier is required.", nameof(configuredItemTypeId));
            }

            itemTypeId = configuredItemTypeId;
            level = configuredLevel;
            role = configuredRole;
            productionDurationSeconds = configuredProductionDurationSeconds;
            unitDefinition = configuredUnitDefinition;
            movementSpeedMultiplier = configuredMovementSpeedMultiplier;
            engagementRange = configuredEngagementRange;
            damage = configuredDamage;
            attackCooldown = configuredAttackCooldown;
            attackStyle = configuredAttackStyle;
            knockbackDistance = configuredKnockbackDistance;
            areaHitRadius = configuredAreaHitRadius;
            nextLevelDefinition = configuredNextLevelDefinition;
            footprint = new List<Vector2Int>();

            HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in configuredFootprint)
            {
                if (!uniqueCells.Add(cell))
                {
                    throw new ArgumentException($"Footprint contains duplicate cell {cell}.", nameof(configuredFootprint));
                }

                footprint.Add(cell);
            }

            if (footprint.Count == 0)
            {
                throw new ArgumentException("An item footprint must contain at least one cell.", nameof(configuredFootprint));
            }

            Validate();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
