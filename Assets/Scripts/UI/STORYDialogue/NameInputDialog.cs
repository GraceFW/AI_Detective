using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 起名弹窗管理器
/// 用于在对话中插入起名弹窗节点
/// </summary>
public class NameInputDialog : MonoBehaviour
{
    public static NameInputDialog Instance { get; private set; }
    
    [Header("UI引用")]
    [Tooltip("弹窗根对象（包含背景遮罩和对话框）")]
    [SerializeField] private GameObject dialogRoot;
    
    [Tooltip("背景遮罩（半透明黑色）")]
    [SerializeField] private Image backgroundMask;
    
    [Tooltip("对话框面板（居中显示）")]
    [SerializeField] private GameObject dialogPanel;
    
    [Tooltip("标题文本（可选）")]
    [SerializeField] private TextMeshProUGUI titleText;
    
    [Tooltip("输入框")]
    [SerializeField] private TMP_InputField inputField;
    
    [Tooltip("确认按钮")]
    [SerializeField] private Button confirmButton;
    
    [Header("设置")]
    [Tooltip("背景遮罩颜色")]
    [SerializeField] private Color maskColor = new Color(0, 0, 0, 0.7f);
    
    [Tooltip("实际保存的名字（无论输入什么，都使用这个名字）")]
    [SerializeField] private string actualPlayerName = "工藤新一";
    
    // 确认回调
    private System.Action<string> onConfirmCallback;
    
    // 当前是否显示中
    private bool isShowing = false;
    
    // 防止文本替换时的递归调用
    private bool isUpdatingText = false;
    
    // 记录玩家输入的字符数（用于显示"工藤新一"的切片）
    private int inputCharacterCount = 0;
    
    // 记录上一次显示的文本（用于计算字符数变化）
    private string previousDisplayText = "";
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // 初始隐藏弹窗
        if (dialogRoot != null)
        {
            dialogRoot.SetActive(false);
        }
        
        // 设置背景遮罩颜色
        if (backgroundMask != null)
        {
            backgroundMask.color = maskColor;
        }
        
