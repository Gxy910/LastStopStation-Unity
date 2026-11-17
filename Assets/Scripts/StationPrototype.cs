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
        private float timeScaleBeforePause;
        private bool audioPausedBeforePause;
        private enum PausePrompt { None, Restart, Quit }
        private PausePrompt pausePrompt;
        private GUIStyle settingsButton;
        private bool openingIntro;
        private const int OpeningPageCount = 2;
        private int openingPage;
        private EscortEncounter escort;
        private PatrolMission patrol;
        private bool focusedPosting;
        private EpilogueStage epilogue;
        private PrototypeVisuals visuals;
        private DialogueVoice dialogueVoice;
        private GameAudio gameAudio;
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
            gameAudio = gameObject.AddComponent<GameAudio>();
            gameAudio.Configure(walker);
            gameAudio.RouteVolume(dialogueVoice.GetComponent<AudioSource>(), GameAudio.VolumeChannel.Dialogue);
            escort = gameObject.AddComponent<EscortEncounter>();
            escort.Configure(walker, pickupYard.transform, chineseFont, visuals, OnEscortFinished, OnEnemyDefeated, dialogueVoice);
            patrol = new PatrolMission(walker, station, chineseFont, signMaterial, visuals, gameAudio);
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
            patrol.Reset();
            epilogue.ResetStage();
            story = new StationStory(startingCoins);
            dialogue = null;
            openingIntro = true;
            openingPage = 0;
            BeginChapter();
        }

        private void BeginChapter()
        {
            StopDialoguePresentation();
            bool atPickup = story.Chapter == 3;
            station.SetActive(!atPickup);
            pickupYard.SetActive(atPickup);

            Transform spawn = atPickup ? pickupSpawn :
                story.Chapter == 4 && epilogue.ReturnSpawn != null ? epilogue.ReturnSpawn :
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
            focusedPosting = false;
            captionEndsAt = Time.time + captionDuration;
            if (story.Chapter > 1) gameAudio.PlayUi(GameAudio.UiCue.Subtitle);
            RefreshControls();
        }

        private void AdvanceOpening(bool skip)
        {
            if (!skip && openingPage < OpeningPageCount - 1)
            {
                openingPage++;
                return;
            }

            openingIntro = false;
            captionEndsAt = Time.time + captionDuration;
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
            // The plain C# story and patrol objects cannot be restored by a Play Mode script reload.
            if (story == null || patrol == null)
            {
                enabled = false;
#if UNITY_EDITOR
                Debug.LogWarning("车站运行状态已丢失，Unity 已结束本次试玩。请重新点击 Play。", this);
                EditorHooks.ExitPlay();
#else
                Debug.LogError("车站运行状态未初始化，已停止车站更新。", this);
#endif
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (pausePrompt != PausePrompt.None) pausePrompt = PausePrompt.None;
                else SetPaused(!paused);
                return;
            }

            if (paused)
            {
                if (Input.GetKeyDown(KeyCode.Backspace)) pausePrompt = PausePrompt.Restart;
                else if (Input.GetKeyDown(KeyCode.Q)) pausePrompt = PausePrompt.Quit;
                else if (pausePrompt == PausePrompt.None && Input.GetKeyDown(KeyCode.Return)) SetPaused(false);
                return;
            }

            if (openingIntro)
            {
                if (Input.GetKeyDown(KeyCode.Return)) AdvanceOpening(true);
                else if (Input.GetKeyDown(KeyCode.Space)) AdvanceOpening(false);
                return;
            }
            if (CaptionIsVisible)
            {
                if (Time.time >= captionEndsAt || Input.GetKeyDown(KeyCode.Space))
                {
                    captionEndsAt = 0f;
                    RefreshControls();
                }
                return;
            }

            if (dialogue != null)
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                    NextDialogueLine();
                return;
            }

            if (Complete)
            {
                if (Input.GetKeyDown(KeyCode.Backspace)) Restart();
                return;
            }

            if (patrol.IsRunning)
            {
                patrol.Tick(Time.deltaTime);
                if (!patrol.IsRunning) epilogue.ResumeKatanaAfterPatrol();
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
            if ((focusedPosting || focused != null || focusedDoor != null || focusedAmbientNpc != null) && Input.GetKeyDown(KeyCode.E))
                InteractFocused();
            else if (Input.GetKeyDown(KeyCode.R))
                ShowDialogue(story.CallDispatch());
        }

        private void InteractFocused()
        {
            if (focusedPosting)
            {
                if (!patrol.Begin(story)) { gameAudio.Play(GameAudio.Cue.Denied); ShowDialogue(new StationDialogue("清障值勤板", patrol.PostingLabel(story))); }
                else
                {
                    focusedPosting = false;
                    focused = null;
                    focusedDoor = null;
                    focusedAmbientNpc = null;
                    epilogue.SuspendKatanaForPatrol();
                    gameAudio.PlayUi(GameAudio.UiCue.Subtitle);
                    RefreshControls();
                }
                return;
            }
            if (focusedAmbientNpc != null)
            {
                gameAudio.Play(GameAudio.Cue.Interact);
                ShowDialogue(focusedAmbientNpc.Talk());
                return;
            }
            if (focusedDoor != null)
            {
                if (!focusedDoor.TryToggle(walker))
                { gameAudio.Play(GameAudio.Cue.Denied); ShowDialogue(new StationDialogue("门", "门的转动范围里有人，请后退一点再开关。")); }
                else gameAudio.Play(GameAudio.Cue.Door);
                return;
            }
            if (focused == null) return;
            int previousChapter = story.Chapter;
            gameAudio.Play(GameAudio.Cue.Interact);
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
            focusedPosting = hasHit && patrol.IsPosting(hit.collider);
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

        private void OnDisable()
        {
            if (paused)
            {
                paused = false;
                RestorePausedWorld();
            }
            StopDialoguePresentation();
            if (escort != null) escort.SuspendSpeech();
        }

        private void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            pausePrompt = PausePrompt.None;
            if (paused)
            {
                timeScaleBeforePause = Time.timeScale;
                audioPausedBeforePause = AudioListener.pause;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            else RestorePausedWorld();
            gameAudio.PlayUi(paused ? GameAudio.UiCue.MenuOpen : GameAudio.UiCue.MenuClose);
            RefreshControls();
        }

        private void RestorePausedWorld()
        {
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = audioPausedBeforePause;
            if (gameAudio != null) gameAudio.SaveVolumes();
        }

        private void ConfirmPauseAction()
        {
            if (pausePrompt == PausePrompt.Restart)
            {
                SetPaused(false);
                Restart();
            }
            else if (pausePrompt == PausePrompt.Quit)
            {
                gameAudio.SaveVolumes();
#if UNITY_EDITOR
                EditorHooks.ExitPlay();
#else
                Application.Quit();
#endif
            }
        }
        private void OnDestroy() { if (patrol != null) patrol.Dispose(); }

        private void OnEscortFinished(EscortEnding ending)
        {
            story.FinishEscort(ending);
            BeginChapter();
        }

        private bool OnEnemyDefeated() => story != null && story.ReportEnemyDefeated();

        private void RefreshControls()
        {
            walker.InputEnabled = !openingIntro && !CaptionIsVisible && !paused && dialogue == null && !Complete &&
                (!escort.IsRunning || !escort.LocksPlayer) && (patrol == null || !patrol.LocksPlayer);
            bool releaseCursor = paused || (Complete && dialogue == null);
            Cursor.lockState = releaseCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = releaseCursor;
        }

        private void OnGUI()
        {
            if (story == null || (!paused && Event.current.type != EventType.Repaint))
                return;

            if (normal == null)
            {
                normal = MakeStyle(22, TextAnchor.MiddleLeft, true);
                small = MakeStyle(16, TextAnchor.MiddleLeft, false);
                large = MakeStyle(32, TextAnchor.MiddleLeft, false);
                centered = MakeStyle(27, TextAnchor.MiddleCenter, false);
                title = MakeStyle(38, TextAnchor.MiddleCenter, false);
            }

            if (paused)
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.015f, .025f, .031f, .76f));
            else if (openingIntro || CaptionIsVisible || (Complete && dialogue == null))
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.025f, .039f, .046f));

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector3 offset = new Vector3((Screen.width - 1280f * scale) * 0.5f,
                (Screen.height - 720f * scale) * 0.5f, 0f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (paused)
                DrawPauseSettings();
            else if (openingIntro)
                DrawOpeningIntro();
            else if (CaptionIsVisible)
                Label(new Rect(35, 312, 1210, 96), story.ChapterCaption, centered, textColor);
            else if (dialogue != null)
            {
                DrawHud();
                DrawDialogue();
            }
            else if (Complete)
                DrawEnding();
            else
            {
                if (patrol.IsRunning) patrol.Draw(normal, small, centered, panelColor, textColor, accentColor);
                else
                {
                    DrawHud();
                    DrawInteraction();
                    patrol.Draw(normal, small, centered, panelColor, textColor, accentColor);
                    if (escort.IsRunning)
                        escort.DrawOverlay(normal, small, centered, panelColor, textColor, accentColor);
                }
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

        private void DrawOpeningIntro()
        {
            bool atStation = openingPage == 0;
            Label(new Rect(100, 64, 1080, 78), "末日车站", title, accentColor);
            YudianHud.Frame(new Rect(212, 156, 856, 410), accentColor);
            Label(new Rect(262, 178, 720, 55), atStation ? "灾后三年 · 旧客运站" : "第一天 · 旧票房的修理工", large, accentColor);
            Fill(new Rect(263, 245, 753, 1), YudianHud.Line);

            Label(new Rect(262, 263, 753, 70), atStation
                ? "灾难过去三年，旧客运站成了幸存者的落脚点。站台上还有人摆摊，收车票的旧票房现在用来修理设备。"
                : "你的通讯装置摔坏了。离开车站前，你得先恢复通话，才能联系到岗亭，问清楚怎么出站。", normal, textColor);
            Label(new Rect(262, 342, 753, 70), atStation
                ? "站外还有感染者。路哨的车队负责接送和运货，也会招人去旧货场清障，好让人和物资能继续往来。"
                : "修理工阿岚就在旧票房。他熟悉站里的设备，先去找他，看看修好这台装置还缺什么。", normal, textColor);
            Label(new Rect(262, 421, 753, 70), atStation
                ? "暖棚也给留下来的人提供落脚和工作的地方。他们有东西需要修，急用的零件还得等路哨车队送到。"
                : "接通通讯后，你可以问路哨怎么出站，也可以在岗亭接一趟货场清障。接下来往哪儿走，由你决定。", normal, textColor);

            Label(new Rect(262, 515, 500, 30), atStation ? "Space  下一页    ·    Enter  跳过" : "Space  开始    ·    Enter  跳过", small, quietColor);
            Label(new Rect(967, 515, 51, 30), (openingPage + 1) + " / " + OpeningPageCount, small, quietColor);
        }

        private void DrawHud()
        {
            bool showPatrolOption = story.CanTakePatrol && (story.CurrentStep == StationStory.Step.ContinueToNextDay || story.Chapter == 4);
            YudianHud.Frame(new Rect(28, 25, 560, showPatrolOption ? 132 : 106), accentColor);
            Label(new Rect(47, 31, 500, 28), "末日车站  /  " + story.ChapterName, small, accentColor);
            Fill(new Rect(47, 63, 510, 1), YudianHud.Line);
            Label(new Rect(47, 70, 520, 44), escort.IsRunning ? escort.Objective : story.Objective, normal, textColor);
            if (showPatrolOption)
            {
                string patrolHint = story.HasKatana
                    ? "可选 · 去岗亭接清障，武士刀可带入货场" : "可选 · 路哨岗亭接清障（无尽模式）";
                Label(new Rect(47, 114, 520, 29), patrolHint, small, accentColor);
            }

            YudianHud.Frame(new Rect(988, 25, 264, 95), accentColor);
            Label(new Rect(1005, 32, 228, 29), $"随身酒币  ·  {story.PersonalCoins} 枚", normal, textColor);
            string status = story.CarriedItem;
            if (string.IsNullOrEmpty(status)) status = story.DeviceRepaired ? "通讯装置  /  已修好" : "通讯装置  /  故障";
            Label(new Rect(1005, 69, 228, 26), status, small, quietColor);
        }

        private void DrawInteraction()
        {
            Fill(new Rect(637, 359, 6, 2), textColor);
            Fill(new Rect(639, 357, 2, 6), textColor);
            if (focusedPosting || focused != null || focusedDoor != null || focusedAmbientNpc != null)
            {
                YudianHud.Frame(new Rect(385, 395, 510, 48), accentColor);
                string label = focusedPosting ? patrol.PostingLabel(story) : focusedAmbientNpc != null ? focusedAmbientNpc.DisplayName :
                    focusedDoor != null ? focusedDoor.DisplayName : focused.DisplayName;
                Label(new Rect(405, 399, 470, 40), $"E   /   {label}", normal, textColor);
            }
            YudianHud.Frame(new Rect(28, 665, 640, 31), new Color(.48f, .57f, .48f));
            Label(new Rect(40, 665, 618, 31),
                story.HasKatana && story.Chapter == 4
                    ? "WASD 移动 · Shift 奔跑 · E 交互 · 1 收刀 · 左键挥刀"
                    : "WASD 移动 · Shift 奔跑 · 鼠标转向 · E 交互 · R 通讯 · Esc 设置", small, quietColor);
        }

        private void DrawDialogue()
        {
            YudianHud.Frame(new Rect(136, 507, 1008, 165), accentColor);
            Label(new Rect(163, 516, 905, 40), dialogue.SpeakerAt(dialogueIndex), large, accentColor);
            Fill(new Rect(165, 561, 950, 1), YudianHud.Line);
            Label(new Rect(165, 570, 949, 49), dialogue.Lines[dialogueIndex], normal, textColor);
            Label(new Rect(165, 626, 949, 27), $"E / Enter  继续       {dialogueIndex + 1} / {dialogue.Lines.Length}", small, quietColor);
        }

        private void DrawPauseSettings()
        {
            if (settingsButton == null)
            {
                settingsButton = new GUIStyle(GUI.skin.button) { font = chineseFont, fontSize = 20, fixedHeight = 0 };
            }

            if (pausePrompt != PausePrompt.None)
            {
                DrawPauseConfirmation();
                return;
            }

            YudianHud.Frame(new Rect(284, 94, 712, 532), YudianHud.Brass);
            Label(new Rect(327, 115, 615, 48), "暂停设置", large, accentColor);
            Label(new Rect(327, 168, 615, 32), "音量", normal, textColor);
            Fill(new Rect(328, 207, 624, 1), YudianHud.Line);

            DrawVolumeRow(224, "总音量", GameAudio.VolumeChannel.Master);
            DrawVolumeRow(269, "开枪", GameAudio.VolumeChannel.Gunfire);
            DrawVolumeRow(314, "脚步", GameAudio.VolumeChannel.Footsteps);
            DrawVolumeRow(359, "环境", GameAudio.VolumeChannel.Ambience);
            DrawVolumeRow(404, "对话", GameAudio.VolumeChannel.Dialogue);
            DrawVolumeRow(449, "交互和提示", GameAudio.VolumeChannel.Effects);
            Fill(new Rect(328, 504, 624, 1), YudianHud.Line);

            if (GUI.Button(new Rect(328, 526, 200, 43), "继续游戏", settingsButton)) SetPaused(false);
            if (GUI.Button(new Rect(540, 526, 200, 43), "重新开始", settingsButton)) pausePrompt = PausePrompt.Restart;
            if (GUI.Button(new Rect(752, 526, 200, 43), "退出游戏", settingsButton)) pausePrompt = PausePrompt.Quit;
            Label(new Rect(328, 584, 624, 22), "Esc 继续  ·  音量设置自动保存", small, quietColor);
        }

        private void DrawVolumeRow(float y, string label, GameAudio.VolumeChannel channel)
        {
            float value = gameAudio.GetVolume(channel);
            Label(new Rect(328, y, 175, 37), label, normal, textColor);
            float next = GUI.HorizontalSlider(new Rect(510, y + 13, 360, 20), value, 0f, 1f);
            if (!Mathf.Approximately(next, value))
                gameAudio.SetVolume(channel, Mathf.Round(next * 100f) / 100f);
            Label(new Rect(899, y, 60, 37), Mathf.RoundToInt(gameAudio.GetVolume(channel) * 100f) + "%", small, quietColor);
        }

        private void DrawPauseConfirmation()
        {
            bool quit = pausePrompt == PausePrompt.Quit;
            YudianHud.Frame(new Rect(356, 237, 568, 251), YudianHud.Brass);
            Label(new Rect(389, 260, 490, 45), quit ? "退出游戏？" : "重新开始？", large, accentColor);
            Label(new Rect(389, 321, 494, 34), "本次游玩进度将丢失。", normal, textColor);
            if (GUI.Button(new Rect(389, 393, 233, 45), "返回设置", settingsButton)) pausePrompt = PausePrompt.None;
            if (GUI.Button(new Rect(645, 393, 245, 45), quit ? "确认退出" : "确认重玩", settingsButton)) ConfirmPauseAction();
        }

        private void DrawEnding()
        {
            Label(new Rect(100, 80, 1080, 65), "旅程暂告一段落", title, accentColor);
            string[] lines = story.EndingLines();
            for (int i = 0; i < lines.Length; i++)
                Label(new Rect(170, 165 + i * 65, 940, 58), lines[i], normal, textColor);
            Label(new Rect(60, 625, 1160, 50), "Backspace 重玩    ·    Esc 设置", centered, quietColor);
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
