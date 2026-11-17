using NUnit.Framework;
using YuDian;

namespace YuDian.Tests
{
    /// <summary>武器数值公式：爆头 1.75 倍向上取整、步枪最后五发升调。</summary>
    internal sealed class WeaponMathTests
    {
        [Test]
        public void HeadshotDamage_IsOnePointSevenFiveTimes_RoundedUp()
        {
            Assert.AreEqual(2, PatrolWeapon.HeadshotDamage(1));   // (7+3)/4 = 2
            Assert.AreEqual(4, PatrolWeapon.HeadshotDamage(2));   // (14+3)/4 = 4
            Assert.AreEqual(6, PatrolWeapon.HeadshotDamage(3));   // (21+3)/4 = 6
            Assert.AreEqual(21, PatrolWeapon.HeadshotDamage(12)); // (84+3)/4 = 21
        }

        [Test]
        public void HeadshotDamage_AlwaysExceedsBodyDamage()
        {
            for (int damage = 1; damage <= 200; damage++)
                Assert.Greater(PatrolWeapon.HeadshotDamage(damage), damage, "爆头伤害应大于身体伤害：" + damage);
        }

        [Test]
        public void RifleShotPitch_RisesOverFinalFiveRounds()
        {
            Assert.That(PatrolWeapon.RifleShotPitch(8), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(PatrolWeapon.RifleShotPitch(5), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(PatrolWeapon.RifleShotPitch(4), Is.EqualTo(1.03f).Within(1e-4f));
            Assert.That(PatrolWeapon.RifleShotPitch(3), Is.EqualTo(1.06f).Within(1e-4f));
            Assert.That(PatrolWeapon.RifleShotPitch(2), Is.EqualTo(1.09f).Within(1e-4f));
            Assert.That(PatrolWeapon.RifleShotPitch(1), Is.EqualTo(1.12f).Within(1e-4f));
            Assert.That(PatrolWeapon.RifleShotPitch(0), Is.EqualTo(1.12f).Within(1e-4f));
        }
    }
}
