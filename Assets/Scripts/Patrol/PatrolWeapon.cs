using UnityEngine;

namespace YuDian
{
    internal sealed class PatrolWeapon
    {
        internal enum Upgrade { Damage, Magazine, Supply }
        internal enum Shot { Cooling, Reloading, Dry, Miss, Hit, Headshot }
        internal const float MeleeRange = 2.6f;
        private const float MeleeRadius = .22f;
        private readonly Transform gun;
        private readonly Transform pistolModel;
        private readonly Transform rifleModel;
        private readonly Transform stick;
        private readonly Camera view;
        private readonly Transform player;
        private readonly FirstPersonWalker walker;
        private readonly GameAudio sounds;
        private readonly AudioSource audio, handlingAudio, feedbackAudio, reloadAudio;
        private readonly AudioSource[] rifleVoices = new AudioSource[3];
        private int nextRifleVoice;
        private readonly AudioClip shotSound, rifleSound, drySound, swingSound, headshotKillSound, mechanicalReloadSound, boltSound;
        private readonly GameObject muzzleFlash;
        private readonly Light weaponLight;
        private readonly GameObject[] blood = new GameObject[30];
        private readonly float[] bloodLife = new float[30];
        private readonly Vector3[] bloodVelocity = new Vector3[30];
        private int nextBlood;
        private readonly Material bloodMaterial;
        private readonly Vector3 gunRest = new Vector3(.16f, -.18f, .42f);
        private float ReloadDuration => IsRifle ? 2.7f : 1.9f;
        private bool reloadBoltPlayed;
        private float cooldown, reload, motion, gaitPhase, gaitWeight, equipMotion;
        private bool usingStick;

        public int Capacity { get; private set; }
        public int Loaded { get; private set; }
        public int Reserve { get; private set; }
        public int Damage { get; private set; }
        public int DamageUpgrades { get; private set; }
        public int StabilityUpgrades { get; private set; }
        public int FireRateUpgrades { get; private set; }
        public int DamageLimit { get; private set; }
        public bool CanUpgradeDamage => Damage < DamageLimit || StabilityUpgrades < 2 || FireRateUpgrades < 2;
        public string NextDamageEffect => Damage < DamageLimit ? "伤害 +1（当前上限 " + DamageLimit + "）" :
            StabilityUpgrades < 2 ? "稳定性提升（" + StabilityUpgrades + "/2）" :
            FireRateUpgrades < 2 ? "射速提升（" + FireRateUpgrades + "/2）" : "当前火力、稳定性和射速已满";
        public int MagazineAmmoGain => Mathf.Min(IsRifle ? 40 : 10, ReserveLimit - Reserve);
        public int MeleeDamage { get; private set; }
        public bool IsRifle { get; private set; }
        public int MagazineLimit => IsRifle ? 40 : 20;
        public int MagazineIncrement => IsRifle ? 4 : 2;
        public const int ReserveLimit = 180;
        public bool CanUpgradeMagazine => Capacity < MagazineLimit || Reserve < ReserveLimit;
        public bool UsingStick => usingStick;
        public bool Reloading => reload > 0f;
        public bool LastShotHeadshotKill { get; private set; }
        public float ReloadProgress => Reloading ? 1f - reload / ReloadDuration : 0f;

