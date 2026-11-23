using System;
using BagManagementBattler.Combat;
using NUnit.Framework;

namespace BagManagementBattler.Tests
{
    public sealed class HealthTests
    {
        [Test]
        public void TakeDamage_DepletesAndRaisesEventExactlyOnce()
        {
            var health = new Health(10f);
            int depletedCount = 0;
            health.Depleted += () => depletedCount++;

            health.TakeDamage(4f);
            health.TakeDamage(6f);
            health.TakeDamage(20f);

            Assert.That(health.Max, Is.EqualTo(10f));
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsDepleted, Is.True);
            Assert.That(depletedCount, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorAndDamage_RejectInvalidRanges()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Health(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Health(float.NaN));

            var health = new Health(10f);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.TakeDamage(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => health.TakeDamage(float.PositiveInfinity));
        }
    }
}
