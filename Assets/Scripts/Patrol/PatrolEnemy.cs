using UnityEngine;

namespace YuDian
{
    internal static class PatrolBalance
    {
        internal static int Health(PatrolEnemy.Kind kind, int wave)
        {
            int w = Mathf.Max(1, wave);
            if (kind == PatrolEnemy.Kind.Charger) return EliteHealth(w);
            if (w <= 10)
            {
                int normal = 5 + w;
                return kind == PatrolEnemy.Kind.Fast ? Mathf.Max(4, normal - 2) :
                    kind == PatrolEnemy.Kind.Charger ? normal * 5 / 4 + 11 : normal;
            }
            if (w <= 20)
            {
                int t = w - 10;
                return kind == PatrolEnemy.Kind.Fast ? 13 + 5 * t / 10 :
                    kind == PatrolEnemy.Kind.Charger ? 29 + 25 * t / 10 : 15 + 17 * t / 10;
            }
            if (w < 51)
            {
                int oldStage = Mathf.Clamp((w - 11) / 10, 0, 4);
                int[] oldNormal = { 32, 35, 38, 41, 44 };
                int[] oldFast = { 18, 20, 21, 23, 24 };
                return kind == PatrolEnemy.Kind.Fast ? oldFast[oldStage] : oldNormal[oldStage];
            }
            int stage = Mathf.Clamp((w - 51) / 10, 0, 5);
            int[] normalHp = { 44, 46, 48, 50, 52, 54 };
            int[] fastHp = { 24, 25, 26, 27, 28, 28 };
            return kind == PatrolEnemy.Kind.Fast ? fastHp[stage] : normalHp[stage];
        }

        private static int EliteHealth(int wave)
        {
            int[] waves = { 5, 20, 40, 75, 95 };
            int[] values = { 28, 90, 210, 300, 320 };
            if (wave <= waves[0]) return values[0];
            for (int i = 1; i < waves.Length; i++)
            {
                if (wave > waves[i]) continue;
                int range = waves[i] - waves[i - 1];
                return values[i - 1] + ((wave - waves[i - 1]) * (values[i] - values[i - 1]) + range / 2) / range;
            }
            return values[values.Length - 1];
        }
    }

    internal sealed class PatrolEnemy : MonoBehaviour
    {
        internal enum Kind { Normal, Fast, Charger }
        private enum ChargePhase { Approach, Windup, Charge, Recover }
        private PatrolYard yard;
        private FirstPersonWalker target;
        private CharacterController body;
        private SphereCollider headHitBox;
        private Transform headBone;
        private Vector3 headBoneOffset;
        private AssetAnimationPlayer animationPlayer;
        private System.Action<int> damagePlayer;
        private int health, maxHealth, attackDamage;
        private float stun, attackCooldown;
        private float speed;
        private float movementScale;
        private Kind kind;
        private ChargePhase chargePhase;
        private float chargeTimer, blockedTime;
        private Vector3 chargeDirection;
        private GameObject chargeMarker;
        private Material dangerMaterial;
        private Material eliteFlesh, eliteWound, eliteBone, eliteMass;
        public bool Alive { get; private set; }
        internal Kind EnemyKind => kind;
        internal int Health => health;
        internal int MaxHealth => maxHealth;
        internal bool IsWindingUp => Alive && kind == Kind.Charger && chargePhase == ChargePhase.Windup;
        internal bool IsHeadHit(Collider hit) => hit != null && hit == headHitBox;
        internal string Warning => kind != Kind.Charger || !Alive ? null :
            chargePhase == ChargePhase.Windup ? "冲撞精英正在蓄力 · 侧移躲开红线" :
            chargePhase == ChargePhase.Charge ? "冲撞中 · 保持侧向移动" :
            chargePhase == ChargePhase.Recover ? "精英停顿 · 趁机反击" : null;

