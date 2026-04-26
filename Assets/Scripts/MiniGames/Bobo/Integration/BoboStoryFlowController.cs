using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orchestrates the story-specific Bobo flow:
/// first battle, branch dialogue, special counter battle, final dialogue, then
/// next-chapter scene loading. The battle rules and UI remain owned by the
/// existing Bobo module.
/// </summary>
[DisallowMultipleComponent]
public class BoboStoryFlowController : MonoBehaviour
{
    private static int activeOrScheduledFlowCount;

    public static bool HasActiveOrScheduledFlow => activeOrScheduledFlowCount > 0;

    [Header("Optional Button Binding")]
    [SerializeField] private Button triggerButton;
    [SerializeField] private bool autoBindSelfButton = true;

    [Header("Battle Config")]
    [SerializeField] private string firstBattleTitle = "\u6ce2\u6ce2\u6512\u7b2c\u4e00\u6218";
    [SerializeField] private string specialBattleTitle = "\u4e00\u5c40\u5b9a\u80dc\u8d1f";
    [SerializeField] private string playerNameFallback = "\u73a9\u5bb6";
    [SerializeField] private string aiName = "\u9ed1\u7fbd\u5feb\u6597";

    [Header("Dialogue Config")]
    [SerializeField] private DialogueSequence firstLoseDialogue = new DialogueSequence();
    [SerializeField] private DialogueSequence firstWinToSpecialDialogue = new DialogueSequence();
    [SerializeField] private DialogueSequence specialEndDialogue = new DialogueSequence();

    [Header("Scene Flow")]
    [SerializeField] private GameSceneEventSO loadSceneEvent;
    [SerializeField] private GameSceneSO nextChapterSceneOverride;
    [SerializeField] private bool saveImmediatelyOnCatch = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    private bool isFlowRunning;
    private bool isStartScheduled;
    private string pendingSourceTag;
    private Coroutine scheduledStartCoroutine;

    private void Reset()
    {
        TryAutoBindButton();
    }

    private void Awake()
    {
        TryAutoBindButton();
        RegisterButtonListener();
    }

