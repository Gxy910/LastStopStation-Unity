using UnityEngine;

namespace YuDian
{
    public sealed class PatrolYard
    {
        private Bounds obstacle;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly PatrolYardLayout layout;
        private readonly GameObject sideGate;
        private readonly Light sideLamp;

        public GameObject Root { get; }
        public Transform Arrival { get; }
        public Transform Exit { get; }
        public Transform Posting { get; }

        public PatrolYard(Transform station, Font font, Material textMaterial)
        {
            var shader = Shader.Find("Standard");
            var concrete = Paint(shader, new Color(.36f, .37f, .35f));
            var wall = Paint(shader, new Color(.19f, .23f, .23f));
            var steel = Paint(shader, new Color(.29f, .35f, .31f));
            var wood = Paint(shader, new Color(.37f, .25f, .16f));
            var green = Paint(shader, new Color(.23f, .95f, .49f), true);
            var amber = Paint(shader, new Color(.96f, .49f, .19f), true);
            var blue = Paint(shader, new Color(.24f, .65f, .94f), true);

            Transform dispatch = station.Find("路哨站长");
            Vector3 offset = dispatch != null ? dispatch.localPosition : new Vector3(0, 0, 44);
            // 值勤板挂到岗亭南墙外侧，沿站台通道走来即可看见。
            Vector3 postingPosition = offset + new Vector3(1.85f, 1.62f, -4.52f);
            // 摆放点由编辑器添加；没有摆放点时沿用原来的位置。
            Transform postingGroup = Node(station, "清障委托板_运行时", postingPosition);
            PatrolPostingMarker marker = station.GetComponentInChildren<PatrolPostingMarker>(true);
            if (marker != null)
            {
                postingGroup.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
                marker.gameObject.SetActive(false);
            }
            Posting = Box(postingGroup, "路哨清障值勤板", Vector3.zero,
                new Vector3(1.38f, .96f, .12f), steel);
            Sign(Posting, "清障委托", new Vector3(0, .17f, -.55f), font, textMaterial, .052f);
            Sign(Posting, "领枪 · 货场 · 撤离", new Vector3(0, -.20f, -.55f), font, textMaterial, .024f);
            Box(postingGroup, "清障牌指示灯", new Vector3(0, .55f, -.09f),
                new Vector3(1.20f, .10f, .10f), amber, false);
            Transform lightNode = Node(postingGroup, "清障牌照明", new Vector3(0, .72f, -.40f));
            Light boardLight = lightNode.gameObject.AddComponent<Light>();
            boardLight.type = LightType.Point; boardLight.color = new Color(1f, .72f, .35f);
            boardLight.range = 3.5f; boardLight.intensity = 1.1f;


            PatrolYardLayout sceneLayout = null;
            foreach (var candidate in Object.FindObjectsOfType<PatrolYardLayout>(true))
                if (candidate.gameObject.scene == station.gameObject.scene) { sceneLayout = candidate; break; }
            var yardPrefab = sceneLayout == null ? Resources.Load<PatrolYardLayout>("PatrolYardEditable/清障货场") : null;
            Root = sceneLayout != null ? sceneLayout.gameObject : yardPrefab != null
                ? Object.Instantiate(yardPrefab).gameObject
                : BuildYard(font, textMaterial, concrete, wall, steel, wood, green, amber, blue);
            layout = Root.GetComponent<PatrolYardLayout>();
            Arrival = layout.arrival;
            Exit = layout.exit;
            foreach (var text in Root.GetComponentsInChildren<TextMesh>(true))
            {
                text.font = font;
                var renderer = text.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = textMaterial != null ? textMaterial : font.material;
            }
            UpdateObstacle();
            // 保持场景现有物件不变：侧门提示贴近东墙，远离南侧撤离门。
            Transform side = Node(Root.transform, "后期侧门_运行时", new Vector3(9.77f, 0, 10.5f));
            sideGate = side.gameObject;
            Box(side, "侧门门板", new Vector3(0, 1.30f, 0), new Vector3(.07f, 2.6f, 2.35f), steel, false);
            Box(side, "侧门警示灯", new Vector3(-.08f, 2.75f, 0), new Vector3(.12f, .13f, 2.2f), amber, false);
            sideLamp = Node(side, "侧门预警照明", new Vector3(-.35f, 2.65f, 0)).gameObject.AddComponent<Light>();
            sideLamp.type = LightType.Point; sideLamp.color = new Color(1f, .32f, .08f); sideLamp.range = 5f; sideLamp.intensity = 1.6f;
            sideGate.SetActive(false);
            Root.SetActive(false);
        }

