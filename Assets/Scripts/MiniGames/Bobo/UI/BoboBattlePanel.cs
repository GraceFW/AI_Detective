using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小游戏的运行时弹窗界面。
/// 这个类负责：
/// 1. 创建和组织 UI
/// 2. 收集玩家三槽输入
/// 3. 把输入交给 BattleController
/// 4. 播放逐槽结算结果
/// 5. 在结束时把结果回调给外层
/// 它不负责规则定义，也不直接修改真实战斗状态。
/// </summary>
public class BoboBattlePanel : MonoBehaviour
{
    /// <summary>
    /// 一个动作按钮的 UI 引用集合。
    /// 把按钮、背景和文字绑在一起，便于统一刷新选中态和可点击态。
    /// </summary>
    private class ActionButtonRef
    {
        public ActionType ActionType;
        public Button Button;
        public Image Background;
        public TextMeshProUGUI Label;
    }

    /// <summary>
    /// 一个槽位行的 UI 引用集合。
    /// 每个槽位都有自己的背景、玩家动作文本、AI 动作文本和四个动作按钮。
    /// </summary>
    private class SlotRowRef
    {
        public Image Background;
        public TextMeshProUGUI PlayerActionText;
        public TextMeshProUGUI AiActionText;
        public List<ActionButtonRef> ActionButtons = new List<ActionButtonRef>();
    }

    /// <summary>
    /// 当前版本允许玩家选择的全部动作。
    /// 如果后续要加新动作，这里和 ActionType / RuleSystem 一起联动修改。
    /// </summary>
    private static readonly ActionType[] SelectableActions =
    {
        ActionType.Charge,
        ActionType.Guard,
        ActionType.Attack,
        ActionType.Ultimate
    };

    private readonly SlotRowRef[] slotRows = new SlotRowRef[BattlePlan.SlotCount];
    private readonly ActionType[] draftActions = new ActionType[BattlePlan.SlotCount];

    private CanvasGroup canvasGroup;
    private BattleController controller;
    private BoboBattleRequest currentRequest;
    private BoboBattleSessionResult lastEndedResult;
    private Coroutine roundAnimationCoroutine;
    private bool isAnimating;
    private bool sessionCompleted;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI roundText;
    private TextMeshProUGUI playerStateText;
    private TextMeshProUGUI aiStateText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI resultText;
    private TextMeshProUGUI submitButtonText;
    private Button submitButton;
    private Button restartButton;
    private Button closeButton;

    /// <summary>
    /// 判断面板当前是否处于“真正打开并接管输入”的状态。
    /// </summary>
    public bool IsVisible
    {
        get { return canvasGroup != null && canvasGroup.blocksRaycasts; }
    }

    /// <summary>
    /// 服务层首次创建面板后调用一次。
    /// </summary>
    public void Initialize(CanvasGroup targetCanvasGroup)
    {
        canvasGroup = targetCanvasGroup;
        BuildUi();
        CreateController();
        HideImmediate();
    }

