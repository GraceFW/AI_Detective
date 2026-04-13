using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 波波攒小游戏的调试入口组件。
/// 可以挂在任意场景物体上使用，支持三种常见调试方式：
/// 1. 挂在 Button 所在物体上，自动监听按钮点击
/// 2. 挂在普通空物体上，然后把 OpenBattle 绑到任意按钮的 OnClick
/// 3. 直接在 Inspector 里使用 ContextMenu 打开小游戏
/// </summary>
[DisallowMultipleComponent]
public class BoboBattleDebugEntry : MonoBehaviour
{
    [Header("Button Binding")]
    [Tooltip("可选。如果不手动指定，且当前物体上存在 Button，则会自动绑定该 Button。")]
    [SerializeField] private Button triggerButton;

    [Tooltip("当 triggerButton 为空时，是否自动尝试获取当前物体上的 Button。")]
    [SerializeField] private bool autoBindSelfButton = true;

    [Header("Battle Config")]
    [SerializeField] private string panelTitle = "波波攒调试";
    [SerializeField] private string playerName = "玩家";
    [SerializeField] private string aiName = "镜像AI";
    [SerializeField] private int startingHP = 3;
    [SerializeField] private int startingEnergy = 0;
    [SerializeField] private string sourceTag = "debug_entry";

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

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
            triggerButton.onClick.RemoveListener(OpenBattle);
        }
    }

    /// <summary>
    /// 提供给 Button.OnClick 直接绑定的公开方法。
    /// </summary>
    public void OpenBattle()
    {
        BoboBattleRequest request = new BoboBattleRequest();
        request.Title = panelTitle;
        request.PlayerName = playerName;
        request.AiName = aiName;
        request.StartingHP = Mathf.Max(1, startingHP);
        request.StartingEnergy = Mathf.Max(0, startingEnergy);
        request.SourceTag = sourceTag;
        request.OnCompleted = HandleBattleCompleted;

        bool opened = BoboBattleService.Open(request);
        if (!opened && verboseLog)
        {
            Debug.LogWarning("[BoboBattleDebugEntry] 波波攒面板当前不可重复打开。");
        }
    }

    [ContextMenu("调试：打开波波攒")]
    private void OpenBattleFromContextMenu()
    {
        OpenBattle();
    }

    private void HandleBattleCompleted(BoboBattleSessionResult result)
    {
        if (!verboseLog)
        {
            return;
        }

        if (result == null)
        {
            Debug.LogWarning("[BoboBattleDebugEntry] 小游戏结束，但未收到结果对象。");
            return;
        }

        string finalState = "无最终快照";
        if (result.FinalModel != null && result.FinalModel.Player != null && result.FinalModel.AI != null)
        {
            finalState = string.Format(
                "Player(HP={0}, EN={1}) / AI(HP={2}, EN={3})",
                result.FinalModel.Player.HP,
                result.FinalModel.Player.Energy,
                result.FinalModel.AI.HP,
                result.FinalModel.AI.Energy
            );
        }

        Debug.Log(string.Format(
            "[BoboBattleDebugEntry] 小游戏结束。Winner={0}, Cancelled={1}, CompletedRounds={2}, FinalState={3}",
            result.Winner,
            result.WasCancelled,
            result.CompletedRounds,
            finalState
        ));
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

        triggerButton.onClick.RemoveListener(OpenBattle);
        triggerButton.onClick.AddListener(OpenBattle);
    }
}
