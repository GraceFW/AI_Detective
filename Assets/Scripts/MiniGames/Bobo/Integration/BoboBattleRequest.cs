using System;

/// <summary>
/// 打开小游戏时由外层传入的请求参数。
/// 它把“是谁打开的、显示什么标题、初始数值是多少、结束后如何回调”集中到一个对象里，
/// 这样对话系统、按钮入口和其他系统都可以走同一种调用方式。
/// </summary>
[Serializable]
public class BoboBattleRequest
{
    public string Title = "波波攒对抗演练";
    public string PlayerName = "玩家";
    public string AiName = "镜像AI";
    public int StartingHP = 3;
    public int StartingEnergy = 0;
    public BoboBattleAiMode AiMode = BoboBattleAiMode.Normal;
    public bool AllowRestartAfterEnd = true;
    public bool AllowCancelBeforeEnd = true;
    public bool ShowCloseButton = false;
    public bool AutoCompleteOnBattleEnd = false;
    public string SourceTag = string.Empty;
    public Action<BoboBattleSessionResult> OnCompleted;
}

public enum BoboBattleAiMode
{
    Normal = 0,
    GuaranteedCounter = 1
}
