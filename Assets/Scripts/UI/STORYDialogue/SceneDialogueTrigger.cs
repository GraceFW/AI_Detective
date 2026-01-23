using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景对话触发器
/// 监听场景加载完成事件，根据场景名自动匹配对应的关卡对话并触发LevelStart类型的对话
/// </summary>
public class SceneDialogueTrigger : MonoBehaviour
{
    [Header("事件监听")]
    [Tooltip("场景加载完成事件（从SceneManager的_loadedSceneEvent_1获取）")]
    [SerializeField] private GameSceneEventSO sceneLoadedEvent;
    
    [Header("场景映射配置")]
    [Tooltip("场景名到关卡编号的映射表")]
    [SerializeField] private List<SceneLevelMapping> sceneMappings = new List<SceneLevelMapping>();
    
    [Header("触发设置")]
    [Tooltip("是否只对Level类型的场景触发对话")]
    [SerializeField] private bool onlyTriggerForLevelScenes = true;
    
    [Tooltip("是否强制弹出对话（强制弹出会中断玩家操作）")]
    [SerializeField] private bool isForced = true;
    
    [Tooltip("触发延迟（秒，场景加载后等待多久再触发对话）")]
    [SerializeField] private float triggerDelay = 0.5f;
    
    [Header("调试")]
    [Tooltip("是否输出详细日志")]
    [SerializeField] private bool verboseLogging = true;
    
    private void OnEnable()
    {
        // 订阅场景加载完成事件
        if (sceneLoadedEvent != null)
        {
            sceneLoadedEvent.OnEventRaised += OnSceneLoaded;
            if (verboseLogging)
            {
                Debug.Log("[SceneDialogueTrigger] 已订阅场景加载事件");
            }
        }
        else
        {
            Debug.LogWarning("[SceneDialogueTrigger] sceneLoadedEvent未配置！请在Inspector中配置场景加载事件。");
        }
    }
    
    private void OnDisable()
    {
        // 取消订阅，避免内存泄漏
        if (sceneLoadedEvent != null)
        {
            sceneLoadedEvent.OnEventRaised -= OnSceneLoaded;
            if (verboseLogging)
            {
                Debug.Log("[SceneDialogueTrigger] 已取消订阅场景加载事件");
            }
        }
    }
    
    /// <summary>
    /// 场景加载完成事件处理
    /// </summary>
    /// <param name="loadedScene">加载完成的场景SO</param>
    private void OnSceneLoaded(GameSceneSO loadedScene)
    {
        if (loadedScene == null)
        {
            Debug.LogWarning("[SceneDialogueTrigger] 接收到的场景SO为空");
            return;
        }
        
        if (verboseLogging)
        {
            Debug.Log($"[SceneDialogueTrigger] 场景加载完成：{loadedScene.name}, 类型={loadedScene.sceneType}");
        }
        
        // 检查是否应该触发对话
        if (!ShouldTriggerDialogue(loadedScene))
        {
            if (verboseLogging)
            {
                Debug.Log($"[SceneDialogueTrigger] 场景 {loadedScene.name} 不符合触发条件，跳过对话");
            }
            return;
        }
        
        // 延迟触发对话（如果设置了延迟）
        if (triggerDelay > 0)
        {
            StartCoroutine(TriggerDialogueDelayed(loadedScene));
        }
        else
        {
            TriggerDialogueForScene(loadedScene);
        }
    }
    
