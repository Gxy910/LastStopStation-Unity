using UnityEngine;

namespace YuDian
{
    /// <summary>根据接送结果布置回站后的人员和物件。</summary>
    public sealed class EpilogueStage : MonoBehaviour
    {
        private Transform station;
        private FirstPersonWalker walker;
        private Transform alan;
        private Vector3 alanPosition;
        private Quaternion alanRotation;
        private bool alanWasActive;
        private Font font;
        private Material signMaterial;
        private GameObject stage;
        private GameObject agreedDelivery;
        private GameObject katana;
        private float swingTimer;
        private Material dark;
        private Material green;
        private Material wood;
        private Material pale;
        private EscortEnding currentEnding;
        private PrototypeVisuals visuals;
        private EpilogueLayout layout;

        public Transform ReturnSpawn => layout != null ? layout.returnSpawn : null;

        public void Configure(Transform stationRoot, FirstPersonWalker player, Font signFont, PrototypeVisuals characterVisuals, Material textMaterial)
        {
            station = stationRoot;
            walker = player;
            font = signFont;
            signMaterial = textMaterial;
            visuals = characterVisuals;
            // 优先使用用户放在场景里的布局；未添加时使用资源中的默认布局。
            layout = station.GetComponentInChildren<EpilogueLayout>(true);
            var prefab = Resources.Load<EpilogueLayout>("Epilogue/第四章_可视化布局");
            if (layout == null && prefab != null) layout = Instantiate(prefab, station, false);
            if (layout != null)
            {
                layout.ApplyPreviewEdits(prefab);
                layout.HidePreviews();
            }
            alan = station.Find("阿岚");
            alanPosition = alan.localPosition;
            alanRotation = alan.localRotation;
            alanWasActive = alan.gameObject.activeSelf;
        }

        public void ResetStage()
        {
            if (alan != null)
            {
                alan.localPosition = alanPosition;
                alan.localRotation = alanRotation;
                alan.gameObject.SetActive(alanWasActive);
            }
            if (stage != null) Destroy(stage);
            if (katana != null) Destroy(katana);
            if (dark != null) Destroy(dark);
            if (green != null) Destroy(green);
            if (wood != null) Destroy(wood);
            if (pale != null) Destroy(pale);
            stage = null;
            agreedDelivery = null;
            katana = null;
            swingTimer = 0;
            currentEnding = EscortEnding.None;
        }

        public void RefreshStage(StationStory story)
        {
            if (story.Ending == EscortEnding.None) return;
            if (currentEnding != story.Ending)
            {
                ResetStage();
                currentEnding = story.Ending;
                CreateStage(story.Ending);
            }
            if (agreedDelivery != null)
                agreedDelivery.SetActive(story.RepairAgreement);
        }

        private void CreateStage(EscortEnding ending)
        {
            stage = new GameObject("回站后的变化");
            stage.transform.SetParent(station, false);
            Shader shader = Shader.Find("Standard");
            dark = Material(shader, new Color(.18f, .22f, .22f));
            green = Material(shader, new Color(.28f, .43f, .35f));
            wood = Material(shader, new Color(.43f, .31f, .2f));
            pale = Material(shader, new Color(.8f, .78f, .65f));

            CreateShelter();
            if (ending == EscortEnding.PaidEscort)
            {
                alan.localPosition = new Vector3(-2.5f, 0, 44.0f);
                ApplyPose(alan, layout != null ? layout.alanAtDispatch : null);
                alan.gameObject.SetActive(true);
                Transform fare = Label(stage.transform, "车队恢复班次 · 暖棚运费上涨", new Vector3(-.2f, 2.6f, 45.5f));
                ApplyPose(fare, layout != null ? layout.fareSign : null);
                return;
            }

            CreateDiscipline();
            if (ending == EscortEnding.AlanWithShelter)
            {
                alan.localPosition = new Vector3(11f, 0, 26f);
                alan.localRotation = Quaternion.identity;
                ApplyPose(alan, layout != null ? layout.alanAtShelter : null);
                alan.gameObject.SetActive(true);
                agreedDelivery = new GameObject("按单送达的急用零件");
                agreedDelivery.transform.SetParent(stage.transform, false);
                agreedDelivery.transform.localPosition = new Vector3(12f, .3f, 27f);
                ApplyPose(agreedDelivery.transform, layout != null ? layout.supplyCrate : null);
                Box(agreedDelivery.transform, "零件箱", Vector3.zero, new Vector3(.65f, .6f, .55f), wood);
                agreedDelivery.SetActive(false);
            }
            else
            {
                alan.gameObject.SetActive(false);
                Transform empty = Label(stage.transform, "阿岚的工位", new Vector3(11f, 1.65f, 26.3f));
                ApplyPose(empty, layout != null ? layout.alanEmptyDeskSign : null);
            }
            CreateKatana();
        }