        public void Initialize(PatrolYard arena, FirstPersonWalker player, PrototypeVisuals visuals,
            Kind enemyKind, int wave, System.Action<int> onAttack)
        {
            yard = arena; target = player; damagePlayer = onAttack; kind = enemyKind;
            maxHealth = health = PatrolBalance.Health(kind, wave);
            attackDamage = kind == Kind.Charger && wave >= 25 ? 2 : 1;
            // 普通怪、快怪每波成长：100 波达到玩家行走速度的 80% / 100%，之后增量逐波衰减。
            float baseSpeed = kind == Kind.Fast ? 2.55f : kind == Kind.Charger ? 1.35f : 1.55f;
            if (kind == Kind.Charger)
                speed = baseSpeed * (1f + Mathf.Max(0, wave) / 5 * .01f);
            else
            {
                int w = Mathf.Max(1, wave);
                float progress = w <= 100 ? (w - 1) / 99f :
                    1f + .99f * (1f - Mathf.Pow(.99f, w - 100)) / (.01f * 99f);
                float targetAt100 = target.WalkingSpeed * (kind == Kind.Fast ? 1f : .8f);
                speed = Mathf.Max(.05f, baseSpeed + (targetAt100 - baseSpeed) * progress);
            }
            movementScale = speed / baseSpeed;
            Alive = true; chargePhase = ChargePhase.Approach;
            body = gameObject.AddComponent<CharacterController>();
            body.center = new Vector3(0, kind == Kind.Charger ? 1.20f : .9f, 0);
            body.height = kind == Kind.Charger ? 2.4f : 1.8f; body.radius = kind == Kind.Charger ? .48f : .33f;
            animationPlayer = visuals.CreateAnimatedModel(transform, "Zombie_Legacy", kind == Kind.Charger ? .34f : .24f, 0);
            if (animationPlayer == null) visuals.CreateElite(transform);
            float size = kind == Kind.Charger ? .34f / .24f : 1f;
            // 保留碰撞与受击区分离，覆盖可见头部、躯干、四肢。
            AddHitBox("感染者躯干受击区", new Vector3(0, 1.00f * size, .04f), new Vector3(1.05f * size, .90f * size, 1.0f * size));
            AddHitBox("感染者腿部受击区", new Vector3(0, .35f * size, .04f), new Vector3(1.05f * size, .70f * size, 1.0f * size));
            var head = new GameObject("感染者头部受击区");
            head.transform.SetParent(transform, false);
            headHitBox = head.AddComponent<SphereCollider>();
            headHitBox.isTrigger = true; headHitBox.center = Vector3.zero; headHitBox.radius = .38f * size;
            head.transform.localPosition = new Vector3(0, 1.67f * size, .04f);
            if (animationPlayer != null)
            {
                foreach (Transform bone in animationPlayer.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "Head") { headBone = bone; break; }
                if (headBone != null)
                {
                    headBoneOffset = headBone.InverseTransformPoint(head.transform.position);
                    animationPlayer.PoseUpdated += SyncHeadHitBox;
                }
            }
            if (kind == Kind.Charger) { CreateEliteDetails(); CreateChargeMarker(); }
        }

        internal void SyncHeadHitBox()
        {
            if (headBone != null && headHitBox != null)
                headHitBox.transform.position = headBone.TransformPoint(headBoneOffset);
        }

        private void CreateEliteDetails()
        {
            eliteFlesh = PatrolYard.Paint(Shader.Find("Standard"), new Color(.45f, .55f, .38f));
            eliteWound = PatrolYard.Paint(Shader.Find("Standard"), new Color(.44f, .055f, .045f));
            eliteBone = PatrolYard.Paint(Shader.Find("Standard"), new Color(.68f, .63f, .49f));
            eliteMass = PatrolYard.Paint(Shader.Find("Standard"), new Color(.36f, .42f, .29f));
            Transform spine = FindRigBone("Spine1");
            Transform leftArm = FindRigBone("LeftArm"), rightArm = FindRigBone("RightArm");
            AddElitePiece(spine, "畸变躯干", PrimitiveType.Sphere, new Vector3(0, -.05f, .02f), new Vector3(0, 1.70f, 0), new Vector3(.85f, .72f, .40f), eliteMass);
            AddElitePiece(spine, "胸口创痕", PrimitiveType.Sphere, new Vector3(.17f, -.02f, .23f), new Vector3(.17f, 1.73f, .23f), new Vector3(.11f, .32f, .04f), eliteWound);
            AddElitePiece(leftArm, "畸变肩部", PrimitiveType.Sphere, new Vector3(-.14f, .02f, 0), new Vector3(-.48f, 1.95f, 0), new Vector3(.57f, .43f, .48f), eliteFlesh);
            AddElitePiece(leftArm, "畸变肩部伤口", PrimitiveType.Sphere, new Vector3(-.20f, .07f, .20f), new Vector3(-.54f, 2.0f, .2f), new Vector3(.20f, .16f, .055f), eliteWound);
            AddElitePiece(rightArm, "另一侧肩部", PrimitiveType.Sphere, new Vector3(.10f, .01f, 0), new Vector3(.43f, 1.94f, 0), new Vector3(.40f, .33f, .39f), eliteFlesh);
            AddElitePiece(leftArm, "外露骨刺", PrimitiveType.Cube, new Vector3(-.17f, .24f, .04f), new Vector3(-.50f, 2.15f, 0), new Vector3(.075f, .28f, .10f), eliteBone);
            AddElitePiece(leftArm, "外露骨刺", PrimitiveType.Cube, new Vector3(-.30f, .19f, -.06f), new Vector3(-.62f, 2.1f, 0), new Vector3(.075f, .22f, .08f), eliteBone);
        }