    /// <summary>
    /// 根据请求打开一场新的小游戏。
    /// </summary>
    public void Show(BoboBattleRequest request)
    {
        currentRequest = request ?? new BoboBattleRequest();
        sessionCompleted = false;
        lastEndedResult = null;
        StopRoundAnimation();

        // 每次 Show 都从请求参数重新初始化整场战斗，确保状态干净。
        controller.StartNewBattle(currentRequest.PlayerName, currentRequest.AiName, currentRequest.StartingHP, currentRequest.StartingEnergy);
        ResetDraft(true);
        UpdateTitle();
        resultText.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(false);
        submitButtonText.text = "锁定三槽";
        UpdateStatus("请选择本回合的三槽行动，结算顺序固定为 1 → 2 → 3。");

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// 按“中途取消”语义关闭面板。
    /// 主要供外层流程中断时使用。
    /// </summary>
    public void CloseAsCancelled()
    {
        CompleteSession(controller == null || controller.Model == null || !controller.Model.IsFinished);
    }

    /// <summary>
    /// 在 UI 内部组装 BattleController 及其依赖。
    /// 当前小游戏规模较小，因此这里直接本地创建依赖，避免引入额外容器。
    /// </summary>
    private void CreateController()
    {
        BattleRuleSystem ruleSystem = new BattleRuleSystem();
        BattleSimulator simulator = new BattleSimulator(ruleSystem);
        BattleAiPlanner aiPlanner = new BattleAiPlanner(ruleSystem, simulator);
        controller = new BattleController(ruleSystem, aiPlanner);
        controller.BattleStateChanged += HandleBattleStateChanged;
        controller.BattleEnded += HandleBattleEnded;
    }

    /// <summary>
    /// 动态创建整套弹窗 UI。
    /// 之所以全部在代码里构建，是为了做到模块化接入，不额外依赖场景预制体。
    /// </summary>
    private void BuildUi()
    {
        // 全屏半透明遮罩，负责挡住底层交互。
        Image blocker = BoboBattleUIFactory.CreateImage("Blocker", transform, new Color(0f, 0f, 0f, 0.72f));
        BoboBattleUIFactory.StretchToParent(blocker.rectTransform);

        // 主面板容器。
        Image panel = BoboBattleUIFactory.CreateImage("Panel", transform, new Color(0.07f, 0.10f, 0.15f, 0.98f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.06f, 0.08f);
        panelRect.anchorMax = new Vector2(0.94f, 0.92f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.26f, 0.42f, 0.60f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(24, 24, 24, 24);
        panelLayout.spacing = 14;
        panelLayout.childAlignment = TextAnchor.UpperLeft;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = false;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        RectTransform header = BoboBattleUIFactory.CreateRect("Header", panel.transform);
        HorizontalLayoutGroup headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 12;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        BoboBattleUIFactory.AddLayoutElement(header, preferredHeight: 56f);

        titleText = BoboBattleUIFactory.CreateText("Title", header, "波波攒对抗演练", 32f, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        BoboBattleUIFactory.AddLayoutElement(titleText, flexibleWidth: 1f, minWidth: 240f);

        roundText = BoboBattleUIFactory.CreateText("Round", header, "第1回合", 24f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.79f, 0.89f, 1f));
        BoboBattleUIFactory.AddLayoutElement(roundText, preferredWidth: 140f);

        closeButton = BoboBattleUIFactory.CreateButton("CloseButton", header, "退出", new Color(0.39f, 0.18f, 0.20f), Color.white, out var closeLabel);
        BoboBattleUIFactory.AddLayoutElement(closeButton, preferredWidth: 110f, preferredHeight: 48f);
        closeButton.onClick.AddListener(OnCloseClicked);

        RectTransform stats = BoboBattleUIFactory.CreateRect("Stats", panel.transform);
        HorizontalLayoutGroup statsLayout = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
        statsLayout.spacing = 12;
        statsLayout.childAlignment = TextAnchor.MiddleCenter;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = true;
        statsLayout.childForceExpandWidth = true;
        statsLayout.childForceExpandHeight = true;
        BoboBattleUIFactory.AddLayoutElement(stats, preferredHeight: 92f);

        playerStateText = CreateStateCard(stats, "玩家");
        TextMeshProUGUI tipsText = CreateStateCard(stats, "同步锁定\n按槽结算\n可读对手但非完美AI");
        tipsText.alignment = TextAlignmentOptions.Center;
        aiStateText = CreateStateCard(stats, "AI");

        TextMeshProUGUI noteText = BoboBattleUIFactory.CreateText("Note", panel.transform, "改动前面的槽位会清空后续选择，确保能量校验始终正确。", 20f, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.73f, 0.82f, 0.94f));
        BoboBattleUIFactory.AddLayoutElement(noteText, preferredHeight: 30f);

        RectTransform slotsRoot = BoboBattleUIFactory.CreateRect("SlotsRoot", panel.transform);
        VerticalLayoutGroup slotsLayout = slotsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        slotsLayout.spacing = 12;
        slotsLayout.childAlignment = TextAnchor.UpperLeft;
        slotsLayout.childControlWidth = true;
        slotsLayout.childControlHeight = false;
        slotsLayout.childForceExpandWidth = true;
        slotsLayout.childForceExpandHeight = false;
        BoboBattleUIFactory.AddLayoutElement(slotsRoot, flexibleHeight: 1f, minHeight: 260f);

        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            slotRows[i] = CreateSlotRow(slotsRoot, i);
        }

        Image statusCard = BoboBattleUIFactory.CreateImage("StatusCard", panel.transform, new Color(0.11f, 0.16f, 0.22f, 0.95f));
        VerticalLayoutGroup statusLayout = statusCard.gameObject.AddComponent<VerticalLayoutGroup>();
        statusLayout.padding = new RectOffset(18, 18, 14, 14);
        statusLayout.spacing = 8;
        statusLayout.childControlWidth = true;
        statusLayout.childControlHeight = false;
        statusLayout.childForceExpandWidth = true;
        statusLayout.childForceExpandHeight = false;
        BoboBattleUIFactory.AddLayoutElement(statusCard, preferredHeight: 120f);

        resultText = BoboBattleUIFactory.CreateText("Result", statusCard.transform, string.Empty, 24f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(1f, 0.87f, 0.48f));
        BoboBattleUIFactory.AddLayoutElement(resultText, preferredHeight: 30f);
        resultText.gameObject.SetActive(false);

        statusText = BoboBattleUIFactory.CreateText("Status", statusCard.transform, string.Empty, 21f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);
        BoboBattleUIFactory.AddLayoutElement(statusText, flexibleHeight: 1f);

        RectTransform footer = BoboBattleUIFactory.CreateRect("Footer", panel.transform);
        HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 12;
        footerLayout.childAlignment = TextAnchor.MiddleRight;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = false;
        footerLayout.childForceExpandHeight = true;
        BoboBattleUIFactory.AddLayoutElement(footer, preferredHeight: 56f);

        restartButton = BoboBattleUIFactory.CreateButton("RestartButton", footer, "再来一局", new Color(0.15f, 0.44f, 0.34f), Color.white, out var restartLabel);
        BoboBattleUIFactory.AddLayoutElement(restartButton, preferredWidth: 140f, preferredHeight: 50f);
        restartButton.onClick.AddListener(RestartBattle);
        restartButton.gameObject.SetActive(false);

        submitButton = BoboBattleUIFactory.CreateButton("SubmitButton", footer, "锁定三槽", new Color(0.17f, 0.40f, 0.68f), Color.white, out submitButtonText);
        BoboBattleUIFactory.AddLayoutElement(submitButton, preferredWidth: 160f, preferredHeight: 50f);
        submitButton.onClick.AddListener(OnSubmitClicked);
    }

    /// <summary>
    /// 创建顶部状态卡片，用于展示玩家/AI 当前 HP 与能量。
    /// </summary>
    private TextMeshProUGUI CreateStateCard(Transform parent, string initialText)
    {
        Image card = BoboBattleUIFactory.CreateImage("StateCard", parent, new Color(0.11f, 0.16f, 0.22f, 0.95f));
        BoboBattleUIFactory.AddLayoutElement(card, flexibleWidth: 1f, preferredHeight: 92f);
        TextMeshProUGUI text = BoboBattleUIFactory.CreateText("Text", card.transform, initialText, 24f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        BoboBattleUIFactory.StretchToParent(text.rectTransform, 16f, 16f, 12f, 12f);
        return text;
    }

    /// <summary>
    /// 创建单个槽位的整行 UI。
    /// </summary>
    private SlotRowRef CreateSlotRow(Transform parent, int slotIndex)
    {
        SlotRowRef row = new SlotRowRef();
        row.Background = BoboBattleUIFactory.CreateImage("SlotRow_" + slotIndex, parent, new Color(0.11f, 0.16f, 0.22f, 0.92f));
        BoboBattleUIFactory.AddLayoutElement(row.Background, preferredHeight: 84f);

        HorizontalLayoutGroup layout = row.Background.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        TextMeshProUGUI slotLabel = BoboBattleUIFactory.CreateText("SlotLabel", row.Background.transform, "槽位 " + (slotIndex + 1), 22f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.83f, 0.91f, 1f));
        BoboBattleUIFactory.AddLayoutElement(slotLabel, preferredWidth: 90f);

        row.PlayerActionText = BoboBattleUIFactory.CreateText("PlayerAction", row.Background.transform, "玩家：未选择", 20f, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
        BoboBattleUIFactory.AddLayoutElement(row.PlayerActionText, preferredWidth: 170f);

        row.AiActionText = BoboBattleUIFactory.CreateText("AiAction", row.Background.transform, "AI：待锁定", 20f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.85f, 0.88f, 0.95f));
        BoboBattleUIFactory.AddLayoutElement(row.AiActionText, preferredWidth: 170f);

        RectTransform buttonsRoot = BoboBattleUIFactory.CreateRect("ButtonsRoot", row.Background.transform);
        HorizontalLayoutGroup buttonsLayout = buttonsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 8;
        buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonsLayout.childControlWidth = true;
        buttonsLayout.childControlHeight = true;
        buttonsLayout.childForceExpandWidth = true;
        buttonsLayout.childForceExpandHeight = true;
        BoboBattleUIFactory.AddLayoutElement(buttonsRoot, flexibleWidth: 1f, minWidth: 480f);

        for (int i = 0; i < SelectableActions.Length; i++)
        {
            ActionType actionType = SelectableActions[i];
            Button button = BoboBattleUIFactory.CreateButton(actionType.GetDisplayName(), buttonsRoot, actionType.GetDisplayName(), actionType.GetThemeColor(), Color.white, out var label);
            BoboBattleUIFactory.AddLayoutElement(button, flexibleWidth: 1f, preferredHeight: 44f);

            ActionButtonRef buttonRef = new ActionButtonRef();
            buttonRef.ActionType = actionType;
            buttonRef.Button = button;
            buttonRef.Background = button.GetComponent<Image>();
            buttonRef.Label = label;
            row.ActionButtons.Add(buttonRef);

            int capturedSlotIndex = slotIndex;
            ActionType capturedActionType = actionType;
            button.onClick.AddListener(() => SelectDraftAction(capturedSlotIndex, capturedActionType));
        }

        return row;
    }

    /// <summary>
    /// 点击“锁定三槽”后的处理。
    /// 真正的战斗执行入口在 Controller，不在 UI。
    /// </summary>
    private void OnSubmitClicked()
    {
        if (controller == null || isAnimating)
        {
            return;
        }

        BattlePlan playerPlan = new BattlePlan(draftActions);
        if (playerPlan.HasUnselectedSlot())
        {
            UpdateStatus("三槽行动未填满，无法锁定。");
            return;
        }

        if (!controller.TrySubmitPlayerPlan(playerPlan, out var roundResult, out var errorMessage))
        {
            UpdateStatus(errorMessage);
            return;
        }

        // AI 方案在提交后一次性揭示，再按槽位播放结算。
        ShowAiPlan(roundResult.AIPlan);
        submitButton.interactable = false;
        closeButton.interactable = false;
        restartButton.interactable = false;
        isAnimating = true;
        roundAnimationCoroutine = StartCoroutine(PlayRoundResult(roundResult));
    }

    /// <summary>
    /// 播放一整回合的逐槽结算动画。
    /// 当前实现是轻量文本 + 高亮切换，后面也可以在这里挂更丰富的表现。
    /// </summary>
    private IEnumerator PlayRoundResult(BattleRoundResult roundResult)
    {
        UpdateStatus("AI 已锁定行动，开始同步结算。");

        for (int i = 0; i < roundResult.SlotInfos.Count; i++)
        {
            ActionResolveInfo info = roundResult.SlotInfos[i];
            // 强调当前结算到的槽位，帮助玩家理解“同步选择、顺序结算”的规则。
            HighlightSlot(info.SlotIndex, true);
            yield return new WaitForSeconds(0.2f);
            ApplyResolveInfo(info);
            UpdateStatus(info.Summary);
            yield return new WaitForSeconds(0.65f);
            HighlightSlot(info.SlotIndex, false);
        }

        roundAnimationCoroutine = null;
        isAnimating = false;
        closeButton.interactable = true;

        if (roundResult.IsBattleFinished)
        {
            ApplyEndedState();
        }
        else
        {
            // 新回合开始前清掉玩家草稿，但保留上一回合已经揭示过的 AI 行动历史文本。
            ResetDraft(false);
            RefreshState(controller.Model);
            UpdateStatus("本回合结算完毕，请重新选择下一轮三槽行动。");
        }
    }

    /// <summary>
    /// 把单槽位结算结果同步到顶部状态卡。
    /// </summary>
    private void ApplyResolveInfo(ActionResolveInfo info)
    {
        playerStateText.text = BuildStateText(currentRequest.PlayerName, info.PlayerHPAfter, info.PlayerEnergyAfter);
        aiStateText.text = BuildStateText(currentRequest.AiName, info.AiHPAfter, info.AiEnergyAfter);
    }

    /// <summary>
    /// Controller 状态改变时刷新 UI。
    /// 正在播放回合动画时不抢刷新，避免状态跳变破坏表现。
    /// </summary>
    private void HandleBattleStateChanged(BattleModel snapshot)
    {
        if (isAnimating || snapshot == null)
        {
            return;
        }

        RefreshState(snapshot);
    }

    /// <summary>
    /// 先缓存结束结果，等 UI 播放到结束态时再统一消费。
    /// </summary>
    private void HandleBattleEnded(BoboBattleSessionResult result)
    {
        lastEndedResult = result;
    }

    /// <summary>
    /// 用快照完整刷新顶部状态和草稿按钮区。
    /// </summary>
    private void RefreshState(BattleModel snapshot)
    {
        if (snapshot == null || currentRequest == null)
        {
            return;
        }

        roundText.text = "第" + snapshot.RoundIndex + "回合";
        playerStateText.text = BuildStateText(currentRequest.PlayerName, snapshot.Player.HP, snapshot.Player.Energy);
        aiStateText.text = BuildStateText(currentRequest.AiName, snapshot.AI.HP, snapshot.AI.Energy);
        UpdateDraftUi();
    }

    /// <summary>
    /// 构造状态卡显示文本。
    /// </summary>
    private string BuildStateText(string displayName, int hp, int energy)
    {
        return string.Format("{0}\nHP {1}  |  EN {2}", displayName, hp, energy);
    }

    /// <summary>
    /// 玩家点击某个槽位动作按钮后的处理。
    /// 这里会强制按顺序选槽，并实时做能量投影校验。
    /// </summary>
    private void SelectDraftAction(int slotIndex, ActionType actionType)
    {
        if (controller == null || controller.Model == null || controller.Model.IsFinished || isAnimating)
        {
            return;
        }

        if (!ArePreviousSlotsFilled(slotIndex))
        {
            UpdateStatus("请按顺序从前往后选择槽位行动。");
            return;
        }

        int projectedEnergy = GetProjectedEnergyBeforeSlot(slotIndex);
        if (!controller.RuleSystem.CanAffordAction(projectedEnergy, actionType))
        {
            UpdateStatus(string.Format("第{0}槽当前能量不足，无法选择{1}。", slotIndex + 1, actionType.GetDisplayName()));
            return;
        }

        draftActions[slotIndex] = actionType;
        // 如果改了前面的槽位，后面的能量前提就变了，因此必须清空后续选择。
        for (int i = slotIndex + 1; i < BattlePlan.SlotCount; i++)
        {
            draftActions[i] = ActionType.None;
        }

        UpdateDraftUi();
        UpdateStatus(string.Format("第{0}槽已设置为{1}。", slotIndex + 1, actionType.GetDisplayName()));
    }

    /// <summary>
    /// 重置当前回合的玩家草稿。
    /// clearAiPlan 为 true 时通常表示整局刚开始或重新开局。
    /// </summary>
    private void ResetDraft(bool clearAiPlan)
    {
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            draftActions[i] = ActionType.None;
            if (clearAiPlan && slotRows[i] != null)
            {
                slotRows[i].AiActionText.text = "AI：待锁定";
            }

            HighlightSlot(i, false);
        }

        UpdateDraftUi();
    }