        public PatrolWeapon(FirstPersonWalker walker, PrototypeVisuals visuals, GameAudio gameAudio)
        {
            this.walker = walker; sounds = gameAudio;
            view = walker.ViewCamera; player = walker.transform;
            gun = HeldModel("委托枪械", gunRest);
            pistolModel = new GameObject("手枪外观").transform; pistolModel.SetParent(gun, false);
            rifleModel = new GameObject("AK47步枪外观").transform; rifleModel.SetParent(gun, false);
            stick = HeldModel("委托短棍", new Vector3(.29f, -.24f, .47f));
            var shader = Shader.Find("Standard");
            Material metal = PatrolYard.Paint(shader, new Color(.13f, .15f, .16f));
            Material edge = PatrolYard.Paint(shader, new Color(.32f, .34f, .33f));
            Material grip = PatrolYard.Paint(shader, new Color(.20f, .16f, .13f));
            Material sight = PatrolYard.Paint(shader, new Color(.82f, .78f, .56f));
            Material flash = PatrolYard.Paint(shader, new Color(1f, .64f, .18f), true);
            if (!CreateAssetModel(pistolModel, "YudianWeapons/Quaternius/Pistol", .13f))
            {
            PatrolYard.Box(pistolModel, "滑套", new Vector3(0, .035f, .05f), new Vector3(.061f, .058f, .225f), metal, false);
            PatrolYard.Box(pistolModel, "枪身", new Vector3(0, -.005f, .025f), new Vector3(.066f, .034f, .16f), edge, false);
            PatrolYard.Box(pistolModel, "握把", new Vector3(0, -.067f, -.03f), new Vector3(.058f, .12f, .065f), grip, false).localRotation = Quaternion.Euler(-14, 0, 0);
            PatrolYard.Box(pistolModel, "枪口", new Vector3(0, .035f, .165f), new Vector3(.045f, .042f, .018f), metal, false);
            PatrolYard.Box(pistolModel, "抛壳口", new Vector3(.031f, .049f, .05f), new Vector3(.003f, .018f, .044f), edge, false);
            PatrolYard.Box(pistolModel, "前准星", new Vector3(0, .07f, .14f), new Vector3(.012f, .012f, .012f), sight, false);
            PatrolYard.Box(pistolModel, "后准星", new Vector3(0, .07f, -.038f), new Vector3(.034f, .009f, .014f), edge, false);
            PatrolYard.Box(pistolModel, "护弓", new Vector3(0, -.035f, .055f), new Vector3(.044f, .016f, .060f), metal, false);
            }
            // 资源缺失时保留原来的低模占位枪，不影响战斗功能。
            if (!CreateAssetModel(rifleModel, "YudianWeapons/Quaternius/AK47", .105f))
            {
            PatrolYard.Box(rifleModel, "机匣", new Vector3(0, .025f, .035f), new Vector3(.075f, .075f, .235f), metal, false);
            PatrolYard.Box(rifleModel, "木制护木", new Vector3(0, .006f, .225f), new Vector3(.075f, .075f, .18f), grip, false);
            PatrolYard.Box(rifleModel, "枪管", new Vector3(0, .035f, .365f), new Vector3(.027f, .027f, .19f), metal, false);
            PatrolYard.Box(rifleModel, "枪口", new Vector3(0, .035f, .465f), new Vector3(.038f, .038f, .035f), edge, false);
            PatrolYard.Box(rifleModel, "枪托", new Vector3(0, -.014f, -.16f), new Vector3(.06f, .105f, .20f), grip, false);
            PatrolYard.Box(rifleModel, "握把", new Vector3(0, -.073f, -.055f), new Vector3(.054f, .13f, .06f), grip, false).localRotation = Quaternion.Euler(-15, 0, 0);
            PatrolYard.Box(rifleModel, "弯弹匣上段", new Vector3(0, -.075f, .075f), new Vector3(.052f, .11f, .08f), edge, false).localRotation = Quaternion.Euler(-10, 0, 0);
            PatrolYard.Box(rifleModel, "弯弹匣下段", new Vector3(0, -.145f, .087f), new Vector3(.052f, .06f, .075f), edge, false).localRotation = Quaternion.Euler(-24, 0, 0);
            PatrolYard.Box(rifleModel, "前准星", new Vector3(0, .07f, .37f), new Vector3(.012f, .04f, .018f), sight, false);
            PatrolYard.Box(rifleModel, "后准星", new Vector3(0, .07f, -.035f), new Vector3(.032f, .016f, .02f), edge, false);
            }
            muzzleFlash = PatrolYard.Box(gun, "枪口焰", new Vector3(0, .035f, .213f), new Vector3(.075f, .075f, .09f), flash, false).gameObject;
            var weaponLightNode = new GameObject("持枪近景柔光");
            weaponLightNode.transform.SetParent(view.transform, false);
            weaponLightNode.transform.localPosition = new Vector3(0, .12f, .08f);
            weaponLight = weaponLightNode.AddComponent<Light>();
            weaponLight.type = LightType.Point; weaponLight.range = 1.1f; weaponLight.intensity = .85f;
            weaponLight.color = new Color(1f, .88f, .72f); weaponLight.shadows = LightShadows.None;
            var flashLight = muzzleFlash.AddComponent<Light>(); flashLight.type = LightType.Point;
            flashLight.range = 2.4f; flashLight.intensity = 1.4f; flashLight.color = new Color(1f, .58f, .22f);
            visuals.CreateWeapon(stick, false); stick.localScale = Vector3.one * .52f;
            PatrolYard.Box(stick, "棍身磨损", new Vector3(.052f, .18f, 0), new Vector3(.012f, .22f, .025f), edge, false);
            PatrolYard.Box(stick, "握柄胶带", new Vector3(0, -.10f, -.078f), new Vector3(.14f, .035f, .012f), grip, false).localRotation = Quaternion.Euler(0, 0, 12);
            PatrolYard.Box(stick, "握柄胶带", new Vector3(0, -.16f, -.078f), new Vector3(.14f, .035f, .012f), grip, false).localRotation = Quaternion.Euler(0, 0, 12);
            bloodMaterial = PatrolYard.Paint(shader, new Color(.48f, .025f, .02f));
            for (int i = 0; i < blood.Length; i++)
            {
                blood[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blood[i].name = "感染者受击血滴";
                Object.Destroy(blood[i].GetComponent<Collider>());
                blood[i].GetComponent<Renderer>().sharedMaterial = bloodMaterial;
                blood[i].transform.localScale = Vector3.one * .065f; blood[i].SetActive(false);
            }
            GameObject audioOwner = gun.parent.gameObject;
            audio = sounds.CreateSource(audioOwner, .7f, GameAudio.VolumeChannel.Gunfire);
            handlingAudio = sounds.CreateSource(audioOwner, .7f, GameAudio.VolumeChannel.Effects);
            feedbackAudio = sounds.CreateSource(audioOwner, .7f, GameAudio.VolumeChannel.Effects);
            reloadAudio = sounds.CreateSource(audioOwner, .7f);
            for (int i = 0; i < rifleVoices.Length; i++)
                rifleVoices[i] = sounds.CreateSource(audioOwner, .7f, GameAudio.VolumeChannel.Gunfire);
            shotSound = MakeSound("手枪", .18f, 115f, .78f);
            rifleSound = Resources.Load<AudioClip>("YudianAudio/PatrolRifleReal") ?? Resources.Load<AudioClip>("YudianAudio/PatrolRifleFire") ?? MakeSound("步枪", .13f, 150f, .72f);
            headshotKillSound = Resources.Load<AudioClip>("YudianAudio/PatrolHeadshotKill") ?? MakeSound("爆头击杀", .12f, 1250f, .25f);
            mechanicalReloadSound = Resources.Load<AudioClip>("YudianAudio/PatrolReloadMechanical");
            boltSound = Resources.Load<AudioClip>("YudianAudio/PatrolBolt");
            rifleSound.LoadAudioData(); headshotKillSound.LoadAudioData();
            if (mechanicalReloadSound != null) mechanicalReloadSound.LoadAudioData();
            if (boltSound != null) boltSound.LoadAudioData();
            drySound = MakeSound("空响", .045f, 610f, .18f);
            swingSound = MakeSound("短棍", .13f, 170f, .18f);
            Hide();
        }

        public void Begin()
        {
            LastShotHeadshotKill = false;
            Capacity = 8; Loaded = Capacity; Reserve = 16; Damage = 2; DamageUpgrades = 0; MeleeDamage = 2; IsRifle = false;
            DamageLimit = 12; StabilityUpgrades = FireRateUpgrades = 0;
            cooldown = reload = motion = gaitPhase = gaitWeight = equipMotion = 0; Equip(false);
        }
        public void Hide() { weaponLight.enabled = false; gun.gameObject.SetActive(false); stick.gameObject.SetActive(false); muzzleFlash.SetActive(false); audio.Stop(); handlingAudio.Stop(); reloadAudio.Stop(); for (int i = 0; i < blood.Length; i++) { bloodLife[i] = 0; if (blood[i] != null) blood[i].SetActive(false); } reload = motion = 0; }
        public void Dispose() { foreach (var drop in blood) if (drop != null) Object.Destroy(drop); if (bloodMaterial != null) Object.Destroy(bloodMaterial); }
        public void Equip(bool melee)
        {
            if (melee != usingStick) { reload = 0; reloadAudio.Stop(); motion = 0; equipMotion = .22f; muzzleFlash.SetActive(false); }
            weaponLight.enabled = true;
            usingStick = melee; gun.gameObject.SetActive(!melee); stick.gameObject.SetActive(melee);
            pistolModel.gameObject.SetActive(!IsRifle); rifleModel.gameObject.SetActive(IsRifle);
            muzzleFlash.transform.localPosition = IsRifle ? new Vector3(0, .035f, .50f) : new Vector3(0, .035f, .213f);
        }
        public void Tick(float dt)
        {
            cooldown = Mathf.Max(0f, cooldown - dt); motion = Mathf.Max(0f, motion - dt);
            equipMotion = Mathf.Max(0f, equipMotion - dt);
            for (int i = 0; i < blood.Length; i++)
            {
                if (bloodLife[i] <= 0) continue;
                bloodLife[i] -= dt;
                if (bloodLife[i] <= 0) { blood[i].SetActive(false); continue; }
                blood[i].transform.position += bloodVelocity[i] * dt;
                bloodVelocity[i] += Vector3.down * (2f * dt);
                blood[i].transform.localScale = Vector3.one * (.065f * Mathf.Clamp01(bloodLife[i] / .2f));
            }
            if (reload > 0)
            {
                reload -= dt;
                if (!reloadBoltPlayed && reload <= ReloadDuration * .32f)
                {
                    reloadBoltPlayed = true;
                    if (boltSound != null) reloadAudio.PlayOneShot(boltSound, .75f);
                    else sounds.Play(GameAudio.Cue.ReloadComplete);
                }
                if (reload <= 0)
                { int rounds = Mathf.Min(Capacity - Loaded, Reserve); Loaded += rounds; Reserve -= rounds; }
            }
            // 摆动跟随碰撞结算后的实际速度：停住、顶墙或暂停时平滑回到持枪位置。
            float speed = walker.InputEnabled ? walker.HorizontalSpeed : 0f;
            gaitPhase += speed * 2.5f * dt;
            float desired = Mathf.Clamp01(speed / 6.2f);
            gaitWeight = Mathf.Lerp(gaitWeight, desired, 1f - Mathf.Exp(-10f * dt));
            float swayX = Mathf.Sin(gaitPhase) * .018f * gaitWeight;
            float swayY = Mathf.Abs(Mathf.Cos(gaitPhase)) * .014f * gaitWeight;
            float recoil = !usingStick ? Mathf.Clamp01(motion / .18f) * (1f - .2f * StabilityUpgrades) : 0;
            float reloadPose = Reloading ? Mathf.Sin(Mathf.PI * ReloadProgress) : 0f;
            float equipPose = Mathf.Clamp01(equipMotion / .22f);
            gun.localPosition = gunRest + new Vector3(swayX, swayY + .013f * recoil - .11f * reloadPose - .18f * equipPose, -.035f * recoil);
            gun.localRotation = Quaternion.Euler(-18f * recoil + 25f * reloadPose, -5f - 12f * reloadPose, 18f * reloadPose - swayX * 90f);
            muzzleFlash.SetActive(gun.gameObject.activeSelf && !usingStick && motion > .105f);
            stick.localPosition = new Vector3(.29f + swayX, -.24f + swayY - .18f * equipPose, .47f);
            stick.localRotation = Quaternion.Euler(14f, 0f, -26f - Mathf.Sin(Mathf.Clamp01(motion / .26f) * Mathf.PI) * 73f - swayX * 90f);
        }
        public void Reload()
        {
            if (!usingStick && reload <= 0 && Loaded < Capacity && Reserve > 0)
            {
                reload = ReloadDuration; reloadBoltPlayed = false;
                if (IsRifle && mechanicalReloadSound != null) reloadAudio.PlayOneShot(mechanicalReloadSound, .8f);
                else sounds.Play(GameAudio.Cue.Reload);
            }
        }
        public int Restock(int wave)
        {
            int old = Reserve;
            int amount = (int)System.Math.Min(50L, 15L + 4L * System.Math.Max(0L, (long)wave - 5L));
            Reserve = Mathf.Min(Reserve + amount, ReserveLimit);
            return Reserve - old;
        }
        public int AddReserve(int amount)
        {
            int added = Mathf.Min(Mathf.Max(0, amount), ReserveLimit - Reserve);
            Reserve += added;
            return added;
        }
        public void SetWave(int wave)
        {
            // 让短棍与普通感染者的生命同步成长，普通怪约需三次命中。
            int normalHealth = PatrolBalance.Health(PatrolEnemy.Kind.Normal, wave);
            MeleeDamage = (normalHealth + 2) / 3;
            DamageLimit = 12 + Mathf.Clamp((wave - 11) / 10, 0, 4);
        }

        public void Apply(Upgrade upgrade)
        {
            if (upgrade == Upgrade.Damage)
            {
                if (Damage < DamageLimit) { Damage++; DamageUpgrades++; }
                else if (StabilityUpgrades < 2) StabilityUpgrades++;
                else if (FireRateUpgrades < 2) FireRateUpgrades++;
                if (!IsRifle && DamageUpgrades >= 3)
                {
                    IsRifle = true;
                    Capacity = Mathf.Max(Capacity, 20); // 原弹匣和备用弹数量保持不变，需换弹装填。
                    Equip(usingStick);
                }
            }
            if (upgrade == Upgrade.Magazine)
            {
                if (Capacity < MagazineLimit) Capacity = Mathf.Min(MagazineLimit, Capacity + MagazineIncrement);
                else Reserve += MagazineAmmoGain;
            }
        }

        public Shot Attack()
        {
            LastShotHeadshotKill = false;
            if (Reloading) return Shot.Reloading;
            if (equipMotion > 0f) return Shot.Cooling;
            if (cooldown > 0) return Shot.Cooling;
            if (!usingStick && Loaded <= 0) { cooldown = .2f; handlingAudio.PlayOneShot(drySound); return Shot.Dry; }
            if (!usingStick) Loaded--;
            motion = usingStick ? .26f : .18f; cooldown = usingStick ? .76f : IsRifle ? .12f - .01f * FireRateUpgrades : .29f;
            if (!usingStick && IsRifle)
            {
                // Loaded 已扣除当前这发；最后五发逐步升调，不影响手枪、空响或反馈音。
                AudioSource voice = rifleVoices[nextRifleVoice++ % rifleVoices.Length];
                voice.pitch = RifleShotPitch(Loaded + 1);
                voice.PlayOneShot(rifleSound);
            }
            else if (usingStick) handlingAudio.PlayOneShot(swingSound);
            else audio.PlayOneShot(shotSound);
            if (!usingStick) muzzleFlash.SetActive(true);
            // HUD 准星位于屏幕正中，始终从相机中心发射；枪的模型位置不参与命中判断。
            Ray aim = view.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            if (!usingStick) walker.AddRecoil((IsRifle ? .22f : .36f) * (1f - .2f * StabilityUpgrades));
            // 同步本帧由动画驱动的头部受击区后，再进行射线和近战查询。
            Physics.SyncTransforms();
            RaycastHit[] hits = usingStick
                ? Physics.SphereCastAll(aim, MeleeRadius, MeleeRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(aim, 65f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            RaycastHit nearest = default; float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(player) || hit.distance >= distance) continue;
                var enemy = collider.GetComponentInParent<PatrolEnemy>();
                if (collider.isTrigger && (enemy == null || !enemy.Alive)) continue;
                distance = hit.distance; nearest = hit;
            }
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool("YuDian.Patrol.DebugDraw", false))
            {
                float drawDistance = nearest.collider == null ? (usingStick ? MeleeRange : 65f) : nearest.distance;
                Debug.DrawRay(aim.origin, aim.direction * drawDistance, nearest.collider == null ? Color.red : Color.yellow, 2f);
                Debug.Log("清障射线: " + (nearest.collider == null ? "未命中" : nearest.collider.name) + " · 距离 " + drawDistance.ToString("F2"));
            }
#endif
            if (nearest.collider == null) return Shot.Miss;
            var target = nearest.collider.GetComponentInParent<PatrolEnemy>();
            if (target == null || !target.Alive) return Shot.Miss;
            // 宽受击区可能略微伸入墙内：还要检查从准星到受击区中心的路径，
            // 避免射线先碰到墙外侧的触发器就隔墙造成伤害。
            float centerDistance = Mathf.Max(nearest.distance,
                Vector3.Dot(nearest.collider.bounds.center - aim.origin, aim.direction));
            foreach (RaycastHit block in Physics.RaycastAll(aim, centerDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (block.collider.transform.IsChildOf(player)) continue;
                if (block.collider.GetComponentInParent<PatrolEnemy>() == target) continue;
                return Shot.Miss;
            }
            bool headshot = !usingStick && target.IsHeadHit(nearest.collider);
            // 爆头按身体伤害的 1.75 倍向上取整。
            int appliedDamage = usingStick ? MeleeDamage : headshot
                ? (int)System.Math.Min(int.MaxValue, ((long)Damage * 7L + 3L) / 4L)
                : Damage;
            for (int i = 0; i < 5; i++)
            {
                int slot = nextBlood++ % blood.Length;
                blood[slot].transform.position = nearest.point;
                bloodVelocity[slot] = -aim.direction * Random.Range(.35f, .9f) + Random.insideUnitSphere * .6f + Vector3.up * .5f;
                bloodLife[slot] = .35f; blood[slot].SetActive(true);
            }
            LastShotHeadshotKill = headshot && target.Health <= appliedDamage;
            target.TakeHit(appliedDamage, usingStick, aim.direction);
            if (LastShotHeadshotKill) feedbackAudio.PlayOneShot(headshotKillSound, .85f);
            return headshot ? Shot.Headshot : Shot.Hit;
        }

        internal static float RifleShotPitch(int roundsBeforeShot) => 1f + .12f * Mathf.Clamp01((5f - roundsBeforeShot) / 4f);

        private static bool CreateAssetModel(Transform parent, string path, float scale)
        {
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null) return false;
            GameObject model = Object.Instantiate(prefab, parent, false);
            model.name = "Quaternius模型";
            model.transform.localRotation = Quaternion.Euler(0, 90, 0) * model.transform.localRotation;
            model.transform.localScale *= scale;
            return true;
        }

        private Transform HeldModel(string name, Vector3 position)
        {
            var root = new GameObject(name).transform; root.SetParent(view.transform, false); root.localPosition = position; return root;
        }
        private static AudioClip MakeSound(string name, float duration, float frequency, float volume)
        {
            const int sampleRate = 22050; int length = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[length]; var random = new System.Random(name.Length);
            float filtered = 0;
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)sampleRate; float decay = Mathf.Pow(1f - i / (float)length, 3f);
                float noise = (float)random.NextDouble() * 2f - 1f; filtered = Mathf.Lerp(filtered, noise, .45f);
                float tone = Mathf.Sin(time * 2f * Mathf.PI * frequency * (1f - .45f * time / duration));
                samples[i] = (tone * .4f + filtered * .6f) * decay * volume;
            }
            AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false); clip.SetData(samples, 0); return clip;
        }
    }
}
