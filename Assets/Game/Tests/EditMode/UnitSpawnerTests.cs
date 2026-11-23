using System.Collections.Generic;
using BagManagementBattler.Bag;
using BagManagementBattler.Combat;
using NUnit.Framework;
using UnityEngine;

namespace BagManagementBattler.Tests
{
    public sealed class UnitSpawnerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [Test]
        public void TestEnemySpawner_SpreadsEnemiesEvenlyInsideBand()
        {
            float previous = float.NegativeInfinity;
            for (int index = 0; index < 8; index++)
            {
                float? y = TestEnemySpawner.SpreadY(index, 8, -1.2f, 1.6f);
                Assert.That(y, Is.Not.Null);
                Assert.That(y.Value, Is.InRange(-1.2f, 1.6f));
                Assert.That(y.Value, Is.GreaterThan(previous + 0.34f));
                previous = y.Value;
            }

            Assert.That(TestEnemySpawner.SpreadY(0, 8, float.NegativeInfinity, 1f), Is.Null);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void Spawn_InitializesUnitAtAnchor_AndReleaseResetsItForReuse()
        {
            UnitSpawner spawner = CreateSpawner(1, out Transform anchor);
            ProductionRequest request = CreateRequest(2f);

            CombatUnit firstSpawn = spawner.Spawn(request.ToLoadout());

            Assert.That(spawner.ActiveCount, Is.EqualTo(1));
            Assert.That(spawner.AvailableCount, Is.Zero);
            Assert.That(firstSpawn.gameObject.activeSelf, Is.True);
            Assert.That(firstSpawn.IsInitialized, Is.True);
            Assert.That(firstSpawn.transform.position, Is.EqualTo(anchor.position));
            Assert.That(firstSpawn.UnitDefinition, Is.SameAs(request.UnitDefinition));
            Assert.That(firstSpawn.WeaponDefinition, Is.SameAs(request.WeaponDefinition));
            Assert.That(firstSpawn.Body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));

            firstSpawn.Body.linearVelocity = new Vector2(4f, 2f);
            Assert.That(spawner.Release(firstSpawn), Is.True);

            Assert.That(spawner.ActiveCount, Is.Zero);
            Assert.That(spawner.AvailableCount, Is.EqualTo(1));
            Assert.That(firstSpawn.gameObject.activeSelf, Is.False);
            Assert.That(firstSpawn.IsInitialized, Is.False);
            Assert.That(firstSpawn.UnitDefinition, Is.Null);
            Assert.That(firstSpawn.WeaponDefinition, Is.Null);
            Assert.That(firstSpawn.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(firstSpawn.transform.position, Is.EqualTo(anchor.position));

            CombatUnit reused = spawner.Spawn(request.ToLoadout());
            Assert.That(reused, Is.SameAs(firstSpawn));
            Assert.That(reused.IsInitialized, Is.True);
        }

        [Test]
        public void Spawn_ExpandsPoolWhenEveryPrewarmedUnitIsActive()
        {
            UnitSpawner spawner = CreateSpawner(1, out _);
            ProductionRequest request = CreateRequest(1f);

            CombatUnit first = spawner.Spawn(request.ToLoadout());
            CombatUnit second = spawner.Spawn(request.ToLoadout());

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(spawner.ActiveCount, Is.EqualTo(2));
            Assert.That(spawner.AvailableCount, Is.Zero);
            Assert.That(spawner.Release(first), Is.True);
            Assert.That(spawner.Release(second), Is.True);
            Assert.That(spawner.AvailableCount, Is.EqualTo(2));
        }

        [Test]
        public void Release_RejectsForeignAndAlreadyReleasedUnits()
        {
            UnitSpawner spawner = CreateSpawner(1, out _);
            ProductionRequest request = CreateRequest(1f);
            CombatUnit owned = spawner.Spawn(request.ToLoadout());

            GameObject foreignObject = new GameObject("Foreign Soldier");
            createdObjects.Add(foreignObject);
            CombatUnit foreign = foreignObject.AddComponent<CombatUnit>();

            Assert.That(spawner.Release(foreign), Is.False);
            Assert.That(spawner.Release(owned), Is.True);
            Assert.That(spawner.Release(owned), Is.False);
        }

        [Test]
        public void Spawn_RejectsAnotherTeamsLoadout_AndChangesNothing()
        {
            UnitSpawner spawner = CreateSpawner(1, out _);
            UnitDefinition enemyDefinition = UnitDefinition.CreateTransient(
                CombatTeam.Enemy,
                UnitArchetype.Basic,
                1,
                1f);
            createdObjects.Add(enemyDefinition);
            var enemyLoadout = new CombatUnitLoadout(
                enemyDefinition,
                CreateRequest(1f).WeaponDefinition);

            Assert.That(() => spawner.Spawn(enemyLoadout), Throws.ArgumentException);
            Assert.That(() => spawner.Spawn(default), Throws.ArgumentException);
            Assert.That(spawner.ActiveCount, Is.Zero);
            Assert.That(spawner.AvailableCount, Is.EqualTo(1));
        }

        private UnitSpawner CreateSpawner(int prewarmCount, out Transform anchor)
        {
            GameObject prefabObject = new GameObject("Combat Soldier Prefab");
            createdObjects.Add(prefabObject);
            CombatUnit prefab = prefabObject.AddComponent<CombatUnit>();

            GameObject anchorObject = new GameObject("Tent Anchor");
            createdObjects.Add(anchorObject);
            anchorObject.transform.position = new Vector3(-2.5f, 0.3f, 0f);
            anchor = anchorObject.transform;

            GameObject spawnerObject = new GameObject("Soldier Spawner");
            createdObjects.Add(spawnerObject);
            UnitSpawner spawner = spawnerObject.AddComponent<UnitSpawner>();
            spawner.Initialize(CombatTeam.Friendly, prefab, anchor, prewarmCount);
            return spawner;
        }

        private ProductionRequest CreateRequest(float movementSpeed)
        {
            UnitDefinition unitDefinition = UnitDefinition.CreateTransient(
                CombatTeam.Friendly,
                UnitArchetype.Basic,
                1,
                movementSpeed);
            BagItemDefinition soldier = BagItemDefinition.CreateTransientSoldier(
                "soldier",
                1,
                unitDefinition,
                new[] { Vector2Int.zero });
            BagItemDefinition weapon = BagItemDefinition.CreateTransientWeapon(
                "weapon",
                1,
                1f,
                1f,
                0.75f,
                new[] { Vector2Int.zero });
            createdObjects.Add(unitDefinition);
            createdObjects.Add(soldier);
            createdObjects.Add(weapon);
            return new ProductionRequest(soldier, weapon);
        }
    }
}