        public void Tick()
        {
            if (katana == null) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) katana.SetActive(!katana.activeSelf);
            if (katana.activeSelf && Input.GetMouseButtonDown(0) && swingTimer <= 0) swingTimer = .25f;
            swingTimer = Mathf.Max(0, swingTimer - Time.deltaTime);
            katana.transform.localRotation = Quaternion.Euler(
                15 + (swingTimer > 0 ? -45 * Mathf.Sin(swingTimer / .25f * Mathf.PI) : 0), 15, -20);
        }

        private void CreateKatana()
        {
            katana = new GameObject("武士刀");
            katana.transform.SetParent(walker.ViewCamera.transform, false);
            katana.transform.localPosition = new Vector3(.42f, -.36f, .7f);
            visuals.CreateWeapon(katana.transform, true);
            katana.SetActive(true);
        }

        private void CreateShelter()
        {
            var shelter = new GameObject("暖棚领班");
            shelter.transform.SetParent(stage.transform, false);
            shelter.transform.localPosition = new Vector3(8.6f, 0, 25.5f);
            ApplyPose(shelter.transform, layout != null ? layout.shelterLeader : null);
            var collider = shelter.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 1, 0);
            collider.radius = .4f;
            collider.height = 1.7f;
            collider.isTrigger = true;
            shelter.AddComponent<StationInteractable>().Configure(StationTarget.ShelterLeader, "暖棚领班");
            visuals.CreateShelterLeader(shelter.transform);
            Transform marker = layout != null ? layout.shelterSign : null;
            // 场景布局中的摆放点被删除时，对应的告示也不再生成。
            // 如果完全没有布局资源，保留原有的默认告示作为兼容。
            if (layout == null || marker != null)
            {
                EpilogueSignText signSettings = marker != null ? marker.GetComponent<EpilogueSignText>() : null;
                string signText = signSettings != null ? signSettings.text : "暖棚入口";
                Transform sign = Label(stage.transform, signText, new Vector3(9f, 2.35f, 27f));
                sign.name = "暖棚入口";
                ApplyPose(sign, marker);
            }
            Transform bench = Box(stage.transform, "暖棚工作台", new Vector3(11f, .48f, 27f), new Vector3(2.1f, .9f, .75f), wood);
            ApplyPose(bench, layout != null ? layout.shelterWorkbench : null);
        }

        private void CreateDiscipline()
        {
            var notice = GameObject.CreatePrimitive(PrimitiveType.Cube);
            notice.name = "路哨纪律簿";
            notice.transform.SetParent(stage.transform, false);
            notice.transform.localPosition = new Vector3(-4f, 1.3f, 43.5f);
            notice.transform.localScale = new Vector3(.55f, .7f, .18f);
            ApplyPose(notice.transform, layout != null ? layout.disciplineNotice : null);
            notice.GetComponent<Renderer>().sharedMaterial = pale;
            notice.AddComponent<StationInteractable>().Configure(StationTarget.DisciplineNotice, "查看路哨纪律簿");
            Transform label = Label(stage.transform, "纪律簿", new Vector3(-4f, 1.85f, 43.35f));
            ApplyPose(label, layout != null ? layout.disciplineLabel : null);
            visuals.CreateGuard(stage.transform, new Vector3(-3.2f, 0, 46f), false, true);
            ApplyPose(stage.transform.Find("接送者·司机"), layout != null ? layout.punishedDriver : null);
            visuals.CreateGuard(stage.transform, new Vector3(-4.5f, 0, 46f), true, true);
            ApplyPose(stage.transform.Find("接送者·侦察员"), layout != null ? layout.punishedScout : null);
        }

        private static void ApplyPose(Transform target, Transform marker)
        {
            if (target != null && marker != null) target.SetPositionAndRotation(marker.position, marker.rotation);
        }

        private Transform Label(Transform parent, string value, Vector3 position)
        {
            var obj = new GameObject(value);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            TextMesh text = obj.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.characterSize = .065f;
            text.fontSize = 54;
            text.color = new Color(.95f, .89f, .7f);
            text.font = font;
            text.GetComponent<MeshRenderer>().sharedMaterial = signMaterial != null ? signMaterial : font.material;
            return obj.transform;
        }

        private static Material Material(Shader shader, Color color)
        {
            var material = new Material(shader);
            material.color = color;
            material.SetFloat("_Glossiness", .1f);
            return material;
        }

        private static Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
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
