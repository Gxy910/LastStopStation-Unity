using System.Collections.Generic;
using UnityEngine;

namespace YuDian
{
    internal sealed class PatrolMission
    {
        private const string RecordKey = "YuDian.Patrol.BestEvacuatedWave";
        private readonly FirstPersonWalker walker;
        private readonly GameObject station;
        private readonly PrototypeVisuals visuals;
        private readonly PatrolYard yard;
        private readonly PatrolWeapon weapon;
        private readonly GameAudio sounds;
        private readonly List<PatrolEnemy> enemies = new List<PatrolEnemy>();
        private readonly Queue<PatrolEnemy.Kind> pending = new Queue<PatrolEnemy.Kind>();
        private readonly Queue<PatrolEnemy.Kind> sidePending = new Queue<PatrolEnemy.Kind>();
        private float sideWarning;
        private bool sideFallbackAnnounced;
        private bool secondElitePending;
        private float secondEliteWarning;
        private int headshotProgress;
        private Color stationAmbient, stationBackground;
        private CameraClearFlags stationClearFlags;
        private readonly List<Light> dimmedLights = new List<Light>();
        private readonly List<float> stationLightIntensity = new List<float>();
        private const int MaxAlive = 6;
        private int spawnedThisWave;
        private float nextSpawnDelay, spawnBlockedNoticeUntil;
        private Vector3 returnPosition;
        private Quaternion returnRotation;
        private readonly Transform returnPoint;
        private int wave, cleared, health, armor, medkits;
        private bool betweenWaves, upgradeChosen, lastHitHeadshot, tutorial;
        private bool AllUpgradesUnavailable => !weapon.CanUpgradeDamage && !weapon.CanUpgradeMagazine && health >= 3 && armor >= 1 && medkits >= 3;
        private string message;
        private float messageUntil, invulnerability, damageFlash, hitFlash, defeatTimer;
        private Texture2D damageTexture;
#if UNITY_EDITOR
        private bool debugNoRecord;
#endif
        public bool IsRunning { get; private set; }
        public bool LocksPlayer => IsRunning && (defeatTimer > 0f || tutorial);
        public int Best => PlayerPrefs.GetInt(RecordKey, 0);
        internal int Wave => wave;
        internal int Cleared => cleared;
        internal int Health => health;
        internal int Armor => armor;
        internal int Medkits => medkits;
        internal PatrolWeapon Weapon => weapon;
        internal bool BetweenWaves => betweenWaves;
        internal IReadOnlyList<PatrolEnemy> Enemies => enemies;
        internal int PendingCount => pending.Count + sidePending.Count + (secondElitePending ? 1 : 0);
        internal int HeadshotProgress => headshotProgress;

        public PatrolMission(FirstPersonWalker player, GameObject stationRoot, Font font,
            Material textMaterial, PrototypeVisuals characterVisuals, GameAudio gameAudio)
        {
            walker = player; station = stationRoot; visuals = characterVisuals; sounds = gameAudio;
            yard = new PatrolYard(station.transform, font, textMaterial);
            weapon = new PatrolWeapon(walker, visuals, sounds);
            returnPoint = new GameObject("清障返回点").transform;
        }

        public bool IsPosting(Collider col) => col != null && col.transform.IsChildOf(yard.Posting);
        public bool CanStart(StationStory story) => story.DeviceRepaired && story.Chapter <= 2;
        public string PostingLabel(StationStory story)
        {
            if (!story.DeviceRepaired) return "通讯装置修好后开放清障委托";
            if (!CanStart(story)) return "接送任务进行中，暂不接单";
            return "领取清障委托 · 最高撤离 " + Best + " 波";
        }

