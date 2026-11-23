using System.Collections;
using BagManagementBattler.Bag;
using BagManagementBattler.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BagManagementBattler.Tests
{
    public sealed class CombatUnitFieldTests
    {
        [Test]
        public void EnemyVisualRoot_IsMirrored_AndResetForPool()
        {
            UnitDefinition enemyDefinition = UnitDefinition.CreateTransient(
                CombatTeam.Enemy, UnitArchetype.Basic, 1, 1f);
            UnitDefinition friendlyDefinition = UnitDefinition.CreateTransient(
                CombatTeam.Friendly, UnitArchetype.Basic, 1, 1f);
            BagItemDefinition weapon = BagItemDefinition.CreateTransientWeapon(
                "weapon.test", 1, 1f, 1f, 0.5f, new[] { Vector2Int.zero },
                attackCooldown: BagItemDefinition.NonLethalTestAttackCooldown);
            GameObject unitObject = new GameObject("Mirrored Unit");
            CombatUnit unit = unitObject.AddComponent<CombatUnit>();

            unit.Initialize(new CombatUnitLoadout(enemyDefinition, weapon), Vector3.zero);

            Assert.That(unit.VisualRoot.parent, Is.SameAs(unit.transform));
            Assert.That(unit.HandPoint.parent, Is.SameAs(unit.VisualRoot));
            Assert.That(unit.VisualRoot.localScale.x, Is.LessThan(0f));
            Assert.That(unit.transform.localScale.x, Is.GreaterThan(0f));

            unit.ResetForPool(Vector3.zero);
            Assert.That(unit.VisualRoot.localScale.x, Is.GreaterThan(0f));

            unit.Initialize(new CombatUnitLoadout(friendlyDefinition, weapon), Vector3.zero);
            Assert.That(unit.VisualRoot.localScale.x, Is.GreaterThan(0f));

            Object.Destroy(unitObject);
            Object.Destroy(enemyDefinition);
            Object.Destroy(friendlyDefinition);
            Object.Destroy(weapon);
        }

        [UnityTest]
        public IEnumerator EngagementRange_IsMeasuredBetweenColliderEdges()
        {
            UnitDefinition friendlyDefinition = UnitDefinition.CreateTransient(
                CombatTeam.Friendly, UnitArchetype.Basic, 1, 1f);
            UnitDefinition enemyDefinition = UnitDefinition.CreateTransient(
                CombatTeam.Enemy, UnitArchetype.Basic, 1, 1f);
            BagItemDefinition weapon = BagItemDefinition.CreateTransientWeapon(
                "weapon.test", 1, 1f, 1f, 0.5f, new[] { Vector2Int.zero },
                attackCooldown: BagItemDefinition.NonLethalTestAttackCooldown);
            CombatUnit friendly = CreateUnit("Friendly Edge Unit", friendlyDefinition, weapon, -0.4f);
            CombatUnit enemy = CreateUnit("Enemy Edge Unit", enemyDefinition, weapon, 0.4f);

            // Centres 0.8 apart (> range 0.5), edges 0.4 apart (<= range): must stop.
            Physics2D.SyncTransforms();
            friendly.RefreshTargetNow();
            enemy.RefreshTargetNow();
            yield return new WaitForFixedUpdate();
            Assert.That(friendly.CurrentTarget, Is.SameAs(enemy));
            Assert.That(friendly.Body.linearVelocity.x, Is.EqualTo(0f));
            Assert.That(enemy.Body.linearVelocity.x, Is.EqualTo(0f));

            // Edges 0.7 apart (> range): must close in.
            friendly.transform.position = new Vector3(-0.55f, 0f, 0f);
            enemy.transform.position = new Vector3(0.55f, 0f, 0f);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(friendly.Body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(enemy.Body.linearVelocity.x, Is.LessThan(0f));

            Object.Destroy(friendly.gameObject);
            Object.Destroy(enemy.gameObject);
            Object.Destroy(friendlyDefinition);
            Object.Destroy(enemyDefinition);
            Object.Destroy(weapon);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SimultaneousSpawns_EndUpSeparated()
        {
            UnitDefinition unitDefinition = UnitDefinition.CreateTransient(
                CombatTeam.Friendly, UnitArchetype.Basic, 1, 1f);
            BagItemDefinition soldier = BagItemDefinition.CreateTransientSoldier(
                "soldier", 1, unitDefinition, new[] { Vector2Int.zero });
            BagItemDefinition weapon = BagItemDefinition.CreateTransientWeapon(
                "weapon.test", 1, 1f, 1f, 0.5f, new[] { Vector2Int.zero },
                attackCooldown: BagItemDefinition.NonLethalTestAttackCooldown);

            GameObject prefabObject = new GameObject("Separation Prefab");
            CombatUnit prefab = prefabObject.AddComponent<CombatUnit>();
            prefabObject.GetComponent<CapsuleCollider2D>().size = new Vector2(0.21f, 0.4f);
            GameObject anchorObject = new GameObject("Separation Anchor");
            GameObject spawnerObject = new GameObject("Separation Spawner");
            UnitSpawner spawner = spawnerObject.AddComponent<UnitSpawner>();
            spawner.Initialize(CombatTeam.Friendly, prefab, anchorObject.transform, 2);

            ProductionRequest request = new ProductionRequest(soldier, weapon);
            CombatUnit first = spawner.Spawn(request.ToLoadout());
            CombatUnit second = spawner.Spawn(request.ToLoadout());
            Assert.That(second, Is.Not.SameAs(first));

            for (int step = 0; step < 50; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            ColliderDistance2D distance = first.GetComponent<CapsuleCollider2D>()
                .Distance(second.GetComponent<CapsuleCollider2D>());
            Assert.That(distance.isValid, Is.True);
            Assert.That(distance.isOverlapped, Is.False);

            Object.Destroy(spawnerObject);
            Object.Destroy(anchorObject);
            Object.Destroy(prefabObject);
            Object.Destroy(soldier);
            Object.Destroy(weapon);
            Object.Destroy(unitDefinition);
            yield return null;
        }

        private static CombatUnit CreateUnit(
            string name,
            UnitDefinition definition,
            BagItemDefinition weapon,
            float x)
        {
            GameObject unitObject = new GameObject(name);
            CombatUnit unit = unitObject.AddComponent<CombatUnit>();
            unitObject.GetComponent<CapsuleCollider2D>().size = new Vector2(0.4f, 0.8f);
            unit.Initialize(new CombatUnitLoadout(definition, weapon), new Vector3(x, 0f, 0f));
            return unit;
        }
    }
}