        private Transform FindRigBone(string name)
        {
            if (animationPlayer == null) return null;
            foreach (Transform bone in animationPlayer.GetComponentsInChildren<Transform>(true))
                if (bone.name == name) return bone;
            return null;
        }

        private void AddElitePiece(Transform bone, string name, PrimitiveType type, Vector3 offset, Vector3 fallback, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type); part.name = name;
            Collider collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            Transform anchor = bone != null ? bone : transform;
            part.transform.SetParent(anchor, true);
            part.transform.position = bone != null ? bone.position + transform.TransformDirection(offset) : transform.TransformPoint(fallback);
            part.transform.rotation = transform.rotation;
            Vector3 scale = anchor.lossyScale;
            part.transform.localScale = new Vector3(size.x / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                size.y / Mathf.Max(.0001f, Mathf.Abs(scale.y)), size.z / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
        }

        private void CreateChargeMarker()
        {
            chargeMarker = new GameObject("冲撞方向预警");
            chargeMarker.transform.SetParent(transform, false);
            chargeMarker.transform.localPosition = new Vector3(0, .04f, 0);
            dangerMaterial = PatrolYard.Paint(Shader.Find("Standard"), new Color(.95f, .12f, .04f), true);
            PatrolYard.Box(chargeMarker.transform, "警戒线", new Vector3(0, .01f, 2.5f), new Vector3(.13f, .018f, 5f), dangerMaterial, false);
            for (int i = 1; i <= 4; i++)
            {
                float z = i * 1.05f;
                var left = PatrolYard.Box(chargeMarker.transform, "方向箭头", new Vector3(-.22f, .012f, z - .22f), new Vector3(.11f, .02f, .62f), dangerMaterial, false);
                left.localRotation = Quaternion.Euler(0, 45f, 0);
                var right = PatrolYard.Box(chargeMarker.transform, "方向箭头", new Vector3(.22f, .012f, z - .22f), new Vector3(.11f, .02f, .62f), dangerMaterial, false);
                right.localRotation = Quaternion.Euler(0, -45f, 0);
            }
            chargeMarker.SetActive(false);
        }

        private void AddHitBox(string name, Vector3 center, Vector3 size)
        {
            var area = new GameObject(name); area.transform.SetParent(transform, false);
            var box = area.AddComponent<BoxCollider>();
            box.isTrigger = true; box.center = center; box.size = size;
        }

        public void Tick(float dt)
        {
            if (!Alive) return;
            stun = Mathf.Max(0, stun - dt);
            attackCooldown = Mathf.Max(0, attackCooldown - dt);
            Vector3 delta = target.transform.position - transform.position; delta.y = 0;
            if (kind == Kind.Charger) { TickCharger(dt, delta); return; }
            if (delta.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), dt * 8f);
            if (stun > 0) { if (animationPlayer != null) animationPlayer.Play("Zombie|ZombieIdle", true); return; }
            if (delta.magnitude < 1.35f && yard.ClearLane(transform.position, target.transform.position))
            {
                if (animationPlayer != null) animationPlayer.Play("Zombie|ZombieBite", true, 1.2f);
                if (attackCooldown <= 0) { attackCooldown = 1.5f; damagePlayer(attackDamage); }
                return;
            }
            if (animationPlayer != null) animationPlayer.Play(kind == Kind.Fast ? "Zombie|ZombieRun" : "Zombie|ZombieWalk", true, Mathf.Min(movementScale, 2f));
            MoveTowardTarget(dt);
        }

