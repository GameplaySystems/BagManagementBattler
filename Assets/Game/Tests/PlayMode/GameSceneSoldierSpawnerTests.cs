using System.Collections;
using System.Collections.Generic;
using BagManagementBattler.Bag;
using BagManagementBattler.Combat;
using BagManagementBattler.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BagManagementBattler.Tests
{
    public sealed class GameSceneSoldierSpawnerTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene loadedScene = SceneManager.GetActiveScene();
            Scene cleanupScene = SceneManager.CreateScene("GameScene Test Cleanup");
            SceneManager.SetActiveScene(cleanupScene);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }
        }

        [UnityTest]
        public IEnumerator GameScene_ProducesConfiguredWeaponsFromAdjacentItems()
        {
            yield return SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Single);
            yield return null;

            GameObject tent = GameObject.Find("Friendly blue base tent sprite-1");
            GameObject spawnerObject = GameObject.Find("Friendly Soldier Spawner");
            GameObject enemySpawnerObject = GameObject.Find("Test Enemy Spawner");
            Assert.That(tent, Is.Not.Null);
            Assert.That(spawnerObject, Is.Not.Null);
            Assert.That(enemySpawnerObject, Is.Not.Null);
            PlayerBase playerBase = tent.GetComponent<PlayerBase>();
            Assert.That(playerBase, Is.Not.Null);
            Assert.That(playerBase.Health.Max, Is.EqualTo(100f));
            Assert.That(tent.GetComponent<PlayerBaseHealthBar>(), Is.Not.Null);
            Assert.That(tent.transform.Find("Health Bar/Background/Fill"), Is.Not.Null);
            Assert.That(tent.GetComponent<Collider2D>().isTrigger, Is.True);

            UnitSpawner spawner = spawnerObject.GetComponent<UnitSpawner>();
            BagProductionController controller =
                spawnerObject.GetComponent<BagProductionController>();
            Assert.That(spawner, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            TestEnemySpawner enemySpawner =
                enemySpawnerObject.GetComponent<TestEnemySpawner>();
            Assert.That(enemySpawner, Is.Not.Null);
            Assert.That(enemySpawner.SpawnedUnit, Is.Not.Null);
            Assert.That(enemySpawner.SpawnedUnit.Team, Is.EqualTo(CombatTeam.Enemy));
            UnitSpawner enemyUnitSpawner = enemySpawnerObject.GetComponent<UnitSpawner>();
            Assert.That(enemyUnitSpawner, Is.Not.Null);
            Assert.That(enemyUnitSpawner.Team, Is.EqualTo(CombatTeam.Enemy));
            Assert.That(enemyUnitSpawner.ActiveUnits, Does.Contain(enemySpawner.SpawnedUnit));
            Assert.That(spawner.Team, Is.EqualTo(CombatTeam.Friendly));
            Assert.That(spawner.IsInitialized, Is.True);
            Assert.That(controller.IsProducing, Is.True);
            Assert.That(controller.ActiveWeaponCount, Is.EqualTo(2));
            Assert.That(spawner.ActiveCount, Is.Zero);
            Assert.That(spawner.AvailableCount, Is.EqualTo(4));

            SpriteRenderer rifleFill = FindRifleFill();
            Assert.That(rifleFill, Is.Not.Null);
            Assert.That(rifleFill.transform.parent.Find("Background")
                .GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(20));
            Assert.That(rifleFill.sortingOrder, Is.EqualTo(21));
            Assert.That(rifleFill.transform.parent.Find("Visual 0")
                .GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(22));
            Assert.That(rifleFill.enabled, Is.True);

            Transform bagSoldier = FindItemObject("soldier.blue");
            Assert.That(bagSoldier.Find("Visual 0")
                .GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(22));
            Assert.That(bagSoldier.Find("Visual 1")
                .GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(23));

            Assert.That(rifleFill.sortingLayerName, Is.EqualTo("Bag"));
            Assert.That(tent.GetComponent<SpriteRenderer>().sortingLayerName, Is.EqualTo("Battlefield"));
            Assert.That(
                GameObject.Find("Cartoon battlefield with yellow dirt road")
                    .GetComponent<SpriteRenderer>().sortingLayerName,
                Is.EqualTo("Background"));

            CombatUnit enemyUnit = enemySpawner.SpawnedUnit;
            UnityEngine.Rendering.SortingGroup enemyGroup =
                enemyUnit.GetComponent<UnityEngine.Rendering.SortingGroup>();
            Assert.That(enemyGroup, Is.Not.Null);
            Assert.That(enemyGroup.sortingLayerName, Is.EqualTo("Battlefield"));
            Assert.That(enemyUnit.VisualRoot.localScale.x, Is.LessThan(0f));
            Assert.That(Mathf.Abs(enemyUnit.VisualRoot.localScale.x), Is.LessThan(1f));
            Assert.That(enemyUnit.transform.localScale.x, Is.GreaterThan(0f));

            // Waits follow the authored durations, so tuning them does not break this test.
            // The crowbar is the faster weapon: its first cycle makes exactly one unit.
            float deadline = Time.time + 15f;
            while (spawner.ActiveCount == 0 && Time.time < deadline)
            {
                yield return null;
            }

            // Combat is live here, so units may fight and die: check each unit's loadout as soon as
            // it is produced, never targeting or positions.
            BagItemDefinition soldierDefinition = FindSoldierDefinition();
            Assert.That(spawner.ActiveCount, Is.EqualTo(1));
            CombatUnit crowbarUnit = FindActiveUnit(spawner, "weapon.crowbar.basic");
            Assert.That(crowbarUnit, Is.Not.Null);
            AssertProducedLoadout(crowbarUnit, soldierDefinition);
            BagItemDefinition crowbarDefinition = crowbarUnit.WeaponDefinition;

            CombatUnit rifleUnit = null;
            while (rifleUnit == null && Time.time < deadline)
            {
                yield return null;
                rifleUnit = FindActiveUnit(spawner, "weapon.rifle.basic");
            }

            Assert.That(rifleUnit, Is.Not.Null);
            AssertProducedLoadout(rifleUnit, soldierDefinition);
            // GameScene's debug enemy fixture spawns a stream (8 at 1.5 s), not a single enemy.
            Assert.That(enemySpawner.SpawnedCount, Is.InRange(2, 8));
            Assert.That(rifleUnit.WeaponDefinition, Is.Not.SameAs(crowbarDefinition));
            List<CombatUnit> producedUnits =
                new List<CombatUnit>(spawner.ActiveUnits);
            foreach (CombatUnit unit in producedUnits)
            {
                AssertProducedLoadout(unit, soldierDefinition);
            }

            int pooledCount = spawner.ActiveCount + spawner.AvailableCount;
            controller.EndProduction();
            yield return null;
            Assert.That(rifleFill.enabled, Is.False);

            producedUnits = new List<CombatUnit>(spawner.ActiveUnits);
            foreach (CombatUnit unit in producedUnits)
            {
                Assert.That(spawner.Release(unit), Is.True);
                Assert.That(unit.IsInitialized, Is.False);
                Assert.That(unit.gameObject.activeSelf, Is.False);
                Assert.That(
                    unit.GetComponentsInChildren<SpriteRenderer>(true),
                    Has.All.Matches<SpriteRenderer>(renderer =>
                        !renderer.enabled && renderer.sprite == null));
            }

            Assert.That(spawner.ActiveCount, Is.Zero);
            Assert.That(spawner.AvailableCount, Is.EqualTo(pooledCount));

            CombatUnit[] reusedUnits = new CombatUnit[4];
            for (int index = 0; index < reusedUnits.Length; index++)
            {
                reusedUnits[index] = spawner.Spawn(
                    new ProductionRequest(soldierDefinition, crowbarDefinition).ToLoadout());
                AssertConfiguredWeaponVisual(reusedUnits[index]);
            }

            foreach (CombatUnit reusedUnit in reusedUnits)
            {
                Assert.That(spawner.Release(reusedUnit), Is.True);
            }
        }

        private static void AssertProducedLoadout(CombatUnit unit, BagItemDefinition soldierDefinition)
        {
            AssertConfiguredWeaponVisual(unit);
            Assert.That(unit.VisualRoot.localScale.x, Is.GreaterThan(0f));
            Assert.That(unit.UnitDefinition, Is.SameAs(soldierDefinition.UnitDefinition));
            BagItemDefinition weapon = unit.WeaponDefinition;
            Assert.That(
                weapon.ItemTypeId,
                Is.EqualTo("weapon.crowbar.basic").Or.EqualTo("weapon.rifle.basic"));
            Assert.That(weapon.AttackStyle, Is.EqualTo(
                weapon.ItemTypeId == "weapon.crowbar.basic" ? AttackStyle.Melee : AttackStyle.Ranged));
            Assert.That(unit.MovementSpeed, Is.EqualTo(
                unit.UnitDefinition.BaseMovementSpeed * weapon.MovementSpeedMultiplier).Within(0.0001f));
            Assert.That(unit.EngagementRange, Is.EqualTo(weapon.EngagementRange));
            Assert.That(unit.Health.Max, Is.EqualTo(unit.UnitDefinition.MaxHealth));
        }

        private static CombatUnit FindActiveUnit(UnitSpawner spawner, string weaponTypeId)
        {
            foreach (CombatUnit unit in spawner.ActiveUnits)
            {
                if (unit.WeaponDefinition.ItemTypeId == weaponTypeId)
                {
                    return unit;
                }
            }

            return null;
        }

        private static SpriteRenderer FindRifleFill()
        {
            return FindItemObject("weapon.rifle.basic").Find("Production Fill")
                .GetComponent<SpriteRenderer>();
        }

        private static Transform FindItemObject(string itemTypeId)
        {
            GameObject items = GameObject.Find("Items");
            Assert.That(items, Is.Not.Null);
            for (int index = 0; index < items.transform.childCount; index++)
            {
                Transform candidate = items.transform.GetChild(index);
                if (candidate.name.StartsWith(itemTypeId))
                {
                    return candidate;
                }
            }

            Assert.Fail($"No bag item object for '{itemTypeId}'.");
            return null;
        }

        private static BagItemDefinition FindSoldierDefinition()
        {
            BagInteractionBootstrap bootstrap =
                Object.FindFirstObjectByType<BagInteractionBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            return bootstrap.Manager.Grid.GetOccupant(new Vector2Int(2, 1)).Definition;
        }

        private static void AssertConfiguredWeaponVisual(CombatUnit unit)
        {
            Assert.That(unit.IsInitialized, Is.True);
            Assert.That(unit.HandPoint, Is.Not.Null);
            Assert.That(unit.HandPoint.name, Is.EqualTo("HandPoint"));
            Assert.That(unit.UnitDefinition.Team, Is.EqualTo(CombatTeam.Friendly));
            Assert.That(unit.WeaponDefinition.Role, Is.EqualTo(BagItemRole.Weapon));
            Assert.That(unit.Body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(unit.GetComponent<CapsuleCollider2D>(), Is.Not.Null);

            Transform weaponVisual = unit.HandPoint.Find("Weapon Visual");
            Assert.That(weaponVisual, Is.Not.Null);
            Assert.That(weaponVisual.parent, Is.SameAs(unit.HandPoint));
            Assert.That(
                (Vector2)weaponVisual.localPosition,
                Is.EqualTo(unit.WeaponDefinition.EquippedLocalPosition));
            Assert.That(
                Mathf.DeltaAngle(
                    weaponVisual.localEulerAngles.z,
                    unit.WeaponDefinition.EquippedLocalRotationDegrees),
                Is.Zero.Within(0.001f));
            Assert.That(
                (Vector2)weaponVisual.localScale,
                Is.EqualTo(unit.WeaponDefinition.EquippedLocalScale));

            SpriteRenderer weaponRenderer = weaponVisual.GetComponent<SpriteRenderer>();
            Assert.That(weaponRenderer, Is.Not.Null);
            Assert.That(weaponRenderer.sprite, Is.EqualTo(unit.WeaponDefinition.IconSprite));
            Assert.That(weaponRenderer.sortingOrder, Is.EqualTo(40));
        }
    }
}