        // 由编辑器创建可持久化的场景资源；运行时无场景货场时也使用相同的构建逻辑。
        public static GameObject CreateEditableYard(Font font, Material textMaterial)
        {
            var shader = Shader.Find("Standard");
            return BuildYard(font, textMaterial,
                Paint(shader, new Color(.36f, .37f, .35f)), Paint(shader, new Color(.19f, .23f, .23f)),
                Paint(shader, new Color(.29f, .35f, .31f)), Paint(shader, new Color(.37f, .25f, .16f)),
                Paint(shader, new Color(.23f, .95f, .49f), true), Paint(shader, new Color(.96f, .49f, .19f), true),
                Paint(shader, new Color(.24f, .65f, .94f), true));
        }

        private static GameObject BuildYard(Font font, Material textMaterial, Material concrete, Material wall,
            Material steel, Material wood, Material green, Material amber, Material blue)
        {
            GameObject yard = new GameObject("清障委托 · 旧货场");
            Transform root = yard.transform;
            root.position = new Vector3(400, 0, 100);
            Box(root, "货场混凝土地面", new Vector3(0, -.14f, 8), new Vector3(20, .28f, 22), concrete);
            Box(root, "西围墙", new Vector3(-10, 1.65f, 8), new Vector3(.35f, 3.3f, 22), wall);
            Box(root, "东围墙", new Vector3(10, 1.65f, 8), new Vector3(.35f, 3.3f, 22), wall);
            Box(root, "装卸区围墙", new Vector3(0, 1.65f, 19), new Vector3(20, 3.3f, .35f), wall);
            Box(root, "入口围墙", new Vector3(0, 1.65f, -3), new Vector3(20, 3.3f, .35f), wall);

            Transform cargoGroup = Node(root, "中央货箱_整体", new Vector3(0, 0, 8));
            Transform cargo = Box(cargoGroup, "中央货箱组", new Vector3(0, 1, 0), new Vector3(5.65f, 2, 4.4f), wood);
            for (int i = -2; i <= 2; i++)
            {
                Box(cargoGroup, "货箱角钢", new Vector3(i * 1.38f, 1, -2.21f), new Vector3(.10f, 1.95f, .04f), steel, false);
                Box(cargoGroup, "货箱角钢", new Vector3(i * 1.38f, 1, 2.21f), new Vector3(.10f, 1.95f, .04f), steel, false);
            }
            Box(cargoGroup, "货箱堆", new Vector3(-1.05f, 2.29f, -.5f), new Vector3(2.5f, .58f, 2.2f), steel, false);

            Transform exitGroup = Node(root, "撤离门_整体", new Vector3(0, 0, -2.77f));
            Box(exitGroup, "撤离门", new Vector3(0, 1.29f, 0), new Vector3(2.75f, 2.58f, .16f), steel);
            Box(exitGroup, "撤离绿灯", new Vector3(0, 2.72f, .16f), new Vector3(2.75f, .14f, .12f), green, false);
            Transform exitSign = Sign(exitGroup, "↑  返回路哨", new Vector3(0, 2.19f, .10f), font, textMaterial, .21f);
            exitSign.localRotation = Quaternion.Euler(0, 180, 0);

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -6.5f : 6.5f;
                Box(root, "装卸口", new Vector3(x, 1.35f, 18.76f), new Vector3(3.1f, 2.7f, .14f), steel);
                Box(root, "来袭方向灯", new Vector3(x, 2.83f, 18.65f), new Vector3(2.7f, .12f, .09f), i == 0 ? amber : blue, false);
                Sign(root, i == 0 ? "西侧装卸口" : "东侧装卸口", new Vector3(x, 2.36f, 18.66f), font, textMaterial, .19f);
                for (int z = 1; z < 18; z += 2)
                    Box(root, "通道标线", new Vector3(x, .008f, z), new Vector3(.06f, .016f, 1.05f), i == 0 ? amber : blue, false);
            }

            var yardLayout = yard.AddComponent<PatrolYardLayout>();
            yardLayout.arrival = Node(root, "清障出生点", new Vector3(0, .05f, .5f));
            yardLayout.exit = Node(exitGroup, "撤离交互点", new Vector3(0, 0, .59f));
            yardLayout.leftSpawn = Node(root, "西侧感染者出生点", new Vector3(-6.5f, .06f, 16));
            yardLayout.rightSpawn = Node(root, "东侧感染者出生点", new Vector3(6.5f, .06f, 16));
            yardLayout.centralObstacle = cargo.GetComponent<BoxCollider>();
            return yard;
        }

