using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 摄像头线索拖放目标
/// 当摄像头线索被拖放到此组件时，显示displayName并更新screen显示对应时间的监控画面
/// </summary>
public class CameraDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("显示组件")]
    [Tooltip("camera对象上显示displayName的文本组件")]
    [SerializeField] private TextMeshProUGUI cameraNameText;

    [Tooltip("screen对象上显示监控画面的Image组件")]
    [SerializeField] private Image screenImage;

    [Header("时间选择器")]
    [Tooltip("Month下拉框")]
    [SerializeField] private TMP_Dropdown monthDropdown;

    [Tooltip("Date下拉框")]
    [SerializeField] private TMP_Dropdown dateDropdown;

    [Tooltip("Hour下拉框")]
    [SerializeField] private TMP_Dropdown hourDropdown;

    [Header("默认显示")]
    [Tooltip("无数据时显示的图像")]
    [SerializeField] private Sprite noDataSprite;

    [Header("高亮效果")]
    [Tooltip("拖拽悬停时的高亮颜色")]
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.6f, 1f, 0.3f);

    [Tooltip("高亮显示的Image（可选）")]
    [SerializeField] private Image highlightImage;

    [Header("调试")]
    [Tooltip("是否显示可点击区域的边框")]
    [SerializeField] private bool showClickableAreas = false;

    [Tooltip("边框颜色")]
    [SerializeField] private Color borderColor = new Color(0f, 1f, 0f, 0.8f);

    [Tooltip("边框宽度（像素）")]
    [SerializeField] private float borderWidth = 2f;

    private Color _originalColor;
    private bool _isHighlighted;
    private CameraClueData _currentCameraClue;
    private CameraFrameView? _currentFrameView; // 缓存当前显示的帧数据
    private readonly List<GameObject> _debugBorders = new List<GameObject>(); // 调试边框对象列表

    private void Awake()
    {
        if (highlightImage != null)
        {
            _originalColor = highlightImage.color;
        }

        // 确保 screenImage 可以接收点击事件
        if (screenImage != null)
        {
            screenImage.raycastTarget = true;
            
            // 在 screenImage 上添加点击转发器，转发到本对象
            var forwarder = screenImage.GetComponent<ClickEventForwarder>();
            if (forwarder == null)
            {
                forwarder = screenImage.gameObject.AddComponent<ClickEventForwarder>();
                Debug.Log("[CameraDropTarget] 已在 screenImage 上添加 ClickEventForwarder");
            }
        }
    }

    /// <summary>
    /// 在编辑器中修改参数时调用（实时更新调试边框）
    /// </summary>
    private void OnValidate()
    {
#if UNITY_EDITOR
        // 延迟调用，确保在编辑器更新后执行
        if (Application.isPlaying && _currentFrameView.HasValue)
        {
            UnityEngine.Object context = this;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (context != null)
                {
                    UpdateDebugBorders();
                }
            };
        }