    /// <summary>
    /// 判断是否应该触发对话
    /// </summary>
    private bool ShouldTriggerDialogue(GameSceneSO scene)
    {
        // 检查场景类型过滤
        if (onlyTriggerForLevelScenes)
        {
            if (scene.sceneType != SceneType.Level)
            {
                if (verboseLogging)
                {
                    Debug.Log($"[SceneDialogueTrigger] 场景类型不匹配：期望Level，实际{scene.sceneType}");
                }
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 延迟触发对话
    /// </summary>
    private IEnumerator TriggerDialogueDelayed(GameSceneSO scene)
    {
        yield return new WaitForSeconds(triggerDelay);
        TriggerDialogueForScene(scene);
    }
    
    /// <summary>
    /// 为指定场景触发对话
    /// </summary>
    private void TriggerDialogueForScene(GameSceneSO scene)
    {
        // 获取场景名称（使用SO资源名称）
        string sceneName = scene.name;
        
        if (verboseLogging)
        {
            Debug.Log($"[SceneDialogueTrigger] 尝试为场景 {sceneName} 触发对话");
        }
        
        // 查找对应的关卡编号
        int? levelNumber = FindLevelNumberBySceneName(sceneName);
        
        if (!levelNumber.HasValue)
        {
            Debug.LogWarning($"[SceneDialogueTrigger] 未找到场景 {sceneName} 的映射配置。请检查场景映射表。");
            return;
        }
        
        if (verboseLogging)
        {
            Debug.Log($"[SceneDialogueTrigger] 找到映射：场景 {sceneName} → 关卡 {levelNumber.Value}");
        }
        
        // 触发对话
        TriggerDialogueForLevel(levelNumber.Value);
    }
    
    /// <summary>
    /// 根据场景名查找对应的关卡编号
    /// </summary>
    private int? FindLevelNumberBySceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return null;
        }
        
        // 遍历映射表查找匹配的场景
        foreach (var mapping in sceneMappings)
        {
            if (mapping.Matches(sceneName))
            {
                return mapping.levelNumber;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 为指定关卡触发LevelStart对话
    /// </summary>
    private void TriggerDialogueForLevel(int levelNumber)
    {
        // 检查DialogueManager是否存在
        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[SceneDialogueTrigger] DialogueManager.Instance未找到！请确保DialogueManager已初始化。");
            return;
        }
        
        // 验证DialogueData是否存在
        if (DialogueManager.Instance.dialogueDataList == null || DialogueManager.Instance.dialogueDataList.Count == 0)
        {
            Debug.LogWarning("[SceneDialogueTrigger] DialogueManager的dialogueDataList为空！请配置对话数据。");
            return;
        }
        
        // 查找对应关卡的DialogueData
        DialogueData dialogueData = DialogueManager.Instance.dialogueDataList.Find(
            d => d != null && d.levelNumber == levelNumber
        );
        
        if (dialogueData == null)
        {
            Debug.LogWarning($"[SceneDialogueTrigger] 未找到关卡 {levelNumber} 的DialogueData。请检查DialogueManager的dialogueDataList配置。");
            return;
        }
        
        // 验证LevelStart对话是否存在
        DialogueSequence levelStartSequence = dialogueData.GetDialogueSequence(DialogueTriggerType.LevelStart);
        
        if (levelStartSequence == null || levelStartSequence.entries == null || levelStartSequence.entries.Length == 0)
        {
            Debug.LogWarning($"[SceneDialogueTrigger] 关卡 {levelNumber} 的DialogueData中没有配置LevelStart类型的对话序列。");
            return;
        }
        
        if (verboseLogging)
        {
            Debug.Log($"[SceneDialogueTrigger] 找到对话数据：{dialogueData.name}，LevelStart对话条目数：{levelStartSequence.entries.Length}");
        }
        
        // 触发对话
        DialogueManager.Instance.ShowDialogue(
            levelNumber: levelNumber,
            triggerType: DialogueTriggerType.LevelStart,
            waveNumber: 0,
            onComplete: null,
            isForced: isForced
        );
        
        if (verboseLogging)
        {
            Debug.Log($"[SceneDialogueTrigger] 已触发关卡 {levelNumber} 的LevelStart对话");
        }
    }
    
    /// <summary>
    /// 手动触发指定场景的对话（用于测试）
    /// </summary>
    [ContextMenu("测试：触发当前场景对话")]
    public void TestTriggerCurrentScene()
    {
        // 尝试从SceneManager获取当前场景
        SceneManager sceneManager = FindObjectOfType<SceneManager>();
        if (sceneManager != null)
        {
            // 通过反射或公共字段获取currentScene（如果可用）
            // 这里使用一个简化的方法：提示用户配置
            Debug.LogWarning("[SceneDialogueTrigger] 测试功能：请在Inspector中查看场景映射表配置");
        }
    }
    
    /// <summary>
    /// 手动触发指定关卡的对话（用于测试）
    /// </summary>
    /// <param name="levelNumber">关卡编号</param>
    [ContextMenu("测试：触发关卡0对话")]
    public void TestTriggerLevel0()
    {
        TriggerDialogueForLevel(0);
    }
    
    /// <summary>
    /// 获取场景映射表的调试信息
    /// </summary>
    public string GetMappingsDebugInfo()
    {
        if (sceneMappings == null || sceneMappings.Count == 0)
        {
            return "场景映射表为空";
        }
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"场景映射表（共{sceneMappings.Count}项）：");
        
        for (int i = 0; i < sceneMappings.Count; i++)
        {
            var mapping = sceneMappings[i];
            string status = mapping.enabled ? "✓" : "✗";
            sb.AppendLine($"  [{i}] {status} {mapping.sceneName} → Level {mapping.levelNumber}");
        }
        
        return sb.ToString();
    }
}