        // 绑定确认按钮
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmButtonClick);
        }
        
        // 绑定输入框回车键确认
        if (inputField != null)
        {
            inputField.onSubmit.AddListener((text) => OnConfirmButtonClick());
            
            // 监听输入框文本变化，无论输入什么，都显示"工藤新一"
            inputField.onValueChanged.AddListener(OnInputFieldValueChanged);
        }
    }
    
    /// <summary>
    /// 显示起名弹窗
    /// </summary>
    /// <param name="onConfirm">确认回调，参数为实际保存的名字</param>
    public void Show(System.Action<string> onConfirm = null)
    {
        if (isShowing)
        {
            Debug.LogWarning("[NameInputDialog] 弹窗已在显示中，忽略重复调用");
            return;
        }
        
        if (dialogRoot == null)
        {
            Debug.LogError("[NameInputDialog] dialogRoot未配置，无法显示弹窗");
            onConfirm?.Invoke(actualPlayerName);
            return;
        }
        
        isShowing = true;
        onConfirmCallback = onConfirm;
        
        // 显示弹窗
        dialogRoot.SetActive(true);
        
        // 初始化输入框，显示空字符串
        if (inputField != null)
        {
            inputCharacterCount = 0;
            previousDisplayText = "";
            isUpdatingText = true;
            inputField.text = "";
            isUpdatingText = false;
            inputField.Select();
            inputField.ActivateInputField();
        }
        
        Debug.Log("[NameInputDialog] 显示起名弹窗");
    }
    
    /// <summary>
    /// 隐藏起名弹窗
    /// </summary>
    public void Hide()
    {
        if (!isShowing)
        {
            return;
        }
        
        isShowing = false;
        
        if (dialogRoot != null)
        {
            dialogRoot.SetActive(false);
        }
        
        // 清空输入框
        if (inputField != null)
        {
            inputCharacterCount = 0;
            previousDisplayText = "";
            inputField.text = "";
        }
        
        Debug.Log("[NameInputDialog] 隐藏起名弹窗");
    }
    
    /// <summary>
    /// 确认按钮点击处理
    /// </summary>
    private void OnConfirmButtonClick()
    {
        if (!isShowing)
        {
            return;
        }
        
        // 无论输入什么，实际保存的名字都是「工藤新一」
        string savedName = actualPlayerName;
        
        Debug.Log($"[NameInputDialog] 玩家输入：{(inputField != null ? inputField.text : "无")}，实际保存：{savedName}");
        
        // 触发回调
        onConfirmCallback?.Invoke(savedName);
        onConfirmCallback = null;
        
        // 隐藏弹窗
        Hide();
    }
    
    /// <summary>
    /// 检查弹窗是否正在显示
    /// </summary>
    public bool IsShowing()
    {
        return isShowing;
    }
    
    /// <summary>
    /// 获取实际保存的玩家名字
    /// </summary>
    public string GetActualPlayerName()
    {
        return actualPlayerName;
    }
    
    /// <summary>
    /// 输入框文本变化处理
    /// 根据玩家输入的字符数，显示"工藤新一"的对应切片
    /// </summary>
    private void OnInputFieldValueChanged(string newText)
    {
        // 防止递归调用
        if (isUpdatingText)
        {
            return;
        }
        
        // 计算字符数变化（使用 previousDisplayText 而不是 inputField.text，因为 inputField.text 可能已经被更新）
        int previousLength = previousDisplayText.Length;
        int currentLength = newText.Length;
        
        // 判断是输入还是删除
        if (currentLength > previousLength)
        {
            // 输入字符：字符数增加
            inputCharacterCount++;
        }
        else if (currentLength < previousLength)
        {
            // 删除字符（退格）：字符数减少
            inputCharacterCount = Mathf.Max(0, inputCharacterCount - 1);
        }
        // 如果长度相同，可能是替换操作，不改变字符数
        
        // 限制字符数不超过"工藤新一"的长度
        int maxLength = actualPlayerName.Length;
        if (inputCharacterCount > maxLength)
        {
            inputCharacterCount = maxLength;
        }
        
        // 根据字符数显示"工藤新一"的切片
        string displayText = GetNameSlice(inputCharacterCount);
        
        // 如果显示的文本与玩家输入的文本不同，更新输入框
        if (newText != displayText)
        {
            isUpdatingText = true;
            int caretPos = inputField.caretPosition;
            inputField.text = displayText;
            previousDisplayText = displayText;
            
            // 调整光标位置
            // 如果是输入，光标在末尾；如果是删除，光标在删除后的位置
            if (currentLength > previousLength)
            {
                // 输入：光标在末尾
                inputField.caretPosition = displayText.Length;
                inputField.selectionAnchorPosition = displayText.Length;
                inputField.selectionFocusPosition = displayText.Length;
            }
            else if (currentLength < previousLength)
            {
                // 删除：光标保持在删除后的位置（但不能超过文本长度）
                int newCaretPos = Mathf.Min(caretPos, displayText.Length);
                inputField.caretPosition = newCaretPos;
                inputField.selectionAnchorPosition = newCaretPos;
                inputField.selectionFocusPosition = newCaretPos;
            }
            else
            {
                // 长度相同：保持光标位置
                int newCaretPos = Mathf.Min(caretPos, displayText.Length);
                inputField.caretPosition = newCaretPos;
                inputField.selectionAnchorPosition = newCaretPos;
                inputField.selectionFocusPosition = newCaretPos;
            }
            
            isUpdatingText = false;
        }
        else
        {
            // 如果文本相同，更新 previousDisplayText
            previousDisplayText = displayText;
        }
    }
    
    /// <summary>
    /// 根据字符数获取"工藤新一"的切片
    /// </summary>
    /// <param name="count">字符数</param>
    /// <returns>对应的切片文本</returns>
    private string GetNameSlice(int count)
    {
        if (count <= 0)
        {
            return "";
        }
        
        int maxLength = actualPlayerName.Length;
        int sliceLength = Mathf.Min(count, maxLength);
        
        // 对于中文字符，直接使用 Substring
        // 因为中文字符在 C# 中每个字符占一个 char（UTF-16）
        return actualPlayerName.Substring(0, sliceLength);
    }
}

