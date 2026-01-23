using UnityEngine;

/// <summary>
/// 场景到关卡编号的映射数据结构
/// 用于将场景名称映射到对应的关卡编号
/// </summary>
[System.Serializable]
public class SceneLevelMapping
{
    [Tooltip("场景名称（GameSceneSO的资源名称，如\"TestFirstLevel\"）")]
    public string sceneName;
    
    [Tooltip("对应的关卡编号（对应DialogueData中的levelNumber）")]
    public int levelNumber;
    
    [Tooltip("是否启用此映射")]
    public bool enabled = true;
    
    /// <summary>
    /// 检查场景名是否匹配
    /// </summary>
    public bool Matches(string name)
    {
        if (!enabled || string.IsNullOrEmpty(sceneName))
        {
            return false;
        }
        
        // 精确匹配
        return sceneName.Equals(name, System.StringComparison.OrdinalIgnoreCase);
    }
}

