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
    [SerializeField] private Image backgroundImage;
    

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
            ApplyClueColor(null);
            return;
        }

        ClueId = clue.id;
        if (nameText != null) nameText.text = clue.displayName;
        ApplyClueColor(clue);

        // 同步绑定到拖拽组件
        if (_draggable != null && clue != null)
        {
            var draggableType = _draggable.GetType();
            var bindMethod = draggableType.GetMethod("Bind", new[] { typeof(ClueData) });
            bindMethod?.Invoke(_draggable, new object[] { clue });
        }

		// 注册到引导系统
		RegisterToGuide();
	}

	private void HandleButtonClicked()
    {
        OnClicked?.Invoke(_clue);
    }

    private void ApplyClueColor(ClueData clue)
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.color = GetColorByClueType(clue);
    }

    private static Color32 GetColorByClueType(ClueData clue)
    {
        if (clue is NormalClueData)
        {
            return new Color32(0x20, 0x20, 0x73, 0xFF);
        }

        if (clue is PersonClueData)
        {
            return new Color32(0xC0, 0x78, 0x46, 0xFF);
        }

        if (clue is CameraClueData)
        {
            return new Color32(0x4A, 0x4A, 0x4A, 0xFF);
        }

        return new Color32(0x20, 0x20, 0x73, 0xFF);
    }

	// 引导相关
	private void RegisterToGuide()
	{
		if (!string.IsNullOrEmpty(ClueId))
		{
			GuideTargetRegistry.Instance.Register(ClueId, transform as RectTransform);
		}
	}
}