    private void OnDestroy()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.RemoveListener(StartFlowFromButton);
        }

        if (isFlowRunning || isStartScheduled)
        {
            SetFlowReserved(false);
        }
    }

    public static bool TryToggleAnyFromButton()
    {
        BoboStoryFlowController controller = FindObjectOfType<BoboStoryFlowController>();
        if (controller == null)
        {
            return false;
        }

        controller.StartFlowFromButton();
        return true;
    }

    public bool UsesTriggerButton(Button button)
    {
        return triggerButton != null && triggerButton == button;
    }

    public void ScheduleStartAfterCurrentDialogue(string sourceTag)
    {
        if (isFlowRunning || isStartScheduled)
        {
            Debug.LogWarning("[BoboStoryFlowController] Story flow is already running or scheduled.");
            return;
        }

        pendingSourceTag = sourceTag ?? string.Empty;
        isStartScheduled = true;
        SetFlowReserved(true);

        if (scheduledStartCoroutine != null)
        {
            StopCoroutine(scheduledStartCoroutine);
        }

        scheduledStartCoroutine = StartCoroutine(StartAfterDialogueClosed());
    }

    public void StartFlowNow()
    {
        // The parameterless method is mainly exposed for Button.OnClick.
        // Treat it as a debug toggle so a second click can close the panel.
        StartFlowFromButton();
    }

    public void StartFlowFromButton()
    {
        if (isFlowRunning || isStartScheduled || BoboBattleService.IsCurrentBattleOpen())
        {
            StopFlowFromButton();
            return;
        }

        StartFlowNow("button");
    }

    public void StartFlowNow(string sourceTag)
    {
        if (isFlowRunning)
        {
            Debug.LogWarning("[BoboStoryFlowController] Story flow is already running.");
            return;
        }

        isStartScheduled = false;
        pendingSourceTag = sourceTag ?? string.Empty;
        isFlowRunning = true;
        SetFlowReserved(true);
        BoboBattleService.ForceHideCurrentWithoutCallback();
        OpenFirstBattle();
    }

    private IEnumerator StartAfterDialogueClosed()
    {
        yield return null;

        // CustomAction nodes run while DialogueManager is still active, so wait
        // until the whole sequence closes before opening the minigame panel.
        while (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive())
        {
            yield return null;
        }

        scheduledStartCoroutine = null;
        isStartScheduled = false;
        StartFlowNow(pendingSourceTag);
    }

    private void OpenFirstBattle()
    {
        BoboBattleRequest request = CreateBaseStoryRequest(firstBattleTitle, 3, 0, BoboBattleAiMode.Normal, "story_first");
        request.OnCompleted = HandleFirstBattleCompleted;

        if (!BoboBattleService.Open(request))
        {
            Debug.LogWarning("[BoboStoryFlowController] Failed to open first Bobo battle.");
            AbortFlowWithoutBranch("Failed to open first Bobo battle.");
        }
    }

    private void HandleFirstBattleCompleted(BoboBattleSessionResult result)
    {
        if (result == null || result.WasCancelled)
        {
            AbortFlowWithoutBranch("First Bobo battle was cancelled or returned no result.");
            return;
        }

        bool playerWon = result != null && !result.WasCancelled && result.Winner == BattleWinner.Player;
        if (!playerWon)
        {
            PlayDialogue(firstLoseDialogue, FinishFlowAndLoadNextChapter);
            return;
        }

        PlayDialogue(firstWinToSpecialDialogue, () =>
        {
            SetCatchKaitoKurobaFlag();
            OpenSpecialBattle();
        });
    }

    private void OpenSpecialBattle()
    {
        BoboBattleRequest request = CreateBaseStoryRequest(specialBattleTitle, 1, 1, BoboBattleAiMode.GuaranteedCounter, "story_special");
        request.OnCompleted = HandleSpecialBattleCompleted;

        if (!BoboBattleService.Open(request))
        {
            Debug.LogWarning("[BoboStoryFlowController] Failed to open special Bobo battle.");
            AbortFlowWithoutBranch("Failed to open special Bobo battle.");
        }
    }

    private void HandleSpecialBattleCompleted(BoboBattleSessionResult result)
    {
        PlayDialogue(specialEndDialogue, FinishFlowAndLoadNextChapter);
    }

    private BoboBattleRequest CreateBaseStoryRequest(string title, int hp, int energy, BoboBattleAiMode aiMode, string sourceSuffix)
    {
        BoboBattleRequest request = new BoboBattleRequest();
        request.Title = title;
        request.PlayerName = ResolvePlayerName();
        request.AiName = aiName;
        request.StartingHP = hp;
        request.StartingEnergy = energy;
        request.AiMode = aiMode;
        request.AllowRestartAfterEnd = false;
        request.AllowCancelBeforeEnd = false;
        request.ShowCloseButton = false;
        request.SourceTag = string.IsNullOrEmpty(pendingSourceTag)
            ? sourceSuffix
            : pendingSourceTag + ":" + sourceSuffix;
        return request;
    }

    private void PlayDialogue(DialogueSequence sequence, System.Action onComplete)
    {
        if (!HasDialogue(sequence))
        {
            onComplete?.Invoke();
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[BoboStoryFlowController] DialogueManager is missing. Skipping configured dialogue.");
            onComplete?.Invoke();
            return;
        }

        DialogueManager.Instance.PlaySequence(sequence, onComplete, true);
    }

    private bool HasDialogue(DialogueSequence sequence)
    {
        return sequence != null && sequence.entries != null && sequence.entries.Length > 0;
    }

    private void FinishFlowAndLoadNextChapter()
    {
        BoboBattleService.ForceHideCurrentWithoutCallback();
        isFlowRunning = false;
        SetFlowReserved(false);
        LoadNextChapter();
    }

    private void AbortFlowWithoutBranch(string reason)
    {
        if (verboseLog)
        {
            Debug.LogWarning("[BoboStoryFlowController] Story flow aborted. " + reason);
        }

        BoboBattleService.ForceHideCurrentWithoutCallback();
        isFlowRunning = false;
        isStartScheduled = false;
        SetFlowReserved(false);
        pendingSourceTag = string.Empty;
        if (scheduledStartCoroutine != null)
        {
            StopCoroutine(scheduledStartCoroutine);
            scheduledStartCoroutine = null;
        }
    }

    private void StopFlowFromButton()
    {
        if (verboseLog)
        {
            Debug.Log("[BoboStoryFlowController] Story flow stopped by debug button.");
        }

        if (scheduledStartCoroutine != null)
        {
            StopCoroutine(scheduledStartCoroutine);
            scheduledStartCoroutine = null;
        }

        isFlowRunning = false;
        isStartScheduled = false;
        pendingSourceTag = string.Empty;
        SetFlowReserved(false);
        BoboBattleService.ForceHideCurrentWithoutCallback();
    }

    private void SetFlowReserved(bool reserved)
    {
        if (reserved)
        {
            if (activeOrScheduledFlowCount <= 0)
            {
                activeOrScheduledFlowCount = 1;
            }

            return;
        }

        activeOrScheduledFlowCount = 0;
    }

    private void SetCatchKaitoKurobaFlag()
    {
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("[BoboStoryFlowController] GameManager is missing. Cannot set isCatchKaitoKuroba.");
            return;
        }

        gameManager.isCatchKaitoKuroba = true;
        if (verboseLog)
        {
            Debug.Log("[BoboStoryFlowController] GameManager.isCatchKaitoKuroba set to true.");
        }

        if (saveImmediatelyOnCatch && DataManager.instance != null)
        {
            DataManager.instance.Save();
        }
    }

    private void LoadNextChapter()
    {
        GameSceneSO nextScene = ResolveNextChapterScene();
        if (nextScene == null)
        {
            Debug.LogError("[BoboStoryFlowController] No next chapter scene is configured or discoverable.");
            return;
        }

        if (loadSceneEvent == null)
        {
            Debug.LogError("[BoboStoryFlowController] LoadSceneEvent is not configured.");
            return;
        }

        loadSceneEvent.RaiseEvent(nextScene);
    }

    private GameSceneSO ResolveNextChapterScene()
    {
        if (nextChapterSceneOverride != null)
        {
            return nextChapterSceneOverride;
        }

        SceneManager sceneManager = FindObjectOfType<SceneManager>();
        if (sceneManager == null)
        {
            return null;
        }

        FieldInfo currentSceneField = typeof(SceneManager).GetField(
            "currentScene",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (currentSceneField == null)
        {
            return null;
        }

        GameSceneSO currentScene = currentSceneField.GetValue(sceneManager) as GameSceneSO;
        return currentScene != null ? currentScene.nextLevelScene : null;
    }

    private string ResolvePlayerName()
    {
        if (NameInputDialog.Instance != null)
        {
            return NameInputDialog.Instance.GetActualPlayerName();
        }

        return string.IsNullOrWhiteSpace(playerNameFallback) ? "\u73a9\u5bb6" : playerNameFallback;
    }

    private BoboBattleSessionResult CreateCancelledResult()
    {
        return new BoboBattleSessionResult
        {
            Winner = BattleWinner.None,
            WasCancelled = true,
            CompletedRounds = 0,
            FinalModel = null
        };
    }

    private void TryAutoBindButton()
    {
        if (triggerButton == null && autoBindSelfButton)
        {
            triggerButton = GetComponent<Button>();
        }
    }

    private void RegisterButtonListener()
    {
        if (triggerButton == null)
        {
            return;
        }

        triggerButton.onClick.RemoveListener(StartFlowFromButton);
        if (!HasPersistentButtonBinding())
        {
            triggerButton.onClick.AddListener(StartFlowFromButton);
        }
    }

    private bool HasPersistentButtonBinding()
    {
        if (triggerButton == null)
        {
            return false;
        }

        int eventCount = triggerButton.onClick.GetPersistentEventCount();
        for (int i = 0; i < eventCount; i++)
        {
            if (triggerButton.onClick.GetPersistentTarget(i) != this)
            {
                continue;
            }

            string methodName = triggerButton.onClick.GetPersistentMethodName(i);
            if (methodName == nameof(StartFlowFromButton) || methodName == nameof(StartFlowNow))
            {
                return true;
            }
        }

        return false;
    }
}