    /// <summary>
    /// 按当前草稿和能量预测结果刷新每个按钮的可用状态与高亮。
    /// </summary>
    private void UpdateDraftUi()
    {
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            SlotRowRef row = slotRows[i];
            if (row == null)
            {
                continue;
            }

            row.PlayerActionText.text = "玩家：" + draftActions[i].GetDisplayName();
            bool slotEditable = controller != null &&
                                controller.Model != null &&
                                !controller.Model.IsFinished &&
                                !isAnimating &&
                                ArePreviousSlotsFilled(i);

            int energyBefore = GetProjectedEnergyBeforeSlot(i);

            for (int buttonIndex = 0; buttonIndex < row.ActionButtons.Count; buttonIndex++)
            {
                ActionButtonRef buttonRef = row.ActionButtons[buttonIndex];
                bool canUseAction = slotEditable && controller.RuleSystem.CanAffordAction(energyBefore, buttonRef.ActionType);
                bool isSelected = draftActions[i] == buttonRef.ActionType;

                buttonRef.Button.interactable = canUseAction;
                buttonRef.Background.color = isSelected
                    ? BoboBattleUIFactory.Tint(buttonRef.ActionType.GetThemeColor(), 1.18f)
                    : buttonRef.ActionType.GetThemeColor();
                buttonRef.Label.fontStyle = isSelected ? FontStyles.Bold | FontStyles.UpperCase : FontStyles.Bold;
            }
        }

