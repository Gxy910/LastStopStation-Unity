using System;
using UnityEngine;

namespace YuDian
{
    /// <summary>接送选择、货场战斗和两人的撤离。</summary>
    public sealed class EscortEncounter : MonoBehaviour
    {
        private enum Phase { Arrival, Offer, Equipment, Fight, Escape, PaidDeparture, Failed }
        private enum EnemyAction { Approach, Windup, Charge, Recover, Defeated }

        private FirstPersonWalker walker;
        private Transform yard;
        private Action<EscortEnding> onFinished;
        private Func<bool> onEnemyDefeated;
        private DialogueVoice dialogueVoice;
        private GameAudio sounds;
        private NpcDialogueGaze escortGaze;
        private GameObject encounterRoot;
        private GameObject equipment;
        private GameObject cart;
        private GameObject alan;
        private GameObject enemy;
        private GameObject heldWeapon;
        private GameObject escorts;
        private GameObject overturnedCart;
        private CharacterController enemyBody;
        private Material metal;
        private Material wood;
        private Material infected;
        private Material cloth;
        private Material highlight;
        private Material danger;
        private AudioClip growl;
        private AudioSource growlSource;
        private Font signFont;
        private PrototypeVisuals visuals;
        private PrototypeVisuals.EliteRig rig;
        private AssetAnimationPlayer enemyAnimator;
        private GameObject chargeMarker;
        private Texture2D damageTexture;
        private float damageFlash;
        private float hitFeedback;
        private float animationClock;
        private float chargeBlockedTime;
        private float corpseFall;
        private Transform corpseVisual;
        private Quaternion corpseStartRotation;
        private Quaternion corpseEndRotation;
        private Vector3 corpseStartPosition;
        private readonly GameObject[] pickupSigns = new GameObject[3];
        private Phase phase;
        private EnemyAction enemyAction;
        private Vector3 chargeDirection;
        private float actionTimer;
        private float attackCooldown;
        private float damageCooldown;
        private float swingTimer;
        private int enemyHealth;
        private int playerHealth;
        private int coins;
        private bool hasMedicine;
        private bool cartPlaced;
        private bool cartUsed;
        private bool alanFollowing;
        private string message;
        private float messageUntil;

        public bool IsRunning { get; private set; }
        public bool LocksPlayer => phase == Phase.Failed || phase == Phase.PaidDeparture;

        public string Objective
        {
            get
            {
                switch (phase)
                {
                    case Phase.Arrival: return "等待路哨接送人员到来，可以四处观察";
                    case Phase.Offer: return "接送人员要求加价，选择是否支付";
                    case Phase.Equipment: return "拿取接送者留下的装备";
                    case Phase.Fight: return "躲开冲撞，趁感染者停顿时反击";
                    case Phase.Escape: return alanFollowing ? "和阿岚一起离开接送区" : "去检修间接阿岚，或离开接送区";
                    case Phase.PaidDeparture: return "跟随接送人员前往安全交接点";
                    default: return "撤离失败，从遭遇前重新尝试";
                }
            }
        }

        public void Configure(FirstPersonWalker player, Transform pickupYard, Font font, PrototypeVisuals characterVisuals, Action<EscortEnding> callback, Func<bool> defeatCallback, DialogueVoice voice)
        {
            walker = player;
            yard = pickupYard;
            signFont = font;
            visuals = characterVisuals;
            onFinished = callback;
            onEnemyDefeated = defeatCallback;
            dialogueVoice = voice;
            sounds = GetComponent<GameAudio>();
            string[] names = { "会合牌", "接送牌底座", "接送会合点 · 标牌" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform sign = yard.Find(names[i]);
                pickupSigns[i] = sign != null ? sign.gameObject : null;
            }
        }

        public void Begin()
        {
            ResetEncounter();
            IsRunning = true;
            foreach (GameObject sign in pickupSigns) if (sign != null) sign.SetActive(false);
            phase = Phase.Arrival;
            playerHealth = 3;
            enemyHealth = 6;
            hasMedicine = true;
            CreateWorld();
            actionTimer = 2.5f;
            escorts.transform.localPosition = Vector3.forward * 2f;
            enemy.SetActive(false);
        }

