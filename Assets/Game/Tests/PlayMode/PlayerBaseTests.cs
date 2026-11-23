using System.Collections;
using System.Collections.Generic;
using BagManagementBattler.Bag;
using BagManagementBattler.Combat;
using BagManagementBattler.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BagManagementBattler.Tests
{
    public sealed class PlayerBaseTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

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

        [UnityTest]
        public IEnumerator EnemyWithoutFriendlyUnit_TargetsAndSiegesBase()
        {
            PlayerBase playerBase = CreatePlayerBase(Vector2.zero, includeHealthBar: false);
            UnitSpawner spawner = CreateSpawner(CombatTeam.Enemy, new Vector2(1.5f, 0f));
            CombatUnitLoadout loadout = CreateLoadout(
                CombatTeam.Enemy,
                damage: 4f,
                cooldown: 0.2f,
                range: 0.05f);
            CombatUnit enemy = spawner.Spawn(loadout);
            Physics2D.SyncTransforms();
            enemy.RefreshTargetNow();

            Assert.That(enemy.CurrentTarget, Is.SameAs(playerBase));
            Assert.That(playerBase.Health.Max, Is.EqualTo(100f));

            for (int step = 0; step < 100 && enemy.SuccessfulAttackCount < 1; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(enemy.StateMachine.CurrentState, Is.TypeOf<AttackState>());
            Assert.That(enemy.CurrentTargetGap, Is.LessThanOrEqualTo(enemy.EngagementRange));
            Assert.That(enemy.SuccessfulAttackCount, Is.EqualTo(1));
            Assert.That(playerBase.Health.Current, Is.EqualTo(96f));

            yield return new WaitForSeconds(0.08f);
            Assert.That(enemy.SuccessfulAttackCount, Is.EqualTo(1));

            for (int step = 0; step < 20 && enemy.SuccessfulAttackCount < 2; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(enemy.SuccessfulAttackCount, Is.EqualTo(2));
            Assert.That(playerBase.Health.Current, Is.EqualTo(92f));
            Assert.That(spawner.ActiveCount, Is.EqualTo(1));
            Assert.That(enemy.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator WalkingEnemy_TargetingBase_SwitchesToFriendlyPlacedInItsPath()
        {
            PlayerBase playerBase = CreatePlayerBase(Vector2.zero, includeHealthBar: false);
            CombatUnit enemy = CreateStandaloneUnit(CombatTeam.Enemy, new Vector2(3f, 0f));
            Physics2D.SyncTransforms();
            enemy.RefreshTargetNow();

            Assert.That(enemy.CurrentTarget, Is.SameAs(playerBase));
            Assert.That(enemy.StateMachine.CurrentState, Is.TypeOf<WalkState>());

            CombatUnit friendly = CreateStandaloneUnit(
                CombatTeam.Friendly,
                new Vector2(2f, 0f));
            Physics2D.SyncTransforms();

            for (int step = 0; step < 20 && enemy.CurrentTarget != friendly; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(enemy.CurrentTarget, Is.SameAs(friendly));
        }

        [UnityTest]
        public IEnumerator AttackingEnemy_KeepsBaseTarget_WhenNearerFriendlyAppears()
        {
            PlayerBase playerBase = CreatePlayerBase(Vector2.zero, includeHealthBar: false);
            CombatUnit enemy = CreateStandaloneUnit(
                CombatTeam.Enemy,
                new Vector2(1f, 0f),
                range: 1f);
            Physics2D.SyncTransforms();
            enemy.RefreshTargetNow();
            yield return new WaitForFixedUpdate();

            Assert.That(enemy.StateMachine.CurrentState, Is.TypeOf<AttackState>());
            Assert.That(enemy.CurrentTarget, Is.SameAs(playerBase));

            CombatUnit friendly = CreateStandaloneUnit(
                CombatTeam.Friendly,
                new Vector2(1.5f, 0f));
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(0.15f);
            yield return new WaitForFixedUpdate();

            Assert.That(enemy.StateMachine.CurrentState, Is.TypeOf<AttackState>());
            Assert.That(enemy.CurrentTarget, Is.SameAs(playerBase));
            Assert.That(enemy.CurrentTarget, Is.Not.SameAs(friendly));
        }

        [Test]
        public void EnemyChoosesNearerBaseOverFartherFriendlyUnit()
        {
            PlayerBase playerBase = CreatePlayerBase(new Vector2(0.5f, 0f), includeHealthBar: false);
            CombatUnit friendly = CreateStandaloneUnit(CombatTeam.Friendly, new Vector2(3f, 0f));
            CombatUnit enemy = CreateStandaloneUnit(CombatTeam.Enemy, new Vector2(1f, 0f));
            Physics2D.SyncTransforms();

            enemy.RefreshTargetNow();

            Assert.That(enemy.CurrentTarget, Is.SameAs(playerBase));
            Assert.That(enemy.CurrentTarget, Is.Not.SameAs(friendly));
        }

        [Test]
        public void EnemyDoesNotAcquireBaseOutsideAcquisitionRange()
        {
            PlayerBase playerBase = CreatePlayerBase(Vector2.zero, includeHealthBar: false);
            CombatUnit enemy = CreateStandaloneUnit(CombatTeam.Enemy, new Vector2(4f, 0f));
            Physics2D.SyncTransforms();

            enemy.RefreshTargetNow();

            Assert.That(enemy.CurrentTarget, Is.Null);
            Assert.That(playerBase.IsTargetable, Is.True);
        }

        [Test]
        public void FriendlyUnit_DoesNotTargetFriendlyBase()
        {
            PlayerBase playerBase = CreatePlayerBase(
                new Vector2(0.5f, 0f),
                includeHealthBar: false);
            CombatUnit friendly = CreateStandaloneUnit(CombatTeam.Friendly, new Vector2(1f, 0f));
            Physics2D.SyncTransforms();

            friendly.RefreshTargetNow();

            Assert.That(friendly.CurrentTarget, Is.Not.SameAs(playerBase));
        }

        [Test]
        public void HealthReachingZero_RaisesDepletedOnce()
        {
            PlayerBase playerBase = CreatePlayerBase(Vector2.zero, includeHealthBar: false);
            int depletedCount = 0;
            playerBase.Depleted += () => depletedCount++;

            playerBase.TakeDamage(100f);
            playerBase.TakeDamage(10f);

            Assert.That(playerBase.Health.Current, Is.Zero);
            Assert.That(playerBase.IsTargetable, Is.False);
            Assert.That(depletedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WorldHealthBar_TracksBaseHealth()
        {
            PlayerBase playerBase = CreatePlayerBase(Vector2.zero, includeHealthBar: true);
            PlayerBaseHealthBar healthBar = playerBase.GetComponent<PlayerBaseHealthBar>();
            yield return null;

            Assert.That(healthBar, Is.Not.Null);
            Assert.That(playerBase.transform.Find("Health Bar/Background/Fill"), Is.Not.Null);
            Assert.That(healthBar.FillFraction, Is.EqualTo(1f));

            playerBase.TakeDamage(25f);
            yield return null;

            Assert.That(healthBar.FillFraction, Is.EqualTo(0.75f).Within(0.001f));
        }

        private PlayerBase CreatePlayerBase(Vector2 position, bool includeHealthBar)
        {
            GameObject baseObject = new GameObject("Player Base");
            createdObjects.Add(baseObject);
            baseObject.transform.position = position;
            BoxCollider2D collider = baseObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.5f, 1f);
            PlayerBase playerBase = baseObject.AddComponent<PlayerBase>();
            if (includeHealthBar)
            {
                baseObject.AddComponent<PlayerBaseHealthBar>();
            }

            return playerBase;
        }

        private UnitSpawner CreateSpawner(CombatTeam team, Vector2 position)
        {
            GameObject prefabObject = new GameObject($"{team} Prefab");
            createdObjects.Add(prefabObject);
            CombatUnit prefab = prefabObject.AddComponent<CombatUnit>();
            prefabObject.GetComponent<CapsuleCollider2D>().size = new Vector2(0.2f, 0.4f);

            GameObject anchorObject = new GameObject($"{team} Anchor");
            createdObjects.Add(anchorObject);
            anchorObject.transform.position = position;

            GameObject spawnerObject = new GameObject($"{team} Spawner");
            createdObjects.Add(spawnerObject);
            UnitSpawner spawner = spawnerObject.AddComponent<UnitSpawner>();
            spawner.Initialize(team, prefab, anchorObject.transform, 1);
            return spawner;
        }

        private CombatUnit CreateStandaloneUnit(
            CombatTeam team,
            Vector2 position,
            float range = 0.05f)
        {
            GameObject unitObject = new GameObject($"{team} Target Priority Unit");
            createdObjects.Add(unitObject);
            CombatUnit unit = unitObject.AddComponent<CombatUnit>();
            unitObject.GetComponent<CapsuleCollider2D>().size = new Vector2(0.2f, 0.4f);
            unit.Initialize(CreateLoadout(team, 1f, 100f, range), position);
            return unit;
        }

        private CombatUnitLoadout CreateLoadout(
            CombatTeam team,
            float damage,
            float cooldown,
            float range)
        {
            UnitDefinition definition = UnitDefinition.CreateTransient(
                team,
                UnitArchetype.Basic,
                1,
                2f);
            BagItemDefinition weapon = BagItemDefinition.CreateTransientWeapon(
                $"weapon.{team.ToString().ToLowerInvariant()}.base-siege",
                1,
                1f,
                1f,
                range,
                new[] { Vector2Int.zero },
                damage: damage,
                attackCooldown: cooldown);
            createdObjects.Add(definition);
            createdObjects.Add(weapon);
            return new CombatUnitLoadout(definition, weapon);
        }
    }
}