        submitButton.interactable = controller != null &&
                                    controller.Model != null &&
                                    !controller.Model.IsFinished &&
                                    !isAnimating &&
                                    !new BattlePlan(draftActions).HasUnselectedSlot();
    }

    /// <summary>
    /// 向玩家揭示 AI 本回合已经锁定的动作。
    /// </summary>
    private void ShowAiPlan(BattlePlan aiPlan)
    {
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            slotRows[i].AiActionText.text = "AI：" + aiPlan[i].GetDisplayName();
        }
    }

    /// <summary>
    /// 当前槽位能否编辑，依赖前面的槽位是否都已经选好。
    /// </summary>
    private bool ArePreviousSlotsFilled(int slotIndex)
    {
        for (int i = 0; i < slotIndex; i++)
        {
            if (draftActions[i] == ActionType.None)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 预测进入某一槽位之前，玩家应当拥有多少能量。
    /// UI 会用它做本地预校验，避免玩家选完三槽后才发现中途能量不够。
    /// </summary>
    private int GetProjectedEnergyBeforeSlot(int slotIndex)
    {
        if (controller == null || controller.Model == null || controller.Model.Player == null)
        {
            return 0;
        }

        int energy = controller.Model.Player.Energy;
        for (int i = 0; i < slotIndex; i++)
        {
            if (draftActions[i] == ActionType.None)
            {
                break;
            }

            energy = controller.RuleSystem.ProjectEnergyAfterAction(energy, draftActions[i]);
        }

        return energy;
    }

    /// <summary>
    /// 设置槽位高亮，用于结算播放时强调当前步骤。
    /// </summary>
    private void HighlightSlot(int slotIndex, bool highlighted)
    {
        if (slotIndex < 0 || slotIndex >= slotRows.Length || slotRows[slotIndex] == null)
        {
            return;
        }

        slotRows[slotIndex].Background.color = highlighted
            ? new Color(0.17f, 0.28f, 0.40f, 0.98f)
            : new Color(0.11f, 0.16f, 0.22f, 0.92f);
    }

    /// <summary>
    /// 把 UI 切换到战斗结束态。
    /// 结束后允许玩家重开，也允许直接关闭返回外层流程。
    /// </summary>
    private void ApplyEndedState()
    {
        BoboBattleSessionResult result = lastEndedResult ?? controller.BuildSessionResult(false);
        resultText.gameObject.SetActive(true);
        resultText.text = GetResultText(result.Winner);
        restartButton.gameObject.SetActive(true);
        restartButton.interactable = true;
        submitButton.interactable = false;
        submitButtonText.text = "已结算";
        UpdateDraftUi();

        switch (result.Winner)
        {
            case BattleWinner.Player:
                UpdateStatus("你赢下了这场波波攒对抗，可以关闭弹窗返回主流程，也可以立即再开一局。");
                break;
            case BattleWinner.AI:
                UpdateStatus("镜像 AI 取得胜利。可以直接关闭返回主流程，也可以再试一局。");
                break;
            case BattleWinner.Draw:
                UpdateStatus("双方同时倒下，判定为平局。");
                break;
            default:
                UpdateStatus("对局已结束。");
                break;
        }
    }

    /// <summary>
    /// 将胜负枚举转成结果标题。
    /// </summary>
    private string GetResultText(BattleWinner winner)
    {
        switch (winner)
        {
            case BattleWinner.Player:
                return "对局结果：玩家获胜";
            case BattleWinner.AI:
                return "对局结果：AI 获胜";
            case BattleWinner.Draw:
                return "对局结果：平局";
            default:
                return "对局已结束";
        }
    }

    /// <summary>
    /// 在当前请求参数下重新开始一局。
    /// </summary>
    private void RestartBattle()
    {
        if (currentRequest == null)
        {
            return;
        }

        StopRoundAnimation();
        Show(currentRequest);
    }

    /// <summary>
    /// 关闭按钮的行为：如果战斗未结束，按取消处理；否则按正常结束关闭。
    /// </summary>
    private void OnCloseClicked()
    {
        bool shouldCancel = controller == null || controller.Model == null || !controller.Model.IsFinished;
        CompleteSession(shouldCancel);
    }

    /// <summary>
    /// 统一收口小游戏生命周期。
    /// 这里会停止动画、隐藏 UI，并把结果回调给外层。
    /// </summary>
    private void CompleteSession(bool wasCancelled)
    {
        if (sessionCompleted)
        {
            HideImmediate();
            return;
        }

        sessionCompleted = true;
        StopRoundAnimation();

        BoboBattleSessionResult result = controller != null ? controller.BuildSessionResult(wasCancelled) : new BoboBattleSessionResult();
        if (!wasCancelled && lastEndedResult != null)
        {
            result = lastEndedResult;
        }

        HideImmediate();

        var callback = currentRequest != null ? currentRequest.OnCompleted : null;
        currentRequest = null;
        callback?.Invoke(result);
    }

    /// <summary>
    /// 停掉正在播放的回合协程。
    /// </summary>
    private void StopRoundAnimation()
    {
        if (roundAnimationCoroutine != null)
        {
            StopCoroutine(roundAnimationCoroutine);
            roundAnimationCoroutine = null;
        }

        isAnimating = false;
    }

    /// <summary>
    /// 立即隐藏面板，但不销毁对象。
    /// 这样后续再次打开时可以复用 UI 结构。
    /// </summary>
    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        resultText.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(false);
        submitButtonText.text = "锁定三槽";
    }

    /// <summary>
    /// 刷新标题。
    /// </summary>
    private void UpdateTitle()
    {
        titleText.text = currentRequest != null ? currentRequest.Title : "波波攒对抗演练";
    }

    /// <summary>
    /// 刷新底部状态说明。
    /// </summary>
    private void UpdateStatus(string message)
    {
        statusText.text = message;
    }
}
