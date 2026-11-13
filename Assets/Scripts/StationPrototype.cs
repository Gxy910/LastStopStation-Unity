using UnityEngine;

namespace YuDian
{
    public sealed class StationPrototype : MonoBehaviour
    {
        //serializeField告诉unity可以把这个字段显示在inspector里并保存它的配置
        [SerializeField] private FirstPersonWalker walker;
        [SerializeField] private GameObject station;
        [SerializeField] private GameObject pickupYard;
        [SerializeField] private Transform stationSpawn;
        [SerializeField] private Transform nextDaySpawn;
        [SerializeField] private Transform pickupSpawn;
        [SerializeField] private GameObject toolBag;
        [SerializeField] private GameObject repairedLamp;
        [SerializeField] private int startingCoins = 3;
        [SerializeField] private float captionDuration = 2.65f;

        private readonly Color panelColor = new Color(0.035f, 0.052f, 0.05f, 0.92f);
        private readonly Color textColor = new Color(0.95f, 0.94f, 0.88f);
        private readonly Color accentColor = new Color(0.91f, 0.68f, 0.34f);
        private readonly Color quietColor = new Color(0.67f, 0.74f, 0.74f);

        private StationStory story;
        private StationInteractable focused;
        private InteractiveDoor focusedDoor;
        private AmbientNpc focusedAmbientNpc;
        private StationDialogue dialogue;
        private int dialogueIndex;
        private Font chineseFont;
        private Font noticeFont;
        private Material signMaterial;
        private Material noticeMaterial;
        private GUIStyle normal;
        private GUIStyle small;
        private GUIStyle large;
        private GUIStyle centered;
        private GUIStyle title;
        private float captionEndsAt;
        private bool paused;
        private EscortEncounter escort;
        private EpilogueStage epilogue;
        private PrototypeVisuals visuals;
        private DialogueVoice dialogueVoice;
        private NpcDialogueGaze speakingGaze;

        private bool CaptionIsVisible => captionEndsAt > 0f;
        private bool Complete => story.CurrentStep == StationStory.Step.MilestoneComplete;

        public void Configure(FirstPersonWalker player, GameObject stationRoot, GameObject yardRoot,
            Transform firstSpawn, Transform dayTwoSpawn, Transform yardSpawn, GameObject tools, GameObject lamp)
        {
            walker = player;
            station = stationRoot;
            pickupYard = yardRoot;
            stationSpawn = firstSpawn;
            nextDaySpawn = dayTwoSpawn;
            pickupSpawn = yardSpawn;
            toolBag = tools;
            repairedLamp = lamp;
        }

        private void Awake()
        {
            chineseFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun" }, 26);
            noticeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun" }, 12);
            Shader signShader = Resources.Load<Shader>("WorldText");
            if (signShader != null)
            {
                signMaterial = new Material(signShader);
                signMaterial.mainTexture = chineseFont.material.mainTexture;
                noticeMaterial = new Material(signShader);
                noticeMaterial.mainTexture = noticeFont.material.mainTexture;
            }
            if (noticeFont.material.mainTexture != null) noticeFont.material.mainTexture.filterMode = FilterMode.Point;
            ApplySignFont(station);
            ApplySignFont(pickupYard);
            visuals = gameObject.AddComponent<PrototypeVisuals>();
            visuals.Initialize(station.transform);
            dialogueVoice = gameObject.AddComponent<DialogueVoice>();
            escort = gameObject.AddComponent<EscortEncounter>();
            escort.Configure(walker, pickupYard.transform, chineseFont, visuals, OnEscortFinished, OnEnemyDefeated, dialogueVoice);
            epilogue = gameObject.AddComponent<EpilogueStage>();
            epilogue.Configure(station.transform, walker, chineseFont, visuals, signMaterial);
        }

        private void Start()
        {
            Restart();
        }

        private void ApplySignFont(GameObject root)
        {
            foreach (TextMesh sign in root.GetComponentsInChildren<TextMesh>(true))
            {
                bool pixelNotice = sign.name.EndsWith("褪色小字");
                sign.font = pixelNotice ? noticeFont : chineseFont;
                if (pixelNotice) { sign.fontSize = 12; sign.characterSize *= 1.5f; }
                sign.GetComponent<MeshRenderer>().sharedMaterial = pixelNotice
                    ? (noticeMaterial != null ? noticeMaterial : noticeFont.material)
                    : (signMaterial != null ? signMaterial : chineseFont.material);
            }
        }

