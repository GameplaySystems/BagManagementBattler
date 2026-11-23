using System;
using System.Collections;
using BagManagementBattler.Bag;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    /// <summary>
    /// Debug fixture until WaveRunner (M2): spawns <c>enemyCount</c> enemies one after another
    /// through the enemy <see cref="UnitSpawner"/>, spread vertically across its band.
    /// </summary>
    public sealed class TestEnemySpawner : MonoBehaviour
    {
        [SerializeField] private UnitSpawner enemySpawner;
        [SerializeField] private UnitDefinition enemyDefinition;
        [SerializeField] private BagItemDefinition weaponDefinition;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField, Min(1)] private int enemyCount = 1;
        [SerializeField, Min(0f)] private float spawnInterval = 1.5f;

        /// <summary>The first enemy spawned.</summary>
        public CombatUnit SpawnedUnit { get; private set; }
        public int SpawnedCount { get; private set; }

        private IEnumerator Start()
        {
            if (!spawnOnStart)
            {
                yield break;
            }

            ValidateReferences();
            for (int index = 0; index < enemyCount; index++)
            {
                if (index > 0)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }

                float? spawnY = enemyCount > 1
                    ? SpreadY(index, enemyCount, enemySpawner.BattlefieldMinY, enemySpawner.BattlefieldMaxY)
                    : (float?)null;
                CombatUnit unit = enemySpawner.Spawn(
                    new CombatUnitLoadout(enemyDefinition, weaponDefinition), spawnY);
                if (index == 0)
                {
                    SpawnedUnit = unit;
                }

                SpawnedCount++;
            }
        }

        /// <summary>Centre of the index-th of count equal slices of the band; null if the band is unbounded.</summary>
        public static float? SpreadY(int index, int count, float minY, float maxY)
        {
            if (float.IsInfinity(minY) || float.IsInfinity(maxY))
            {
                return null;
            }

            return Mathf.Lerp(minY, maxY, (index + 0.5f) / count);
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

            if (enemyCount < 1 || !(spawnInterval >= 0f) || float.IsInfinity(spawnInterval))
            {
                throw new InvalidOperationException(
                    "The test enemy spawner needs at least one enemy and a finite, non-negative interval.");
            }
        }
    }
}
