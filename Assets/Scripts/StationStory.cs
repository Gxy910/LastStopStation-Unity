namespace YuDian
{
    public enum StationTarget
    {
        Alan,
        ToolBag,
        Stallholder,
        Dispatch,
        Exit,
        Pickup,
        ShelterLeader,
        DisciplineNotice
    }

    public sealed class StationDialogue
    {
        public string Speaker { get; }
        public string[] Lines { get; }
        private readonly string[] speakers;

        public StationDialogue(string speaker, params string[] lines)
        {
            Speaker = speaker;
            Lines = lines;
        }
        public StationDialogue(string[] lineSpeakers, string[] lines)
        {
            speakers = lineSpeakers;
            Lines = lines;
            Speaker = speakers != null && speakers.Length > 0 ? speakers[0] : "";
        }
        public string SpeakerAt(int index) => speakers != null && index >= 0 && index < speakers.Length ? speakers[index] : Speaker;
    }

    public sealed class StationStory
    {
        public enum Step
        {
            MeetAlan,
            CollectTools,
            ReturnTools,
            ContinueToNextDay,
            HearOffers,
            DeliverLamp,
            SignContract,
            LeaveForPickup,
            ReachPickup,
            Encounter,
            Epilogue,
            MilestoneComplete
        }

        public const int Fare = 4;
        public int Chapter { get; private set; } = 1;//初始章节为1
        public Step CurrentStep { get; private set; } = Step.MeetAlan;
        public int PersonalCoins { get; private set; }
        public bool DeviceRepaired { get; private set; }
        public bool LampDelivered { get; private set; }
        public bool ContractSigned { get; private set; }
        public int RepairFee { get; private set; }
        public EscortEnding Ending { get; private set; }
        public bool RefundClaimed { get; private set; }
        public bool RepairAgreement { get; private set; }
        public bool HasKatana { get; private set; }
        public bool EnemyDefeated { get; private set; }
        public bool ToolsAvailable => Chapter == 1 &&
            (CurrentStep == Step.MeetAlan || CurrentStep == Step.CollectTools);

        public StationStory(int personalCoins)
        {
            PersonalCoins = System.Math.Max(0, personalCoins);
        }

        public string ChapterName => Chapter == 1
            ? "第一章 · 票房里的修理台"
            : Chapter == 2 ? "第二章 · 两份邀约"
            : Chapter == 3 ? "第三章 · 接送费" : "第四章 · 归途";

        public string ChapterCaption => Chapter == 1
            ? "灾后三年 · 旧客运站 · 主角与阿岚"
            : Chapter == 2
                ? "次日清晨 · 旧客运站 · 主角、阿岚与孙姨"
                : Chapter == 3 ? "当天傍晚 · 站外接送点 · 主角、阿岚与路哨接送队"
                : Ending == EscortEnding.AlanLost
                    ? "当天夜里 · 旧客运站 · 主角与两帮负责人"
                    : "几天后 · 旧客运站 · 主角与阿岚";

        public string Objective
        {
            get
            {
                switch (CurrentStep)
                {
                    case Step.MeetAlan: return "去旧票房找修理工阿岚";
                    case Step.CollectTools: return "回站台左侧行李间找阿岚的工具包";
                    case Step.ReturnTools: return "把工具包带回旧票房";
                    case Step.ContinueToNextDay: return "去出站门，继续到第二天";
                    case Step.HearOffers: return "去旧票房，听阿岚说两份邀约";
                    case Step.DeliverLamp: return "把修好的台灯送给站台口的孙姨";
                    case Step.SignContract: return "带维修费钱袋去路哨岗亭签约";
                    case Step.LeaveForPickup: return "去出站门，前往站外接送点";
                    case Step.ReachPickup: return "去接送牌前确认会合";
                    case Step.Encounter: return "接送途中遇到危险";
                    case Step.Epilogue:
                        return Ending == EscortEnding.PaidEscort
                            ? "找路哨站长申请退回一半车费，或去出站门结束"
                            : Ending == EscortEnding.AlanWithShelter
                                ? "去暖棚入口找领班，或去出站门结束"
                                : "到岗亭和暖棚看看，或去出站门结束";
                    default: return "已完成本次原型";
                }
            }
        }

        public string CarriedItem
        {
            get
            {
                switch (CurrentStep)
                {
                    case Step.ReturnTools: return "阿岚的工具包";
                    case Step.DeliverLamp: return "修好的台灯";
                    case Step.SignContract: return "维修费钱袋 · 4 枚酒币";
                    default:
                        if (HasKatana) return "武士刀 · 战斗获得";
                        return ContractSigned ? "双人接送收据" : "";
                }
            }
        }

        public StationDialogue Interact(StationTarget target)
        {
            switch (target)
            {
                case StationTarget.Alan: return TalkToAlan();
                case StationTarget.ToolBag: return PickUpTools();
                case StationTarget.Stallholder: return TalkToStallholder();
                case StationTarget.Dispatch: return TalkToDispatch();
                case StationTarget.Exit: return UseExit();
                case StationTarget.Pickup: return ConfirmPickup();
                case StationTarget.ShelterLeader: return TalkToShelter();
                case StationTarget.DisciplineNotice: return ReadDisciplineNotice();
                default: return null;
            }
        }

        public StationDialogue CallDispatch()
        {
            if (!DeviceRepaired)
                return new StationDialogue("通讯装置", "接通了电源。听筒里只有断断续续的杂音。");

            return new StationDialogue("通讯 · 路哨值班台",
                "值班台收到。站内通道开放，站外接送请到岗亭登记。");
        }

        private StationDialogue TalkToAlan()
        {
            if (CurrentStep == Step.Epilogue)
            {
                if (Ending == EscortEnding.PaidEscort)
                    return new StationDialogue("阿岚",
                        "我知道你又付了一笔。这里零件齐，工位也在一楼，我会留下修车队的通讯设备。",
                        "路上那笔是你出的。按规退下来的钱，你收着。");
                if (Ending == EscortEnding.AlanWithShelter)
                    return new StationDialogue("阿岚",
                        "收了钱还能把我们扔下，我以后怎么敢坐他们的车？",
                        "领班给我留了张桌子，坏设备也能送到手边。我去暖棚。");
            }
            switch (CurrentStep)
            {
                case Step.MeetAlan:
                    CurrentStep = Step.CollectTools;
                    return new StationDialogue("阿岚",
                        "你那台通讯装置摔坏了？给我看看。",
                        "工具包落在站台左侧的行李间了，我今天走不过去。能帮我拿回来吗？");
                case Step.CollectTools:
                    return new StationDialogue("阿岚", "沿通道回站台，行李间在左侧。工具包放在最前头那只箱子上。");
                case Step.ReturnTools:
                    DeviceRepaired = true;
                    CurrentStep = Step.ContinueToNextDay;
                    return new StationDialogue("阿岚",
                        "是这包。你等等，我先把松掉的接头弄好。",
                        "能双向通话了。按 R 呼一下值班台，看看那边能不能收到。");
                case Step.ContinueToNextDay:
                    return new StationDialogue("阿岚", "今天的活儿差不多了。你明早来一趟，我有件事想跟你说。");
                case Step.HearOffers:
                    CurrentStep = Step.DeliverLamp;
                    return new StationDialogue("阿岚",
                        "路哨答应给我一楼的工位，工具也齐。我想先去看看。",
                        "暖棚那边也留了张桌子。领班说，坏了的东西他们能送到我手边。",
                        "请先把台灯送到站台口的孙姨手里。她答应付四枚，刚好够咱俩的车费。",
                        "拿到钱袋后去路哨岗亭签约，再从出站门去会合点。");
                case Step.DeliverLamp:
                    return new StationDialogue("阿岚", "孙姨在站台口摆摊。让她先试试灯，钱袋直接拿去岗亭就行。");
                case Step.SignContract:
                    return new StationDialogue("阿岚", "用这袋维修费去岗亭签约，收据上记咱俩的名字。");
                default:
                    return new StationDialogue("阿岚", "车订好了？走吧，我在站外会合点等你。");
            }
        }

        private StationDialogue PickUpTools()
        {
            if (!ToolsAvailable)
                return null;

            CurrentStep = Step.ReturnTools;
            return new StationDialogue("行李间", "拿到了阿岚的工具包。");
        }

        private StationDialogue TalkToStallholder()
        {
            if (CurrentStep == Step.DeliverLamp)
            {
                LampDelivered = true;
                RepairFee = Fare;
                CurrentStep = Step.SignContract;
                return new StationDialogue("孙姨",
                    "亮了。晚上我就靠这盏灯照摊子。",
                    "这是说好的四枚酒币。拿着钱袋去路哨岗亭，替你们俩订车吧。");
            }

            return new StationDialogue("孙姨", LampDelivered
                ? "灯好好的。你们路上小心。"
                : "我那盏台灯还在票房修着。沿站台北边通道走，票房在左边。");
        }

        private StationDialogue TalkToDispatch()
        {
            if (CurrentStep == Step.Epilogue)
            {
                if (Ending == EscortEnding.PaidEscort && !RefundClaimed)
                {
                    RefundClaimed = true;
                    PersonalCoins += Fare / 2;
                    return new StationDialogue("路哨站长",
                        "收据和通讯记录都在。途中加价，按规退回一半正式车费。",
                        "退还 2 枚酒币。阿岚会留在岗亭旁，负责车队的通讯设备。");
                }
                if (Ending == EscortEnding.PaidEscort)
                    return new StationDialogue("路哨站长",
                        "退款已经记在账上。车队恢复了班次，暖棚的货按新运费计算。");
                if (Ending == EscortEnding.AlanWithShelter)
                {
                    if (!RepairAgreement)
                    {
                        RepairAgreement = true;
                        return new StationDialogue("路哨站长",
                            "阿岚要留在暖棚？可以。车队把备用通讯机送去试修。",
                            "交期和价钱写进工单，暖棚已经订好的急用零件随车送到。");
                    }
                    return new StationDialogue("路哨站长", "单子已经签了。设备送去暖棚，按约验收。");
                }
                return new StationDialogue("路哨站长",
                    "巡查找到阿岚了。以后接人的差事，别来领。",
                    "岗亭收过钱，接送者擅自离岗，已经按违约处理。");
            }
            if (CurrentStep == Step.SignContract)
            {
                RepairFee = 0;
                ContractSigned = true;
                CurrentStep = Step.LeaveForPickup;
                return new StationDialogue("路哨站长",
                    "四枚，收讫。车费已经付清，乘客是你和阿岚两人。",
                    "收据拿好。从出站门去站外接送牌，我们会派两个人来接。");
            }

            return new StationDialogue("路哨站长", ContractSigned
                ? "收据在你手上。出站后去接送牌会合。"
                : "双人接送四枚酒币。付款后给你开收据。");
        }

        private StationDialogue UseExit()
        {
            if (Chapter == 4)
            {
                CurrentStep = Step.MilestoneComplete;
                return new StationDialogue("余电", "这一段旅程结束了。");
            }
            if (Chapter == 1)
            {
                if (CurrentStep != Step.ContinueToNextDay)
                    return new StationDialogue("出站门", "先去旧票房找阿岚，帮他取回工具并修好通讯装置。");
                Chapter = 2;
                CurrentStep = Step.HearOffers;
                return null;
            }

            if (CurrentStep != Step.LeaveForPickup)
                return new StationDialogue("出站门", "去接送点前，先到岗亭把两人的车费交好。");

            Chapter = 3;
            CurrentStep = Step.ReachPickup;
            return null;
        }

        private StationDialogue ConfirmPickup()
        {
            if (CurrentStep != Step.ReachPickup)
                return null;

            CurrentStep = Step.Encounter;
            return new StationDialogue("阿岚",
                "接送牌到了。收据你拿着吧，我们在这儿等路哨的人。",
                "他们来了……等等，前面那东西在动！");
        }

        public bool PayEscortSurcharge()
        {
            if (CurrentStep != Step.Encounter || PersonalCoins < 2)
                return false;
            PersonalCoins -= 2;
            return true;
        }

        public bool ReportEnemyDefeated()
        {
            if (CurrentStep != Step.Encounter || EnemyDefeated) return false;
            EnemyDefeated = true;
            return true;
        }

        public void FinishEscort(EscortEnding ending)
        {
            if (CurrentStep != Step.Encounter || ending == EscortEnding.None)
                return;
            if (ending != EscortEnding.PaidEscort && !EnemyDefeated) return;
            Ending = ending;
            HasKatana = ending != EscortEnding.PaidEscort;
            Chapter = 4;
            CurrentStep = Step.Epilogue;
        }

        private StationDialogue TalkToShelter()
        {
            if (CurrentStep != Step.Epilogue)
                return new StationDialogue("暖棚领班", "暖棚在这边，先照顾好自己。");
            if (Ending == EscortEnding.AlanLost)
                return new StationDialogue("暖棚领班", "我给阿岚留了桌子。他还在等你回去。");
            if (Ending == EscortEnding.AlanWithShelter)
            {
                if (RepairAgreement)
                    return new StationDialogue(new[] { "暖棚领班", "阿岚", "暖棚领班" }, new[] {
                        "车队的设备会送来，暖棚订好的急件也有着落了。",
                        "设备放在桌边就好，我会按工单修。",
                        "好，东西到了我给你送来。" });
                return new StationDialogue("暖棚领班", "那张桌子还空着，设备我们送到他手边。想谈车队维修，可以去找站长。");
            }
            return new StationDialogue("暖棚领班", "车队多开了一班。我们的运费也涨了，零件还得再等等。");
        }

        private StationDialogue ReadDisciplineNotice()
        {
            return new StationDialogue("路哨纪律簿", "已收款的接送委托未完成。两名接送者因擅离受砍手处罚。");
        }

        public string[] EndingLines()
        {
            string sun = "孙姨：仍在站台摆摊，修好的台灯放在她的摊位上。";
            if (Ending == EscortEnding.PaidEscort)
                return new[] {
                    "阿岚：留在路哨，负责维修车队的通讯设备。",
                    RefundClaimed ? "路哨站长：按规退回半数正式车费，车队恢复班次。" : "路哨站长：车队恢复班次；半数正式车费尚未领取。",
                    "暖棚领班：车队运费上涨，急需的零件还要等待。",
                    "两名接送者：完成了这次加价后的撤离。", sun };
            if (Ending == EscortEnding.AlanWithShelter)
                return new[] {
                    "阿岚：与主角脱险，留在暖棚继续修理设备。",
                    RepairAgreement ? "路哨站长与暖棚领班：双方谈成维修工单，急用零件随车送达。" : "路哨站长与暖棚领班：暖棚接纳阿岚，车队维修仍可再谈。",
                    "两名接送者：因擅自离岗违约，被路哨砍手处罚。", sun };
            return new[] {
                "阿岚：被留在接送地点，路哨巡查时发现他已经遇难。",
                "路哨站长：不再给主角安排接人的差事。",
                "暖棚领班：原本为阿岚留好的桌子空了下来。",
                "两名接送者：因擅自离岗违约，被路哨砍手处罚。", sun };
        }
    }

    public enum EscortEnding
    {
        None,
        PaidEscort,
        AlanWithShelter,
        AlanLost
    }
}