        public bool Begin(StationStory story)
        {
            if (IsRunning || !CanStart(story)) return false;
#if UNITY_EDITOR
            debugNoRecord = false;
#endif
            returnPosition = walker.transform.position; returnRotation = walker.transform.rotation;
            station.SetActive(false); yard.Root.SetActive(true);
            stationAmbient = RenderSettings.ambientLight;
            stationClearFlags = walker.ViewCamera.clearFlags; stationBackground = walker.ViewCamera.backgroundColor;
            walker.ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            walker.ViewCamera.backgroundColor = new Color(.065f, .08f, .095f);
            RenderSettings.ambientLight = stationAmbient * .72f;
            dimmedLights.Clear(); stationLightIntensity.Clear();
            foreach (Light light in Object.FindObjectsOfType<Light>())
            {
                if (light.type != LightType.Directional) continue;
                dimmedLights.Add(light); stationLightIntensity.Add(light.intensity); light.intensity *= .75f;
            }
            walker.MoveTo(yard.Arrival); Physics.SyncTransforms();
            weapon.Begin(); health = 3; armor = medkits = 0; wave = cleared = 0; IsRunning = true;
            lastHitHeadshot = false; headshotProgress = 0; message = null; invulnerability = damageFlash = hitFlash = defeatTimer = 0; tutorial = true;
            return true;
        }

