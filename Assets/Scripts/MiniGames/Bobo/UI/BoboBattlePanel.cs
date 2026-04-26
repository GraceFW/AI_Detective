using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BoboBattlePanel : MonoBehaviour
{
    private enum ActionVisualOwner
    {
        Shared = 0,
        Player = 1,
        AI = 2
    }

    [Serializable]
    private class ActionButtonBinding
    {
        [SerializeField] private ActionType actionType = ActionType.None;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [FormerlySerializedAs("label")]
        [SerializeField] private TextMeshProUGUI actionTitle;
        [SerializeField] private TextMeshProUGUI actionText;
        [SerializeField] private Graphic selectedFrame;

        public ActionType ActionType => actionType;
        public Button Button => button;
        public Image Background => background;
        public Image IconImage => iconImage;
        public TextMeshProUGUI ActionTitle => actionTitle;
        public TextMeshProUGUI ActionText => actionText;
        public Graphic SelectedFrame => selectedFrame;
    }

    [Serializable]
    private class CardSlotBinding
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Graphic highlightFrame;
        [SerializeField] private Image actionIcon;
        [SerializeField] private TextMeshProUGUI slotIndexText;
        [FormerlySerializedAs("actionText")]
        [SerializeField] private TextMeshProUGUI actionTitle;
        [SerializeField] private TextMeshProUGUI actionText;
        [SerializeField] private GameObject cutline;
        [SerializeField] private GameObject hiddenRoot;
        [SerializeField] private TextMeshProUGUI hiddenText;

        public Button Button => button;
        public Image Background => background;
        public Graphic HighlightFrame => highlightFrame;
        public Image ActionIcon => actionIcon;
        public TextMeshProUGUI SlotIndexText => slotIndexText;
        public TextMeshProUGUI ActionTitle => actionTitle;
        public TextMeshProUGUI ActionText => actionText;
        public GameObject Cutline => cutline;
        public GameObject HiddenRoot => hiddenRoot;
        public TextMeshProUGUI HiddenText => hiddenText;
    }

    [Serializable]
    private class ActionVisualBinding
    {
        [SerializeField] private ActionVisualOwner owner = ActionVisualOwner.Shared;
        [SerializeField] private ActionType actionType = ActionType.None;
        [SerializeField] private Sprite sprite;

        public ActionVisualOwner Owner => owner;
        public ActionType ActionType => actionType;
        public Sprite Sprite => sprite;
    }

    private enum TooltipSourceType
    {
        ActionPalette = 0,
        PlayerSlot = 1,
        AiSlot = 2
    }

    private enum TooltipPlacement
    {
        Right = 0,
        Left = 1,
        Above = 2,
        Below = 3
    }

    [Serializable]
    private class TooltipTextConfig
    {
        [SerializeField] private string title;
        [SerializeField] [TextArea(2, 5)] private string body;
        [Header("Card Display")]
        [SerializeField] private string actionTitle;
        [SerializeField] [TextArea(1, 3)] private string actionText;

        public string Title => title;
        public string Body => body;
        public string ActionTitle => string.IsNullOrEmpty(actionTitle) ? title : actionTitle;
        public string ActionText => string.IsNullOrEmpty(actionText) ? body : actionText;
    }

    [Serializable]
    private class ActionTooltipBinding
    {
        [SerializeField] private ActionType actionType = ActionType.None;
        [SerializeField] private TooltipTextConfig content = new TooltipTextConfig();

        public ActionType ActionType => actionType;
        public TooltipTextConfig Content => content;
    }

    [Serializable]
    private class TooltipPlacementBinding
    {
        [SerializeField] private TooltipSourceType sourceType = TooltipSourceType.ActionPalette;
        [SerializeField] private TooltipPlacement placement = TooltipPlacement.Right;
        [SerializeField] private Vector2 offset = new Vector2(26f, -18f);

        public TooltipSourceType SourceType => sourceType;
        public TooltipPlacement Placement => placement;
        public Vector2 Offset => offset;
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI aiNameText;

    [Header("Status Pips")]
    [SerializeField] private Image[] playerHpPips = new Image[0];
    [SerializeField] private Image[] playerEnergyPips = new Image[0];
    [SerializeField] private Image[] aiHpPips = new Image[0];
    [SerializeField] private Image[] aiEnergyPips = new Image[0];

    [Header("Action Palette")]
    [SerializeField] private ActionButtonBinding[] actionButtons = new ActionButtonBinding[0];

    [Header("Battle Slots")]
    [SerializeField] private CardSlotBinding[] playerCardSlots = new CardSlotBinding[BattlePlan.SlotCount];
    [SerializeField] private CardSlotBinding[] aiCardSlots = new CardSlotBinding[BattlePlan.SlotCount];

    [Header("Action Visuals")]
    [SerializeField] private ActionVisualBinding[] actionVisuals = new ActionVisualBinding[0];

    [Header("Footer")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI submitButtonText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button closeButton;

    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private CanvasGroup tooltipCanvasGroup;
    [SerializeField] private TextMeshProUGUI tooltipTitleText;
    [SerializeField] private TextMeshProUGUI tooltipBodyText;
    [SerializeField] private TooltipAutoSize tooltipAutoSize;
    [SerializeField] private ActionTooltipBinding[] actionTooltipContents = new ActionTooltipBinding[0];
    [SerializeField] private ActionTooltipBinding[] aiActionTooltipContents = new ActionTooltipBinding[0];
    [SerializeField] private TooltipTextConfig playerEditableEmptySlotTooltip = new TooltipTextConfig();
    [SerializeField] private TooltipTextConfig playerLockedEmptySlotTooltip = new TooltipTextConfig();
    [SerializeField] private TooltipTextConfig aiHiddenSlotTooltip = new TooltipTextConfig();
    [SerializeField] private TooltipPlacementBinding[] tooltipPlacements = new TooltipPlacementBinding[0];

    [Header("Theme")]
    [SerializeField] private Color playerCardNormalColor = new Color(0.11f, 0.16f, 0.22f, 0.92f);
    [SerializeField] private Color playerCardFocusColor = new Color(0.19f, 0.27f, 0.38f, 0.98f);
    [SerializeField] private Color resolvingCardColor = new Color(0.28f, 0.41f, 0.56f, 0.98f);
    [SerializeField] private Color aiCardNormalColor = new Color(0.21f, 0.21f, 0.24f, 0.92f);
    [SerializeField] private Color aiCardRevealColor = new Color(0.30f, 0.24f, 0.18f, 0.98f);
    [SerializeField] private Color pipEnabledColor = Color.white;
    [SerializeField] private Color pipDisabledColor = new Color(1f, 1f, 1f, 0.22f);

    [Header("Options")]
    [SerializeField] private bool autoApplyPreferredFont = true;
    [SerializeField] private bool logBindingWarnings = true;
    [SerializeField] private string aiHiddenSlotText = "?";

    [Header("Round Presentation")]
    [SerializeField] private float engagementRevealLeadDelay = 0.2f;
    [SerializeField] private float engagementReadDuration = 1.75f;
    [SerializeField] private float engagementTransitionDelay = 0.45f;

    private readonly ActionType[] draftActions = new ActionType[BattlePlan.SlotCount];
    private readonly ActionType[] revealedAiActions = new ActionType[BattlePlan.SlotCount];

    private BattleController controller;
    private BoboBattleRequest currentRequest;
    private BoboBattleSessionResult lastEndedResult;
    private Coroutine roundAnimationCoroutine;
    private ActionType selectedPaletteAction = ActionType.None;
    private int focusedPlayerSlotIndex;
    private int resolvingSlotIndex = -1;
    private bool isAnimating;
    private bool sessionCompleted;
    private bool isInitialized;
    private Canvas rootCanvas;

    public bool IsVisible => canvasGroup != null && canvasGroup.blocksRaycasts;

    public bool Initialize(CanvasGroup targetCanvasGroup)
    {
        if (targetCanvasGroup != null)
        {
            canvasGroup = targetCanvasGroup;
        }
        else if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = GetComponent<Canvas>();
        }

        if (autoApplyPreferredFont)
        {
            ApplyPreferredFontToBindings();
        }

        if (!ValidateBindings())
        {
            return false;
        }

        CreateController();
        BindUiEvents();
        HideTooltip();
        HideImmediate();
        isInitialized = true;
        return true;
    }

    public void Show(BoboBattleRequest request)
    {
        if (!isInitialized && !Initialize(canvasGroup))
        {
            return;
        }

        currentRequest = request ?? new BoboBattleRequest();
        sessionCompleted = false;
        lastEndedResult = null;
        StopRoundAnimation();

        controller.AiMode = currentRequest.AiMode;
        controller.StartNewBattle(currentRequest.PlayerName, currentRequest.AiName, currentRequest.StartingHP, currentRequest.StartingEnergy);
        ResetDraft(true);
        UpdateTitle();

        if (resultText != null) resultText.gameObject.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(false);
        ApplyCloseButtonState();
        if (submitButtonText != null) submitButtonText.text = "确定";

        UpdateStatus("先选左侧动作，再放入下方三张玩家牌位。");

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void CloseAsCancelled()
    {
        TryCloseAsCancelled(false);
    }

    public void ForceHideWithoutCallback()
    {
        sessionCompleted = true;
        StopRoundAnimation();
        currentRequest = null;
        lastEndedResult = null;
        HideImmediate();
    }

    public bool TryCloseAsCancelled(bool force)
    {
        if (!IsVisible)
        {
            return false;
        }

        if (!force && !CanCloseCurrentBattle())
        {
            return false;
        }

        CompleteSession(controller == null || controller.Model == null || !controller.Model.IsFinished);
        return true;
    }

    public bool DebugCompleteAsPlayerWin()
    {
        if (!IsVisible || controller == null || controller.Model == null)
        {
            return false;
        }

        StopRoundAnimation();
        isAnimating = false;

        BattleModel model = controller.Model;
        if (model.Player != null)
        {
            model.Player.HP = Mathf.Max(1, model.Player.HP);
        }

        if (model.AI != null)
        {
            model.AI.HP = 0;
        }

        model.Winner = BattleWinner.Player;
        model.IsFinished = true;
        lastEndedResult = controller.BuildSessionResult(false);

        CompleteSession(false);
        return true;
    }

    public bool DebugCompleteAsAiWin()
    {
        if (!IsVisible || controller == null || controller.Model == null)
        {
            return false;
        }

        StopRoundAnimation();
        isAnimating = false;

        BattleModel model = controller.Model;
        if (model.AI != null)
        {
            model.AI.HP = Mathf.Max(1, model.AI.HP);
        }

        if (model.Player != null)
        {
            model.Player.HP = 0;
        }

        model.Winner = BattleWinner.AI;
        model.IsFinished = true;
        lastEndedResult = controller.BuildSessionResult(false);

        CompleteSession(false);
        return true;
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.BattleStateChanged -= HandleBattleStateChanged;
            controller.BattleEnded -= HandleBattleEnded;
        }
    }

    private void CreateController()
    {
        if (controller != null)
        {
            controller.BattleStateChanged -= HandleBattleStateChanged;
            controller.BattleEnded -= HandleBattleEnded;
        }

        BattleRuleSystem ruleSystem = new BattleRuleSystem();
        BattleSimulator simulator = new BattleSimulator(ruleSystem);
        BattleAiPlanner aiPlanner = new BattleAiPlanner(ruleSystem, simulator);
        controller = new BattleController(ruleSystem, aiPlanner);
        controller.BattleStateChanged += HandleBattleStateChanged;
        controller.BattleEnded += HandleBattleEnded;
    }

    private void BindUiEvents()
    {
        EnsureTooltipCanvasGroup();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartBattle);
            restartButton.onClick.AddListener(RestartBattle);
        }

        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(OnSubmitClicked);
            submitButton.onClick.AddListener(OnSubmitClicked);
        }

        for (int i = 0; i < playerCardSlots.Length; i++)
        {
            CardSlotBinding slot = playerCardSlots[i];
            if (slot == null) continue;
            if (slot.SlotIndexText != null) slot.SlotIndexText.text = (i + 1).ToString();
            if (slot.Button == null) continue;

            int capturedIndex = i;
            slot.Button.onClick.RemoveAllListeners();
            slot.Button.onClick.AddListener(() => OnPlayerCardSlotClicked(capturedIndex));
            ConfigurePlayerSlotHover(slot, capturedIndex);
            ConfigurePlayerSlotDrop(slot, capturedIndex);
        }

        for (int i = 0; i < aiCardSlots.Length; i++)
        {
            CardSlotBinding slot = aiCardSlots[i];
            if (slot != null && slot.SlotIndexText != null)
            {
                slot.SlotIndexText.text = (i + 1).ToString();
            }

            ConfigureAiSlotHover(slot, i);
        }

        for (int i = 0; i < actionButtons.Length; i++)
        {
            ActionButtonBinding binding = actionButtons[i];
            if (binding == null || binding.Button == null) continue;

            ApplyActionButtonContent(binding);
            ApplyActionVisual(binding.ActionType, ActionVisualOwner.Player, binding.IconImage, binding.ActionTitle);

            int capturedIndex = i;
            binding.Button.onClick.RemoveAllListeners();
            binding.Button.onClick.AddListener(() => OnPaletteActionClicked(actionButtons[capturedIndex].ActionType));
            ConfigureActionButtonHover(binding);
            ConfigureActionButtonDrag(binding);
        }
    }

    private void ConfigureActionButtonHover(ActionButtonBinding binding)
    {
        if (binding == null || binding.Button == null)
        {
            return;
        }

        BoboBattleHoverTarget hoverTarget = EnsureComponent<BoboBattleHoverTarget>(binding.Button.gameObject);
        hoverTarget.Configure(
            () => GetActionTooltipTitle(binding.ActionType, TooltipSourceType.ActionPalette),
            () => GetActionTooltipBody(binding.ActionType, TooltipSourceType.ActionPalette),
            (title, body, target) => ShowTooltip(title, body, target, TooltipSourceType.ActionPalette),
            target => MoveTooltip(target, TooltipSourceType.ActionPalette),
            HideTooltip);
    }

    private void ApplyActionButtonContent(ActionButtonBinding binding)
    {
        if (binding == null)
        {
            return;
        }

        if (binding.ActionTitle != null)
        {
            binding.ActionTitle.text = GetActionDisplayTitle(binding.ActionType, TooltipSourceType.ActionPalette);
        }

        if (binding.ActionText != null)
        {
            binding.ActionText.text = GetActionDisplayText(binding.ActionType, TooltipSourceType.ActionPalette);
        }
    }

    private void ConfigureActionButtonDrag(ActionButtonBinding binding)
    {
        if (binding == null || binding.Button == null)
        {
            return;
        }

        BoboBattleDragActionItem dragItem = EnsureComponent<BoboBattleDragActionItem>(binding.Button.gameObject);
        dragItem.Configure(
            binding.ActionType,
            rootCanvas,
            binding.Background,
            binding.IconImage,
            binding.ActionTitle,
            OnActionDragStarted,
            OnActionDragEnded);
    }

    private void ConfigurePlayerSlotHover(CardSlotBinding slot, int slotIndex)
    {
        if (slot == null || slot.Button == null)
        {
            return;
        }

        BoboBattleHoverTarget hoverTarget = EnsureComponent<BoboBattleHoverTarget>(slot.Button.gameObject);
        hoverTarget.Configure(
            () => BuildPlayerSlotTooltipTitle(slotIndex),
            () => BuildPlayerSlotTooltipBody(slotIndex),
            (title, body, target) => ShowTooltip(title, body, target, TooltipSourceType.PlayerSlot),
            target => MoveTooltip(target, TooltipSourceType.PlayerSlot),
            HideTooltip);
    }

    private void ConfigurePlayerSlotDrop(CardSlotBinding slot, int slotIndex)
    {
        if (slot == null || slot.Button == null)
        {
            return;
        }

        BoboBattleCardDropSlot dropSlot = EnsureComponent<BoboBattleCardDropSlot>(slot.Button.gameObject);
        dropSlot.Configure(slotIndex, OnActionDroppedToSlot, HideTooltip);
    }

    private void ConfigureAiSlotHover(CardSlotBinding slot, int slotIndex)
    {
        if (slot == null)
        {
            return;
        }

        GameObject hoverObject = slot.Button != null
            ? slot.Button.gameObject
            : slot.Background != null ? slot.Background.gameObject : null;
        if (hoverObject == null)
        {
            return;
        }

        BoboBattleHoverTarget hoverTarget = EnsureComponent<BoboBattleHoverTarget>(hoverObject);
        hoverTarget.Configure(
            () => BuildAiSlotTooltipTitle(slotIndex),
            () => BuildAiSlotTooltipBody(slotIndex),
            (title, body, target) => ShowTooltip(title, body, target, TooltipSourceType.AiSlot),
            target => MoveTooltip(target, TooltipSourceType.AiSlot),
            HideTooltip);
    }

    private void ApplyPreferredFontToBindings()
    {
        BoboBattleUIFactory.ApplyPreferredFont(titleText);
        BoboBattleUIFactory.ApplyPreferredFont(roundText);
        BoboBattleUIFactory.ApplyPreferredFont(playerNameText);
        BoboBattleUIFactory.ApplyPreferredFont(aiNameText);
        BoboBattleUIFactory.ApplyPreferredFont(statusText);
        BoboBattleUIFactory.ApplyPreferredFont(resultText);
        BoboBattleUIFactory.ApplyPreferredFont(submitButtonText);
        BoboBattleUIFactory.ApplyPreferredFont(tooltipTitleText);
        BoboBattleUIFactory.ApplyPreferredFont(tooltipBodyText);

        for (int i = 0; i < actionButtons.Length; i++)
        {
            if (actionButtons[i] == null) continue;
            BoboBattleUIFactory.ApplyPreferredFont(actionButtons[i].ActionTitle);
            BoboBattleUIFactory.ApplyPreferredFont(actionButtons[i].ActionText);
        }

        ApplyFontsToSlots(playerCardSlots);
        ApplyFontsToSlots(aiCardSlots);
    }

    private void ApplyFontsToSlots(CardSlotBinding[] slots)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            CardSlotBinding slot = slots[i];
            if (slot == null) continue;
            BoboBattleUIFactory.ApplyPreferredFont(slot.SlotIndexText);
            BoboBattleUIFactory.ApplyPreferredFont(slot.ActionTitle);
            BoboBattleUIFactory.ApplyPreferredFont(slot.ActionText);
            BoboBattleUIFactory.ApplyPreferredFont(slot.HiddenText);
        }
    }

    private bool ValidateBindings()
    {
        bool valid = true;

        valid &= ValidateReference(canvasGroup, "CanvasGroup");
        valid &= ValidateReference(titleText, "TitleText");
        valid &= ValidateReference(roundText, "RoundText");
        valid &= ValidateReference(statusText, "StatusText");
        valid &= ValidateReference(resultText, "ResultText");
        valid &= ValidateReference(submitButton, "SubmitButton");
        valid &= ValidateReference(submitButtonText, "SubmitButtonText");
        valid &= ValidateReference(restartButton, "RestartButton");
        valid &= ValidateReference(closeButton, "CloseButton");

        valid &= ValidateSlots(playerCardSlots, true, "PlayerCardSlots");
        valid &= ValidateSlots(aiCardSlots, false, "AiCardSlots");

        if (actionButtons == null || actionButtons.Length == 0)
        {
            LogBindingError("ActionButtons 未绑定。");
            valid = false;
        }
        else
        {
            for (int i = 0; i < actionButtons.Length; i++)
            {
                ActionButtonBinding binding = actionButtons[i];
                if (binding == null)
                {
                    LogBindingError("ActionButtons[" + i + "] 未绑定。");
                    valid = false;
                    continue;
                }

                valid &= ValidateReference(binding.Button, "ActionButtons[" + i + "].Button");
                valid &= ValidateReference(binding.Background, "ActionButtons[" + i + "].Background");
                valid &= ValidateReference(binding.ActionTitle, "ActionButtons[" + i + "].ActionTitle");
            }
        }

        return valid;
    }

    private bool ValidateSlots(CardSlotBinding[] slots, bool requireButton, string fieldName)
    {
        if (slots == null || slots.Length != BattlePlan.SlotCount)
        {
            LogBindingError(fieldName + " 数量错误。");
            return false;
        }

        bool valid = true;
        for (int i = 0; i < slots.Length; i++)
        {
            CardSlotBinding slot = slots[i];
            if (slot == null)
            {
                LogBindingError(fieldName + "[" + i + "] 未绑定。");
                valid = false;
                continue;
            }

            valid &= ValidateReference(slot.Background, fieldName + "[" + i + "].Background");
            valid &= ValidateReference(slot.SlotIndexText, fieldName + "[" + i + "].SlotIndexText");
            valid &= ValidateReference(slot.ActionTitle, fieldName + "[" + i + "].ActionTitle");
            if (requireButton)
            {
                valid &= ValidateReference(slot.Button, fieldName + "[" + i + "].Button");
            }
        }

        return valid;
    }

    private bool ValidateReference(UnityEngine.Object reference, string fieldName)
    {
        if (reference != null) return true;
        LogBindingError("缺少 UI 引用: " + fieldName);
        return false;
    }

    private void LogBindingError(string message)
    {
        if (logBindingWarnings)
        {
            Debug.LogError("[BoboBattlePanel] " + message, this);
        }
    }

    private void OnPaletteActionClicked(ActionType actionType)
    {
        if (controller == null || controller.Model == null || controller.Model.IsFinished || isAnimating)
        {
            return;
        }

        if (actionType == ActionType.None)
        {
            return;
        }

        selectedPaletteAction = actionType;
        if (!IsSlotEditable(focusedPlayerSlotIndex))
        {
            focusedPlayerSlotIndex = GetNextAssignableSlotIndex();
        }

        UpdateDraftUi();

        if (IsSlotEditable(focusedPlayerSlotIndex))
        {
            TryAssignActionToSlot(focusedPlayerSlotIndex, selectedPaletteAction);
        }
        else
        {
            UpdateStatus("动作已选中，请点击下方可编辑牌位。");
        }
    }

    private void OnActionDragStarted()
    {
        HideTooltip();
    }

    private void OnActionDragEnded()
    {
        UpdateDraftUi();
    }

    private void OnActionDroppedToSlot(int slotIndex, ActionType actionType)
    {
        if (actionType == ActionType.None)
        {
            return;
        }

        selectedPaletteAction = actionType;
        focusedPlayerSlotIndex = slotIndex;
        UpdateDraftUi();
        TryAssignActionToSlot(slotIndex, actionType);
    }

    private void OnPlayerCardSlotClicked(int slotIndex)
    {
        if (controller == null || controller.Model == null || controller.Model.IsFinished || isAnimating)
        {
            return;
        }

        if (!IsSlotEditable(slotIndex))
        {
            UpdateStatus("请按顺序从左到右放置玩家牌位。");
            return;
        }

        focusedPlayerSlotIndex = slotIndex;
        UpdateDraftUi();

        if (selectedPaletteAction != ActionType.None)
        {
            TryAssignActionToSlot(slotIndex, selectedPaletteAction);
        }
        else
        {
            UpdateStatus(string.Format("已选中第 {0} 张牌位，请从左侧选择动作。", slotIndex + 1));
        }
    }

    private bool TryAssignActionToSlot(int slotIndex, ActionType actionType)
    {
        if (!IsSlotEditable(slotIndex))
        {
            UpdateStatus("当前牌位不可编辑。");
            return false;
        }

        if (!controller.RuleSystem.CanPlaceActionInDraft(draftActions, slotIndex, actionType, out string placementError))
        {
            UpdateStatus(placementError);
            UpdateDraftUi();
            return false;
        }

        int projectedEnergy = GetProjectedEnergyBeforeSlot(slotIndex);
        if (!controller.RuleSystem.CanAffordAction(projectedEnergy, actionType))
        {
            UpdateStatus(string.Format("第 {0} 张牌位能量不足，无法放入 {1}。", slotIndex + 1, actionType.GetDisplayName()));
            UpdateDraftUi();
            return false;
        }

        draftActions[slotIndex] = actionType;
        for (int i = slotIndex + 1; i < BattlePlan.SlotCount; i++)
        {
            draftActions[i] = ActionType.None;
        }

        int nextSlot = GetNextAssignableSlotIndex();
        focusedPlayerSlotIndex = nextSlot >= 0 ? nextSlot : BattlePlan.SlotCount - 1;

        UpdateDraftUi();
        UpdateStatus(string.Format("第 {0} 张牌位已设置为 {1}。", slotIndex + 1, actionType.GetDisplayName()));
        return true;
    }

    private void OnSubmitClicked()
    {
        if (controller == null || isAnimating)
        {
            return;
        }

        BattlePlan playerPlan = new BattlePlan(draftActions);
        if (playerPlan.HasUnselectedSlot())
        {
            UpdateStatus("玩家三张牌尚未填满，无法确认。");
            return;
        }

        if (!controller.TrySubmitPlayerPlan(playerPlan, out var roundResult, out var errorMessage))
        {
            UpdateStatus(errorMessage);
            return;
        }

        if (submitButton != null) submitButton.interactable = false;
        if (closeButton != null) closeButton.interactable = false;
        if (restartButton != null) restartButton.interactable = false;

        isAnimating = true;
        roundAnimationCoroutine = StartCoroutine(PlayRoundResult(roundResult));
    }

    private IEnumerator PlayRoundResult(BattleRoundResult roundResult)
    {
        UpdateStatus("AI 已揭示牌型，开始按 1 -> 2 -> 3 依次结算。");

        for (int i = 0; i < roundResult.SlotInfos.Count; i++)
        {
            ActionResolveInfo info = roundResult.SlotInfos[i];
            resolvingSlotIndex = info.SlotIndex;
            RevealAiAction(info.SlotIndex, roundResult.AIPlan[info.SlotIndex]);
            UpdateDraftUi();
            yield return new WaitForSeconds(engagementRevealLeadDelay);
            ApplyResolveInfo(info);
            UpdateStatus(BuildEngagementStatus(info));
            yield return new WaitForSeconds(engagementReadDuration);
            resolvingSlotIndex = -1;
            UpdateDraftUi();

            bool hasNextEngagement = i < roundResult.SlotInfos.Count - 1 && !info.BattleEndedAfterSlot;
            if (hasNextEngagement)
            {
                yield return new WaitForSeconds(engagementTransitionDelay);
            }
        }

        roundAnimationCoroutine = null;
        isAnimating = false;
        resolvingSlotIndex = -1;
        ApplyCloseButtonState(roundResult.IsBattleFinished);

        if (roundResult.IsBattleFinished)
        {
            ApplyEndedState();
        }
        else
        {
            ResetDraft(true);
            RefreshState(controller.Model);
            UpdateStatus("本回合结算完成，请继续选择下一轮玩家三张牌。");
        }
    }

    private void ApplyResolveInfo(ActionResolveInfo info)
    {
        UpdatePipGroup(playerHpPips, info.PlayerHPAfter);
        UpdatePipGroup(playerEnergyPips, info.PlayerEnergyAfter);
        UpdatePipGroup(aiHpPips, info.AiHPAfter);
        UpdatePipGroup(aiEnergyPips, info.AiEnergyAfter);
    }

    private string BuildEngagementStatus(ActionResolveInfo info)
    {
        if (info == null)
        {
            return string.Empty;
        }

        return string.Format(
            "标题：当前回合第{0}次交手\n玩家行动：{1}\nAI行动：{2}\n结果：{3}",
            info.SlotIndex + 1,
            FormatStatusAction(info.PlayerSelectedAction, info.PlayerExecutedAction, info.PlayerFailedByEnergy),
            FormatStatusAction(info.AiSelectedAction, info.AiExecutedAction, info.AiFailedByEnergy),
            BuildEngagementResultText(info));
    }

    private string FormatStatusAction(ActionType selectedAction, ActionType executedAction, bool failedByEnergy)
    {
        if (failedByEnergy)
        {
            return string.Format("{0}（能量不足，未成功执行）", selectedAction.GetDisplayName());
        }

        if (selectedAction != executedAction && executedAction != ActionType.None)
        {
            return string.Format("{0}（实际执行：{1}）", selectedAction.GetDisplayName(), executedAction.GetDisplayName());
        }

        if (selectedAction != executedAction && executedAction == ActionType.None)
        {
            return string.Format("{0}（未成功执行）", selectedAction.GetDisplayName());
        }

        return selectedAction.GetDisplayName();
    }

    private string BuildEngagementResultText(ActionResolveInfo info)
    {
        if (!string.IsNullOrEmpty(info.Summary))
        {
            return info.Summary;
        }

        if (info.DamageToAI > 0 || info.DamageToPlayer > 0)
        {
            return string.Format("玩家受到 {0} 点伤害，AI 受到 {1} 点伤害。", info.DamageToPlayer, info.DamageToAI);
        }

        if (info.PlayerBlocked || info.AiBlocked)
        {
            return "本次交手出现了格挡，伤害被成功拦下。";
        }

        if (info.PlayerActionCancelled || info.AiActionCancelled)
        {
            return "本次交手中双方行动发生抵消，没有造成有效伤害。";
        }

        return "本次交手结束，双方状态已更新。";
    }

    private void HandleBattleStateChanged(BattleModel snapshot)
    {
        if (!isAnimating && snapshot != null)
        {
            RefreshState(snapshot);
        }
    }

    private void HandleBattleEnded(BoboBattleSessionResult result)
    {
        lastEndedResult = result;
    }

    private void RefreshState(BattleModel snapshot)
    {
        if (snapshot == null || currentRequest == null)
        {
            return;
        }

        if (roundText != null) roundText.text = "第 " + snapshot.RoundIndex + " 回合";
        if (playerNameText != null) playerNameText.text = currentRequest.PlayerName;
        if (aiNameText != null) aiNameText.text = currentRequest.AiName;

        UpdatePipGroup(playerHpPips, snapshot.Player.HP);
        UpdatePipGroup(playerEnergyPips, snapshot.Player.Energy);
        UpdatePipGroup(aiHpPips, snapshot.AI.HP);
        UpdatePipGroup(aiEnergyPips, snapshot.AI.Energy);
        UpdateDraftUi();
    }

    private void UpdatePipGroup(Image[] pips, int activeCount)
    {
        if (pips == null) return;
        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] != null)
            {
                pips[i].color = i < activeCount ? pipEnabledColor : pipDisabledColor;
            }
        }
    }

    private void ResetDraft(bool clearAiPlan)
    {
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            draftActions[i] = ActionType.None;
            if (clearAiPlan)
            {
                revealedAiActions[i] = ActionType.None;
            }
        }

        selectedPaletteAction = ActionType.None;
        focusedPlayerSlotIndex = 0;
        resolvingSlotIndex = -1;
        UpdateDraftUi();
    }

    private void UpdateDraftUi()
    {
        UpdateActionPaletteUi();
        UpdatePlayerCardSlotsUi();
        UpdateAiCardSlotsUi();

        if (submitButton != null)
        {
            submitButton.interactable = controller != null &&
                                        controller.Model != null &&
                                        !controller.Model.IsFinished &&
                                        !isAnimating &&
                                        !new BattlePlan(draftActions).HasUnselectedSlot();
        }
    }

    private void UpdateActionPaletteUi()
    {
        int targetSlotIndex = GetEffectiveFocusedSlotIndex();
        int energyBefore = targetSlotIndex >= 0 ? GetProjectedEnergyBeforeSlot(targetSlotIndex) : 0;

        for (int i = 0; i < actionButtons.Length; i++)
        {
            ActionButtonBinding binding = actionButtons[i];
            if (binding == null || binding.Button == null) continue;

            bool canUse = controller != null &&
                          controller.Model != null &&
                          !controller.Model.IsFinished &&
                          !isAnimating &&
                          targetSlotIndex >= 0 &&
                          controller.RuleSystem.CanAffordAction(energyBefore, binding.ActionType) &&
                          controller.RuleSystem.CanPlaceActionInDraft(draftActions, targetSlotIndex, binding.ActionType, out _);
            bool isSelected = selectedPaletteAction == binding.ActionType;

            binding.Button.interactable = canUse;
            SetActionButtonColor(binding, isSelected);
            ApplyActionButtonContent(binding);

            if (binding.ActionTitle != null)
            {
                binding.ActionTitle.fontStyle = isSelected ? FontStyles.Bold | FontStyles.UpperCase : FontStyles.Bold;
            }

            if (binding.SelectedFrame != null)
            {
                binding.SelectedFrame.gameObject.SetActive(isSelected);
            }
        }
    }

    private void UpdatePlayerCardSlotsUi()
    {
        for (int i = 0; i < playerCardSlots.Length; i++)
        {
            CardSlotBinding slot = playerCardSlots[i];
            if (slot == null) continue;

            ActionType actionType = draftActions[i];
            bool isFocused = i == GetEffectiveFocusedSlotIndex() && !isAnimating;
            bool isResolving = i == resolvingSlotIndex;

            if (slot.Background != null)
            {
                slot.Background.color = isResolving ? resolvingCardColor : isFocused ? playerCardFocusColor : playerCardNormalColor;
            }

            if (slot.HighlightFrame != null)
            {
                slot.HighlightFrame.gameObject.SetActive(isFocused || isResolving);
            }

            if (slot.Button != null)
            {
                slot.Button.interactable = controller != null &&
                                           controller.Model != null &&
                                           !controller.Model.IsFinished &&
                                           !isAnimating &&
                                           IsSlotEditable(i);
            }

            if (slot.ActionTitle != null)
            {
                slot.ActionTitle.text = actionType == ActionType.None
                    ? "未放置"
                    : GetActionDisplayTitle(actionType, TooltipSourceType.PlayerSlot);
                slot.ActionTitle.color = actionType == ActionType.None ? new Color(0f, 0f, 0f, 0.55f) : Color.black;
            }

            if (slot.ActionText != null)
            {
                slot.ActionText.text = actionType == ActionType.None
                    ? string.Empty
                    : GetActionDisplayText(actionType, TooltipSourceType.PlayerSlot);
                slot.ActionText.color = actionType == ActionType.None ? new Color(0f, 0f, 0f, 0.45f) : Color.black;
            }

            if (slot.Cutline != null)
            {
                slot.Cutline.SetActive(actionType != ActionType.None);
            }

            if (slot.HiddenRoot != null) slot.HiddenRoot.SetActive(false);
            ApplyCardActionVisual(slot, actionType, ActionVisualOwner.Player);
        }
    }

    private void UpdateAiCardSlotsUi()
    {
        for (int i = 0; i < aiCardSlots.Length; i++)
        {
            CardSlotBinding slot = aiCardSlots[i];
            if (slot == null) continue;

            bool revealed = revealedAiActions[i] != ActionType.None;
            bool isResolving = i == resolvingSlotIndex;

            if (slot.Background != null)
            {
                slot.Background.color = isResolving ? resolvingCardColor : revealed ? aiCardRevealColor : aiCardNormalColor;
            }

            if (slot.HighlightFrame != null)
            {
                slot.HighlightFrame.gameObject.SetActive(isResolving);
            }

            if (slot.HiddenRoot != null) slot.HiddenRoot.SetActive(!revealed);
            if (slot.HiddenText != null) slot.HiddenText.text = aiHiddenSlotText;
            if (slot.ActionTitle != null)
            {
                slot.ActionTitle.text = revealed
                    ? GetActionDisplayTitle(revealedAiActions[i], TooltipSourceType.AiSlot)
                    : string.Empty;
                slot.ActionTitle.color = Color.black;
            }

            if (slot.ActionText != null)
            {
                slot.ActionText.text = revealed
                    ? GetActionDisplayText(revealedAiActions[i], TooltipSourceType.AiSlot)
                    : string.Empty;
                slot.ActionText.color = Color.black;
            }

            if (slot.Cutline != null)
            {
                slot.Cutline.SetActive(revealed);
            }

            ApplyCardActionVisual(slot, revealed ? revealedAiActions[i] : ActionType.None, ActionVisualOwner.AI);
        }
    }

    private void RevealAiAction(int slotIndex, ActionType actionType)
    {
        if (slotIndex < 0 || slotIndex >= BattlePlan.SlotCount)
        {
            return;
        }

        revealedAiActions[slotIndex] = actionType;
    }

    private void RevealAiPlan(BattlePlan aiPlan)
    {
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            revealedAiActions[i] = aiPlan[i];
        }

        UpdateDraftUi();
    }

    private void ApplyCardActionVisual(CardSlotBinding slot, ActionType actionType, ActionVisualOwner owner)
    {
        if (slot == null || slot.ActionIcon == null) return;

        Sprite sprite = GetActionSprite(actionType, owner);
        slot.ActionIcon.sprite = sprite;
        slot.ActionIcon.enabled = sprite != null && actionType != ActionType.None;
        slot.ActionIcon.color = actionType == ActionType.None ? Color.clear : actionType.GetThemeColor();
    }

    private void ApplyActionVisual(ActionType actionType, ActionVisualOwner owner, Image iconImage, TextMeshProUGUI fallbackLabel)
    {
        if (iconImage == null) return;

        Sprite sprite = GetActionSprite(actionType, owner);
        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;

        if (sprite == null && fallbackLabel != null)
        {
            fallbackLabel.text = actionType.GetDisplayName();
        }
    }

    private Sprite GetActionSprite(ActionType actionType, ActionVisualOwner owner)
    {
        Sprite sharedSprite = null;

        for (int i = 0; i < actionVisuals.Length; i++)
        {
            ActionVisualBinding visual = actionVisuals[i];
            if (visual == null || visual.ActionType != actionType)
            {
                continue;
            }

            if (visual.Owner == owner)
            {
                return visual.Sprite;
            }

            if (visual.Owner == ActionVisualOwner.Shared && sharedSprite == null)
            {
                sharedSprite = visual.Sprite;
            }
        }

        return sharedSprite;
    }

    private void SetActionButtonColor(ActionButtonBinding binding, bool isSelected)
    {
        if (binding == null || binding.Background == null) return;

        Color baseColor = binding.ActionType.GetThemeColor();
        binding.Background.color = isSelected ? BoboBattleUIFactory.Tint(baseColor, 1.18f) : baseColor;
    }

    private int GetEffectiveFocusedSlotIndex()
    {
        if (IsSlotEditable(focusedPlayerSlotIndex))
        {
            return focusedPlayerSlotIndex;
        }

        return GetNextAssignableSlotIndex();
    }

    private int GetNextAssignableSlotIndex()
    {
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            if (draftActions[i] == ActionType.None && ArePreviousSlotsFilled(i))
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsSlotEditable(int slotIndex)
    {
        return slotIndex >= 0 &&
               slotIndex < BattlePlan.SlotCount &&
               ArePreviousSlotsFilled(slotIndex);
    }

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

    private void ApplyEndedState()
    {
        BoboBattleSessionResult result = lastEndedResult ?? controller.BuildSessionResult(false);

        if (resultText != null)
        {
            resultText.gameObject.SetActive(true);
            resultText.text = GetResultText(result.Winner);
        }

        if (restartButton != null)
        {
            bool allowRestart = currentRequest == null || currentRequest.AllowRestartAfterEnd;
            restartButton.gameObject.SetActive(allowRestart);
            restartButton.interactable = allowRestart;
        }

        if (submitButton != null) submitButton.interactable = false;
        ApplyCloseButtonState(true);
        if (submitButtonText != null) submitButtonText.text = "已结束";

        UpdateDraftUi();

        switch (result.Winner)
        {
            case BattleWinner.Player:
                UpdateStatus("你赢下了这场对局，可以返回主流程，或直接再来一局。");
                break;
            case BattleWinner.AI:
                UpdateStatus("AI 赢下了这场对局，可以继续尝试新的组合。");
                break;
            case BattleWinner.Draw:
                UpdateStatus("双方同时倒下，本局判定为平局。");
                break;
            default:
                UpdateStatus("对局已结束。");
                break;
        }
    }

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

    private void RestartBattle()
    {
        if (currentRequest == null) return;
        if (!currentRequest.AllowRestartAfterEnd) return;

        StopRoundAnimation();
        Show(currentRequest);
    }

    private void OnCloseClicked()
    {
        if (!CanCloseCurrentBattle())
        {
            return;
        }

        bool shouldCancel = controller == null || controller.Model == null || !controller.Model.IsFinished;
        CompleteSession(shouldCancel);
    }

    private bool CanCloseCurrentBattle()
    {
        if (controller == null || controller.Model == null || controller.Model.IsFinished)
        {
            return true;
        }

        return CanCancelBeforeEnd();
    }

    private bool CanCancelBeforeEnd()
    {
        return currentRequest == null || currentRequest.AllowCancelBeforeEnd;
    }

    private void ApplyCloseButtonState(bool battleFinished = false)
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.gameObject.SetActive(false);
        closeButton.interactable = false;
    }

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

        Action<BoboBattleSessionResult> callback = currentRequest != null ? currentRequest.OnCompleted : null;
        currentRequest = null;
        callback?.Invoke(result);
    }

    private void StopRoundAnimation()
    {
        if (roundAnimationCoroutine != null)
        {
            StopCoroutine(roundAnimationCoroutine);
            roundAnimationCoroutine = null;
        }

        isAnimating = false;
        resolvingSlotIndex = -1;
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (resultText != null) resultText.gameObject.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(false);
        if (submitButtonText != null) submitButtonText.text = "确定";
        HideTooltip();
    }

    private void EnsureTooltipCanvasGroup()
    {
        if (tooltipRoot == null)
        {
            return;
        }

        if (tooltipCanvasGroup == null)
        {
            tooltipCanvasGroup = tooltipRoot.GetComponent<CanvasGroup>();
        }

        if (tooltipCanvasGroup == null)
        {
            tooltipCanvasGroup = tooltipRoot.gameObject.AddComponent<CanvasGroup>();
        }

        if (tooltipAutoSize == null)
        {
            tooltipAutoSize = tooltipRoot.GetComponent<TooltipAutoSize>();
        }

        if (tooltipAutoSize == null)
        {
            tooltipAutoSize = tooltipRoot.gameObject.AddComponent<TooltipAutoSize>();
        }

        tooltipAutoSize.Configure(tooltipRoot, tooltipTitleText, tooltipBodyText);
    }

    private void ShowTooltip(string title, string body, RectTransform target, TooltipSourceType sourceType)
    {
        if (tooltipRoot == null || target == null)
        {
            return;
        }

        EnsureTooltipCanvasGroup();

        if (tooltipTitleText != null)
        {
            tooltipTitleText.text = string.IsNullOrEmpty(title) ? "Tip" : title;
        }

        if (tooltipBodyText != null)
        {
            tooltipBodyText.text = body ?? string.Empty;
        }

        tooltipRoot.gameObject.SetActive(true);
        tooltipCanvasGroup.alpha = 1f;
        tooltipCanvasGroup.blocksRaycasts = false;
        tooltipCanvasGroup.interactable = false;
        if (tooltipAutoSize != null)
        {
            tooltipAutoSize.RefreshLayout();
        }
        MoveTooltip(target, sourceType);
    }

    private void MoveTooltip(RectTransform target, TooltipSourceType sourceType)
    {
        if (tooltipRoot == null || rootCanvas == null || target == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        TooltipPlacementBinding placementBinding = GetTooltipPlacementBinding(sourceType);
        TooltipPlacement placement = placementBinding != null ? placementBinding.Placement : TooltipPlacement.Right;
        Vector2 offset = placementBinding != null ? placementBinding.Offset : new Vector2(26f, -18f);

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Vector3 worldAnchorPoint = GetTooltipWorldAnchorPoint(corners, placement);
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldAnchorPoint);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            eventCamera,
            out Vector2 localPoint);

        tooltipRoot.pivot = GetTooltipPivot(placement);
        tooltipRoot.anchoredPosition = localPoint + offset;
    }

    private void HideTooltip()
    {
        if (tooltipRoot == null)
        {
            return;
        }

        EnsureTooltipCanvasGroup();
        tooltipCanvasGroup.alpha = 0f;
        tooltipCanvasGroup.blocksRaycasts = false;
        tooltipCanvasGroup.interactable = false;
        tooltipRoot.gameObject.SetActive(false);
    }

    private TooltipPlacementBinding GetTooltipPlacementBinding(TooltipSourceType sourceType)
    {
        if (tooltipPlacements == null)
        {
            return null;
        }

        for (int i = 0; i < tooltipPlacements.Length; i++)
        {
            TooltipPlacementBinding placement = tooltipPlacements[i];
            if (placement != null && placement.SourceType == sourceType)
            {
                return placement;
            }
        }

        return null;
    }

    private TooltipTextConfig GetActionTooltipConfig(ActionType actionType, TooltipSourceType sourceType)
    {
        ActionTooltipBinding[] bindings = sourceType == TooltipSourceType.AiSlot && aiActionTooltipContents != null && aiActionTooltipContents.Length > 0
            ? aiActionTooltipContents
            : actionTooltipContents;

        if (bindings == null)
        {
            return null;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            ActionTooltipBinding binding = bindings[i];
            if (binding != null && binding.ActionType == actionType)
            {
                return binding.Content;
            }
        }

        if (!ReferenceEquals(bindings, actionTooltipContents) && actionTooltipContents != null)
        {
            for (int i = 0; i < actionTooltipContents.Length; i++)
            {
                ActionTooltipBinding binding = actionTooltipContents[i];
                if (binding != null && binding.ActionType == actionType)
                {
                    return binding.Content;
                }
            }
        }

        return null;
    }

    private string GetActionTooltipTitle(ActionType actionType, TooltipSourceType sourceType)
    {
        TooltipTextConfig config = GetActionTooltipConfig(actionType, sourceType);
        if (config != null && !string.IsNullOrEmpty(config.Title))
        {
            return config.Title;
        }

        return actionType.GetDisplayName();
    }

    private string GetActionTooltipBody(ActionType actionType, TooltipSourceType sourceType)
    {
        TooltipTextConfig config = GetActionTooltipConfig(actionType, sourceType);
        if (config != null && !string.IsNullOrEmpty(config.Body))
        {
            return config.Body;
        }

        return actionType.GetTooltipDescription();
    }

    private string GetActionDisplayTitle(ActionType actionType, TooltipSourceType sourceType)
    {
        TooltipTextConfig config = GetActionTooltipConfig(actionType, sourceType);
        if (config != null && !string.IsNullOrEmpty(config.ActionTitle))
        {
            return config.ActionTitle;
        }

        return GetActionTooltipTitle(actionType, sourceType);
    }

    private string GetActionDisplayText(ActionType actionType, TooltipSourceType sourceType)
    {
        TooltipTextConfig config = GetActionTooltipConfig(actionType, sourceType);
        if (config != null && !string.IsNullOrEmpty(config.ActionText))
        {
            return config.ActionText;
        }

        return GetActionTooltipBody(actionType, sourceType);
    }

    private string ResolveTooltipTitle(TooltipTextConfig config, string fallback)
    {
        return config != null && !string.IsNullOrEmpty(config.Title) ? config.Title : fallback;
    }

    private string ResolveTooltipBody(TooltipTextConfig config, string fallback)
    {
        return config != null && !string.IsNullOrEmpty(config.Body) ? config.Body : fallback;
    }

    private Vector3 GetTooltipWorldAnchorPoint(Vector3[] corners, TooltipPlacement placement)
    {
        switch (placement)
        {
            case TooltipPlacement.Left:
                return (corners[0] + corners[1]) * 0.5f;
            case TooltipPlacement.Above:
                return (corners[1] + corners[2]) * 0.5f;
            case TooltipPlacement.Below:
                return (corners[0] + corners[3]) * 0.5f;
            default:
                return (corners[2] + corners[3]) * 0.5f;
        }
    }

    private Vector2 GetTooltipPivot(TooltipPlacement placement)
    {
        switch (placement)
        {
            case TooltipPlacement.Left:
                return new Vector2(1f, 0.5f);
            case TooltipPlacement.Above:
                return new Vector2(0.5f, 0f);
            case TooltipPlacement.Below:
                return new Vector2(0.5f, 1f);
            default:
                return new Vector2(0f, 0.5f);
        }
    }

    private string BuildPlayerSlotTooltipTitle(int slotIndex)
    {
        ActionType actionType = IsSlotIndexValid(slotIndex) ? draftActions[slotIndex] : ActionType.None;
        return actionType == ActionType.None
            ? ResolveTooltipTitle(
                IsSlotEditable(slotIndex) ? playerEditableEmptySlotTooltip : playerLockedEmptySlotTooltip,
                string.Format("Player Slot {0}", slotIndex + 1))
            : GetActionTooltipTitle(actionType, TooltipSourceType.PlayerSlot);
    }

    private string BuildPlayerSlotTooltipBody(int slotIndex)
    {
        if (!IsSlotIndexValid(slotIndex))
        {
            return string.Empty;
        }

        ActionType actionType = draftActions[slotIndex];
        if (actionType == ActionType.None)
        {
            return IsSlotEditable(slotIndex)
                ? ResolveTooltipBody(playerEditableEmptySlotTooltip, "This slot is editable. Click a left action or drag one into this slot.")
                : ResolveTooltipBody(playerLockedEmptySlotTooltip, "This slot is locked until all previous player slots are filled in order.");
        }

        return GetActionTooltipBody(actionType, TooltipSourceType.PlayerSlot);
    }

    private string BuildAiSlotTooltipTitle(int slotIndex)
    {
        if (!IsSlotIndexValid(slotIndex))
        {
            return string.Empty;
        }

        ActionType actionType = revealedAiActions[slotIndex];
        return actionType == ActionType.None
            ? ResolveTooltipTitle(aiHiddenSlotTooltip, string.Format("AI Slot {0}", slotIndex + 1))
            : GetActionTooltipTitle(actionType, TooltipSourceType.AiSlot);
    }

    private string BuildAiSlotTooltipBody(int slotIndex)
    {
        if (!IsSlotIndexValid(slotIndex))
        {
            return string.Empty;
        }

        ActionType actionType = revealedAiActions[slotIndex];
        return actionType == ActionType.None
            ? ResolveTooltipBody(aiHiddenSlotTooltip, "This AI card is still hidden. It will be revealed after the player confirms the round.")
            : GetActionTooltipBody(actionType, TooltipSourceType.AiSlot);
    }

    private bool IsSlotIndexValid(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < BattlePlan.SlotCount;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        if (target == null)
        {
            return null;
        }

        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    private void UpdateTitle()
    {
        if (titleText != null)
        {
            titleText.text = currentRequest != null ? currentRequest.Title : "波波攒对抗演练";
        }

        if (playerNameText != null && currentRequest != null) playerNameText.text = currentRequest.PlayerName;
        if (aiNameText != null && currentRequest != null) aiNameText.text = currentRequest.AiName;
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
