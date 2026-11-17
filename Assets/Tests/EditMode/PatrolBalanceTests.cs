using NUnit.Framework;
using YuDian;

namespace YuDian.Tests
{
    /// <summary>敌人血量表：开局表、精英插值、51 波之后的阶段表与单调性。</summary>
    internal sealed class PatrolBalanceTests
    {
        [Test]
        public void WaveOne_MatchesOpeningTable()
        {
            Assert.AreEqual(6, PatrolBalance.Health(PatrolEnemy.Kind.Normal, 1));
            Assert.AreEqual(4, PatrolBalance.Health(PatrolEnemy.Kind.Fast, 1));
            Assert.AreEqual(28, PatrolBalance.Health(PatrolEnemy.Kind.Charger, 1));
        }

        [Test]
        public void EliteHealth_HitsInterpolationAnchors()
        {
            Assert.AreEqual(28, PatrolBalance.Health(PatrolEnemy.Kind.Charger, 5));
            Assert.AreEqual(90, PatrolBalance.Health(PatrolEnemy.Kind.Charger, 20));
            Assert.AreEqual(210, PatrolBalance.Health(PatrolEnemy.Kind.Charger, 40));
            Assert.AreEqual(300, PatrolBalance.Health(PatrolEnemy.Kind.Charger, 75));
            Assert.AreEqual(320, PatrolBalance.Health(PatrolEnemy.Kind.Charger, 95));
            Assert.AreEqual(320, PatrolBalance.Health(PatrolEnemy.Kind.Charger, 130));
        }

        [Test]
        public void LateWaves_UsePublishedStageTable()
        {
            // README/HUD 的说法：51 波后普通 44、快怪 24，每 10 波进一档，封顶 54/28。
            Assert.AreEqual(44, PatrolBalance.Health(PatrolEnemy.Kind.Normal, 51));
            Assert.AreEqual(24, PatrolBalance.Health(PatrolEnemy.Kind.Fast, 51));
            Assert.AreEqual(44, PatrolBalance.Health(PatrolEnemy.Kind.Normal, 60));
            Assert.AreEqual(46, PatrolBalance.Health(PatrolEnemy.Kind.Normal, 61));
            Assert.AreEqual(54, PatrolBalance.Health(PatrolEnemy.Kind.Normal, 101));
            Assert.AreEqual(28, PatrolBalance.Health(PatrolEnemy.Kind.Fast, 101));
        }

        [Test]
        public void Health_NeverDecreases_FromWave1To150()
        {
            foreach (PatrolEnemy.Kind kind in new[] { PatrolEnemy.Kind.Normal, PatrolEnemy.Kind.Fast, PatrolEnemy.Kind.Charger })
            {
                int previous = 0;
                for (int wave = 1; wave <= 150; wave++)
                {
                    int current = PatrolBalance.Health(kind, wave);
                    Assert.GreaterOrEqual(current, previous, string.Format("{0} 第 {1} 波血量低于上一波", kind, wave));
                    previous = current;
                }
            }
        }

        [Test]
        public void InvalidWave_ClampsToFirstWave()
        {
            Assert.AreEqual(PatrolBalance.Health(PatrolEnemy.Kind.Normal, 1), PatrolBalance.Health(PatrolEnemy.Kind.Normal, 0));
            Assert.AreEqual(PatrolBalance.Health(PatrolEnemy.Kind.Normal, 1), PatrolBalance.Health(PatrolEnemy.Kind.Normal, -7));
        }
    }
}
