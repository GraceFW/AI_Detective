using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 线索列表项 UI
/// 支持点击和拖拽功能
/// </summary>
public class ClueListItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button button;

    private ClueData _clue;
    private Component _draggable; // 使用 Component 类型避免编译顺序问题

    public string ClueId { get; private set; }
    public ClueData Clue => _clue;

    public event Action<ClueData> OnClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(HandleButtonClicked);
        }

        // 确保有 CanvasGroup（DraggableClueItem 需要）
        if (GetComponent<CanvasGroup>() == null)
        {
            gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        // 延迟初始化拖拽组件，确保所有脚本都已编译
        InitializeDraggable();
    }

    private void InitializeDraggable()
    {
        // 使用反射获取或添加拖拽组件，避免编译顺序问题
        // 尝试从当前程序集中查找类型
        var draggableType = System.Type.GetType("DraggableClueItem, Assembly-CSharp");
        if (draggableType == null)
        {
            // 如果找不到，尝试从所有已加载的程序集中查找
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                draggableType = assembly.GetType("DraggableClueItem");
                if (draggableType != null) break;
            }
        }
        
        if (draggableType == null)
        {
            Debug.LogWarning("ClueListItemUI: DraggableClueItem 类型未找到，可能需要重新编译 Unity 项目。");
            return;
        }

        _draggable = GetComponent(draggableType);
        if (_draggable == null)
        {
            _draggable = gameObject.AddComponent(draggableType);
        }

        // 如果组件已存在且有线索数据，同步绑定
        if (_draggable != null && _clue != null)
        {
            var bindMethod = draggableType.GetMethod("Bind", new[] { typeof(ClueData) });
            bindMethod?.Invoke(_draggable, new object[] { _clue });
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleButtonClicked);
        }
    }

    public void Bind(ClueData clue)
    {
        _clue = clue;

        if (clue == null)
        {
            ClueId = null;
            if (nameText != null) nameText.text = string.Empty;
            return;
        }

        ClueId = clue.id;
        if (nameText != null) nameText.text = clue.displayName;

        // 同步绑定到拖拽组件
        if (_draggable != null && clue != null)
        {
            var draggableType = _draggable.GetType();
            var bindMethod = draggableType.GetMethod("Bind", new[] { typeof(ClueData) });
            bindMethod?.Invoke(_draggable, new object[] { clue });
        }
    }

    private void HandleButtonClicked()
    {
        OnClicked?.Invoke(_clue);
    }
}
