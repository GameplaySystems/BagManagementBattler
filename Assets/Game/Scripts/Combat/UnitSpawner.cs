using System;
using System.Collections.Generic;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    /// <summary>Pools and spawns the units of one team; the scene has one instance per team.</summary>
    public sealed class UnitSpawner : MonoBehaviour
    {
        [SerializeField] private CombatTeam team;
        [SerializeField] private CombatUnit unitPrefab;
        [SerializeField] private Transform spawnAnchor;
        [SerializeField, Min(0)] private int prewarmCount = 4;
        [SerializeField, Min(0f)] private float spawnLaneSpacing = 0.15f;
        [SerializeField] private float battlefieldMinY = float.NegativeInfinity;
        [SerializeField] private float battlefieldMaxY = float.PositiveInfinity;

        private readonly Queue<CombatUnit> availableUnits = new Queue<CombatUnit>();
        private readonly HashSet<CombatUnit> activeUnits = new HashSet<CombatUnit>();
        private readonly HashSet<CombatUnit> ownedUnits = new HashSet<CombatUnit>();
        private bool initialized;
        private int spawnCount;

        public CombatTeam Team => team;
        public int ActiveCount => activeUnits.Count;
        public int AvailableCount => availableUnits.Count;
        public IReadOnlyCollection<CombatUnit> ActiveUnits => activeUnits;
        public bool IsInitialized => initialized;

        private void Awake()
        {
            if (unitPrefab != null && spawnAnchor != null)
            {
                Initialize(team, unitPrefab, spawnAnchor, prewarmCount);
            }
        }

        private void OnDrawGizmos()
        {
            CombatUnit.DrawBandGizmo(transform.position.x, battlefieldMinY, battlefieldMaxY);
        }

        private void Start()
        {
            if (initialized)
            {
                return;
            }

            if (unitPrefab == null || spawnAnchor == null)
            {
                Debug.LogError("Unit spawner is missing its prefab or spawn anchor.", this);
                enabled = false;
                return;
            }

            Initialize(team, unitPrefab, spawnAnchor, prewarmCount);
        }

        public void Initialize(
            CombatTeam configuredTeam,
            CombatUnit configuredPrefab,
            Transform configuredSpawnAnchor,
            int configuredPrewarmCount = 4)
        {
            if (initialized)
            {
                throw new InvalidOperationException("The unit spawner is already initialized.");
            }

            team = configuredTeam;
            unitPrefab = configuredPrefab != null
                ? configuredPrefab
                : throw new ArgumentNullException(nameof(configuredPrefab));
            spawnAnchor = configuredSpawnAnchor != null
                ? configuredSpawnAnchor
                : throw new ArgumentNullException(nameof(configuredSpawnAnchor));

            if (configuredPrewarmCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredPrewarmCount));
            }

            prewarmCount = configuredPrewarmCount;
            initialized = true;
            for (int index = 0; index < prewarmCount; index++)
            {
                availableUnits.Enqueue(CreateUnit());
            }
        }

        public CombatUnit Spawn(CombatUnitLoadout loadout)
        {
            EnsureInitialized();
            if (loadout.UnitDefinition == null || loadout.UnitDefinition.Team != team)
            {
                throw new ArgumentException($"This spawner only spawns {team} units.", nameof(loadout));
            }

            CombatUnit unit = availableUnits.Count > 0
                ? availableUnits.Dequeue()
                : CreateUnit();
            // Lanes cycle 0, +1, -1 so units spawned together never start stacked.
            int lane = spawnCount++ % 3;
            float laneOffset = lane == 0 ? 0f : (lane == 1 ? spawnLaneSpacing : -spawnLaneSpacing);
            unit.Initialize(
                loadout,
                spawnAnchor.position + Vector3.up * laneOffset,
                battlefieldMinY,
                battlefieldMaxY);
            activeUnits.Add(unit);
            unit.gameObject.SetActive(true);
            return unit;
        }

        public bool Release(CombatUnit unit)
        {
            EnsureInitialized();

            if (unit == null || !ownedUnits.Contains(unit) || !activeUnits.Remove(unit))
            {
                return false;
            }

            unit.ResetForPool(spawnAnchor.position);
            unit.gameObject.SetActive(false);
            availableUnits.Enqueue(unit);
            return true;
        }

        private CombatUnit CreateUnit()
        {
            CombatUnit unit = Instantiate(unitPrefab, transform);
            unit.name = $"{unitPrefab.name} Pooled {ownedUnits.Count}";
            unit.gameObject.SetActive(false);
            unit.ResetForPool(spawnAnchor.position);
            ownedUnits.Add(unit);
            return unit;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("Initialize the unit spawner before use.");
            }
        }
    }
}