#endif
    }

    /// <summary>
    /// 当有对象被拖放到此处时调用（Unity EventSystem 标准接口）
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // 这个方法由 Unity EventSystem 在标准拖放流程中调用
        // 但我们使用自定义的 OnClueDrop 方法，因为 DraggableClueItem 手动检测
        ClearHighlight();
    }

    /// <summary>
    /// 当线索被拖放到此处时调用（由 DraggableClueItem 调用）
    /// </summary>
    public void OnClueDrop(ClueData clue)
    {
        if (clue == null)
        {
            Debug.LogWarning("[CameraDropTarget] 拖放的线索为空");
            return;
        }

        // 检查是否为摄像头类型线索
        if (clue is CameraClueData cameraClue)
        {
            _currentCameraClue = cameraClue;
            UpdateDisplay();
            Debug.Log($"[CameraDropTarget] 成功加载摄像头线索: {clue.displayName}");
        }
        else
        {
            
            screenImage.sprite = noDataSprite;
            Debug.Log($"[CameraDropTarget] 拖放的不是摄像头线索，类型: {clue.GetType().Name}");
        }

        ClearHighlight();
    }

    /// <summary>
    /// 更新显示（camera名称和screen画面）
    /// </summary>
    private void UpdateDisplay()
    {
        if (_currentCameraClue == null)
        {
            return;
        }

        // 更新camera文本显示displayName
        if (cameraNameText != null)
        {
            cameraNameText.text = _currentCameraClue.displayName;
        }
        else
        {
            Debug.LogWarning("[CameraDropTarget] cameraNameText 未配置");
        }

        // 获取当前时间
        CameraTime currentTime = GetCurrentTime();

        // 获取对应时间的监控画面
        CameraFrameView frameView = _currentCameraClue.GetFrameOrDefault(currentTime);
        _currentFrameView = frameView; // 缓存当前帧

        // 更新screen显示
        if (screenImage != null)
        {
            if (frameView.image != null)
            {
                screenImage.sprite = frameView.image;
                Debug.Log($"[CameraDropTarget] 显示监控画面，时间: {currentTime}");
            }           
        }
        else
        {
            Debug.LogWarning("[CameraDropTarget] screenImage 未配置");
        }

        // 更新调试边框
        UpdateDebugBorders();
    }

    /// <summary>
    /// 从下拉框获取当前时间
    /// </summary>
    private CameraTime GetCurrentTime()
    {
        int month = 1;
        int day = 1;
        int hour = 0;

        if (monthDropdown != null)
        {
            month = monthDropdown.value + 1;
        }
        else
        {
            Debug.LogWarning("[CameraDropTarget] monthDropdown 未配置，使用默认值 1");
        }

        if (dateDropdown != null)
        {
            day = dateDropdown.value + 1;
        }
        else
        {
            Debug.LogWarning("[CameraDropTarget] dateDropdown 未配置，使用默认值 1");
        }

        if (hourDropdown != null)
        {
            hour = hourDropdown.value + 1;
        }
        else
        {
            Debug.LogWarning("[CameraDropTarget] hourDropdown 未配置，使用默认值 0");
        }

        return new CameraTime(month, day, hour);
    }

    /// <summary>
    /// 外部调用：刷新显示（例如当时间下拉框改变时）
    /// </summary>
    public void RefreshDisplay()
    {
        if (_currentCameraClue != null)
        {
            UpdateDisplay();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 检查是否有正在拖拽的线索
        if (DraggableClueItem.CurrentDragging != null)
        {
            // 只有摄像头线索才显示高亮
            var clueData = DraggableClueItem.CurrentDragging.ClueData;
            if (clueData is CameraClueData)
            {
                ShowHighlight();
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHighlight();
    }

    private void ShowHighlight()
    {
        if (_isHighlighted)
        {
            return;
        }

        _isHighlighted = true;

        if (highlightImage != null)
        {
            highlightImage.color = highlightColor;
        }
    }

    private void ClearHighlight()
    {
        if (!_isHighlighted)
        {
            return;
        }

        _isHighlighted = false;

        if (highlightImage != null)
        {
            highlightImage.color = _originalColor;
        }
    }

    /// <summary>
    /// 当点击监控画面时调用（Unity EventSystem 标准接口）
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 检查前置条件
        if (_currentCameraClue == null || !_currentFrameView.HasValue)
        {
            return;
        }
        
        if (screenImage == null || screenImage.sprite == null)
        {
            return;
        }
        
        // 获取当前帧的可点击区域
        var areas = _currentFrameView.Value.areas;
        if (areas == null || areas.Count == 0)
        {
            return;
        }
        
        // 转换坐标到归一化坐标 (0-1)
        Vector2 normalizedPos;
        if (!TryGetNormalizedPosition(eventData, out normalizedPos))
        {
            return;
        }
        
        // 检测点击区域并收集线索
        TryCollectClueFromAreas(areas, normalizedPos);
    }

    /// <summary>
    /// 将屏幕点击坐标转换为归一化坐标 (0-1)
    /// </summary>
    private bool TryGetNormalizedPosition(PointerEventData eventData, out Vector2 normalizedPos)
    {
        normalizedPos = Vector2.zero;
        
        RectTransform rectTransform = screenImage.rectTransform;
        if (rectTransform == null)
        {
            return false;
        }
        
        // 转换屏幕坐标到RectTransform本地坐标
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint))
        {
            return false;
        }
        
        // 转换为归一化坐标 (0-1)
        Rect rect = rectTransform.rect;
        float normalizedX = (localPoint.x - rect.xMin) / rect.width;
        float normalizedY = (localPoint.y - rect.yMin) / rect.height;
        
        // 边界检查
        if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1)
        {
            return false;
        }
        
        normalizedPos = new Vector2(normalizedX, normalizedY);
        return true;
    }

    /// <summary>
    /// 检测点击区域并收集对应的线索
    /// </summary>
    private void TryCollectClueFromAreas(IReadOnlyList<ClickableArea> areas, Vector2 normalizedPos)
    {
        foreach (var area in areas)
        {
            if (area == null || area.rect == null)
            {
                continue;
            }
            
            // 检测点击位置是否在区域内
            if (area.rect.Contains(normalizedPos))
            {
                // 检查线索数据有效性
                if (area.reveals == null)
                {
                    Debug.LogWarning($"[CameraDropTarget] ClickableArea 在位置 {normalizedPos} 的 reveals 为空");
                    return;
                }
                
                // 收集线索
                if (ClueManager.instance != null)
                {
                    bool success = ClueManager.instance.RevealClue(area.reveals.id);
                    if (success)
                    {
                        Debug.Log($"[CameraDropTarget] 成功收集线索: {area.reveals.displayName} (ID: {area.reveals.id})");
                    }
                    else
                    {
                        Debug.Log($"[CameraDropTarget] 线索已存在: {area.reveals.displayName}");
                    }
                }
                else
                {
                    Debug.LogWarning("[CameraDropTarget] ClueManager.instance 为空");
                }
                
                // 找到匹配区域后立即返回
                return;
            }
        }
        
        // 未点击到任何可点击区域
        Debug.Log("[CameraDropTarget] 点击位置未命中任何可点击区域");
    }

    /// <summary>
    /// 更新调试边框显示
    /// </summary>
    private void UpdateDebugBorders()
    {
        // 先清除所有现有边框
        ClearDebugBorders();

        // 如果开关关闭，或没有当前帧，直接返回
        if (!showClickableAreas || !_currentFrameView.HasValue)
        {
            return;
        }

        if (screenImage == null)
        {
            return;
        }

        var areas = _currentFrameView.Value.areas;
        if (areas == null || areas.Count == 0)
        {
            return;
        }

        // 为每个可点击区域创建边框
        foreach (var area in areas)
        {
            if (area == null || area.rect == null)
            {
                continue;
            }

            CreateDebugBorder(area.rect);
        }
    }

    /// <summary>
    /// 创建单个边框对象
    /// </summary>
    private void CreateDebugBorder(Rect normalizedRect)
    {
        if (screenImage == null)
        {
            return;
        }

        RectTransform screenRect = screenImage.rectTransform;
        if (screenRect == null)
        {
            return;
        }

        // 创建边框容器
        GameObject borderObj = new GameObject($"DebugBorder_{_debugBorders.Count}");
        borderObj.transform.SetParent(screenRect, false);

        RectTransform borderRect = borderObj.AddComponent<RectTransform>();

        // 计算实际位置和尺寸
        Rect screenLocalRect = screenRect.rect;
        float width = normalizedRect.width * screenLocalRect.width;
        float height = normalizedRect.height * screenLocalRect.height;
        
        // 锚点已设置在左下角，所以 anchoredPosition 直接是相对于左下角的偏移
        float x = normalizedRect.x * screenLocalRect.width;
        float y = normalizedRect.y * screenLocalRect.height;

        // 设置锚点为左下角
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.zero;
        borderRect.pivot = Vector2.zero;

        // 设置位置和尺寸
        borderRect.anchoredPosition = new Vector2(x, y);
        borderRect.sizeDelta = new Vector2(width, height);

        // 创建四条边
        CreateBorderLine(borderRect, "Top", new Vector2(0, 1), new Vector2(1, 1), borderWidth);
        CreateBorderLine(borderRect, "Bottom", new Vector2(0, 0), new Vector2(1, 0), borderWidth);
        CreateBorderLine(borderRect, "Left", new Vector2(0, 0), new Vector2(0, 1), borderWidth);
        CreateBorderLine(borderRect, "Right", new Vector2(1, 0), new Vector2(1, 1), borderWidth);

        _debugBorders.Add(borderObj);
    }

    /// <summary>
    /// 创建单条边框线
    /// </summary>
    private void CreateBorderLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float thickness)
    {
        GameObject lineObj = new GameObject($"Line_{name}");
        lineObj.transform.SetParent(parent, false);

        RectTransform lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = anchorMin;
        lineRect.anchorMax = anchorMax;
        lineRect.pivot = new Vector2(0.5f, 0.5f);

        // 根据线的方向设置尺寸
        if (name == "Top" || name == "Bottom")
        {
            // 水平线
            lineRect.sizeDelta = new Vector2(0, thickness);
        }
        else
        {
            // 垂直线
            lineRect.sizeDelta = new Vector2(thickness, 0);
        }

        // 添加Image组件显示边框
        Image lineImage = lineObj.AddComponent<Image>();
        lineImage.color = borderColor;
        lineImage.raycastTarget = false; // 不阻挡射线
    }

    /// <summary>
    /// 清除所有调试边框
    /// </summary>
    private void ClearDebugBorders()
    {
        foreach (var border in _debugBorders)
        {
            if (border != null)
            {
                Destroy(border);
            }
        }
        _debugBorders.Clear();
    }

    /// <summary>
    /// 组件销毁时清理边框
    /// </summary>
    private void OnDestroy()
    {
        ClearDebugBorders();
    }
}

