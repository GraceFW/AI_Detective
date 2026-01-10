using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 摄像头线索拖放目标
/// 当摄像头线索被拖放到此组件时，显示displayName并更新screen显示对应时间的监控画面
/// </summary>
public class CameraDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
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

    private Color _originalColor;
    private bool _isHighlighted;
    private CameraClueData _currentCameraClue;

    private void Awake()
    {
        if (highlightImage != null)
        {
            _originalColor = highlightImage.color;
        }
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
            hour = hourDropdown.value;
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
}

