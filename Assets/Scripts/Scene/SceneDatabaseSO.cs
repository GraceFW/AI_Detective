using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景数据库：
/// - 在 Inspector 中配置所有可用的 GameSceneSO
/// - 通过场景名查回对应的 GameSceneSO 原始资源实例
/// </summary>
[CreateAssetMenu(menuName = "GameScene/SceneDatabase")]
public class SceneDatabaseSO : ScriptableObject
{
    private static SceneDatabaseSO _instance;
    public static SceneDatabaseSO Instance
    {
        get
        {
            if (_instance == null)
            {
                // 尝试通过 Resources 查找（可选）
                _instance = Resources.Load<SceneDatabaseSO>("SceneDatabase");
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Tooltip("项目中所有可载入的 GameSceneSO 列表")]
    public List<GameSceneSO> allScenes = new List<GameSceneSO>();

    private void OnEnable()
    {
        // 允许场景中直接引用的这个资源成为单例实例
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            // 如果有多个实例，保留第一个，避免静默覆盖
            Debug.LogWarning("[SceneDatabaseSO] 检测到多个 SceneDatabaseSO 实例，建议项目中只保留一个。");
        }
    }

    /// <summary>
    /// 通过 GameSceneSO 的资源名查找原始资源实例
    /// </summary>
    public GameSceneSO GetSceneByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        for (int i = 0; i < allScenes.Count; i++)
        {
            var s = allScenes[i];
            if (s != null && s.name == name)
            {
                return s;
            }
        }

        Debug.LogWarning($"[SceneDatabaseSO] 未在 allScenes 中找到名为 {name} 的 GameSceneSO");
        return null;
    }
}


