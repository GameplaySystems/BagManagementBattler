using System;
using BagManagementBattler.Bag;
using BagManagementBattler.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BagManagementBattler.Tests
{
    public sealed class CombatDataTests
    {
        private static readonly Vector2Int[] OneCell = { Vector2Int.zero };

        [Test]
        public void UnitDefinition_RejectsInvalidMaximumHealth()
        {
            Assert.Throws<InvalidOperationException>(() => UnitDefinition.CreateTransient(
                CombatTeam.Friendly, UnitArchetype.Basic, 1, 1f, maxHealth: 0f));
            Assert.Throws<InvalidOperationException>(() => UnitDefinition.CreateTransient(
                CombatTeam.Friendly, UnitArchetype.Basic, 1, 1f, maxHealth: float.NaN));
        }

        [Test]
        public void WeaponDefinition_RejectsEveryInvalidCombatField()
        {
            AssertInvalidWeapon(damage: 0f);
            AssertInvalidWeapon(damage: float.NaN);
            AssertInvalidWeapon(attackCooldown: 0f);
            AssertInvalidWeapon(attackCooldown: float.PositiveInfinity);
            AssertInvalidWeapon(attackStyle: (AttackStyle)99);
            AssertInvalidWeapon(knockbackDistance: -0.01f);
            AssertInvalidWeapon(knockbackDistance: float.NaN);
            AssertInvalidWeapon(areaHitRadius: -0.01f);
            AssertInvalidWeapon(areaHitRadius: float.PositiveInfinity);
        }

        [Test]
        public void AuthoredCombatConfigs_MatchM1BaselineAndValidate()
        {
            BagItemDefinition crowbar = AssetDatabase.LoadAssetAtPath<BagItemDefinition>(
                "Assets/Game/Configs/BasicCrowbar.asset");
            BagItemDefinition rifle = AssetDatabase.LoadAssetAtPath<BagItemDefinition>(
                "Assets/Game/Configs/BasicRifle.asset");
            Assert.That(crowbar, Is.Not.Null);
            Assert.That(rifle, Is.Not.Null);
            Assert.DoesNotThrow(crowbar.Validate);
            Assert.DoesNotThrow(rifle.Validate);
            Assert.That(crowbar.Damage, Is.EqualTo(6f));
            Assert.That(crowbar.AttackCooldown, Is.EqualTo(1f));
            Assert.That(crowbar.AttackStyle, Is.EqualTo(AttackStyle.Melee));
            Assert.That(crowbar.KnockbackDistance, Is.Zero);
            Assert.That(crowbar.AreaHitRadius, Is.Zero);
            Assert.That(rifle.Damage, Is.EqualTo(4f));
            Assert.That(rifle.AttackCooldown, Is.EqualTo(0.6f));
            Assert.That(rifle.AttackStyle, Is.EqualTo(AttackStyle.Ranged));

            AssertUnitHealth("Friendly", 1, 10f);
            AssertUnitHealth("Friendly", 2, 17f);
            AssertUnitHealth("Friendly", 3, 28.9f);
            AssertUnitHealth("Enemy", 1, 10f);
            AssertUnitHealth("Enemy", 2, 17f);
            AssertUnitHealth("Enemy", 3, 28.9f);
        }

        private static void AssertInvalidWeapon(
            float damage = 1f,
            float attackCooldown = 1f,
            AttackStyle attackStyle = AttackStyle.Melee,
            float knockbackDistance = 0f,
            float areaHitRadius = 0f)
        {
            Assert.Throws<InvalidOperationException>(() => BagItemDefinition.CreateTransientWeapon(
                "weapon.invalid",
                1,
                1f,
                1f,
                1f,
                OneCell,
                damage: damage,
                attackCooldown: attackCooldown,
                attackStyle: attackStyle,
                knockbackDistance: knockbackDistance,
                areaHitRadius: areaHitRadius));
        }

        [Test]
        public void UnitPrefab_SerializesSpriteFlashMaterial()
        {
            // A serialized material (not Shader.Find) keeps the flash shader in player builds.
            CombatUnit prefab = AssetDatabase.LoadAssetAtPath<CombatUnit>(
                "Assets/Game/Prefabs/CombatUnit.prefab");
            Assert.That(prefab, Is.Not.Null);
            UnitFeedback feedback = prefab.GetComponent<UnitFeedback>();
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.FlashMaterial, Is.Not.Null);
            Assert.That(feedback.FlashMaterial.shader.name, Is.EqualTo("BagManagementBattler/SpriteFlash"));
            Assert.That(feedback.FlashMaterial.HasProperty("_FlashAmount"), Is.True);
        }

        private static void AssertUnitHealth(string teamName, int level, float expected)
        {
            UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                $"Assets/Game/Configs/Units/{teamName}BasicLevel{level}.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.DoesNotThrow(definition.Validate);
            Assert.That(definition.MaxHealth, Is.EqualTo(expected).Within(0.001f));
        }
    }
}
