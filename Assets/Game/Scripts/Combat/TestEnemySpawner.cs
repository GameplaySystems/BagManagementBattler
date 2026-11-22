using System;
using BagManagementBattler.Bag;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    /// <summary>Debug fixture: spawns one enemy through the enemy <see cref="UnitSpawner"/>.</summary>
    public sealed class TestEnemySpawner : MonoBehaviour
    {
        [SerializeField] private UnitSpawner enemySpawner;
        [SerializeField] private UnitDefinition enemyDefinition;
        [SerializeField] private BagItemDefinition weaponDefinition;
        [SerializeField] private bool spawnOnStart = true;

        public CombatUnit SpawnedUnit { get; private set; }

        private void Start()
        {
            if (spawnOnStart)
            {
                Spawn();
            }
        }

        public CombatUnit Spawn()
        {
            if (SpawnedUnit != null)
            {
                throw new InvalidOperationException("The test enemy has already been spawned.");
            }

            ValidateReferences();
            SpawnedUnit = enemySpawner.Spawn(new CombatUnitLoadout(enemyDefinition, weaponDefinition));
            return SpawnedUnit;
        }

        private void ValidateReferences()
        {
            if (enemySpawner == null || enemyDefinition == null || weaponDefinition == null)
            {
                throw new InvalidOperationException(
                    "The test enemy spawner is missing a required reference.");
            }

            if (enemySpawner.Team != CombatTeam.Enemy)
            {
                throw new InvalidOperationException(
                    "The test enemy spawner requires the enemy unit spawner.");
            }
        }
    }
}
