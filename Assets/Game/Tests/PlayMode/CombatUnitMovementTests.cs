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
        [TearDown]
        public void DestroyLeftoverUnits()
        {
            // A failed assert skips the test's own cleanup; leftover units would become targets in later tests.
            foreach (CombatUnit unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(unit.gameObject);
            }
        }

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
            UnitSpawner spawner = spawnerObject.AddComponent<UnitSpawner>();
            spawner.Initialize(CombatTeam.Friendly, prefab, anchorObject.transform, 1);

            CombatUnit unit = spawner.Spawn(
                new ProductionRequest(soldierDefinition, weaponDefinition).ToLoadout());
            float startingX = unit.transform.position.x;

            // Velocity eases in (2 speed x 10/s response x 0.02 s = 0.4 per step), so wait past the ramp.
            for (int step = 0; step < 10; step++)
            {
                yield return new WaitForFixedUpdate();
            }

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
            // X edges already touch, but the target is 0.8 higher: the capsules' rounded ends
            // are still ~0.17 apart, so the 2D gap is open.
            CombatUnit friendly = CreateUnit(CombatTeam.Friendly, new Vector3(-0.2f, 0f, 0f));
            CombatUnit enemy = CreateUnit(CombatTeam.Enemy, new Vector3(0.2f, 0.8f, 0f));
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
        public void EdgeGap_UsesTheRealColliderShape_ForTallBodies()
        {
            // Bodies are 0.4 x 0.8: stacked 0.85 apart they are 0.05 apart, not 0.45 (half-width circles).
            CombatUnit lower = CreateUnit(CombatTeam.Friendly, Vector3.zero);
            CombatUnit upper = CreateUnit(CombatTeam.Enemy, new Vector3(0f, 0.85f, 0f));

            Assert.That(lower.EdgeGapTo(upper), Is.EqualTo(0.05f).Within(0.005f));
            Assert.That(upper.EdgeGapTo(lower), Is.EqualTo(0.05f).Within(0.005f));
            DestroyUnits(lower, upper);
        }

        [UnityTest]
        public IEnumerator CrowdOfTwentySameTeamUnits_EndsWithNoOverlaps()
        {
            const int unitCount = 20;
            var random = new System.Random(7);
            var crowd = new CombatUnit[unitCount];
            for (int index = 0; index < unitCount; index++)
            {
                // A tight, overlapping clump inside the GameScene band.
                var position = new Vector3(
                    (float)random.NextDouble() * 0.6f,
                    (float)random.NextDouble() * 0.6f - 0.3f,
                    0f);
                crowd[index] = CreateUnit(CombatTeam.Friendly, position, -1.2f, 1.6f);
            }

            for (int step = 0; step < 400; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            for (int a = 0; a < unitCount; a++)
            {
                Assert.That(crowd[a].transform.position.y, Is.InRange(-1.2001f, 1.6001f));
                for (int b = a + 1; b < unitCount; b++)
                {
                    Assert.That(crowd[a].EdgeGapTo(crowd[b]), Is.GreaterThanOrEqualTo(-0.005f),
                        $"units {a} and {b} overlap");
                }
            }

            DestroyUnits(crowd);
        }

        // Mirrors CombatUnit.separationDeadZone's default.
        private const float SeparationDeadZone = 0.02f;
        private const int SettleSteps = 150;
        private const int MeasureSteps = 150; // 3 s at the default 0.02 s fixed step

        [UnityTest]
        public IEnumerator OneVersusEightMeleeCrowd_SettlesWithoutJitter()
        {
            CombatUnit[] units = CreateOneVersusEightCrowd();
            for (int step = 0; step < SettleSteps; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            var previous = new Vector2[units.Length];
            var lastStep = new Vector2[units.Length];
            var flips = new int[units.Length];
            for (int index = 0; index < units.Length; index++)
            {
                previous[index] = units[index].Body.position;
            }

            for (int step = 0; step < MeasureSteps; step++)
            {
                yield return new WaitForFixedUpdate();
                for (int index = 0; index < units.Length; index++)
                {
                    Vector2 position = units[index].Body.position;
                    Vector2 delta = position - previous[index];
                    previous[index] = position;
                    if (delta.sqrMagnitude < 0.0001f * 0.0001f)
                    {
                        continue;
                    }

                    if (Vector2.Dot(delta, lastStep[index]) < 0f)
                    {
                        flips[index]++;
                    }

                    lastStep[index] = delta;
                }
            }

            for (int index = 0; index < units.Length; index++)
            {
                Assert.That(flips[index], Is.LessThanOrEqualTo(3), $"{units[index].name} #{index} jitters");
            }

            DestroyUnits(units);
        }

        [UnityTest]
        public IEnumerator OneVersusEightMeleeCrowd_OpponentsNeverOverlapBeyondTheDeadZone()
        {
            CombatUnit[] units = CreateOneVersusEightCrowd();
            CombatUnit enemy = units[0];
            float minimumGap = float.PositiveInfinity;
            string worst = string.Empty;
            for (int step = 0; step < SettleSteps + MeasureSteps; step++)
            {
                yield return new WaitForFixedUpdate();
                for (int index = 1; index < units.Length; index++)
                {
                    float gap = units[index].EdgeGapTo(enemy);
                    if (gap < minimumGap)
                    {
                        minimumGap = gap;
                        worst = $"friendly #{index} at step {step}, engaged {units[index].IsEngaged}";
                    }
                }
            }

            Assert.That(minimumGap, Is.GreaterThanOrEqualTo(-SeparationDeadZone - 0.005f), worst);
            // Not all eight fit around one body, but the front of the crowd must have reached it.
            Assert.That(System.Array.Exists(units, unit => unit.IsEngaged), Is.True);

            DestroyUnits(units);
        }

        [UnityTest]
        public IEnumerator OneVersusEightMeleeCrowd_WaitingUnitsAdvanceOnceTheFrontRowLeaves()
        {
            CombatUnit[] units = CreateOneVersusEightCrowd();
            for (int step = 0; step < SettleSteps; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            var survivors = new System.Collections.Generic.List<CombatUnit> { units[0] };
            for (int index = 1; index < units.Length; index++)
            {
                if (units[index].IsEngaged)
                {
                    Object.DestroyImmediate(units[index].gameObject);
                }
                else
                {
                    survivors.Add(units[index]);
                }
            }

            Assert.That(survivors.Count, Is.GreaterThan(1), "every friendly was engaged; nobody was waiting");
            for (int step = 0; step < SettleSteps; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(survivors.Exists(unit => unit.Team == CombatTeam.Friendly && unit.IsEngaged), Is.True);
            DestroyUnits(survivors.ToArray());
        }

        // Index 0 is the enemy at the origin; eight friendlies start on a ring around it.
        private static CombatUnit[] CreateOneVersusEightCrowd()
        {
            var units = new CombatUnit[9];
            units[0] = CreateUnit(CombatTeam.Enemy, Vector3.zero);
            for (int index = 1; index < units.Length; index++)
            {
                float angle = (index - 1) * Mathf.PI / 4f + 0.1f;
                units[index] = CreateUnit(
                    CombatTeam.Friendly,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.5f);
            }

            Physics2D.SyncTransforms();
            return units;
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