        public void Tick(float dt)
        {
            if (!IsRunning) return;
            if (tutorial)
            {
                if (Input.GetKeyDown(KeyCode.Space)) { tutorial = false; StartWave(); }
                return;
            }
            weapon.Tick(dt); invulnerability = Mathf.Max(0, invulnerability - dt);
            damageFlash = Mathf.Max(0, damageFlash - dt); hitFlash = Mathf.Max(0, hitFlash - dt);
            if (defeatTimer > 0)
            {
                defeatTimer -= dt;
                if (defeatTimer <= 0) Finish(false);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) weapon.Equip(false);
            if (Input.GetKeyDown(KeyCode.Alpha2)) weapon.Equip(true);
            if (Input.GetKeyDown(KeyCode.R)) weapon.Reload();
            if (Input.GetKeyDown(KeyCode.F)) UseMedkit();
            bool fire = WantsFire(Input.GetMouseButtonDown(0), Input.GetMouseButton(0));
            if (fire)
            {
                HandleShot(weapon.Attack());
            }
            if (Input.GetKeyDown(KeyCode.E) && TryExit()) return;
            if (betweenWaves)
            {
                if (!upgradeChosen)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha7)) ChooseUpgrade(PatrolWeapon.Upgrade.Damage);
                    if (Input.GetKeyDown(KeyCode.Alpha8)) ChooseUpgrade(PatrolWeapon.Upgrade.Magazine);
                    if (Input.GetKeyDown(KeyCode.Alpha9)) ChooseUpgrade(PatrolWeapon.Upgrade.Supply);
                }
                if ((upgradeChosen || AllUpgradesUnavailable) && Input.GetKeyDown(KeyCode.Space)) StartWave();
                return;
            }
            for (int i = 0; i < enemies.Count && IsRunning; i++) enemies[i].Tick(dt);
            if (!IsRunning) return;
            int alive = AliveCount();
            if (sidePending.Count > 0) sideWarning = Mathf.Max(0f, sideWarning - dt);
            if (secondElitePending) secondEliteWarning = Mathf.Max(0f, secondEliteWarning - dt);
            yard.SetSideGate(wave >= 11, sidePending.Count > 0);
            if (PendingCount > 0)
            {
                nextSpawnDelay -= dt;
                if (alive < MaxAlive && nextSpawnDelay <= 0f)
                {
                    bool spawned = secondElitePending && secondEliteWarning <= 0f ? SpawnSecondElite() :
                        sidePending.Count > 0 && sideWarning <= 0f ? SpawnSide() : SpawnNext();
                    if (spawned) alive++;
                    nextSpawnDelay = .55f;
                }
            }
            // 一波只有在所有批次都入场、并且场内敌人全部倒下时才算清空。
            if (alive == 0 && PendingCount == 0)
            {
                cleared = wave; betweenWaves = true; upgradeChosen = false;
                int supplied = weapon.Restock(wave);
                sounds.Play(GameAudio.Cue.WaveClear);
                Notice(supplied > 0 ? "本波清空 · 自动补充 " + supplied + " 发备用弹药" : "本波清空 · 备用弹已满");
            }
        }

        internal void HandleShot(PatrolWeapon.Shot shot)
        {
            if (!IsRunning) return;
            if (shot == PatrolWeapon.Shot.Dry) { Notice("弹匣空了 · R 换弹，或按 2 切换短棍"); return; }
            if (shot != PatrolWeapon.Shot.Hit && shot != PatrolWeapon.Shot.Headshot) return;
            lastHitHeadshot = shot == PatrolWeapon.Shot.Headshot; hitFlash = .24f; if (!weapon.LastShotHeadshotKill) sounds.Play(GameAudio.Cue.Hit);
            if (wave < 51 || !weapon.LastShotHeadshotKill) return;
            headshotProgress++;
            if (headshotProgress < 5) return;
            headshotProgress -= 5;
            int gained = weapon.AddReserve(4);
            Notice(gained > 0 ? "爆头补给 · 备用弹 +" + gained : "爆头补给 · 备用弹已满");
        }

        internal bool WantsFire(bool pressed, bool held) => pressed ||
            (weapon.IsRifle && !weapon.UsingStick && weapon.Loaded > 0 && held);

        internal bool ChooseUpgrade(PatrolWeapon.Upgrade choice)
        {
            if (!IsRunning || !betweenWaves || upgradeChosen) return false;
            if (choice == PatrolWeapon.Upgrade.Supply)
            {
                if (health < 3) { health++; Notice("生命恢复 1 格"); }
                else if (armor < 1) { armor = 1; Notice("获得防弹衣 · 可抵挡 1 次伤害"); }
                else if (medkits < 3) { medkits++; Notice("获得血包 · 按 F 使用 (" + medkits + "/3)"); }
                else { Notice("生命、护甲和血包已满 · 请选择其他强化"); return false; }
            }
            else if (choice == PatrolWeapon.Upgrade.Magazine)
            {
                if (!weapon.CanUpgradeMagazine) { Notice("弹匣和备用弹已满 · 请选择其他强化"); return false; }
                int oldCapacity = weapon.Capacity, oldReserve = weapon.Reserve;
                weapon.Apply(choice);
                Notice(weapon.Capacity > oldCapacity ? "弹匣容量 +" + (weapon.Capacity - oldCapacity) + " · R 换弹装填" :
                    "弹匣已满 " + weapon.MagazineLimit + " 发 · 备用弹 +" + (weapon.Reserve - oldReserve));
            }
            else
            {
                if (!weapon.CanUpgradeDamage) { Notice("当前火力强化已满 · 请选择其他强化"); return false; }
                bool hadRifle = weapon.IsRifle; string effect = weapon.NextDamageEffect; weapon.Apply(choice);
                Notice(!hadRifle && weapon.IsRifle ? "获得 AK47 · 弹匣容量 20 发，按 R 装填" : effect);
            }
            upgradeChosen = true;
            return true;
        }

        internal bool UseMedkit()
        {
            if (!IsRunning || defeatTimer > 0f) return false;
            if (medkits <= 0) { Notice("没有血包"); return false; }
            if (health >= 3) { Notice("生命已满 · 血包已保留"); return false; }
            medkits--; health++;
            Notice("使用血包 · 恢复 1 格生命 (剩余 " + medkits + "/3)");
            return true;
        }

        private string DamageUpgradeLabel()
        {
            if (weapon.IsRifle) return "7 火力强化：" + weapon.NextDamageEffect;
            return weapon.DamageUpgrades == 2 ? "7 火力强化：换 AK47 · 弹匣容量提升至 20" :
                "7 火力强化：伤害 +1 · 第 3 次换 AK47（" + weapon.DamageUpgrades + "/3）";
        }

        private string MagazineUpgradeLabel()
        {
            if (weapon.Capacity < weapon.MagazineLimit)
                return "8 弹匣扩容：+" + weapon.MagazineIncrement + " 发（" + weapon.Capacity + "/" + weapon.MagazineLimit + "）";
            return weapon.Reserve < PatrolWeapon.ReserveLimit ? "8 弹匣已满：备用弹 +" + weapon.MagazineAmmoGain + "（上限 180）" : "8 弹匣和备用弹已满";
        }

        private string SupplyUpgradeLabel()
        {
            if (health < 3) return "9 急救补给：立即恢复 1 格生命";
            if (armor < 1) return "9 急救补给：获得防弹衣，抵挡 1 次伤害";
            if (medkits < 3) return "9 急救补给：获得血包（" + medkits + "/3，F 使用）";
            return "9 生命、护甲和血包已满";
        }

        private static void WaveComposition(int number, out int total, out int fast, out bool charger)
        {
            charger = number == 5 || (number > 5 && number % 5 == 0);
            if (number <= 4) { total = number + 2; fast = number >= 3 ? 1 : 0; return; }
            if (number == 5) { total = 1; fast = 0; return; }
            // 51 波 28 只；52 波增 1 只，之后每 4 波增 1 只。
            total = number >= 51 ? (int)System.Math.Min(int.MaxValue, 28L + ((long)number - 51L + 3L) / 4L) :
                Mathf.Min(28, 6 + (number - 6) / 2);
            fast = Mathf.Min(total / 2, 1 + (number - 6) / 3);
        }

        internal static bool IsDoubleEliteWave(int number) => number >= 80 && (number - 80) % 25 == 0;

        private static string DescribeWave(int number)
        {
            if (number == 2) return "下一波：普通感染者从两侧来袭";
            if (number == 3) return "下一波：加入一只快速感染者";
            if (number == 4) return "下一波：前后两批进场，留意增援";
            if (number == 5) return "下一波：一只冲撞型精英 · 蓄力时侧移";
            WaveComposition(number, out int total, out int fast, out bool charger);
            return "下一波：" + total + " 只 · 快怪 " + fast + (IsDoubleEliteWave(number) ? " · 两只冲撞精英" : charger ? " · 有冲撞精英" : " · 分批来袭");
        }

        internal void StartWave()
        {
            if (!IsRunning || wave == int.MaxValue || (wave > 0 && (!betweenWaves || (!upgradeChosen && !AllUpgradesUnavailable)))) return;
            ClearEnemies(); wave++; betweenWaves = false; upgradeChosen = false;
            weapon.SetWave(wave);
            spawnedThisWave = 0;
            WaveComposition(wave, out int total, out int fast, out bool charger);
            bool doubleElite = IsDoubleEliteWave(wave);
            int ordinary = total - fast - (doubleElite ? 2 : charger ? 1 : 0);
            secondElitePending = doubleElite;
            secondEliteWarning = doubleElite ? 3f : 0f;
            bool sideWave = wave >= 11 && wave % 2 == 1;
            if (sideWave)
            {
                if (wave != 15 && ordinary > 0) { ordinary--; sidePending.Enqueue(PatrolEnemy.Kind.Normal); }
                if (wave >= 13 && fast > 0) { fast--; sidePending.Enqueue(PatrolEnemy.Kind.Fast); }
                else if (ordinary > 0) { ordinary--; sidePending.Enqueue(PatrolEnemy.Kind.Normal); }
                sideWarning = 3f; sideFallbackAnnounced = false;
            }
            yard.SetSideGate(wave >= 11, sidePending.Count > 0);
            // 交错安排快怪，避免集中出现在最后一批；精英优先于后续增援出现。
            if (charger) pending.Enqueue(PatrolEnemy.Kind.Charger);
            while (ordinary > 0 || fast > 0)
            {
                if (ordinary > 0) { pending.Enqueue(PatrolEnemy.Kind.Normal); ordinary--; }
                if (ordinary > 0) { pending.Enqueue(PatrolEnemy.Kind.Normal); ordinary--; }
                if (fast > 0) { pending.Enqueue(PatrolEnemy.Kind.Fast); fast--; }
            }
            int firstBatch = Mathf.Min(wave == 4 ? 3 : MaxAlive, pending.Count);
            for (int i = 0; i < firstBatch; i++) SpawnNext();
            nextSpawnDelay = wave == 4 ? 4f : .55f;
            sounds.Play(GameAudio.Cue.WaveStart);
            Notice("第 " + wave + " 波 · " + total + " 只感染者" + (doubleElite ? " · 两侧装卸口预警，第二只精英即将入场" : sidePending.Count > 0 ? " · 东侧门预警，3 秒后增援" : charger ? " · 注意冲撞精英" : wave >= 3 ? " · 注意快怪" : ""));
        }

        private int AliveCount()
        {
            int alive = 0;
            foreach (PatrolEnemy enemy in enemies) if (enemy != null && enemy.Alive) alive++;
            return alive;
        }

        private bool SpawnNext()
        {
            if (pending.Count == 0) return false;
            int lane = wave == 1 ? 0 : spawnedThisWave % 2;
            if (!SafeOriginalLane(ref lane, pending.Peek())) { NoticeSpawnBlocked(); return false; }
            Spawn(pending.Dequeue(), lane);
            return true;
        }

        private bool IsSafeSpawn(int lane, PatrolEnemy.Kind kind)
        {
            int offset = wave == 1 ? spawnedThisWave : spawnedThisWave / 2;
            Vector3 point = yard.SpawnPosition(lane, offset);
            Vector3 delta = point - walker.transform.position; delta.y = 0f;
            if (delta.sqrMagnitude < 16f) return false;
            float radius = kind == PatrolEnemy.Kind.Charger ? .48f : .33f;
            float height = kind == PatrolEnemy.Kind.Charger ? 2.4f : 1.8f;
            Vector3 lower = point + Vector3.up * (radius + .08f);
            Vector3 upper = point + Vector3.up * (height - radius);
            foreach (Collider obstacle in Physics.OverlapCapsule(lower, upper, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (obstacle != null && obstacle.enabled) return false;
            return true;
        }

        private bool SafeOriginalLane(ref int lane, PatrolEnemy.Kind kind)
        {
            Physics.SyncTransforms();
            if (IsSafeSpawn(lane, kind)) return true;
            int other = 1 - lane;
            if (!IsSafeSpawn(other, kind)) return false;
            lane = other;
            return true;
        }

        private void NoticeSpawnBlocked()
        {
            if (Time.time < spawnBlockedNoticeUntil) return;
            Notice("装卸口暂不安全 · 增援等待安全入口");
            spawnBlockedNoticeUntil = Time.time + 3f;
        }

        private bool SpawnSecondElite()
        {
            // 两侧装卸口已持续预警；选择远离玩家的安全入口。
            int lane = spawnedThisWave % 2;
            if (!SafeOriginalLane(ref lane, PatrolEnemy.Kind.Charger)) { NoticeSpawnBlocked(); return false; }
            secondElitePending = false;
            Spawn(PatrolEnemy.Kind.Charger, lane);
            Notice("装卸口 · 精英增援已进入");
            return true;
        }

        private bool SpawnSide()
        {
            if (sidePending.Count == 0) return false;
            int lane = 2;
            Physics.SyncTransforms();
            if (!IsSafeSpawn(lane, sidePending.Peek()))
            {
                lane = spawnedThisWave % 2;
                if (!SafeOriginalLane(ref lane, sidePending.Peek())) { NoticeSpawnBlocked(); return false; }
                if (!sideFallbackAnnounced)
                {
                    Notice("侧门通道受阻 · 增援将在 3 秒后改从装卸口进入");
                    sideFallbackAnnounced = true; sideWarning = 3f; return false;
                }
            }
            Spawn(sidePending.Dequeue(), lane);
            return true;
        }

        private void Spawn(PatrolEnemy.Kind kind, int lane)
        {
            int index = spawnedThisWave++;
            var obj = new GameObject(kind == PatrolEnemy.Kind.Charger ? "清障冲撞精英" : "清障感染者 " + (index + 1));
            obj.transform.SetParent(yard.Root.transform, false);
            obj.transform.position = yard.SpawnPosition(lane, wave == 1 ? index : index / 2);
            var enemy = obj.AddComponent<PatrolEnemy>();
            enemy.Initialize(yard, walker, visuals, kind, wave, EnemyDamagePlayer);
            enemies.Add(enemy);
        }

        internal void DamagePlayer() { EnemyDamagePlayer(1); }

        private void EnemyDamagePlayer(int amount)
        {
            if (!IsRunning || tutorial || defeatTimer > 0 || invulnerability > 0) return;
            invulnerability = 1f;
            sounds.Play(GameAudio.Cue.Hurt);
            if (armor > 0)
            {
                armor--; damageFlash = .25f;
                Notice("防弹衣抵挡了这次伤害 · 护甲已消耗");
                return;
            }
            health = Mathf.Max(0, health - Mathf.Max(1, amount)); damageFlash = .8f;
            if (health <= 0) { defeatTimer = .8f; Notice("你倒下了 · 即将返回路哨"); }
            else Notice("受到攻击 · 生命 " + health + "/3");
        }

        internal bool TryExit()
        {
            if (!IsRunning || defeatTimer > 0) return false;
            Vector3 delta = walker.transform.position - yard.Exit.position; delta.y = 0;
            if (delta.magnitude >= 2.2f) return false;
            Finish(true); return true;
        }

        public void Finish(bool evacuated)
        {
            if (!IsRunning) return;
            bool canRecord = true;
#if UNITY_EDITOR
            canRecord = !debugNoRecord;
#endif
            if (evacuated && canRecord && cleared > Best) { PlayerPrefs.SetInt(RecordKey, cleared); PlayerPrefs.Save(); }
            sounds.Play(GameAudio.Cue.Evacuate);
            IsRunning = false; ClearEnemies(); weapon.Hide(); armor = medkits = headshotProgress = 0; yard.Root.SetActive(false);
            RenderSettings.ambientLight = stationAmbient;
            walker.ViewCamera.clearFlags = stationClearFlags; walker.ViewCamera.backgroundColor = stationBackground;
            for (int i = 0; i < dimmedLights.Count; i++) if (dimmedLights[i] != null) dimmedLights[i].intensity = stationLightIntensity[i];
            dimmedLights.Clear(); stationLightIntensity.Clear(); station.SetActive(true);
            returnPoint.SetPositionAndRotation(returnPosition, returnRotation);
            walker.MoveTo(returnPoint); Physics.SyncTransforms();
            Notice(evacuated ? "撤离完成 · 本次清空 " + cleared + " 波 · 装备已交还" : "委托失败 · 未更新纪录 · 装备已交还");
        }

#if UNITY_EDITOR
        internal bool DebugJumpToWave(int targetWave)
        {
            if (!IsRunning || targetWave < 1) return false;
            debugNoRecord = true;
            foreach (PatrolEnemy enemy in enemies) if (enemy != null) enemy.gameObject.SetActive(false);
            tutorial = false; wave = targetWave - 1; betweenWaves = targetWave > 1; upgradeChosen = true;
            StartWave();
            return wave == targetWave;
        }
#endif

        public void Dispose()
        {
            weapon.Dispose();
            if (damageTexture != null) Object.Destroy(damageTexture);
            damageTexture = null;
        }

        public void Reset()
        {
            if (IsRunning) Finish(false);
            message = null;
        }

        private void ClearEnemies()
        {
            foreach (PatrolEnemy enemy in enemies) if (enemy != null) Object.Destroy(enemy.gameObject);
            enemies.Clear(); pending.Clear(); sidePending.Clear(); secondElitePending = false;
            spawnedThisWave = 0; nextSpawnDelay = sideWarning = secondEliteWarning = spawnBlockedNoticeUntil = 0f;
        }
        private void Notice(string value) { message = value; messageUntil = Time.time + 4f; }

        public void Draw(GUIStyle normal, GUIStyle small, GUIStyle centered, Color panel, Color text, Color accent)
        {
            if (!IsRunning)
            {
                if (message != null && Time.time < messageUntil)
                {
                    YudianHud.Frame(new Rect(300, 545, 680, 52), accent);
                    Label(new Rect(320, 549, 640, 44), message, centered, text);
                }
                return;
            }
            if (tutorial)
            {
                YudianHud.Frame(new Rect(145, 120, 990, 475), accent);
                Label(new Rect(180, 145, 920, 46), "货场清障  /  出勤说明", centered, accent);
                Label(new Rect(205, 210, 880, 40), "WASD 移动 · Shift 奔跑 · 左键攻击", normal, text);
                Label(new Rect(205, 255, 880, 40), "1 枪械 · 2 短棍 · R 换弹 · F 使用血包", normal, text);
                Label(new Rect(205, 300, 880, 40), "每波结束：7 / 8 / 9 选择强化，Space 开始下一波", normal, text);
                Label(new Rect(205, 345, 880, 40), "返回绿灯门按 E 撤离，只结算已清空的波次", normal, text);
                Label(new Rect(205, 435, 880, 46), "按 Space 开始第一波", centered, accent);
                return;
            }
            if (damageFlash > 0)
            {
                if (damageTexture == null) damageTexture = EscortEncounter.CreateDamageTexture();
                Color old = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(damageFlash / .8f));
                GUI.DrawTexture(new Rect(0, 0, 1280, 720), damageTexture); GUI.color = old;
            }
            YudianHud.Frame(new Rect(28, 25, 412, 103), accent);
            Label(new Rect(45, 31, 370, 24), "路哨  /  清障值勤", small, accent);
            Label(new Rect(45, 59, 370, 29), "第 " + wave + " 波     已清空 " + cleared + " 波", normal, text);
            Label(new Rect(45, 95, 60, 21), "生命", small, YudianHud.Muted);
            for (int i = 0; i < 3; i++)
                Fill(new Rect(102 + i * 28, 101, 19, 8), i < health ? new Color(.82f, .40f, .28f) : new Color(.25f, .24f, .21f));
            Label(new Rect(202, 95, 220, 23), "护甲 " + armor + "/1   血包 " + medkits + "/3", small, YudianHud.Muted);

            YudianHud.Frame(new Rect(986, 25, 266, 103), accent);
            Label(new Rect(1003, 33, 224, 23), weapon.UsingStick ? "装备  /  短棍" : weapon.IsRifle ? "装备  /  AK47" : "装备  /  手枪", small, accent);
            Label(new Rect(1003, 62, 230, 32), weapon.UsingStick ? "近战应急" : weapon.Loaded + " / " + weapon.Capacity + "    备用 " + weapon.Reserve, normal, text);
            Label(new Rect(1003, 96, 225, 22), weapon.Reloading ? "正在换弹" : weapon.UsingStick ? "伤害 " + weapon.MeleeDamage + " · 距离约 2.6 米" : weapon.IsRifle ? "按住左键连射 · R 换弹" : "R  换弹    ·    1 / 2 切换", small, YudianHud.Muted);
            if (weapon.Reloading)
            {
                Fill(new Rect(1003, 117, 216, 4), new Color(.20f, .23f, .20f));
                Fill(new Rect(1003, 117, 216 * weapon.ReloadProgress, 4), accent);
            }
            if (wave >= 51)
                Label(new Rect(1003, 130, 240, 22), "爆头补给 " + headshotProgress + " / 5", small, text);
            Fill(new Rect(637, 359, 6, 2), text); Fill(new Rect(639, 357, 2, 6), text);
            if (hitFlash > 0)
            {
                Color hit = new Color(.62f, 1f, .67f, Mathf.Clamp01(hitFlash / .24f));
                Fill(new Rect(627, 347, 4, 4), hit); Fill(new Rect(649, 347, 4, 4), hit);
                Fill(new Rect(627, 369, 4, 4), hit); Fill(new Rect(649, 369, 4, 4), hit);
                Label(new Rect(663, 340, 100, 30), lastHitHeadshot ? "爆头" : "命中", small, hit);
            }
            YudianHud.Frame(new Rect(28, 656, 1005, 40), new Color(.48f, .57f, .48f));
            Label(new Rect(40, 658, 980, 35), "WASD 移动  ·  Shift 奔跑  ·  左键攻击  ·  1 枪  2 短棍  ·  R 换弹  ·  F 血包  ·  绿灯门 E 撤离", small, text);
            if (betweenWaves)
            {
                YudianHud.Frame(new Rect(195, 140, 890, 220), accent);
                Label(new Rect(220, 150, 840, 36), "清障记录  /  已清空第 " + cleared + " 波", centered, accent);
                Label(new Rect(225, 190, 830, 28), DescribeWave(wave == int.MaxValue ? wave : wave + 1), small, text);
                Fill(new Rect(225, 225, 830, 1), YudianHud.Line);
                if (upgradeChosen)
                    Label(new Rect(225, 246, 830, 35), "强化已选   /   Space 开始下一波", normal, text);
                else
                {
                    Label(new Rect(225, 232, 830, 32), DamageUpgradeLabel(), small, text);
                    Label(new Rect(225, 264, 830, 32), MagazineUpgradeLabel(), small, text);
                    Label(new Rect(225, 296, 830, 32), SupplyUpgradeLabel(), small, text);
                    if (AllUpgradesUnavailable) Label(new Rect(700, 331, 360, 22), "全部已满 · Space 跳过", small, accent);
                }
                Label(new Rect(225, 331, 830, 22), "也可以前往绿灯门按 E 撤离", small, YudianHud.Muted);
            }
            if (!betweenWaves && PendingCount > 0)
            {
                YudianHud.Frame(new Rect(465, 25, 350, 42), accent);
                Label(new Rect(480, 30, 320, 30), "场内 " + AliveCount() + "  ·  待进场 " + PendingCount, small, text);
            }
            int bossBar = 0;
            foreach (PatrolEnemy elite in enemies)
            {
                if (elite == null || !elite.Alive || elite.EnemyKind != PatrolEnemy.Kind.Charger) continue;
                float y = 78 + bossBar++ * 20;
                Label(new Rect(465, y - 3, 44, 20), "精英", small, text);
                Fill(new Rect(510, y, 260, 14), new Color(.12f, .10f, .10f, .9f));
                Fill(new Rect(512, y + 2, 256 * Mathf.Clamp01(elite.Health / (float)elite.MaxHealth), 10), new Color(.72f, .16f, .10f));
            }
            if (secondElitePending)
                Label(new Rect(465, 112, 350, 24), "装卸口预警 · 精英增援待入场", small, new Color(1f, .48f, .30f));
            DrawOffscreenWarnings(small, text);
            foreach (PatrolEnemy enemy in enemies)
            {
                if (enemy == null || !enemy.Alive || string.IsNullOrEmpty(enemy.Warning)) continue;
                YudianHud.Frame(new Rect(390, 130, 500, 42), new Color(.95f, .40f, .25f));
                Label(new Rect(405, 134, 470, 32), enemy.Warning, centered, text);
                break;
            }
            Vector3 d = walker.transform.position - yard.Exit.position; d.y = 0;
            if (d.magnitude < 2.2f)
            {
                YudianHud.Frame(new Rect(390, 400, 500, 43), new Color(.45f, .78f, .52f));
                Label(new Rect(410, 403, 460, 37), "E  /  撤离，按已清空波数结算", normal, text);
            }
            if (message != null && Time.time < messageUntil)
            {
                YudianHud.Frame(new Rect(310, 542, 660, 48), accent);
                Label(new Rect(330, 545, 620, 42), message, centered, accent);
            }
        }
        private void DrawOffscreenWarnings(GUIStyle style, Color text)
        {
            bool left = false, right = false;
            Camera camera = walker.ViewCamera;
            foreach (PatrolEnemy enemy in enemies)
            {
                if (enemy == null || !enemy.IsWindingUp) continue;
                Vector3 world = enemy.transform.position + Vector3.up * 1.3f;
                Vector3 view = camera.WorldToViewportPoint(world);
                if (view.z > 0f && view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f) continue;
                Vector3 local = camera.transform.InverseTransformPoint(world);
                if (local.x < 0f) left = true; else right = true;
            }
            Color warning = new Color(1f, .25f, .16f);
            if (left) Label(new Rect(30, 310, 180, 34), "◀ 精英蓄力", style, warning);
            if (right) Label(new Rect(1070, 310, 180, 34), "精英蓄力 ▶", style, warning);
        }

        private static void Fill(Rect r, Color c) { Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        private static void Label(Rect r, string s, GUIStyle style, Color c) { style.normal.textColor = c; GUI.Label(r, s, style); }
    }
}
