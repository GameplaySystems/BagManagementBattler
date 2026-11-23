using System.Collections;
using System.Collections.Generic;
using BagManagementBattler.Bag;
using BagManagementBattler.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BagManagementBattler.Tests
{
    public sealed class CombatUnitCombatTests
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
        public IEnumerator MeleeUnit_KillsTenHealthTargetInThreeHits()
        {
            yield return AssertKillsInThreeHits(AttackStyle.Melee, typeof(MeleeAttackState));
        }

        [UnityTest]
        public IEnumerator RangedUnit_KillsTenHealthTargetInThreeHits()
        {
            yield return AssertKillsInThreeHits(AttackStyle.Ranged, typeof(RangedAttackState));
        }

        [UnityTest]
        public IEnumerator PendingAttack_DoesNotLandAfterAttackerDies()
        {
            BagItemDefinition attackWeapon = CreateWeapon(AttackStyle.Ranged, 4f, 0.2f);
            BagItemDefinition passiveWeapon = CreateWeapon(AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            CombatUnit attacker = CreateUnit(CombatTeam.Friendly, new Vector2(-0.3f, 0f), attackWeapon, 10f);
            CombatUnit target = CreateUnit(CombatTeam.Enemy, new Vector2(0.3f, 0f), passiveWeapon, 10f);
            AcquireTargets(attacker, target);

            yield return new WaitForFixedUpdate();
            Assert.That(attacker.StateMachine.CurrentState, Is.TypeOf<RangedAttackState>());
            attacker.TakeDamage(10f);
            Assert.That(attacker.StateMachine.CurrentState, Is.TypeOf<DeathState>());

            yield return new WaitForSeconds(0.3f);
            Assert.That(attacker.SuccessfulAttackCount, Is.Zero);
            Assert.That(target.Health.Current, Is.EqualTo(10f));
        }

        [UnityTest]
        public IEnumerator AreaHit_DamagesOnlyOpponentsInsideRadius()
        {
            BagItemDefinition areaWeapon = CreateWeapon(
                AttackStyle.Ranged, 2f, 0.1f, areaHitRadius: 0.5f);
            BagItemDefinition passiveWeapon = CreateWeapon(AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            CombatUnit attacker = CreateUnit(CombatTeam.Friendly, Vector2.zero, areaWeapon, 10f);
            CombatUnit primary = CreateUnit(CombatTeam.Enemy, new Vector2(0.5f, 0f), passiveWeapon, 10f);
            CombatUnit nearbyOpponent = CreateUnit(CombatTeam.Enemy, new Vector2(0.7f, 0.3f), passiveWeapon, 10f);
            CombatUnit outsideOpponent = CreateUnit(CombatTeam.Enemy, new Vector2(1.3f, 0.6f), passiveWeapon, 10f);
            CombatUnit nearbyFriendly = CreateUnit(CombatTeam.Friendly, new Vector2(0.55f, 0.2f), passiveWeapon, 10f);
            AcquireTargets(attacker, primary);

            yield return WaitForAttack(attacker);

            Assert.That(primary.Health.Current, Is.EqualTo(8f));
            Assert.That(nearbyOpponent.Health.Current, Is.EqualTo(8f));
            Assert.That(outsideOpponent.Health.Current, Is.EqualTo(10f));
            Assert.That(nearbyFriendly.Health.Current, Is.EqualTo(10f));
        }

        [UnityTest]
        public IEnumerator Knockback_CompletesInsideBattlefieldBand()
        {
            BagItemDefinition knockbackWeapon = CreateWeapon(
                AttackStyle.Melee, 1f, 0.1f, knockbackDistance: 0.5f);
            BagItemDefinition passiveWeapon = CreateWeapon(AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            CombatUnit attacker = CreateUnit(CombatTeam.Friendly, Vector2.zero, knockbackWeapon, 10f);
            CombatUnit target = CreateUnit(
                CombatTeam.Enemy,
                new Vector2(0.4f, 0.15f),
                passiveWeapon,
                10f,
                -0.2f,
                0.2f);
            AcquireTargets(attacker, target);
            float startX = target.Body.position.x;

            yield return WaitForAttack(attacker);
            yield return new WaitForSeconds(0.15f);

            Assert.That(target.Body.position.x, Is.GreaterThan(startX + 0.1f));
            Assert.That(target.Body.position.y, Is.InRange(-0.2001f, 0.2001f));
        }

        [UnityTest]
        public IEnumerator DeadSpawnedUnit_ReturnsToItsPoolAfterDelay()
        {
            BagItemDefinition attackWeapon = CreateWeapon(AttackStyle.Melee, 10f, 0.1f);
            BagItemDefinition passiveWeapon = CreateWeapon(AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            CombatUnit attacker = CreateUnit(CombatTeam.Friendly, new Vector2(-0.3f, 0f), attackWeapon, 10f);
            UnitSpawner enemySpawner = CreateSpawner(CombatTeam.Enemy, new Vector2(0.3f, 0f));
            UnitDefinition enemyDefinition = CreateUnitDefinition(CombatTeam.Enemy, 10f);
            CombatUnit target = enemySpawner.Spawn(new CombatUnitLoadout(enemyDefinition, passiveWeapon));
            AcquireTargets(attacker, target);

            yield return WaitForAttack(attacker);
            Assert.That(target.StateMachine.CurrentState, Is.TypeOf<DeathState>());
            Assert.That(enemySpawner.ActiveCount, Is.EqualTo(1));

            yield return new WaitForSeconds(0.6f);
            Assert.That(enemySpawner.ActiveCount, Is.Zero);
            Assert.That(enemySpawner.AvailableCount, Is.EqualTo(1));
            Assert.That(target.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void PoolRelease_ClearsActiveHitFlash()
        {
            BagItemDefinition weapon = CreateWeapon(AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            UnitSpawner spawner = CreateSpawner(CombatTeam.Friendly, Vector2.zero);
            var texture = new Texture2D(1, 1);
            createdObjects.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            createdObjects.Add(sprite);
            UnitDefinition definition = UnitDefinition.CreateTransient(
                CombatTeam.Friendly,
                UnitArchetype.Basic,
                1,
                1f,
                bodySprite: sprite,
                maxHealth: 10f);
            createdObjects.Add(definition);
            CombatUnit unit = spawner.Spawn(new CombatUnitLoadout(definition, weapon));
            SpriteRenderer renderer = unit.VisualRoot.GetComponentInChildren<SpriteRenderer>();
            CombatUnit secondUnit = spawner.Spawn(new CombatUnitLoadout(definition, weapon));
            SpriteRenderer secondRenderer = secondUnit.VisualRoot.GetComponentInChildren<SpriteRenderer>();
            var properties = new MaterialPropertyBlock();
            int flashAmountId = Shader.PropertyToID("_FlashAmount");
            Assert.That(renderer.sharedMaterial, Is.SameAs(secondRenderer.sharedMaterial));

            unit.TakeDamage(1f);
            Assert.That(unit.HitFlashAmount, Is.EqualTo(1f));
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(flashAmountId), Is.EqualTo(1f));

            Assert.That(spawner.Release(unit), Is.True);
            Assert.That(unit.HitFlashAmount, Is.Zero);
            Assert.That(renderer.HasPropertyBlock(), Is.False);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(flashAmountId), Is.Zero);
            Assert.That(spawner.Release(secondUnit), Is.True);
            CombatUnit reused = spawner.Spawn(new CombatUnitLoadout(definition, weapon));
            Assert.That(reused, Is.SameAs(unit));
            Assert.That(reused.HitFlashAmount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HitFlash_ClearsPropertyBlockWhenItEnds()
        {
            BagItemDefinition weapon = CreateWeapon(
                AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            var texture = new Texture2D(1, 1);
            createdObjects.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            createdObjects.Add(sprite);
            UnitDefinition definition = UnitDefinition.CreateTransient(
                CombatTeam.Friendly, UnitArchetype.Basic, 1, 1f, bodySprite: sprite, maxHealth: 10f);
            createdObjects.Add(definition);
            GameObject unitObject = new GameObject("Flash Test Unit");
            createdObjects.Add(unitObject);
            CombatUnit unit = unitObject.AddComponent<CombatUnit>();
            unit.Initialize(new CombatUnitLoadout(definition, weapon), Vector3.zero);
            SpriteRenderer bodyRenderer = unit.VisualRoot.GetComponentInChildren<SpriteRenderer>();
            Assert.That(bodyRenderer, Is.Not.Null);
            Assert.That(bodyRenderer.HasPropertyBlock(), Is.False);

            unit.TakeDamage(1f);
            Assert.That(bodyRenderer.HasPropertyBlock(), Is.True);

            yield return new WaitForSeconds(0.2f);
            Assert.That(unit.HitFlashAmount, Is.Zero);
            Assert.That(bodyRenderer.HasPropertyBlock(), Is.False);
        }

        [UnityTest]
        public IEnumerator Death_SquashesAndFades_AndPoolReleaseRestoresVisuals()
        {
            BagItemDefinition weapon = CreateWeapon(
                AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            UnitSpawner spawner = CreateSpawner(CombatTeam.Friendly, Vector2.zero);
            var texture = new Texture2D(1, 1);
            createdObjects.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            createdObjects.Add(sprite);
            UnitDefinition definition = UnitDefinition.CreateTransient(
                CombatTeam.Friendly, UnitArchetype.Basic, 1, 1f, bodySprite: sprite, maxHealth: 10f);
            createdObjects.Add(definition);
            CombatUnit unit = spawner.Spawn(new CombatUnitLoadout(definition, weapon));
            SpriteRenderer body = unit.VisualRoot.GetComponentInChildren<SpriteRenderer>();
            Vector3 baseScale = unit.VisualRoot.localScale;

            unit.TakeDamage(10f);
            Assert.That(unit.StateMachine.CurrentState, Is.TypeOf<DeathState>());
            Assert.That(unit.IsPlayingDeathVisual, Is.True);
            yield return new WaitForSeconds(0.25f);

            // Mid-delay: squashed (wider, shorter) and partly faded.
            Assert.That(unit.VisualRoot.localScale.y, Is.LessThan(baseScale.y - 0.01f));
            Assert.That(Mathf.Abs(unit.VisualRoot.localScale.x), Is.GreaterThan(Mathf.Abs(baseScale.x) + 0.01f));
            Assert.That(body.color.a, Is.InRange(0.01f, 0.99f));

            // Released before the delay ends (e.g. wave cleanup): the tween dies and visuals reset.
            Assert.That(spawner.Release(unit), Is.True);
            Assert.That(unit.IsPlayingDeathVisual, Is.False);
            Assert.That(unit.VisualRoot.localScale, Is.EqualTo(baseScale));
            Assert.That(body.color.a, Is.EqualTo(1f));

            yield return new WaitForSeconds(0.35f);
            Assert.That(unit.VisualRoot.localScale, Is.EqualTo(baseScale));
            Assert.That(body.color.a, Is.EqualTo(1f));

            CombatUnit reused = spawner.Spawn(new CombatUnitLoadout(definition, weapon));
            Assert.That(reused, Is.SameAs(unit));
            Assert.That(reused.VisualRoot.localScale, Is.EqualTo(baseScale));
            Assert.That(body.color.a, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator PoolRelease_ResetsStateMachine()
        {
            BagItemDefinition passiveWeapon = CreateWeapon(
                AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            UnitSpawner spawner = CreateSpawner(CombatTeam.Friendly, new Vector2(-0.3f, 0f));
            UnitDefinition definition = CreateUnitDefinition(CombatTeam.Friendly, 10f);
            CombatUnit unit = spawner.Spawn(new CombatUnitLoadout(definition, passiveWeapon));
            CombatUnit target = CreateUnit(CombatTeam.Enemy, new Vector2(0.3f, 0f), passiveWeapon, 10f);
            AcquireTargets(unit, target);

            yield return new WaitForFixedUpdate();
            Assert.That(unit.IsEngaged, Is.True);

            Assert.That(spawner.Release(unit), Is.True);
            Assert.That(unit.StateMachine.CurrentState, Is.Null);
            Assert.That(unit.IsEngaged, Is.False);
            Assert.That(unit.IsResting, Is.False);
        }

        // Scene check: a lone crowbar stopped ~0.6 short of a rifle line. Cause was death by rifle fire, not
        // movement; this guards that, with non-lethal rifles, nothing in movement stops it short of contact.
        [UnityTest]
        public IEnumerator LoneMeleeUnit_ClosesToContactWithLineOfRangedEnemies()
        {
            BagItemDefinition crowbar = CreateMovementWeapon(
                AttackStyle.Melee, 0.15f);
            BagItemDefinition rifle = CreateMovementWeapon(
                AttackStyle.Ranged, 1f);
            CombatUnit melee = CreateUnit(
                CombatTeam.Friendly, new Vector2(-2f, 0.2f), crowbar, 10f, -1.2f, 1.6f, PrefabCapsule);
            var enemies = new List<CombatUnit>();
            for (int index = 0; index < 5; index++)
            {
                enemies.Add(CreateUnit(
                    CombatTeam.Enemy,
                    new Vector2(1f, -0.7f + index * 0.35f),
                    rifle,
                    10f,
                    -1.2f,
                    1.6f,
                    PrefabCapsule));
            }

            Physics2D.SyncTransforms();
            string trace = string.Empty;
            for (int step = 0; step < 400 && !melee.IsEngaged; step++)
            {
                yield return new WaitForFixedUpdate();
                if (step % 25 == 0 || step > 370)
                {
                    trace += $"\n t{step}: {Describe(melee)}";
                }
            }

            foreach (CombatUnit enemy in enemies)
            {
                trace += $"\n enemy {Describe(enemy)}";
            }

            Assert.That(melee.StateMachine.CurrentState, Is.TypeOf<MeleeAttackState>(), trace);
        }

        private static readonly Vector2 PrefabCapsule = new Vector2(0.25f, 0.6f);

        private static string Describe(CombatUnit unit)
        {
            string target = unit.CurrentTarget != null
                ? $"{unit.CurrentTarget.Body.position} gap {unit.EdgeGapTo(unit.CurrentTarget):F3}"
                : "none";
            return $"{unit.StateMachine.CurrentState?.GetType().Name} pos {unit.Body.position} " +
                   $"vel {unit.Body.linearVelocity} target {target}";
        }

        private BagItemDefinition CreateMovementWeapon(
            AttackStyle style,
            float range,
            float damage = 1f,
            float cooldown = BagItemDefinition.NonLethalTestAttackCooldown)
        {
            BagItemDefinition weapon = BagItemDefinition.CreateTransientWeapon(
                $"weapon.{style.ToString().ToLowerInvariant()}.line",
                1,
                1f,
                1f,
                range,
                new[] { Vector2Int.zero },
                damage: damage,
                attackCooldown: cooldown,
                attackStyle: style);
            createdObjects.Add(weapon);
            return weapon;
        }

        private IEnumerator AssertKillsInThreeHits(AttackStyle style, System.Type expectedState)
        {
            BagItemDefinition attackWeapon = CreateWeapon(style, 4f, 0.1f);
            BagItemDefinition passiveWeapon = CreateWeapon(AttackStyle.Melee, 1f, BagItemDefinition.NonLethalTestAttackCooldown);
            CombatUnit attacker = CreateUnit(CombatTeam.Friendly, new Vector2(-0.3f, 0f), attackWeapon, 10f);
            CombatUnit target = CreateUnit(CombatTeam.Enemy, new Vector2(0.3f, 0f), passiveWeapon, 10f);
            AcquireTargets(attacker, target);

            yield return new WaitForFixedUpdate();
            Assert.That(attacker.StateMachine.CurrentState.GetType(), Is.EqualTo(expectedState));
            for (int step = 0; step < 30 && !target.Health.IsDepleted; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(target.Health.IsDepleted, Is.True);
            Assert.That(attacker.SuccessfulAttackCount, Is.EqualTo(3));
            Assert.That(target.StateMachine.CurrentState, Is.TypeOf<DeathState>());
        }

        private IEnumerator WaitForAttack(CombatUnit attacker)
        {
            for (int step = 0; step < 30 && attacker.SuccessfulAttackCount == 0; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(attacker.SuccessfulAttackCount, Is.EqualTo(1));
        }

        private CombatUnit CreateUnit(
            CombatTeam team,
            Vector2 position,
            BagItemDefinition weapon,
            float maxHealth,
            float minY = float.NegativeInfinity,
            float maxY = float.PositiveInfinity,
            Vector2? capsuleSize = null)
        {
            UnitDefinition definition = CreateUnitDefinition(team, maxHealth);
            GameObject unitObject = new GameObject($"{team} Combat Test Unit");
            createdObjects.Add(unitObject);
            CombatUnit unit = unitObject.AddComponent<CombatUnit>();
            unitObject.GetComponent<CapsuleCollider2D>().size = capsuleSize ?? new Vector2(0.2f, 0.4f);
            unit.Initialize(new CombatUnitLoadout(definition, weapon), position, minY, maxY);
            return unit;
        }

        private UnitDefinition CreateUnitDefinition(CombatTeam team, float maxHealth)
        {
            UnitDefinition definition = UnitDefinition.CreateTransient(
                team, UnitArchetype.Basic, 1, 1f, maxHealth: maxHealth);
            createdObjects.Add(definition);
            return definition;
        }

        private BagItemDefinition CreateWeapon(
            AttackStyle style,
            float damage,
            float cooldown,
            float knockbackDistance = 0f,
            float areaHitRadius = 0f)
        {
            BagItemDefinition weapon = BagItemDefinition.CreateTransientWeapon(
                $"weapon.{style.ToString().ToLowerInvariant()}.test",
                1,
                1f,
                1f,
                1f,
                new[] { Vector2Int.zero },
                damage: damage,
                attackCooldown: cooldown,
                attackStyle: style,
                knockbackDistance: knockbackDistance,
                areaHitRadius: areaHitRadius);
            createdObjects.Add(weapon);
            return weapon;
        }

        private UnitSpawner CreateSpawner(CombatTeam team, Vector2 position)
        {
            GameObject prefabObject = new GameObject($"{team} Combat Prefab");
            createdObjects.Add(prefabObject);
            CombatUnit prefab = prefabObject.AddComponent<CombatUnit>();
            prefabObject.GetComponent<CapsuleCollider2D>().size = new Vector2(0.2f, 0.4f);

            GameObject anchorObject = new GameObject($"{team} Combat Anchor");
            createdObjects.Add(anchorObject);
            anchorObject.transform.position = position;

            GameObject spawnerObject = new GameObject($"{team} Combat Spawner");
            createdObjects.Add(spawnerObject);
            UnitSpawner spawner = spawnerObject.AddComponent<UnitSpawner>();
            spawner.Initialize(team, prefab, anchorObject.transform, 1);
            return spawner;
        }

        private static void AcquireTargets(CombatUnit attacker, CombatUnit target)
        {
            Physics2D.SyncTransforms();
            attacker.RefreshTargetNow();
            target.RefreshTargetNow();
            Assert.That(attacker.CurrentTarget, Is.SameAs(target));
        }
    }
}
