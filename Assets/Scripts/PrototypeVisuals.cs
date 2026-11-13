using System.Collections.Generic;
using UnityEngine;

namespace YuDian
{
    /// <summary>为现有低模角色补充可辨认的五官、装备和战斗道具。</summary>
    public sealed class PrototypeVisuals : MonoBehaviour
    {
        public sealed class EliteRig
        {
            public Transform torso, head, leftArm, rightArm, leftLeg, rightLeg;
        }

        private readonly List<Material> materials = new List<Material>();
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private Material skin, skinDark, eye, white, hair, greyHair, jacket, armor, metal, brass, cloth, blood, bone, infected, pale;
        private Material characterAtlas;
        private Material zombieAtlas;
        private Transform stationAlan;

        public void Initialize(Transform station)
        {
            skin = Mat(new Color(.65f, .46f, .35f));
            skinDark = Mat(new Color(.48f, .32f, .25f));
            eye = Mat(new Color(.035f, .035f, .03f));
            white = Mat(new Color(.8f, .78f, .67f));
            hair = Mat(new Color(.105f, .095f, .075f));
            greyHair = Mat(new Color(.42f, .40f, .36f));
            jacket = Mat(new Color(.19f, .25f, .23f));
            armor = Mat(new Color(.12f, .15f, .15f));
            metal = Mat(new Color(.39f, .43f, .40f), .45f);
            brass = Mat(new Color(.68f, .50f, .22f), .35f);
            cloth = Mat(new Color(.32f, .27f, .21f));
            blood = Mat(new Color(.29f, .055f, .045f));
            bone = Mat(new Color(.71f, .69f, .55f));
            infected = Mat(new Color(.36f, .42f, .31f));
            pale = Mat(new Color(.61f, .65f, .51f));
            characterAtlas = Mat(Color.white);
            characterAtlas.mainTexture = Resources.Load<Texture2D>("Quaternius/Zombie_Atlas");
            zombieAtlas = Mat(Color.white);
            zombieAtlas.mainTexture = Resources.Load<Texture2D>("Quaternius/ZombieTexture");
            Texture2D damagedSkin = MakeMottledSkin();
            infected.mainTexture = damagedSkin;
            pale.mainTexture = damagedSkin;
            stationAlan = station.Find("阿岚");
            AddStationFace(stationAlan, 1.365f, 0);
            AddAlanMobilityChair(stationAlan);
            AddStationFace(station.Find("孙姨"), 1.63f, 1);
            AddStationFace(station.Find("路哨站长"), 1.63f, 2);
        }

        private Material Mat(Color color, float metallic = 0f)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = color;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", .18f);
            materials.Add(m);
            return m;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
            foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
        }