        // 根据场景中的中央货箱计算绕行区域。角点使用根节点的局部坐标。
        private void UpdateObstacle()
        {
            if (layout.centralObstacle == null) { obstacle = new Bounds(Vector3.zero, Vector3.zero); return; }
            BoxCollider box = layout.centralObstacle;
            Vector3 half = box.size * .5f;
            Vector3 min = new Vector3(float.PositiveInfinity, 0, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, 0, float.NegativeInfinity);
            foreach (float x in new[] { -half.x, half.x })
                foreach (float z in new[] { -half.z, half.z })
                {
                    Vector3 point = Root.transform.InverseTransformPoint(box.transform.TransformPoint(box.center + new Vector3(x, 0, z)));
                    min.x = Mathf.Min(min.x, point.x); min.z = Mathf.Min(min.z, point.z);
                    max.x = Mathf.Max(max.x, point.x); max.z = Mathf.Max(max.z, point.z);
                }
            const float margin = .525f;
            min.x -= margin; min.z -= margin; max.x += margin; max.z += margin;
            obstacle = new Bounds(new Vector3((min.x + max.x)*.5f, .65f, (min.z + max.z)*.5f), new Vector3(max.x-min.x, 2, max.z-min.z));
            const float cornerMargin = .16f;
            corners[0] = new Vector3(min.x-cornerMargin,.65f,min.z-cornerMargin);
            corners[1] = new Vector3(max.x+cornerMargin,.65f,min.z-cornerMargin);
            corners[2] = new Vector3(max.x+cornerMargin,.65f,max.z+cornerMargin);
            corners[3] = new Vector3(min.x-cornerMargin,.65f,max.z+cornerMargin);
        }

        public void SetSideGate(bool visible, bool warning)
        {
            sideGate.SetActive(visible);
            sideLamp.enabled = warning;
        }

        public Vector3 SpawnPosition(int lane, int index)
        {
            if (lane == 2) return Root.transform.TransformPoint(new Vector3(8.8f, .06f, 10.5f + (index % 2) * .65f));
            Transform spawn = lane == 0 ? layout.leftSpawn : layout.rightSpawn;
            if (spawn == null) return Root.transform.TransformPoint(new Vector3(lane == 0 ? -6.5f : 6.5f, .06f, 16 + (index % 3 - 1) * .75f));
            return spawn.position + Root.transform.forward * ((index % 3 - 1) * .75f);
        }

        // 货场只有一个中央障碍。两边可走，用四个拐角取最短可走路线。
        public Vector3 Steer(Vector3 fromWorld, Vector3 targetWorld)
        {
            Vector3 from = Root.transform.InverseTransformPoint(fromWorld);
            Vector3 target = Root.transform.InverseTransformPoint(targetWorld);
            from.y = target.y = .65f;
            if (Clear(from, target)) return targetWorld;

            float best = float.PositiveInfinity;
            Vector3 next = target;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 corner = corners[i];
                if (!Clear(from, corner) || (from - corner).sqrMagnitude < .028f) continue;
                for (int j = 0; j < corners.Length; j++)
                {
                    if (!Clear(corners[j], target)) continue;
                    int difference = Mathf.Abs(i - j);
                    float distance = Vector3.Distance(from, corner) + Vector3.Distance(corners[j], target);
                    if (difference == 2) distance += obstacle.size.x + obstacle.size.z;
                    else if (i != j) distance += Vector3.Distance(corner, corners[j]);
                    if (distance >= best) continue;
                    best = distance;
                    next = corner;
                }
            }
            return Root.transform.TransformPoint(next);
        }

        public bool ClearLane(Vector3 fromWorld, Vector3 targetWorld)
        {
            Vector3 from = Root.transform.InverseTransformPoint(fromWorld);
            Vector3 target = Root.transform.InverseTransformPoint(targetWorld);
            from.y = target.y = .65f;
            return Clear(from, target);
        }

        private bool Clear(Vector3 from, Vector3 target)
        {
            Vector3 delta = target - from;
            return delta.sqrMagnitude < .0001f ||
                !obstacle.IntersectRay(new Ray(from, delta.normalized), out float distance) || distance >= delta.magnitude;
        }

        internal static Material Paint(Shader shader, Color color, bool lit = false)
        {
            var material = new Material(shader) { color = color };
            if (lit)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * .55f);
            }
            return material;
        }

        internal static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = size;
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;
            shape.GetComponent<Collider>().enabled = solid;
            return shape.transform;
        }

        private static Transform Sign(Transform parent, string message, Vector3 position, Font font, Material material, float size)
        {
            Transform node = Node(parent, message, position);
            var text = node.gameObject.AddComponent<TextMesh>();
            text.font = font;
            text.anchor = TextAnchor.MiddleCenter;
            text.characterSize = size;
            text.fontSize = 42;
            text.color = new Color(.93f, .92f, .79f);
            text.text = message;
            node.GetComponent<MeshRenderer>().sharedMaterial = material != null ? material : font.material;
            return node;
        }

        private static Transform Node(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }
    }
}

