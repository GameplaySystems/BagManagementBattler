using System.Collections;
using BagManagementBattler.Bag;
using BagManagementBattler.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BagManagementBattler.Tests
{
    public sealed class CombatUnitMovementTests
    {
        [UnityTest]
        public IEnumerator SpawnedKinematicSoldier_AdvancesTowardPositiveX()
        {
            UnitDefinition unitDefinition = UnitDefinition.CreateTransient(
                CombatTeam.Friendly,
                UnitArchetype.Basic,
                1,
                2f);
            BagItemDefinition soldierDefinition = BagItemDefinition.CreateTransientSoldier(
                "soldier",
                1,
                unitDefinition,
                new[] { Vector2Int.zero });
            BagItemDefinition weaponDefinition = BagItemDefinition.CreateTransientWeapon(
                "weapon",
                1,
                1f,
                1f,
                0.75f,
                new[] { Vector2Int.zero });

            GameObject prefabObject = new GameObject("Combat Soldier Prefab");
            CombatUnit prefab = prefabObject.AddComponent<CombatUnit>();
            GameObject anchorObject = new GameObject("Tent Anchor");
            anchorObject.transform.position = new Vector3(-2.5f, 0.3f, 0f);
            GameObject spawnerObject = new GameObject("Soldier Spawner");
            SoldierSpawner spawner = spawnerObject.AddComponent<SoldierSpawner>();
            spawner.Initialize(prefab, anchorObject.transform, 1);

            CombatUnit unit = spawner.Spawn(
                new ProductionRequest(soldierDefinition, weaponDefinition));
            float startingX = unit.transform.position.x;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(unit.Body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(unit.Body.linearVelocity.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(unit.transform.position.x, Is.GreaterThan(startingX));
            Assert.That(unit.transform.position.y, Is.EqualTo(anchorObject.transform.position.y).Within(0.001f));

            Object.Destroy(spawnerObject);
            Object.Destroy(anchorObject);
            Object.Destroy(prefabObject);
            Object.Destroy(soldierDefinition);
            Object.Destroy(weaponDefinition);
            Object.Destroy(unitDefinition);
            yield return null;
        }

        private const float MeleeRange = 0.05f;

        [UnityTest]
        public IEnumerator MeleeUnit_KeepsMovingUntilTouching_WhenOnlyXEdgesMeet()
        {
            // X edges already touch, but the target is 0.6 higher: the 2D gap is still open.
            CombatUnit friendly = CreateUnit(CombatTeam.Friendly, new Vector3(-0.2f, 0f, 0f));
            CombatUnit enemy = CreateUnit(CombatTeam.Enemy, new Vector3(0.2f, 0.6f, 0f));
            AcquireTargets(friendly, enemy);

            yield return new WaitForFixedUpdate();
            Assert.That(friendly.EdgeGapTo(enemy), Is.GreaterThan(MeleeRange));
            Assert.That(friendly.Body.linearVelocity.y, Is.GreaterThan(0f));

            for (int step = 0; step < 100 && friendly.Body.linearVelocity != Vector2.zero; step++)
            {
                // Stopping is allowed only once the units actually touch.
                yield return new WaitForFixedUpdate();
                if (friendly.Body.linearVelocity == Vector2.zero)
                {
                    Assert.That(friendly.EdgeGapTo(enemy), Is.LessThanOrEqualTo(MeleeRange));
                }
            }

            Assert.That(friendly.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(friendly.EdgeGapTo(enemy), Is.InRange(-0.1f, MeleeRange));
            DestroyUnits(friendly, enemy);
        }

        [UnityTest]
        public IEnumerator TargetOffsetOnY_IsReachedOnTheTwoDimensionalDistance()
        {
            CombatUnit friendly = CreateUnit(CombatTeam.Friendly, new Vector3(-1.5f, 0f, 0f));
            CombatUnit enemy = CreateUnit(CombatTeam.Enemy, new Vector3(1.5f, 1f, 0f));
            AcquireTargets(friendly, enemy);

            for (int step = 0; step < 200; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(friendly.CurrentTarget, Is.SameAs(enemy));
            Assert.That(friendly.EdgeGapTo(enemy), Is.InRange(-0.1f, MeleeRange));
            Assert.That(friendly.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(enemy.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            DestroyUnits(friendly, enemy);
        }

        [UnityTest]
        public IEnumerator UnitsNeverLeaveTheBattlefieldBand_EvenWithSeparationAndAHighTarget()
        {
            const float minY = -0.1f;
            const float maxY = 0.1f;
            // Three stacked units push each other apart; the fourth spawns above the band.
            CombatUnit[] friendlies =
            {
                CreateUnit(CombatTeam.Friendly, Vector3.zero, minY, maxY),
                CreateUnit(CombatTeam.Friendly, Vector3.zero, minY, maxY),
                CreateUnit(CombatTeam.Friendly, Vector3.zero, minY, maxY),
                CreateUnit(CombatTeam.Friendly, new Vector3(0f, 0.5f, 0f), minY, maxY),
            };
            CombatUnit enemy = CreateUnit(CombatTeam.Enemy, new Vector3(3f, 2f, 0f));
            Physics2D.SyncTransforms();

            for (int step = 0; step < 60; step++)
            {
                foreach (CombatUnit unit in friendlies)
                {
                    Assert.That(unit.transform.position.y, Is.InRange(minY - 0.0001f, maxY + 0.0001f));
                }

                yield return new WaitForFixedUpdate();
            }

            Assert.That(friendlies[0].CurrentTarget, Is.SameAs(enemy));
            DestroyUnits(friendlies);
            DestroyUnits(enemy);
        }

        [Test]
        public void Initialize_RejectsAnInvertedBand()
        {
            GameObject unitObject = new GameObject("Inverted Band Unit");
            CombatUnit unit = unitObject.AddComponent<CombatUnit>();
            CombatUnitLoadout loadout = CreateLoadout(CombatTeam.Friendly);

            Assert.Throws<System.ArgumentException>(
                () => unit.Initialize(loadout, Vector3.zero, 1f, -1f));
            Assert.That(unit.IsInitialized, Is.False);

            Object.Destroy(unitObject);
            Object.Destroy(loadout.UnitDefinition);
            Object.Destroy(loadout.WeaponDefinition);
        }

        private static CombatUnitLoadout CreateLoadout(CombatTeam team)
        {
            UnitDefinition definition = UnitDefinition.CreateTransient(
                team, UnitArchetype.Basic, 1, 1f);
            BagItemDefinition weapon = BagItemDefinition.CreateTransientWeapon(
                "weapon.melee.test", 1, 1f, 1f, MeleeRange, new[] { Vector2Int.zero });
            return new CombatUnitLoadout(definition, weapon);
        }

        private static CombatUnit CreateUnit(
            CombatTeam team,
            Vector3 position,
            float minY = float.NegativeInfinity,
            float maxY = float.PositiveInfinity)
        {
            GameObject unitObject = new GameObject($"{team} Movement Unit");
            CombatUnit unit = unitObject.AddComponent<CombatUnit>();
            unitObject.GetComponent<CapsuleCollider2D>().size = new Vector2(0.4f, 0.8f);
            unit.Initialize(CreateLoadout(team), position, minY, maxY);
            return unit;
        }

        private static void AcquireTargets(CombatUnit friendly, CombatUnit enemy)
        {
            Physics2D.SyncTransforms();
            friendly.RefreshTargetNow();
            enemy.RefreshTargetNow();
            Assert.That(friendly.CurrentTarget, Is.SameAs(enemy));
            Assert.That(enemy.CurrentTarget, Is.SameAs(friendly));
        }

        private static void DestroyUnits(params CombatUnit[] units)
        {
            foreach (CombatUnit unit in units)
            {
                Object.Destroy(unit.UnitDefinition);
                Object.Destroy(unit.WeaponDefinition);
                Object.Destroy(unit.gameObject);
            }
        }
    }
}
