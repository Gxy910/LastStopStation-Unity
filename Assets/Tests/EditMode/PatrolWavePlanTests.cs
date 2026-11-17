using NUnit.Framework;
using YuDian;

namespace YuDian.Tests
{
    /// <summary>波次组成与双精英判定，对应 README 里「51 波 28 只」「第 80 波起每 25 波双精英」。</summary>
    internal sealed class PatrolWavePlanTests
    {
        [Test]
        public void Wave51_Has28Enemies_AsAdvertised()
        {
            PatrolMission.WaveComposition(51, out int total, out int fast, out bool charger);
            Assert.AreEqual(28, total);
            Assert.AreEqual(14, fast);
            Assert.IsFalse(charger);
        }

        [Test]
        public void WavesAfter51_AddOneEveryFour()
        {
            PatrolMission.WaveComposition(52, out int t52, out _, out _);
            PatrolMission.WaveComposition(55, out int t55, out _, out _);
            PatrolMission.WaveComposition(56, out int t56, out _, out _);
            PatrolMission.WaveComposition(60, out int t60, out _, out _);
            Assert.AreEqual(29, t52);
            Assert.AreEqual(29, t55);
            Assert.AreEqual(30, t56);
            Assert.AreEqual(31, t60);
        }

        [Test]
        public void OpeningWaves_FollowTutorialTable()
        {
            PatrolMission.WaveComposition(1, out int total, out int fast, out bool charger);
            Assert.AreEqual(3, total);
            Assert.AreEqual(0, fast);
            Assert.IsFalse(charger);

            PatrolMission.WaveComposition(3, out total, out fast, out charger);
            Assert.AreEqual(5, total);
            Assert.AreEqual(1, fast);

            PatrolMission.WaveComposition(5, out total, out fast, out charger);
            Assert.AreEqual(1, total);
            Assert.IsTrue(charger);
        }

        [Test]
        public void ChargerWaves_AreEveryFifthWave()
        {
            foreach (int wave in new[] { 5, 10, 25, 50, 51, 100 })
            {
                PatrolMission.WaveComposition(wave, out _, out _, out bool charger);
                bool expected = wave == 5 || (wave > 5 && wave % 5 == 0);
                Assert.AreEqual(expected, charger, "第 " + wave + " 波冲撞精英标记不符合规则");
            }
        }

        [Test]
        public void DoubleEliteStartsAtWave80_Every25Waves()
        {
            Assert.IsFalse(PatrolMission.IsDoubleEliteWave(79));
            Assert.IsTrue(PatrolMission.IsDoubleEliteWave(80));
            Assert.IsFalse(PatrolMission.IsDoubleEliteWave(81));
            Assert.IsFalse(PatrolMission.IsDoubleEliteWave(104));
            Assert.IsTrue(PatrolMission.IsDoubleEliteWave(105));
            Assert.IsTrue(PatrolMission.IsDoubleEliteWave(130));
            Assert.IsFalse(PatrolMission.IsDoubleEliteWave(154));
        }
    }
}