        private void Restart()
        {
            StopDialoguePresentation();
            escort.ResetEncounter();
            epilogue.ResetStage();
            story = new StationStory(startingCoins);
            paused = false;
            dialogue = null;
            BeginChapter();
        }

        private void BeginChapter()
        {
            StopDialoguePresentation();
            bool atPickup = story.Chapter == 3;
            station.SetActive(!atPickup);
            pickupYard.SetActive(atPickup);

            Transform spawn = atPickup ? pickupSpawn :
                story.Chapter == 2 || story.Chapter == 4 ? nextDaySpawn : stationSpawn;
            /*
            这个嵌套写法可以理解为
            Transform spawn;

            if (atPickup)
            {
                spawn = pickupSpawn;
            }
            else if (story.Chapter == 2)
            {
                spawn = nextDaySpawn;
            }
            else
            {
                spawn = stationSpawn;
            }

            walker.MoveTo(spawn);
            */
            walker.MoveTo(spawn);
            RefreshWorld();
            Physics.SyncTransforms();
            focused = null;
            focusedDoor = null;
            focusedAmbientNpc = null;
            captionEndsAt = Time.unscaledTime + captionDuration;
            RefreshControls();
        }

        private void RefreshWorld()
        {
            toolBag.SetActive(story.ToolsAvailable);
            repairedLamp.SetActive(story.LampDelivered);
            epilogue.RefreshStage(story);
        }

        private void Update()
        {
            if (CaptionIsVisible)
            {
                if (Time.unscaledTime >= captionEndsAt || Input.GetKeyDown(KeyCode.Space))
                {
                    captionEndsAt = 0f;
                    RefreshControls();
                }
                return;
            }

            if (paused)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
                {
                    paused = false;
                    if (dialogue != null) PresentDialogueLine();
                    RefreshControls();
                }
                else if (Input.GetKeyDown(KeyCode.Backspace))
                    Restart();
                else if (Input.GetKeyDown(KeyCode.Q))
                    Application.Quit();
                return;
            }

