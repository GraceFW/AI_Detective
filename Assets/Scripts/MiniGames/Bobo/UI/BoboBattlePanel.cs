using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BoboBattlePanel : MonoBehaviour
{
    [Serializable]
    private class ActionButtonBinding
    {
        [SerializeField] private ActionType actionType = ActionType.None;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Graphic selectedFrame;

        public ActionType ActionType => actionType;
        public Button Button => button;
        public Image Background => background;
        public Image IconImage => iconImage;
        public TextMeshProUGUI Label => label;
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
        [SerializeField] private TextMeshProUGUI actionText;
        [SerializeField] private GameObject hiddenRoot;
        [SerializeField] private TextMeshProUGUI hiddenText;

        public Button Button => button;
        public Image Background => background;
        public Graphic HighlightFrame => highlightFrame;
        public Image ActionIcon => actionIcon;
        public TextMeshProUGUI SlotIndexText => slotIndexText;
        public TextMeshProUGUI ActionText => actionText;
        public GameObject HiddenRoot => hiddenRoot;
        public TextMeshProUGUI HiddenText => hiddenText;
    }

    [Serializable]
    private class ActionVisualBinding
    {
        [SerializeField] private ActionType actionType = ActionType.None;
        [SerializeField] private Sprite sprite;

        public ActionType ActionType => actionType;
        public Sprite Sprite => sprite;
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

        controller.StartNewBattle(currentRequest.PlayerName, currentRequest.AiName, currentRequest.StartingHP, currentRequest.StartingEnergy);
        ResetDraft(true);
        UpdateTitle();

        if (resultText != null) resultText.gameObject.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(false);
        if (submitButtonText != null) submitButtonText.text = "确定";

        UpdateStatus("先选左侧动作，再放入下方三张玩家牌位。");

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void CloseAsCancelled()
    {
        CompleteSession(controller == null || controller.Model == null || !controller.Model.IsFinished);
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
        }

        for (int i = 0; i < aiCardSlots.Length; i++)
        {
            CardSlotBinding slot = aiCardSlots[i];
            if (slot != null && slot.SlotIndexText != null)
            {
                slot.SlotIndexText.text = (i + 1).ToString();
            }
        }

        for (int i = 0; i < actionButtons.Length; i++)
        {
            ActionButtonBinding binding = actionButtons[i];
            if (binding == null || binding.Button == null) continue;

            if (binding.Label != null) binding.Label.text = binding.ActionType.GetDisplayName();
            ApplyActionVisual(binding.ActionType, binding.IconImage, binding.Label);

            int capturedIndex = i;
            binding.Button.onClick.RemoveAllListeners();
            binding.Button.onClick.AddListener(() => OnPaletteActionClicked(actionButtons[capturedIndex].ActionType));
        }
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

        for (int i = 0; i < actionButtons.Length; i++)
        {
            if (actionButtons[i] != null) BoboBattleUIFactory.ApplyPreferredFont(actionButtons[i].Label);
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
                valid &= ValidateReference(binding.Label, "ActionButtons[" + i + "].Label");
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
            valid &= ValidateReference(slot.ActionText, fieldName + "[" + i + "].ActionText");
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

        RevealAiPlan(roundResult.AIPlan);

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
            UpdateDraftUi();
            yield return new WaitForSeconds(0.2f);
            ApplyResolveInfo(info);
            UpdateStatus(info.Summary);
            yield return new WaitForSeconds(0.65f);
            resolvingSlotIndex = -1;
            UpdateDraftUi();
        }

        roundAnimationCoroutine = null;
        isAnimating = false;
        resolvingSlotIndex = -1;
        if (closeButton != null) closeButton.interactable = true;

        if (roundResult.IsBattleFinished)
        {
            ApplyEndedState();
        }
        else
        {
            ResetDraft(false);
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
                          controller.RuleSystem.CanAffordAction(energyBefore, binding.ActionType);
            bool isSelected = selectedPaletteAction == binding.ActionType;

            binding.Button.interactable = canUse;
            SetActionButtonColor(binding, isSelected);

            if (binding.Label != null)
            {
                binding.Label.text = binding.ActionType.GetDisplayName();
                binding.Label.fontStyle = isSelected ? FontStyles.Bold | FontStyles.UpperCase : FontStyles.Bold;
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

            if (slot.ActionText != null)
            {
                slot.ActionText.text = actionType == ActionType.None ? "未放置" : actionType.GetDisplayName();
                slot.ActionText.color = actionType == ActionType.None ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            }

            if (slot.HiddenRoot != null) slot.HiddenRoot.SetActive(false);
            ApplyCardActionVisual(slot, actionType);
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
            if (slot.ActionText != null) slot.ActionText.text = revealed ? revealedAiActions[i].GetDisplayName() : string.Empty;

            ApplyCardActionVisual(slot, revealed ? revealedAiActions[i] : ActionType.None);
        }
    }

    private void RevealAiPlan(BattlePlan aiPlan)
    {
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            revealedAiActions[i] = aiPlan[i];
        }

        UpdateDraftUi();
    }

    private void ApplyCardActionVisual(CardSlotBinding slot, ActionType actionType)
    {
        if (slot == null || slot.ActionIcon == null) return;

        Sprite sprite = GetActionSprite(actionType);
        slot.ActionIcon.sprite = sprite;
        slot.ActionIcon.enabled = sprite != null && actionType != ActionType.None;
        slot.ActionIcon.color = actionType == ActionType.None ? Color.clear : actionType.GetThemeColor();
    }

    private void ApplyActionVisual(ActionType actionType, Image iconImage, TextMeshProUGUI fallbackLabel)
    {
        if (iconImage == null) return;

        Sprite sprite = GetActionSprite(actionType);
        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;

        if (sprite == null && fallbackLabel != null)
        {
            fallbackLabel.text = actionType.GetDisplayName();
        }
    }

    private Sprite GetActionSprite(ActionType actionType)
    {
        for (int i = 0; i < actionVisuals.Length; i++)
        {
            if (actionVisuals[i] != null && actionVisuals[i].ActionType == actionType)
            {
                return actionVisuals[i].Sprite;
            }
        }

        return null;
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
            restartButton.gameObject.SetActive(true);
            restartButton.interactable = true;
        }

        if (submitButton != null) submitButton.interactable = false;
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
        StopRoundAnimation();
        Show(currentRequest);
    }

    private void OnCloseClicked()
    {
        bool shouldCancel = controller == null || controller.Model == null || !controller.Model.IsFinished;
        CompleteSession(shouldCancel);
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