        public void ResetEncounter()
        {
            StopEscortSpeech();
            IsRunning = false;
            foreach (GameObject sign in pickupSigns) if (sign != null) sign.SetActive(true);
            if (heldWeapon != null) Destroy(heldWeapon);
            if (encounterRoot != null) Destroy(encounterRoot);
            if (metal != null) Destroy(metal);
            if (wood != null) Destroy(wood);
            if (infected != null) Destroy(infected);
            if (cloth != null) Destroy(cloth);
            if (highlight != null) Destroy(highlight);
            if (danger != null) Destroy(danger);
            if (growl != null) Destroy(growl);
            if (damageTexture != null) Destroy(damageTexture);
            heldWeapon = null;
            encounterRoot = null;
            cart = null;
            enemy = null;
            growlSource = null;
            alan = null;
            escorts = null;
            overturnedCart = null;
            cartPlaced = false;
            cartUsed = false;
            alanFollowing = false;
            message = null;
            actionTimer = 0;
            attackCooldown = 0;
            damageCooldown = 0;
            swingTimer = 0;
            damageFlash = 0;
            hitFeedback = 0;
            animationClock = 0;
            chargeBlockedTime = 0;
            corpseFall = 0;
            corpseVisual = null;
        }

        public void Tick(StationStory story, float deltaTime)
        {
            if (!IsRunning) return;
            coins = story.PersonalCoins;
            if (messageUntil < Time.time) message = null;
            attackCooldown = Mathf.Max(0, attackCooldown - deltaTime);
            damageCooldown = Mathf.Max(0, damageCooldown - deltaTime);
            damageFlash = Mathf.Max(0, damageFlash - deltaTime);
            hitFeedback = Mathf.Max(0, hitFeedback - deltaTime);
            AnimateWeapon(deltaTime);
            AnimateCorpse(deltaTime);

            if (phase == Phase.Arrival)
            {
                actionTimer -= deltaTime;
                escorts.transform.localPosition = Vector3.forward * Mathf.Max(0, actionTimer / 2.5f * 2f);
                if (actionTimer <= 0)
                {
                    phase = Phase.Offer;
                    enemy.SetActive(true);
                    foreach (AssetAnimationPlayer guard in escorts.GetComponentsInChildren<AssetAnimationPlayer>()) guard.Play("Idle_Gun", true);
                    Transform speaker = escorts.transform.Find("接送者·司机");
                    if (speaker != null)
                    {
                        escortGaze = speaker.gameObject.AddComponent<NpcDialogueGaze>();
                        escortGaze.Focus(walker.transform);
                    }
                    if (dialogueVoice != null) dialogueVoice.Speak("路哨接送者", "前面有感染者。再加两枚，我们才带你们出去。", false);
                }
                return;
            }

            if (phase == Phase.Offer)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    if (!ChoosePaidEscort(story))
                        ShowMessage("个人酒币不足 2 枚，请选择自行撤离。", 3f);
                }
                if (Input.GetKeyDown(KeyCode.Alpha2)) StartEquipmentPhase();
                return;
            }

            if (phase == Phase.PaidDeparture)
            {
                actionTimer -= deltaTime;
                escorts.transform.localPosition += Vector3.forward * (1.2f * deltaTime);
                if (actionTimer <= 0) Finish(EscortEnding.PaidEscort);
                return;
            }

