using UnityEngine;

namespace YuDian
{
    internal sealed class PatrolEnemy : MonoBehaviour
    {
        internal enum Kind { Normal, Fast, Charger }
        private enum ChargePhase { Approach, Windup, Charge, Recover }
        private PatrolYard yard;
        private FirstPersonWalker target;
        private CharacterController body;
        private SphereCollider headHitBox;
        private AssetAnimationPlayer animationPlayer;
        private System.Action damagePlayer;
        private int health;
        private float stun, attackCooldown;
        private float speed;
        private Kind kind;
        private ChargePhase chargePhase;
        private float chargeTimer, blockedTime;
        private Vector3 chargeDirection;
        private GameObject chargeMarker;
        private Material dangerMaterial;
        public bool Alive { get; private set; }
        internal Kind EnemyKind => kind;
        internal int Health => health;
        internal bool IsHeadHit(Collider hit) => hit != null && hit == headHitBox;
        internal string Warning => kind != Kind.Charger || !Alive ? null :
            chargePhase == ChargePhase.Windup ? "冲撞精英正在蓄力 · 侧移躲开红线" :
            chargePhase == ChargePhase.Charge ? "冲撞中 · 保持侧向移动" :
            chargePhase == ChargePhase.Recover ? "精英停顿 · 趁机反击" : null;

        public void Initialize(PatrolYard arena, FirstPersonWalker player, PrototypeVisuals visuals,
            Kind enemyKind, int wave, System.Action onAttack)
        {
            yard = arena; target = player; damagePlayer = onAttack; kind = enemyKind;
            // 普通怪从 6 点生命开始，随波数增长；快怪较脆，精英更耐打。
            long normalHealth = 6L + System.Math.Max(0L, (long)wave - 1L);
            long chosenHealth = kind == Kind.Fast ? System.Math.Max(4L, normalHealth - 2L) :
                kind == Kind.Charger ? normalHealth * 5L / 4L + 11L : normalHealth;
            health = (int)System.Math.Min(int.MaxValue, chosenHealth);
            speed = kind == Kind.Fast ? 2.55f : kind == Kind.Charger ? 1.35f : 1.55f;
            Alive = true; chargePhase = ChargePhase.Approach;
            body = gameObject.AddComponent<CharacterController>();
            body.center = new Vector3(0, kind == Kind.Charger ? 1.05f : .9f, 0);
            body.height = kind == Kind.Charger ? 2.1f : 1.8f; body.radius = kind == Kind.Charger ? .44f : .33f;
            animationPlayer = visuals.CreateAnimatedModel(transform, "Zombie_Legacy", kind == Kind.Charger ? .30f : .24f, 0);
            if (animationPlayer == null) visuals.CreateElite(transform);
            float size = kind == Kind.Charger ? 1.2f : 1f;
            // 保留碰撞与受击区分离，覆盖可见头部、躯干、四肢。
            AddHitBox("感染者躯干受击区", new Vector3(0, 1.05f * size, .04f), new Vector3(1.05f * size, .95f * size, 1.0f * size));
            AddHitBox("感染者腿部受击区", new Vector3(0, .35f * size, .04f), new Vector3(1.05f * size, .70f * size, 1.0f * size));
            var head = new GameObject("感染者头部受击区");
            head.transform.SetParent(transform, false);
            headHitBox = head.AddComponent<SphereCollider>();
            headHitBox.isTrigger = true; headHitBox.center = new Vector3(0, 1.67f * size, .04f); headHitBox.radius = .38f * size;
            if (kind == Kind.Charger) CreateChargeMarker();
        }

        private void CreateChargeMarker()
        {
            chargeMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chargeMarker.name = "冲撞方向预警";
            chargeMarker.transform.SetParent(transform, false);
            chargeMarker.transform.localPosition = new Vector3(0, .04f, 2.5f);
            chargeMarker.transform.localScale = new Vector3(.38f, .018f, 5f);
            var markerCollider = chargeMarker.GetComponent<Collider>(); markerCollider.enabled = false; Destroy(markerCollider);
            dangerMaterial = PatrolYard.Paint(Shader.Find("Standard"), new Color(.95f, .12f, .04f), true);
            chargeMarker.GetComponent<Renderer>().sharedMaterial = dangerMaterial;
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
                if (attackCooldown <= 0) { attackCooldown = 1.5f; damagePlayer(); }
                return;
            }
            if (animationPlayer != null) animationPlayer.Play(speed > 2 ? "Zombie|ZombieRun" : "Zombie|ZombieWalk", true);
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
                        if (animationPlayer != null) { animationPlayer.Play("Zombie|ZombieWalk", true); animationPlayer.SetLean(0); }
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
                    { damagePlayer(); Recover(); }
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

        private void OnDestroy()
        {
            if (dangerMaterial != null) Destroy(dangerMaterial);
        }
    }
}
