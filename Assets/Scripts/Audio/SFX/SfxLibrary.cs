using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音效配置库（ScriptableObject）
/// 在 Inspector 中配置所有音效条目，运行时建立 Dictionary 索引
/// </summary>
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Audio/Sfx Library", order = 1)]
public class SfxLibrary : ScriptableObject
{
    [Tooltip("音效配置列表")]
    [SerializeField] private List<SfxEntry> entries = new List<SfxEntry>();

    // 运行时索引字典
    private Dictionary<SfxId, SfxEntry> _entryDict;

    /// <summary>
    /// 获取音效配置（首次调用时建立索引）
    /// </summary>
    public SfxEntry Get(SfxId id)
    {
        if (_entryDict == null)
        {
            BuildDictionary();
        }

        if (_entryDict.TryGetValue(id, out var entry))
        {
            return entry;
        }

        Debug.LogWarning($"[SfxLibrary] 未找到音效配置: {id}");
        return null;
    }

    /// <summary>
    /// 建立运行时索引字典
    /// </summary>
    private void BuildDictionary()
    {
        _entryDict = new Dictionary<SfxId, SfxEntry>();

        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("[SfxLibrary] entries 列表为空，请在 Inspector 中配置音效");
            return;
        }

        foreach (var entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            if (_entryDict.ContainsKey(entry.id))
            {
                Debug.LogWarning($"[SfxLibrary] 发现重复的音效 ID: {entry.id}，将覆盖之前的配置");
            }

            _entryDict[entry.id] = entry;
        }

        Debug.Log($"[SfxLibrary] 已建立索引，共 {_entryDict.Count} 个音效配置");
    }

    /// <summary>
    /// 验证配置（Editor 中使用）
    /// </summary>
    private void OnValidate()
    {
        // 在 Editor 中验证配置
        if (entries != null)
        {
            var idSet = new HashSet<SfxId>();
            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    if (idSet.Contains(entry.id))
                    {
                        Debug.LogWarning($"[SfxLibrary] 发现重复的音效 ID: {entry.id}");
                    }
                    else
                    {
                        idSet.Add(entry.id);
                    }
                }
            }
        }
    }
}