        private Texture2D MakeMottledSkin()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.name = "感染者斑驳皮肤";
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float broad = Mathf.PerlinNoise(x * .058f, y * .058f);
                float fine = Mathf.PerlinNoise(x * .25f + 11, y * .25f + 17);
                float value = Mathf.Clamp01(.55f + broad * .34f + fine * .13f);
                float patch = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.66f, .78f, broad));
                texture.SetPixel(x, y, new Color(value, value * (1 - patch * .20f), value * (1 - patch * .25f), 1));
            }
            texture.Apply();
            textures.Add(texture);
            return texture;
        }

        private void AddStationFace(Transform actor, float y, int kind)
        {
            if (actor == null) return;
            Transform face = Node(actor, "角色面部细节", new Vector3(0, y, kind == 0 ? -.115f : -.143f));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * .054f;
                Piece(face, PrimitiveType.Sphere, "眼白", new Vector3(x, .015f, -.005f), new Vector3(.043f, .022f, .016f), white);
                Piece(face, PrimitiveType.Sphere, "瞳孔", new Vector3(x, .015f, kind == 0 ? -.010f : -.014f), new Vector3(.016f, .018f, .012f), eye);
                if (kind != 0)
                {
                    Transform brow = Piece(face, PrimitiveType.Cube, "眉", new Vector3(x, .046f, -.01f), new Vector3(.05f, kind == 2 ? .016f : .009f, .014f), kind == 1 ? greyHair : hair);
                    if (kind == 2) brow.localRotation = Quaternion.Euler(0, 0, side * 17f);
                }
            }
            Piece(face, PrimitiveType.Cube, "嘴", new Vector3(0, -.063f, -.005f), new Vector3(kind == 1 ? .062f : .047f, .009f, .012f), skinDark);
            if (kind == 0)
            {
                Piece(face, PrimitiveType.Cube, "护目镜横梁", new Vector3(0, .068f, .012f), new Vector3(.19f, .017f, .025f), metal);
                Piece(face, PrimitiveType.Cube, "额前碎发", new Vector3(-.045f, .107f, .014f), new Vector3(.13f, .04f, .02f), hair);
            }
            else if (kind == 1)
            {
                Piece(face, PrimitiveType.Sphere, "灰色发髻", new Vector3(0, .11f, .22f), new Vector3(.17f, .16f, .17f), greyHair);
                Piece(face, PrimitiveType.Cube, "左眼角纹", new Vector3(-.089f, -.005f, 0), new Vector3(.032f, .006f, .012f), skinDark);
                Piece(face, PrimitiveType.Cube, "右眼角纹", new Vector3(.089f, -.005f, 0), new Vector3(.032f, .006f, .012f), skinDark);
            }
            else
            {
                Transform scar = Piece(face, PrimitiveType.Cube, "旧伤疤", new Vector3(.083f, -.018f, -.002f), new Vector3(.012f, .08f, .015f), blood);
                scar.localRotation = Quaternion.Euler(0, 0, 22);
            }
        }

        private void AddAlanMobilityChair(Transform actor)
        {
            if (actor == null) return;
            Transform chair = Node(actor, "助行轮椅", Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform wheel = Piece(chair, PrimitiveType.Cylinder, "轮椅轮", new Vector3(side * .26f, .22f, -.02f), new Vector3(.44f, .025f, .44f), armor);
                wheel.localRotation = Quaternion.Euler(0, 0, 90);
                Transform hub = Piece(chair, PrimitiveType.Cylinder, "轮毂", new Vector3(side * .29f, .22f, -.02f), new Vector3(.12f, .028f, .12f), metal);
                hub.localRotation = Quaternion.Euler(0, 0, 90);
                Piece(chair, PrimitiveType.Cube, "椅架", new Vector3(side * .21f, .40f, .10f), new Vector3(.035f, .38f, .035f), metal);
            }
            Piece(chair, PrimitiveType.Cube, "椅背", new Vector3(0, .83f, .16f), new Vector3(.4f, .55f, .055f), cloth);
        }

        public GameObject CreateAlanReplica(Transform parent)
        {
            var result = new GameObject("阿岚 · 与车站一致的模型");
            result.transform.SetParent(parent, false);
            if (stationAlan == null) return result;
            foreach (string name in new[] { "低模外观", "角色面部细节", "助行轮椅" })
            {
                Transform original = stationAlan.Find(name);
                if (original != null) Instantiate(original.gameObject, result.transform, false).name = name;
            }
            foreach (Collider collider in result.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
            return result;
        }

        public void AddSimpleFace(Transform actor, float headY, float frontZ)
        {
            Transform face = Node(actor, "面部细节", new Vector3(0, headY, frontZ));
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(face, PrimitiveType.Sphere, "眼睛", new Vector3(side * .06f, .025f, -.005f), new Vector3(.035f, .025f, .018f), white);
                Piece(face, PrimitiveType.Sphere, "瞳孔", new Vector3(side * .06f, .025f, -.015f), new Vector3(.014f, .017f, .01f), eye);
                Piece(face, PrimitiveType.Cube, "眉", new Vector3(side * .06f, .055f, -.005f), new Vector3(.045f, .008f, .012f), hair);
            }
            Piece(face, PrimitiveType.Cube, "嘴", new Vector3(0, -.07f, -.005f), new Vector3(.05f, .008f, .01f), skinDark);
        }

        public void CreateGuard(Transform parent, Vector3 position, bool scout, bool injured = false)
        {
            Transform root = Node(parent, scout ? "接送者·侦察员" : "接送者·司机", position);
            AssetAnimationPlayer animated = CreateAnimatedModel(root, scout ? "Characters_Shaun" : "Characters_Matt", 1.15f, 180f, injured ? null : scout ? "Shotgun" : "Rifle");
            if (animated != null)
            {
                animated.Play(injured ? "Idle" : "Walk_Gun", true);
                if (injured) AddInjuryBandage(animated.transform, scout);
                return;
            }
            Piece(root, PrimitiveType.Capsule, "躯干", new Vector3(0, 1.05f, 0), new Vector3(.48f, .39f, .30f), jacket);
            Piece(root, PrimitiveType.Cube, "防护背心", new Vector3(0, 1.14f, -.17f), new Vector3(.43f, .48f, .12f), armor);
            Piece(root, PrimitiveType.Cube, "路哨臂章", new Vector3(-.28f, 1.31f, -.02f), new Vector3(.04f, .10f, .16f), brass);
            Piece(root, PrimitiveType.Sphere, "头", new Vector3(0, 1.69f, 0), new Vector3(.33f, .39f, .31f), scout ? skinDark : skin);
            Piece(root, PrimitiveType.Cube, scout ? "护目镜" : "帽檐", new Vector3(0, 1.77f, -.14f), new Vector3(.32f, .065f, .09f), scout ? metal : armor);
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(root, PrimitiveType.Capsule, "裤腿", new Vector3(side * .105f, .42f, 0), new Vector3(.15f, .34f, .16f), cloth);
                Piece(root, PrimitiveType.Cube, "靴", new Vector3(side * .105f, .08f, -.04f), new Vector3(.16f, .13f, .26f), armor);
                Piece(root, PrimitiveType.Capsule, "袖", new Vector3(side * .29f, 1.10f, 0), new Vector3(.13f, .28f, .14f), jacket);
                Piece(root, PrimitiveType.Sphere, "眼睛", new Vector3(side * .052f, 1.70f, -.153f), new Vector3(.025f, .022f, .012f), eye);
            }
            Piece(root, PrimitiveType.Cube, "口罩或胡茬", new Vector3(0, 1.62f, -.15f), new Vector3(.17f, .045f, .022f), scout ? cloth : hair);
            Piece(root, PrimitiveType.Cube, "枪托", new Vector3(.24f, 1.07f, -.17f), new Vector3(.10f, .12f, .32f), cloth);
            Piece(root, PrimitiveType.Cube, "枪身", new Vector3(.24f, 1.10f, -.38f), new Vector3(.075f, .10f, .40f), metal);
            Piece(root, PrimitiveType.Cube, "枪管", new Vector3(.24f, 1.13f, -.63f), new Vector3(.035f, .035f, .22f), armor);
        }

        private void AddInjuryBandage(Transform model, bool scout)
        {
            Transform wrist = null;
            Transform fingerBase = null;
            var hiddenFingers = new List<Transform>();
            foreach (Transform boneTransform in model.GetComponentsInChildren<Transform>(true))
            {
                if (boneTransform.name == "LowerArm.R") wrist = boneTransform;
                if (boneTransform.name == "Index1.R" || boneTransform.name == "Middle1.R" ||
                    boneTransform.name == "Pinky1.R" || boneTransform.name == "Thumb1.R")
                {
                    if (boneTransform.name == "Middle1.R") fingerBase = boneTransform;
                    hiddenFingers.Add(boneTransform);
                }
            }
            if (wrist == null) return;
            var wrap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wrap.name = scout ? "侦察员包扎的右腕" : "司机包扎的右腕";
            wrap.transform.SetParent(model, false);
            wrap.transform.position = fingerBase != null ? fingerBase.position : wrist.position;
            wrap.transform.localScale = new Vector3(.20f, .18f, .20f);
            wrap.GetComponent<Renderer>().sharedMaterial = white;
            Collider collider = wrap.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            Piece(wrap.transform, PrimitiveType.Cube, "渗血绷带", new Vector3(0, .1f, -.48f), new Vector3(.45f, .12f, .05f), blood);
            SkinnedMeshRenderer body = null;
            foreach (SkinnedMeshRenderer candidate in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (candidate.enabled) { body = candidate; break; }
            model.gameObject.AddComponent<InjuryBandage>().Configure(hiddenFingers.ToArray(), wrist, fingerBase, wrap.transform, body);
        }

        public void CreateShelterLeader(Transform root)
        {
            Piece(root, PrimitiveType.Capsule, "外套", new Vector3(0, 1.07f, 0), new Vector3(.48f, .36f, .3f), jacket);
            Piece(root, PrimitiveType.Cube, "工作围裙", new Vector3(0, .96f, -.17f), new Vector3(.36f, .58f, .05f), cloth);
            Piece(root, PrimitiveType.Sphere, "面部", new Vector3(0, 1.64f, 0), new Vector3(.32f, .37f, .30f), skin);
            Piece(root, PrimitiveType.Sphere, "头发", new Vector3(0, 1.79f, .025f), new Vector3(.33f, .16f, .29f), greyHair);
            AddSimpleFace(root, 1.64f, -.149f);
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(root, PrimitiveType.Capsule, "袖子", new Vector3(side * .29f, 1.12f, 0), new Vector3(.13f, .25f, .14f), jacket);
                Piece(root, PrimitiveType.Sphere, "手", new Vector3(side * .29f, .87f, -.02f), new Vector3(.10f, .10f, .10f), skin);
                Piece(root, PrimitiveType.Capsule, "裤腿", new Vector3(side * .10f, .40f, 0), new Vector3(.15f, .32f, .16f), cloth);
                Piece(root, PrimitiveType.Cube, "鞋", new Vector3(side * .10f, .08f, -.04f), new Vector3(.15f, .13f, .24f), armor);
            }
            Piece(root, PrimitiveType.Cube, "暖棚臂章", new Vector3(-.29f, 1.28f, -.08f), new Vector3(.14f, .08f, .03f), brass);
        }

        public AssetAnimationPlayer CreateAnimatedModel(Transform parent, string name, float scale, float yaw, string weapon = null)
        {
            string path = "Quaternius/" + name;
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null) return null;
            GameObject model = Instantiate(prefab, parent, false);
            model.name = name + " · 角色模型";
            model.transform.localScale = Vector3.one * scale;
            model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Animator animator = model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            string[] weapons = { "Axe", "Guitar", "Knife", "Pistol", "Rifle", "Shotgun", "SMG", "Spear", "WoodenBat_Barbed", "WoodenBat_Saw" };
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = name == "Zombie_Legacy" ? zombieAtlas : characterAtlas;
                foreach (string item in weapons)
                    if (renderer.name == item) renderer.enabled = renderer.name == weapon;
            }
            var player = model.AddComponent<AssetAnimationPlayer>();
            player.Configure(model, path);
            return player;
        }

        public EliteRig CreateElite(Transform root)
        {
            var rig = new EliteRig();
            rig.torso = Node(root, "躯干关节", new Vector3(0, .82f, 0));
            Piece(rig.torso, PrimitiveType.Capsule, "破损躯干", new Vector3(0, .44f, 0), new Vector3(.74f, .49f, .43f), infected);
            Piece(rig.torso, PrimitiveType.Cube, "撕裂衣襟", new Vector3(-.13f, .48f, .20f), new Vector3(.30f, .58f, .04f), jacket).localRotation = Quaternion.Euler(0, 0, -7);
            Piece(rig.torso, PrimitiveType.Cube, "另一侧衣襟", new Vector3(.21f, .41f, .18f), new Vector3(.18f, .45f, .04f), jacket).localRotation = Quaternion.Euler(0, 0, 12);
            Piece(rig.torso, PrimitiveType.Cube, "胸口撕裂伤", new Vector3(.08f, .52f, .225f), new Vector3(.12f, .32f, .024f), blood).localRotation = Quaternion.Euler(0, 0, -24);
            for (int i = 0; i < 3; i++)
                Piece(rig.torso, PrimitiveType.Cube, "暴露的肋骨", new Vector3(.07f, .43f + i * .07f, .242f), new Vector3(.10f, .018f, .012f), bone).localRotation = Quaternion.Euler(0, 0, -20);
            Piece(rig.torso, PrimitiveType.Sphere, "变异肩部", new Vector3(.39f, .77f, 0), new Vector3(.35f, .27f, .37f), infected);
            Piece(rig.torso, PrimitiveType.Cube, "肩部破布", new Vector3(-.36f, .72f, .02f), new Vector3(.20f, .15f, .32f), cloth).localRotation = Quaternion.Euler(0, 0, -18);
            rig.head = Node(rig.torso, "头部关节", new Vector3(0, 1.14f, 0));
            Piece(rig.head, PrimitiveType.Sphere, "感染头部", Vector3.zero, new Vector3(.43f, .52f, .40f), infected);
            Piece(rig.head, PrimitiveType.Sphere, "变异瘤", new Vector3(.17f, .14f, .02f), new Vector3(.17f, .19f, .18f), pale);
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(rig.head, PrimitiveType.Sphere, "眼窝", new Vector3(side * .09f, .02f, .182f), new Vector3(.12f, .065f, .04f), eye);
                Piece(rig.head, PrimitiveType.Sphere, "感染的眼", new Vector3(side * .09f, .02f, .204f), new Vector3(.035f, .023f, .012f), side > 0 ? blood : bone);
                Piece(rig.head, PrimitiveType.Cube, "下压的眉骨", new Vector3(side * .09f, .07f, .17f), new Vector3(.14f, .035f, .04f), infected).localRotation = Quaternion.Euler(0, 0, -side * 14);
            }
            Piece(rig.head, PrimitiveType.Cube, "嘴部创口", new Vector3(0, -.14f, .185f), new Vector3(.19f, .045f, .032f), blood).localRotation = Quaternion.Euler(0, 0, -8);
            for (int i = -2; i <= 2; i++) Piece(rig.head, PrimitiveType.Cube, "断牙", new Vector3(i * .032f, -.14f, .207f), new Vector3(.016f, .018f, .01f), bone);
            Piece(rig.head, PrimitiveType.Cube, "面部伤痕", new Vector3(.12f, -.055f, .18f), new Vector3(.02f, .16f, .015f), blood).localRotation = Quaternion.Euler(0, 0, -20);
            rig.leftArm = Limb(rig.torso, "左臂关节", new Vector3(-.53f, .77f, 0), -.06f);
            rig.rightArm = Limb(rig.torso, "右臂关节", new Vector3(.53f, .77f, 0), .06f);
            rig.leftLeg = Leg(root, "左腿关节", new Vector3(-.23f, .82f, 0));
            rig.rightLeg = Leg(root, "右腿关节", new Vector3(.23f, .82f, 0));
            return rig;
        }

        private Transform Limb(Transform parent, string name, Vector3 pos, float offset)
        {
            Transform limb = Node(parent, name, pos);
            Piece(limb, PrimitiveType.Capsule, "上臂", new Vector3(offset, -.24f, 0), new Vector3(.24f, .26f, .25f), infected);
            Piece(limb, PrimitiveType.Capsule, "前臂", new Vector3(offset, -.68f, .08f), new Vector3(.19f, .24f, .20f), pale);
            Piece(limb, PrimitiveType.Cube, "撕裂伤", new Vector3(offset, -.61f, .18f), new Vector3(.11f, .19f, .018f), blood).localRotation = Quaternion.Euler(0, 0, 20);
            Piece(limb, PrimitiveType.Sphere, "手", new Vector3(offset, -.91f, .1f), new Vector3(.20f, .20f, .18f), infected);
            for (int finger = -1; finger <= 1; finger++)
            {
                Transform claw = Piece(limb, PrimitiveType.Cube, "指爪", new Vector3(offset + finger * .055f, -1.03f, .14f), new Vector3(.035f, .14f, .035f), infected);
                claw.localRotation = Quaternion.Euler(18, 0, finger * 9);
            }
            return limb;
        }

        private Transform Leg(Transform parent, string name, Vector3 pos)
        {
            Transform limb = Node(parent, name, pos);
            Piece(limb, PrimitiveType.Capsule, "破裤腿", new Vector3(0, -.36f, 0), new Vector3(.29f, .34f, .30f), cloth);
            Piece(limb, PrimitiveType.Cube, "脚", new Vector3(0, -.73f, .1f), new Vector3(.25f, .16f, .42f), armor);
            return limb;
        }

        public void CreateWeapon(Transform root, bool katana)
        {
            if (!katana)
            {
                Piece(root, PrimitiveType.Cylinder, "木棍主体", new Vector3(0, .16f, 0), new Vector3(.075f, .34f, .075f), cloth);
                Piece(root, PrimitiveType.Cylinder, "缠布握把", new Vector3(0, -.11f, 0), new Vector3(.082f, .09f, .082f), armor);
                for (int i = 0; i < 3; i++) Piece(root, PrimitiveType.Cylinder, "铁箍", new Vector3(0, .32f + i * .045f, 0), new Vector3(.082f, .012f, .082f), metal);
                return;
            }
            Piece(root, PrimitiveType.Cube, "刀柄", new Vector3(0, -.18f, 0), new Vector3(.06f, .25f, .06f), armor);
            for (int i = 0; i < 4; i++) Piece(root, PrimitiveType.Cube, "柄绳", new Vector3(0, -.26f + i * .05f, -.032f), new Vector3(.064f, .014f, .008f), white).localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 25 : -25);
            Piece(root, PrimitiveType.Cylinder, "圆形护手", new Vector3(0, -.04f, 0), new Vector3(.15f, .015f, .15f), brass);
            Piece(root, PrimitiveType.Cube, "刀身", new Vector3(.012f, .31f, 0), new Vector3(.075f, .70f, .025f), metal).localRotation = Quaternion.Euler(0, 0, -2);
            Piece(root, PrimitiveType.Cube, "刃口", new Vector3(.052f, .31f, -.002f), new Vector3(.012f, .69f, .028f), white).localRotation = Quaternion.Euler(0, 0, -2);
            Piece(root, PrimitiveType.Cube, "刀尖", new Vector3(.03f, .70f, 0), new Vector3(.052f, .10f, .022f), metal).localRotation = Quaternion.Euler(0, 0, 18);
        }

        private static Transform Node(Transform parent, string name, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            return obj.transform;
        }

        private static Transform Piece(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
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
            return obj.transform;
        }
    }

}