        private void TickCharger(float dt, Vector3 delta)
        {
            chargeTimer -= dt;
            switch (chargePhase)
            {
                case ChargePhase.Approach:
                    if (delta.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(delta);
                    if (delta.magnitude <= 4.5f && yard.ClearLane(transform.position, target.transform.position))
                    {
                        chargeDirection = delta.sqrMagnitude > .01f ? delta.normalized : transform.forward;
                        transform.rotation = Quaternion.LookRotation(chargeDirection);
                        chargePhase = ChargePhase.Windup; chargeTimer = 1.5f; chargeMarker.SetActive(true);
                        if (animationPlayer != null) { animationPlayer.Play("Zombie|ZombieBite", false, 2f); animationPlayer.SetLean(15); }
                    }
                    else
                    {
                        if (animationPlayer != null) { animationPlayer.Play("Zombie|ZombieWalk", true, Mathf.Min(movementScale, 1.35f)); animationPlayer.SetLean(0); }
                        MoveTowardTarget(dt);
                    }
                    break;
                case ChargePhase.Windup:
                    if (chargeTimer <= 0f)
                    {
                        chargePhase = ChargePhase.Charge; chargeTimer = 1.1f; blockedTime = 0f;
                        chargeMarker.SetActive(false);
                        if (animationPlayer != null) { animationPlayer.Play("Zombie|ZombieRun", true, 1.3f); animationPlayer.SetLean(32); }
                    }
                    break;
                case ChargePhase.Charge:
                    Vector3 before = transform.position;
                    body.Move(chargeDirection * 7f * dt + Vector3.down * 2f * dt);
                    Vector3 moved = transform.position - before; moved.y = 0;
                    blockedTime = moved.magnitude < 7f * dt * .08f ? blockedTime + dt : 0f;
                    if (NearSegment(before, transform.position, target.transform.position, 1.05f) && ChargePathClear(target.transform.position))
                    { damagePlayer(attackDamage); Recover(); }
                    else if (chargeTimer <= 0f || blockedTime >= .18f) Recover();
                    break;
                case ChargePhase.Recover:
                    if (chargeTimer <= 0f) { chargePhase = ChargePhase.Approach; if (animationPlayer != null) animationPlayer.SetLean(0); }
                    break;
            }
        }

        private void Recover()
        {
            chargePhase = ChargePhase.Recover; chargeTimer = 1.45f;
            if (chargeMarker != null) chargeMarker.SetActive(false);
            if (animationPlayer != null) { animationPlayer.Play("Zombie|ZombieIdle", true, .6f); animationPlayer.SetLean(20); }
        }

        // 冲撞被墙或其他实体挡住时，不隔着障碍伤害玩家。
        private bool ChargePathClear(Vector3 targetPosition)
        {
            Vector3 origin = transform.position + Vector3.up * .9f;
            Vector3 end = targetPosition + Vector3.up * .9f;
            Vector3 direction = end - origin;
            if (direction.sqrMagnitude < .0001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.transform.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }

        private static bool NearSegment(Vector3 from, Vector3 to, Vector3 point, float radius)
        {
            from.y = to.y = point.y = 0;
            Vector3 line = to - from;
            float t = line.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(point - from, line) / line.sqrMagnitude) : 0f;
            return (point - (from + line * t)).sqrMagnitude <= radius * radius;
        }

        private void MoveTowardTarget(float dt)
        {
            Vector3 destination = yard.Steer(transform.position, target.transform.position);
            Vector3 move = destination - transform.position; move.y = 0;
            if (move.sqrMagnitude > .001f) body.Move(move.normalized * speed * dt + Vector3.down * 2f * dt);
        }

        public void TakeHit(int amount, bool melee, Vector3 direction)
        {
            if (!Alive) return;
            health -= amount;
            if (health <= 0)
            {
                Alive = false; body.enabled = false; gameObject.SetActive(false);
            }
            else if (melee && kind != Kind.Charger)
            {
                stun = .65f;
                Vector3 push = direction; push.y = 0;
                if (push.sqrMagnitude > .001f) body.Move(push.normalized * .45f);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!UnityEditor.EditorPrefs.GetBool("YuDian.Patrol.DebugDraw", false) || !Application.isPlaying) return;
            foreach (Collider area in GetComponentsInChildren<Collider>())
            {
                if (!area.isTrigger) continue;
                Gizmos.matrix = area.transform.localToWorldMatrix;
                if (area == headHitBox && area is SphereCollider sphere)
                { Gizmos.color = Color.red; Gizmos.DrawWireSphere(sphere.center, sphere.radius); }
                else if (area is BoxCollider box)
                { Gizmos.color = Color.yellow; Gizmos.DrawWireCube(box.center, box.size); }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif

        private void OnDestroy()
        {
            if (animationPlayer != null) animationPlayer.PoseUpdated -= SyncHeadHitBox;
            if (dangerMaterial != null) Destroy(dangerMaterial);
            if (eliteFlesh != null) Destroy(eliteFlesh);
            if (eliteWound != null) Destroy(eliteWound);
            if (eliteBone != null) Destroy(eliteBone);
            if (eliteMass != null) Destroy(eliteMass);
        }
    }
}