            if (phase == Phase.Failed)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
                    Begin();
                return;
            }

            if (phase == Phase.Equipment)
            {
                if (Near(equipment.transform.position, 2f) && Input.GetKeyDown(KeyCode.E))
                    TakeEquipment();
                return;
            }

            if (phase == Phase.Fight)
            {
                if (Input.GetMouseButtonDown(0) && attackCooldown <= 0) Attack();
                if (Input.GetKeyDown(KeyCode.H) && hasMedicine && playerHealth < 3)
                {
                    playerHealth = Mathf.Min(3, playerHealth + 2);
                    hasMedicine = false;
                    if (sounds != null) sounds.Play(GameAudio.Cue.Interact);
                    ShowMessage("使用恢复补给。", 2f);
                }
                if (!cartPlaced && Near(cart.transform.position, 2.1f) && Input.GetKeyDown(KeyCode.E))
                    PlaceCart();
                TickEnemy(deltaTime);
                return;
            }

            if (phase == Phase.Escape && alanFollowing)
            {
                Vector3 destination = walker.transform.position - walker.transform.forward * 1.1f;
                destination.y = alan.transform.position.y;
                Vector3 next = Vector3.MoveTowards(alan.transform.position, destination, 4.6f * deltaTime);
                Vector3 direction = next - alan.transform.position;
                if (direction.sqrMagnitude > .00001f)
                    alan.transform.rotation = Quaternion.Slerp(alan.transform.rotation, Quaternion.LookRotation(-direction), 8f * deltaTime);
                alan.transform.position = next;
            }

            if (phase == Phase.Escape && Input.GetKeyDown(KeyCode.E))
            {
                if (!alanFollowing && Near(alan.transform.position, 2.2f))
                    RescueAlan();
                else if (Near(yard.TransformPoint(new Vector3(0, 0, -6.8f)), 2f))
                    LeaveArea();
            }
        }

        private bool ChoosePaidEscort(StationStory story)
        {
            if (!story.PayEscortSurcharge()) return false;
            StopEscortSpeech();
            coins = story.PersonalCoins;
            phase = Phase.PaidDeparture;
            actionTimer = 2.5f;
            ShowMessage("接送者留下救援，两人前往安全交接点。", 3f);
            return true;
        }

        private void TakeEquipment()
        {
            equipment.SetActive(false);
            if (sounds != null) sounds.Play(GameAudio.Cue.Interact);
            EquipWeapon(false);
            phase = Phase.Fight;
            enemyAction = EnemyAction.Approach;
            ShowMessage("拿到短棍。鼠标左键攻击，H 使用恢复补给。", 4f);
        }

        private void PlaceCart()
        {
            cartPlaced = true;
            if (sounds != null) sounds.Play(GameAudio.Cue.Door);
            cart.transform.localPosition = new Vector3(0, .32f, -.2f);
            ShowMessage("推车已就位。感染者撞上后会停顿更久。", 3f);
        }

        private void RescueAlan()
        {
            alanFollowing = true;
            ShowMessage("阿岚已和你会合。一起前往出口。", 3f);
        }

        private void LeaveArea()
        {
            Finish(alanFollowing ? EscortEnding.AlanWithShelter : EscortEnding.AlanLost);
        }

        private void StartEquipmentPhase()
        {
            StopEscortSpeech();
            phase = Phase.Equipment;
            equipment.SetActive(true);
            enemy.SetActive(true);
            cart.SetActive(true);
            overturnedCart.SetActive(true);
            escorts.SetActive(false);
            ShowMessage("接送者弃约离开。感染者撞翻了入口旁的行李车。", 4f);
        }

        private void StopEscortSpeech()
        {
            if (escortGaze != null) escortGaze.Release();
            escortGaze = null;
            if (dialogueVoice != null) dialogueVoice.StopVoice();
        }
        public void SuspendSpeech() { StopEscortSpeech(); }

        private void TickEnemy(float dt)
        {
            if (enemy == null || !enemy.activeSelf) return;
            Vector3 target = walker.transform.position;
            Vector3 direction = target - enemy.transform.position;
            direction.y = 0;
            actionTimer -= dt;

            switch (enemyAction)
            {
                case EnemyAction.Approach:
                    if (direction.magnitude > 4.2f)
                        enemyBody.Move(direction.normalized * 1.35f * dt);
                    else
                    {
                        enemyAction = EnemyAction.Windup;
                        actionTimer = 1.7f;
                        chargeDirection = direction.sqrMagnitude > .01f ? direction.normalized : -yard.forward;
                        if (chargeDirection.sqrMagnitude > .01f) enemy.transform.rotation = Quaternion.LookRotation(chargeDirection);
                        chargeMarker.SetActive(true);
                        if (growlSource != null) growlSource.PlayOneShot(growl);
                    }
                    if (direction.sqrMagnitude > .01f)
                        enemy.transform.rotation = Quaternion.LookRotation(direction);
                    break;
                case EnemyAction.Windup:
                    if (actionTimer <= 0)
                    {
                        enemyAction = EnemyAction.Charge;
                        actionTimer = 1.1f;
                        chargeBlockedTime = 0;
                        chargeMarker.SetActive(false);
                    }
                    break;
                case EnemyAction.Charge:
                    Vector3 before = enemy.transform.position;
                    enemyBody.Move(chargeDirection * 7f * dt);
                    Vector3 movement = enemy.transform.position - before;
                    movement.y = 0;
                    float expected = 7f * dt;
                    chargeBlockedTime = movement.magnitude < expected * .08f ? chargeBlockedTime + dt : 0;
                    if (cartPlaced && !cartUsed && SegmentNear(before, enemy.transform.position, cart.transform.position, 1f))
                    {
                        cartUsed = true;
                        cart.transform.localRotation = Quaternion.Euler(0, 0, 65);
                        Recover(2.8f);
                        ShowMessage("推车挡住了冲撞，趁它停顿反击！", 2.8f);
                    }
                    else if (FlatDistance(enemy.transform.position, target) < 1.15f && damageCooldown <= 0 &&
                             !IsInProtectedRearArc(enemy.transform.forward, target - enemy.transform.position))
                    {
                        playerHealth--;
                        if (sounds != null) sounds.Play(GameAudio.Cue.Hurt);
                        damageFlash = .8f;
                        damageCooldown = 1.2f;
                        Recover(1.45f);
                        if (playerHealth <= 0) phase = Phase.Failed;
                        else ShowMessage("受到冲撞。按 H 使用一次恢复补给。", 2.5f);
                    }
                    else if (actionTimer <= 0 || chargeBlockedTime >= .18f)
                        Recover(1.45f);
                    break;
                case EnemyAction.Recover:
                    if (actionTimer <= 0) enemyAction = EnemyAction.Approach;
                    break;
            }
            AnimateEnemy(dt);
        }

        private void Recover(float seconds)
        {
            enemyAction = EnemyAction.Recover;
            actionTimer = seconds;
            chargeMarker.SetActive(false);
        }

        private void AnimateEnemy(float dt)
        {
            if (enemyAnimator != null)
            {
                if (enemyAction == EnemyAction.Approach) { enemyAnimator.Play("Zombie|ZombieWalk", true); enemyAnimator.SetLean(0); }
                else if (enemyAction == EnemyAction.Windup) { enemyAnimator.Play("Zombie|ZombieBite", false, 3f); enemyAnimator.SetLean(15); }
                else if (enemyAction == EnemyAction.Charge) { enemyAnimator.Play("Zombie|ZombieRun", true, 1.3f); enemyAnimator.SetLean(32); }
                else if (enemyAction == EnemyAction.Recover) { enemyAnimator.Play("Zombie|ZombieIdle", true, .5f); enemyAnimator.SetLean(24); }
                return;
            }
            if (rig == null) return;
            animationClock += dt;
            float step = Mathf.Sin(animationClock * (enemyAction == EnemyAction.Charge ? 15f : 6f));
            if (enemyAction == EnemyAction.Windup)
            {
                float wind = 1f - Mathf.Clamp01(actionTimer / 1.7f);
                rig.torso.localRotation = Quaternion.Euler(20f + wind * 18f, 0, 0);
                rig.head.localRotation = Quaternion.Euler(-18f, 0, -8f);
                rig.leftArm.localRotation = Quaternion.Euler(-80f - wind * 28f, 0, 22f);
                rig.rightArm.localRotation = Quaternion.Euler(-80f - wind * 28f, 0, -22f);
                rig.leftLeg.localRotation = Quaternion.Euler(18f, 0, 0);
                rig.rightLeg.localRotation = Quaternion.Euler(-18f, 0, 0);
            }
            else if (enemyAction == EnemyAction.Charge)
            {
                rig.torso.localRotation = Quaternion.Euler(46, 0, 0);
                rig.head.localRotation = Quaternion.Euler(-30, 0, 0);
                rig.leftArm.localRotation = Quaternion.Euler(-112, 0, 18);
                rig.rightArm.localRotation = Quaternion.Euler(-112, 0, -18);
                rig.leftLeg.localRotation = Quaternion.Euler(step * 42, 0, 0);
                rig.rightLeg.localRotation = Quaternion.Euler(-step * 42, 0, 0);
            }
            else if (enemyAction == EnemyAction.Recover)
            {
                rig.torso.localRotation = Quaternion.Euler(38, 0, 8);
                rig.head.localRotation = Quaternion.Euler(24, 0, -16);
                rig.leftArm.localRotation = Quaternion.Euler(18, 0, 18);
                rig.rightArm.localRotation = Quaternion.Euler(10, 0, -18);
                rig.leftLeg.localRotation = Quaternion.identity;
                rig.rightLeg.localRotation = Quaternion.identity;
            }
            else
            {
                rig.torso.localRotation = Quaternion.Euler(8, 0, step * 3);
                rig.head.localRotation = Quaternion.Euler(-5, 0, 7);
                rig.leftArm.localRotation = Quaternion.Euler(step * 25, 0, 12);
                rig.rightArm.localRotation = Quaternion.Euler(-step * 25, 0, -12);
                rig.leftLeg.localRotation = Quaternion.Euler(-step * 24, 0, 0);
                rig.rightLeg.localRotation = Quaternion.Euler(step * 24, 0, 0);
            }
        }

        private void Attack()
        {
            attackCooldown = .55f;
            swingTimer = .25f;
            if (sounds != null) sounds.Play(GameAudio.Cue.Swing);
            Vector3 toEnemy = enemy.transform.position + Vector3.up - walker.ViewCamera.transform.position;
            if (toEnemy.magnitude <= 2.3f && Vector3.Dot(walker.ViewCamera.transform.forward, toEnemy.normalized) > .45f)
            {
                if (enemyAction != EnemyAction.Recover)
                {
                    ShowMessage("躲开冲撞，等它停下来再反击！", 1.5f);
                    return;
                }
                enemyHealth--;
                if (sounds != null) sounds.Play(GameAudio.Cue.Hit);
                hitFeedback = .45f;
                if (enemyHealth <= 0)
                {
                    if (onEnemyDefeated == null || !onEnemyDefeated())
                    {
                        enemyHealth = 1;
                        ShowMessage("当前任务尚不能开启撤离。", 2f);
                        return;
                    }
                    enemyAction = EnemyAction.Defeated;
                    chargeMarker.SetActive(false);
                    enemyBody.enabled = false;
                    if (enemyAnimator != null)
                    {
                        enemyAnimator.enabled = false;
                        corpseVisual = enemyAnimator.transform;
                        corpseStartRotation = corpseVisual.localRotation;
                        corpseStartPosition = corpseVisual.localPosition;
                        Vector3 towardCenter = yard.position - enemy.transform.position;
                        towardCenter.y = 0;
                        float fallAngle = Vector3.Dot(enemy.transform.right, towardCenter) > 0 ? -90f : 90f;
                        corpseEndRotation = Quaternion.Euler(0, 0, fallAngle);
                        corpseFall = .65f;
                    }
                    EquipWeapon(true);
                    phase = Phase.Escape;
                    alan.SetActive(true);
                    ShowMessage("获得武士刀。阿岚：我在检修间，出口方向也标在屏幕上。", 8f);
                }
            }
        }

        private void AnimateCorpse(float dt)
        {
            if (corpseVisual == null || corpseFall <= 0) return;
            corpseFall = Mathf.Max(0, corpseFall - dt);
            float progress = 1f - corpseFall / .65f;
            corpseVisual.localRotation = Quaternion.Slerp(corpseStartRotation, corpseEndRotation, progress);
            corpseVisual.localPosition = Vector3.Lerp(corpseStartPosition, corpseStartPosition + Vector3.up * .22f, progress);
            if (corpseFall <= 0) SettleCorpseOnFloor();
        }

        private void SettleCorpseOnFloor()
        {
            var renderer = corpseVisual.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null) return;
            float floor = yard.TransformPoint(Vector3.zero).y;
            corpseVisual.position += Vector3.up * Mathf.Clamp(floor + .3f - renderer.bounds.center.y, -.5f, .5f);
            Bounds bounds = renderer.bounds;
            Vector3 center = yard.InverseTransformPoint(bounds.center);
            float safeX = Mathf.Clamp(center.x, -9.2f + bounds.extents.x, 9.2f - bounds.extents.x);
            float safeZ = Mathf.Clamp(center.z, -7.65f + bounds.extents.z, 7.65f - bounds.extents.z);
            Vector3 shift = yard.TransformVector(new Vector3(safeX - center.x, 0, safeZ - center.z));
            corpseVisual.position += Vector3.ClampMagnitude(shift, .8f);
        }

        private void AnimateWeapon(float dt)
        {
            if (heldWeapon == null) return;
            swingTimer = Mathf.Max(0, swingTimer - dt);
            heldWeapon.transform.localRotation = Quaternion.Euler(
                15 + (swingTimer > 0 ? -45 * Mathf.Sin(swingTimer / .25f * Mathf.PI) : 0), 15, -20);
        }

        private void EquipWeapon(bool katana)
        {
            if (heldWeapon != null) Destroy(heldWeapon);
            heldWeapon = new GameObject(katana ? "武士刀" : "短棍");
            heldWeapon.transform.SetParent(walker.ViewCamera.transform, false);
            heldWeapon.transform.localPosition = new Vector3(.42f, -.36f, .7f);
            heldWeapon.transform.localRotation = Quaternion.Euler(15, 15, -20);
            visuals.CreateWeapon(heldWeapon.transform, katana);
        }

        private void Finish(EscortEnding ending)
        {
            Action<EscortEnding> callback = onFinished;
            ResetEncounter();
            callback(ending);
        }

        private void CreateWorld()
        {
            encounterRoot = new GameObject("接送事件");
            encounterRoot.transform.SetParent(yard, false);
            Shader shader = Shader.Find("Standard");
            metal = MakeMaterial(shader, new Color(.20f, .25f, .24f));
            wood = MakeMaterial(shader, new Color(.39f, .27f, .17f));
            infected = MakeMaterial(shader, new Color(.42f, .50f, .34f));
            cloth = MakeMaterial(shader, new Color(.25f, .34f, .30f));
            highlight = MakeMaterial(shader, new Color(.86f, .65f, .22f));
            danger = MakeMaterial(shader, new Color(.83f, .12f, .08f));
            growl = CreateGrowl();
            damageTexture = CreateDamageTexture();

            equipment = new GameObject("接送者的装备箱");
            equipment.transform.SetParent(encounterRoot.transform, false);
            equipment.transform.localPosition = new Vector3(-2.5f, .42f, -4.2f);
            Box(equipment.transform, "箱体", Vector3.zero, new Vector3(1, .65f, .7f), wood);
            Box(equipment.transform, "标记", new Vector3(0, .34f, 0), new Vector3(.45f, .03f, .35f), highlight);
            equipment.SetActive(false);

            cart = new GameObject("可推动的行李车");
            cart.transform.SetParent(encounterRoot.transform, false);
            cart.transform.localPosition = new Vector3(-2.5f, .32f, -.2f);
            Box(cart.transform, "底板", Vector3.zero, new Vector3(1.1f, .18f, .7f), metal);
            Box(cart.transform, "把手", new Vector3(0, .55f, .31f), new Vector3(1.05f, 1.1f, .08f), metal);
            Box(cart.transform, "黄色标识", new Vector3(0, .15f, -.34f), new Vector3(.7f, .15f, .05f), highlight);
            cart.SetActive(false);

            overturnedCart = new GameObject("被撞翻的行李车");
            overturnedCart.transform.SetParent(encounterRoot.transform, false);
            overturnedCart.transform.localPosition = new Vector3(2f, .25f, -2.6f);
            overturnedCart.transform.localRotation = Quaternion.Euler(0, 0, 65);
            Box(overturnedCart.transform, "底板", Vector3.zero, new Vector3(1.1f, .18f, .7f), metal);
            Box(overturnedCart.transform, "把手", new Vector3(0, .55f, .31f), new Vector3(1.05f, 1.1f, .08f), metal);
            overturnedCart.SetActive(false);

            escorts = new GameObject("两名路哨接送者");
            escorts.transform.SetParent(encounterRoot.transform, false);
            visuals.CreateGuard(escorts.transform, new Vector3(-1.65f, 0, 3.45f), false);
            visuals.CreateGuard(escorts.transform, new Vector3(1.65f, 0, 3.45f), true);

            enemy = new GameObject("精英感染者");
            enemy.transform.SetParent(encounterRoot.transform, false);
            enemy.transform.localPosition = new Vector3(0, .03f, 3f);
            if (sounds != null)
                growlSource = sounds.CreateSource(enemy, .8f, GameAudio.VolumeChannel.Effects, spatialBlend: 1f);
            enemyBody = enemy.AddComponent<CharacterController>();
            enemyBody.center = new Vector3(0, 1, 0);
            enemyBody.height = 2f;
            enemyBody.radius = .48f;
            enemyAnimator = visuals.CreateAnimatedModel(enemy.transform, "Zombie_Legacy", .24f, 0);
            if (enemyAnimator == null) rig = visuals.CreateElite(enemy.transform);
            else enemyAnimator.Play("Zombie|ZombieIdle", true);
            chargeMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chargeMarker.name = "冲撞方向预警";
            chargeMarker.transform.SetParent(enemy.transform, false);
            chargeMarker.transform.localPosition = new Vector3(0, .03f, 2.5f);
            chargeMarker.transform.localScale = new Vector3(.32f, .02f, 5f);
            chargeMarker.GetComponent<Renderer>().sharedMaterial = danger;
            Collider markerCollider = chargeMarker.GetComponent<Collider>();
            markerCollider.enabled = false;
            Destroy(markerCollider);
            chargeMarker.SetActive(false);

            alan = new GameObject("检修间的阿岚");
            alan.transform.SetParent(encounterRoot.transform, false);
            alan.transform.localPosition = new Vector3(6.6f, 0, 5.2f);
            visuals.CreateAlanReplica(alan.transform);
            Box(alan.transform, "求助灯", new Vector3(0, 1.9f, 0), new Vector3(.2f, .2f, .2f), highlight);
            Light beacon = alan.AddComponent<Light>();
            beacon.shadows = LightShadows.Soft;
            beacon.type = LightType.Point;
            beacon.color = new Color(1f, .72f, .3f);
            beacon.intensity = 1.4f;
            beacon.range = 4f;
            var sign = new GameObject("检修间标记");
            sign.transform.SetParent(alan.transform, false);
            sign.transform.localPosition = new Vector3(0, 2.25f, 0);
            TextMesh label = sign.AddComponent<TextMesh>();
            label.text = "阿岚 · 检修间";
            label.anchor = TextAnchor.MiddleCenter;
            label.characterSize = .055f;
            label.fontSize = 48;
            label.color = new Color(.95f, .89f, .7f);
            label.font = signFont;
            sign.GetComponent<MeshRenderer>().sharedMaterial = signFont.material;
            alan.SetActive(false);

            var exit = new GameObject("撤离出口");
            exit.transform.SetParent(encounterRoot.transform, false);
            exit.transform.localPosition = new Vector3(0, 0, -7.55f);
            Box(exit.transform, "出口标记", new Vector3(0, 2, 0), new Vector3(2, .3f, .1f), highlight);
            var exitLabel = new GameObject("撤离出口文字");
            exitLabel.transform.SetParent(exit.transform, false);
            exitLabel.transform.localPosition = new Vector3(0, 2.34f, 0);
            exitLabel.transform.localRotation = Quaternion.Euler(0, 180, 0);
            TextMesh exitText = exitLabel.AddComponent<TextMesh>();
            exitText.text = "撤离出口";
            exitText.anchor = TextAnchor.MiddleCenter;
            exitText.characterSize = .07f;
            exitText.fontSize = 48;
            exitText.color = new Color(.95f, .89f, .7f);
            exitText.font = signFont;
            exitText.GetComponent<MeshRenderer>().sharedMaterial = signFont.material;
        }

        private static Material MakeMaterial(Shader shader, Color color)
        {
            var material = new Material(shader);
            material.color = color;
            material.SetFloat("_Glossiness", .1f);
            return material;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            Primitive(parent, PrimitiveType.Cube, name, position, scale, material);
        }

        private static void Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            Primitive(parent, PrimitiveType.Sphere, name, position, scale, material);
        }

        private static void Primitive(Transform parent, PrimitiveType type, string name, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        private static AudioClip CreateGrowl()
        {
            const int rate = 22050;
            var samples = new float[rate / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Sin(Mathf.PI * i / samples.Length);
                samples[i] = .16f * envelope * Mathf.Sin(2 * Mathf.PI * 75 * t + Mathf.Sin(t * 29) * 2);
            }
            AudioClip clip = AudioClip.Create("感染者低吼", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        internal static Texture2D CreateDamageTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x + .5f) / size * 2 - 1);
                float dy = Mathf.Abs((y + .5f) / size * 2 - 1);
                float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.58f, .98f, Mathf.Max(dx, dy)));
                float fleck = Mathf.PerlinNoise(x * .21f, y * .21f);
                float alpha = Mathf.Clamp01(edge * (.18f + fleck * .24f));
                texture.SetPixel(x, y, new Color(.52f, .015f, .012f, alpha));
            }
            texture.Apply();
            return texture;
        }

        private bool Near(Vector3 point, float distance) => FlatDistance(walker.transform.position, point) <= distance;
        private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // 后方中心线左右各 45 度为安全范围，边界本身也算安全。
        private static bool IsInProtectedRearArc(Vector3 facing, Vector3 toPlayer)
        {
            facing.y = 0f;
            toPlayer.y = 0f;
            if (facing.sqrMagnitude < .000001f || toPlayer.sqrMagnitude < .000001f) return false;
            const float rearBoundary = -.70710678f;
            return Vector3.Dot(facing.normalized, toPlayer.normalized) <= rearBoundary + .000001f;
        }

        private static bool SegmentNear(Vector3 a, Vector3 b, Vector3 point, float radius)
        {
            Vector2 from = new Vector2(a.x, a.z);
            Vector2 line = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 target = new Vector2(point.x, point.z);
            float t = line.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(target - from, line) / line.sqrMagnitude) : 0;
            return Vector2.Distance(from + line * t, target) <= radius;
        }

        private void ShowMessage(string value, float seconds)
        {
            message = value;
            messageUntil = Time.time + seconds;
        }

        public void DrawOverlay(GUIStyle normal, GUIStyle small, GUIStyle centered, Color panel, Color text, Color accent)
        {
            if (damageFlash > 0 && damageTexture != null)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(damageFlash / .8f));
                GUI.DrawTexture(new Rect(0, 0, 1280, 720), damageTexture);
                GUI.color = previous;
            }
            if (phase == Phase.Arrival)
            {
                Fill(new Rect(340, 545, 600, 55), panel);
                Write(new Rect(350, 550, 580, 45), "路哨接送人员来了。可以移动并观察周围。", centered, text);
                return;
            }
            if (phase == Phase.Offer)
            {
                Fill(new Rect(190, 460, 900, 193), panel);
                Fill(new Rect(190, 460, 4, 193), accent);
                Write(new Rect(215, 468, 850, 42), "接送者：前面有感染者。再加 2 枚，我们才带你们出去。", normal, text);
                Write(new Rect(215, 510, 850, 31), $"车费已付 4 枚  ·  你的酒币 {coins} 枚  ·  可移动观察", small, accent);
                Write(new Rect(215, 546, 850, 35), "1  加付 2 枚，请他们救援（阿岚去路哨）", normal, text);
                Write(new Rect(215, 584, 850, 35), "2  拒绝加价，自己战斗并决定是否接阿岚", normal, text);
                if (!string.IsNullOrEmpty(message)) Write(new Rect(215, 619, 850, 28), message, small, accent);
                return;
            }
            if (phase == Phase.Failed)
            {
                Fill(new Rect(240, 260, 800, 180), panel);
                Write(new Rect(260, 275, 760, 65), "你倒下了", centered, accent);
                Write(new Rect(260, 352, 760, 45), "按 Enter 或 E 从遭遇前重新尝试", centered, text);
                return;
            }
            if (phase == Phase.Fight || phase == Phase.Escape)
            {
                Fill(new Rect(985, 130, 267, 76), panel);
                Write(new Rect(1003, 136, 235, 28), $"生命 {playerHealth}/3  ·  补给 {(hasMedicine ? "1" : "0")}", small, text);
                if (phase == Phase.Fight) Write(new Rect(1003, 169, 235, 27), $"感染者 {enemyHealth}/6", small, accent);
                Write(new Rect(28, 625, 595, 30), "鼠标左键攻击  ·  H 恢复  ·  E 交互", small, text);
                if (hitFeedback > 0)
                    Write(new Rect(655, 330, 120, 30), "命中", small, new Color(.75f, .96f, .71f));
                if (phase == Phase.Fight)
                {
                    string cue = enemyAction == EnemyAction.Windup ? "正在蓄力！侧移躲开红色冲撞线" :
                        enemyAction == EnemyAction.Charge ? "冲撞中！向侧面移动" :
                        enemyAction == EnemyAction.Recover ? "感染者喘息中！靠近按左键反击" : "感染者靠近中，准备侧移";
                    Color cueColor = enemyAction == EnemyAction.Recover ? new Color(.64f, .88f, .62f) : accent;
                    Fill(new Rect(365, 146, 550, 48), panel);
                    Fill(new Rect(365, 146, 4, 48), cueColor);
                    Write(new Rect(377, 151, 526, 38), cue, centered, cueColor);
                }
                else
                {
                    Fill(new Rect(670, 218, 580, 76), panel);
                    Write(new Rect(685, 221, 550, 30), ExitGuidance(), small, accent);
                    string second = alanFollowing ? "阿岚已会合，一起去出口" : AlanGuidance();
                    Write(new Rect(685, 253, 550, 30), second, small, text);
                }
            }
            string prompt = null;
            if (phase == Phase.Equipment && Near(equipment.transform.position, 2f)) prompt = "E  ·  拿取短棍和补给";
            if (phase == Phase.Fight && !cartPlaced && Near(cart.transform.position, 2.1f)) prompt = "E  ·  推动行李车，挡住感染者的冲撞";
            if (phase == Phase.Fight && cartPlaced && !cartUsed && Near(cart.transform.position, 2.1f)) prompt = "推车已就位";
            if (phase == Phase.Escape && !alanFollowing && Near(alan.transform.position, 2.2f)) prompt = "E  ·  接上阿岚，一起离开";
            if (phase == Phase.Escape && Near(yard.TransformPoint(new Vector3(0, 0, -6.8f)), 2f)) prompt = "E  ·  离开接送区";
            if (prompt != null)
            {
                Fill(new Rect(330, 393, 620, 46), panel);
                Write(new Rect(345, 397, 590, 38), prompt, normal, text);
            }
            if (!string.IsNullOrEmpty(message))
            {
                Fill(new Rect(235, 535, 810, 65), panel);
                Write(new Rect(255, 540, 770, 55), message, normal, text);
            }
        }

        private string DirectionTo(Vector3 destination)
        {
            Vector3 delta = destination - walker.transform.position;
            delta.y = 0;
            float distance = delta.magnitude;
            Vector3 local = walker.ViewCamera.transform.InverseTransformDirection(delta.normalized);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            string direction = Mathf.Abs(angle) < 25 ? "正前方" :
                angle >= 25 && angle < 70 ? "右前方" : angle >= 70 && angle < 115 ? "右侧" :
                angle >= 115 && angle < 155 ? "右后方" :
                angle <= -25 && angle > -70 ? "左前方" : angle <= -70 && angle > -115 ? "左侧" :
                angle <= -115 && angle > -155 ? "左后方" : "身后";
            return direction + " · 约 " + Mathf.RoundToInt(distance) + " 米";
        }

        private string ExitGuidance()
        {
            Vector3 local = yard.InverseTransformPoint(walker.transform.position);
            if (local.x > 6.3f && local.z > 2.1f)
                return "撤离：先沿货架右侧绕到南面 · " + DirectionTo(yard.TransformPoint(new Vector3(7.7f, 0, 1.8f)));
            return "撤离出口：" + DirectionTo(yard.TransformPoint(new Vector3(0, 0, -6.8f)));
        }

        private string AlanGuidance()
        {
            Vector3 local = yard.InverseTransformPoint(walker.transform.position);
            if (local.x < 7.1f && local.z < 2.1f)
                return "去检修间：先到货架右侧 · " + DirectionTo(yard.TransformPoint(new Vector3(7.7f, 0, 1.7f)));
            if (local.x < 3.0f && local.z >= 2.1f)
                return "去检修间：先绕到货架南面 · " + DirectionTo(yard.TransformPoint(new Vector3(local.x, 0, 1.7f)));
            return "阿岚在检修间：" + DirectionTo(alan.transform.position);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Write(Rect rect, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }
    }
}