            if (dialogue != null)
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                    NextDialogueLine();
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    paused = true;
                    StopDialoguePresentation();
                    RefreshControls();
                }
                return;
            }

            if (Complete)
            {
                if (Input.GetKeyDown(KeyCode.Backspace))
                    Restart();
                else if (Input.GetKeyDown(KeyCode.Escape))
                    Application.Quit();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                paused = true;
                if (escort != null) escort.SuspendSpeech();
                RefreshControls();
                return;
            }

            if (escort.IsRunning)
            {
                focused = null;
                focusedDoor = null;
                focusedAmbientNpc = null;
                escort.Tick(story, Time.deltaTime);
                RefreshControls();
                return;
            }

            epilogue.Tick();
            UpdateFocus();
            if ((focused != null || focusedDoor != null || focusedAmbientNpc != null) && Input.GetKeyDown(KeyCode.E))
                InteractFocused();
            else if (Input.GetKeyDown(KeyCode.R))
                ShowDialogue(story.CallDispatch());
        }

        private void InteractFocused()
        {
            if (focusedAmbientNpc != null)
            {
                ShowDialogue(focusedAmbientNpc.Talk());
                return;
            }
            if (focusedDoor != null)
            {
                if (!focusedDoor.TryToggle(walker))
                    ShowDialogue(new StationDialogue("门", "门的转动范围里有人，请后退一点再开关。"));
                return;
            }
            if (focused == null) return;
            int previousChapter = story.Chapter;
            StationDialogue reply = story.Interact(focused.Target);
            if (previousChapter != story.Chapter)
                BeginChapter();
            else
            {
                RefreshWorld();
                ShowDialogue(reply);
            }
        }

        private void UpdateFocus()
        {
            Transform view = walker.ViewCamera.transform;
            // 相机低头时射线可能先碰到主角自己的控制器；跳过主角，再选最近的场景物体。
            RaycastHit hit = default;
            bool hasHit = false;
            foreach (RaycastHit candidate in Physics.RaycastAll(view.position, view.forward, 3.4f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                if (candidate.collider.transform.IsChildOf(walker.transform)) continue;
                if (!hasHit || candidate.distance < hit.distance) { hit = candidate; hasHit = true; }
            }
            focused = hasHit ? hit.collider.GetComponentInParent<StationInteractable>() : null;
            focusedDoor = hasHit ? hit.collider.GetComponentInParent<InteractiveDoor>() : null;
            focusedAmbientNpc = hasHit ? hit.collider.GetComponentInParent<AmbientNpc>() : null;
        }

        private void ShowDialogue(StationDialogue reply)
        {
            StopDialoguePresentation();
            dialogue = reply;
            dialogueIndex = 0;
            if (dialogue != null && dialogue.Lines != null && dialogue.Lines.Length > 0) PresentDialogueLine();
            RefreshControls();
        }

        private void NextDialogueLine()
        {
            StopDialoguePresentation();
            dialogueIndex++;
            if (dialogueIndex >= dialogue.Lines.Length)
            {
                dialogue = null;
                if (story.CurrentStep == StationStory.Step.Encounter && !escort.IsRunning)
                    escort.Begin();
            }
            else PresentDialogueLine();
            RefreshControls();
        }

        private void PresentDialogueLine()
        {
            string speaker = dialogue.SpeakerAt(dialogueIndex);
            Transform actor = FindSpeaker(speaker);
            if (actor != null)
            {
                speakingGaze = actor.GetComponent<NpcDialogueGaze>();
                if (speakingGaze == null) speakingGaze = actor.gameObject.AddComponent<NpcDialogueGaze>();
                speakingGaze.Focus(walker.transform);
            }
            bool serious = story != null && story.Chapter == 4 && story.Ending == EscortEnding.AlanLost;
            if (dialogueVoice != null) dialogueVoice.Speak(speaker, dialogue.Lines[dialogueIndex], serious);
        }

        private Transform FindSpeaker(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (GameObject root in new[] { station, pickupYard })
            {
                if (root == null || !root.activeInHierarchy) continue;
                foreach (Transform actor in root.GetComponentsInChildren<Transform>(true))
                    if (actor.name == name && actor.gameObject.activeInHierarchy &&
                        (actor.GetComponent<StationInteractable>() != null || actor.GetComponent<AmbientNpc>() != null))
                        return actor;
            }
            return null;
        }

        private void StopDialoguePresentation()
        {
            if (speakingGaze != null) speakingGaze.Release();
            speakingGaze = null;
            if (dialogueVoice != null) dialogueVoice.StopVoice();
        }

        private void OnDisable() { StopDialoguePresentation(); if (escort != null) escort.SuspendSpeech(); }

        private void OnEscortFinished(EscortEnding ending)
        {
            story.FinishEscort(ending);
            BeginChapter();
        }

        private bool OnEnemyDefeated() => story != null && story.ReportEnemyDefeated();

        private void RefreshControls()
        {
            walker.InputEnabled = !CaptionIsVisible && !paused && dialogue == null && !Complete &&
                (!escort.IsRunning || !escort.LocksPlayer);
            bool releaseCursor = paused || (Complete && dialogue == null);
            Cursor.lockState = releaseCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = releaseCursor;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || story == null)
                return;

            if (normal == null)
            {
                normal = MakeStyle(22, TextAnchor.MiddleLeft, true);
                small = MakeStyle(16, TextAnchor.MiddleLeft, false);
                large = MakeStyle(32, TextAnchor.MiddleLeft, false);
                centered = MakeStyle(27, TextAnchor.MiddleCenter, false);
                title = MakeStyle(38, TextAnchor.MiddleCenter, false);
            }

            if (CaptionIsVisible || paused || (Complete && dialogue == null))
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.025f, 0.039f, 0.046f));

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector3 offset = new Vector3((Screen.width - 1280f * scale) * 0.5f,
                (Screen.height - 720f * scale) * 0.5f, 0f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (CaptionIsVisible)
                Label(new Rect(35, 312, 1210, 96), story.ChapterCaption, centered, textColor);
            else if (paused)
                DrawMenu("暂停", "Enter / Esc 继续    Backspace 重玩    Q 退出");
            else if (dialogue != null)
            {
                DrawHud();
                DrawDialogue();
            }
            else if (Complete)
                DrawEnding();
            else
            {
                DrawHud();
                DrawInteraction();
                if (escort.IsRunning)
                    escort.DrawOverlay(normal, small, centered, panelColor, textColor, accentColor);
            }

            GUI.matrix = previous;
        }

        private GUIStyle MakeStyle(int size, TextAnchor anchor, bool wrap)
        {
            return new GUIStyle
            {
                font = chineseFont,
                fontSize = size,
                alignment = anchor,
                wordWrap = wrap
            };
        }

        private void DrawHud()
        {
            Fill(new Rect(28, 25, 565, 103), panelColor);
            Fill(new Rect(28, 25, 3, 103), accentColor);
            Fill(new Rect(47, 62, 525, 1), new Color(.42f, .40f, .31f, .8f));
            Label(new Rect(47, 37, 528, 24), story.ChapterName, small, accentColor);
            Label(new Rect(47, 65, 528, 45), escort.IsRunning ? escort.Objective : story.Objective, normal, textColor);

            Fill(new Rect(985, 25, 267, 96), panelColor);
            Fill(new Rect(985, 25, 267, 2), accentColor);
            Label(new Rect(1004, 36, 227, 30), $"个人酒币  {story.PersonalCoins} 枚", normal, textColor);
            string status = story.CarriedItem;
            if (string.IsNullOrEmpty(status))
                status = story.DeviceRepaired ? "通讯装置 · 已修好" : "通讯装置 · 故障";
            Label(new Rect(1004, 72, 227, 26), status, small, quietColor);
        }

        private void DrawInteraction()
        {
            Fill(new Rect(637, 359, 6, 2), textColor);
            Fill(new Rect(639, 357, 2, 6), textColor);
            if (focused != null || focusedDoor != null || focusedAmbientNpc != null)
            {
                Fill(new Rect(460, 393, 360, 46), panelColor);
                string label = focusedAmbientNpc != null ? focusedAmbientNpc.DisplayName :
                    focusedDoor != null ? focusedDoor.DisplayName : focused.DisplayName;
                Label(new Rect(476, 396, 328, 40), $"E  ·  {label}", centered, textColor);
            }

            Fill(new Rect(28, 665, 595, 31), panelColor);
            Label(new Rect(40, 665, 570, 31),
                story.HasKatana && story.Chapter == 4
                    ? "WASD 移动 · Shift 奔跑 · E 交互 · 1 收刀 · 左键挥刀"
                    : "WASD 移动 · Shift 奔跑 · 鼠标转向 · E 交互 · R 通讯 · Esc 暂停", small, quietColor);
        }

        private void DrawDialogue()
        {
            Fill(new Rect(136, 508, 1008, 164), panelColor);
            Fill(new Rect(136, 508, 1008, 2), accentColor);
            Fill(new Rect(136, 508, 4, 164), accentColor);
            Label(new Rect(165, 521, 949, 37), dialogue.SpeakerAt(dialogueIndex), large, accentColor);
            Label(new Rect(165, 563, 949, 50), dialogue.Lines[dialogueIndex], normal, textColor);
            Label(new Rect(165, 624, 949, 28), $"E / Enter  继续    {dialogueIndex + 1}/{dialogue.Lines.Length}",
                small, quietColor);
        }

        private void DrawMenu(string heading, string instructions)
        {
            Label(new Rect(100, 271, 1080, 65), heading, title, accentColor);
            Label(new Rect(60, 349, 1160, 61), instructions, centered, textColor);
        }

        private void DrawEnding()
        {
            Label(new Rect(100, 80, 1080, 65), "旅程暂告一段落", title, accentColor);
            string[] lines = story.EndingLines();
            for (int i = 0; i < lines.Length; i++)
                Label(new Rect(170, 165 + i * 65, 940, 58), lines[i], normal, textColor);
            Label(new Rect(60, 625, 1160, 50), "Backspace 重玩    ·    Esc 退出", centered, quietColor);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Label(Rect rect, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, value, style);
        }
    }
}
