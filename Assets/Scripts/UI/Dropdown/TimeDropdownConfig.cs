using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 时间类型枚举（日/时/分）
/// </summary>
public enum TimeType
{
    Day,    // 日（默认1-31）
    Hour,   // 时（默认0-23）
    Minute  // 分（默认0-59）
}

/// <summary>
/// 通用时间下拉栏配置脚本（支持日/时/分）
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))] // 自动添加 TMP_Dropdown 组件，避免漏绑
public class TimeDropdownConfig : MonoBehaviour
{
    [Header("基础配置")]
    [Tooltip("下拉栏对应的时间类型")]
    [SerializeField] private TimeType _timeType; // 时间类型（日/时/分）
    
    [Tooltip("自定义最小值（留空则用默认值）")]
    [SerializeField] private int _customMinValue; // 自定义最小值
    [Tooltip("自定义最大值（留空则用默认值）")]
    [SerializeField] private int _customMaxValue; // 自定义最大值
    
    [Tooltip("显示格式（如：{0:D2}分 → 01分；{0}日 → 1日）")]
    [SerializeField] private string _displayFormat = "{0:D2}"; // 默认两位数显示

    private TMP_Dropdown _timeDropdown; // 下拉栏组件引用
    private int _defaultMin; // 默认最小值
    private int _defaultMax; // 默认最大值

    void Awake()
    {
        // 获取下拉栏组件（RequireComponent 确保存在）
        _timeDropdown = GetComponent<TMP_Dropdown>();
        
        // 根据时间类型设置默认范围
        SetDefaultRange();
        
        // 初始化下拉选项
        InitTimeDropdown();

    }

    /// <summary>
    /// 根据时间类型设置默认数值范围
    /// </summary>
    private void SetDefaultRange()
    {
        switch (_timeType)
        {
            case TimeType.Day:
                _defaultMin = 1;
                _defaultMax = 31;
                _displayFormat = string.IsNullOrEmpty(_displayFormat) ? "{0}日" : _displayFormat;
                break;
            case TimeType.Hour:
                _defaultMin = 0;
                _defaultMax = 23;
                _displayFormat = string.IsNullOrEmpty(_displayFormat) ? "{0:D2}时" : _displayFormat;
                break;
            case TimeType.Minute:
                _defaultMin = 0;
                _defaultMax = 59;
                _displayFormat = string.IsNullOrEmpty(_displayFormat) ? "{0:D2}分" : _displayFormat;
                break;
        }
    }

    /// <summary>
    /// 初始化时间下拉栏选项
    /// </summary>
    public void InitTimeDropdown()
    {
        // 清空原有选项
        _timeDropdown.ClearOptions();

        // 确定最终的数值范围（优先使用自定义值，无则用默认值）
        int minValue = _customMinValue != 0 ? _customMinValue : _defaultMin;
        int maxValue = _customMaxValue != 0 ? _customMaxValue : _defaultMax;

        // 校验范围合法性
        if (minValue > maxValue)
        {
            Debug.LogError($"[{_timeType}] 最小值({minValue})大于最大值({maxValue})，请检查配置！");
            return;
        }

        // 生成选项列表
        List<TMP_Dropdown.OptionData> optionList = new List<TMP_Dropdown.OptionData>();
        for (int i = minValue; i <= maxValue; i++)
        {
            // 按格式拼接显示文本
            string optionText = string.Format(_displayFormat, i);
            optionList.Add(new TMP_Dropdown.OptionData(optionText));
        }

        // 赋值并刷新显示
        _timeDropdown.options = optionList;
        _timeDropdown.value = 0; // 默认选中第一个选项
        _timeDropdown.RefreshShownValue();
    }

    /// <summary>
    /// 外部动态修改时间范围
    /// </summary>
    /// <param name="newMin">新最小值</param>
    /// <param name="newMax">新最大值</param>
    public void UpdateTimeRange(int newMin, int newMax)
    {
        _customMinValue = newMin;
        _customMaxValue = newMax;
        InitTimeDropdown();
    }
}
