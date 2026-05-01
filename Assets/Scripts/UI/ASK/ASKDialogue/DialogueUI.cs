using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 对话 UI 管理器
///
/// 核心职责：
/// 1. 管理人物名字、头像、对话文本、选项按钮、导航按钮等 UI 显示
/// 2. 在显示对话文本前，调用 DialogueTextPreprocessor 对原始文本做预处理
/// 3. 将预处理后的富文本交给 TypewriterEffect 或 TMP 直接显示
/// 4. 处理选项点击、历史翻页等 UI 事件
///
/// 设计边界：
/// - 本类不再自行实现关键词扫描和 <link> 注入
/// - 本类不再维护“关键词 -> 线索ID”的本地缓存
/// - 交互文本的自动标注统一交给 DialogueTextPreprocessor / InteractiveTextMarkupBuilder
/// - 文本的 hover / click 交互统一交给 InteractiveTextView
///
/// 这样做的好处：
/// - DialogueUI 只负责“显示”
/// - 文本构建逻辑从 UI 层剥离，职责更清晰
/// - 后续扩展 NPC / 地点 / 术语点击时，无需再修改 DialogueUI
/// </summary>
public class DialogueUI : MonoBehaviour
{
	[Header("基础 UI 组件")]
	[Tooltip("显示人物名字的 TMP 文本")]
	[SerializeField] private TextMeshProUGUI nameText;

	[Tooltip("显示人物头像的 Image")]
	[SerializeField] private Image portraitImage;

	[Tooltip("显示对话正文的 TMP 文本")]
	[SerializeField] private TextMeshProUGUI dialogueText;

	[Header("交互文本预处理")]
	[Tooltip("关键词数据库：用于将原始对话文本中的关键词自动处理为可点击 link")]
	[SerializeField] private CaseKeywordDatabase keywordDatabase;

	[Header("选项 UI")]
	[Tooltip("选项按钮容器")]
	[SerializeField] private Transform choiceContainer;

	[Tooltip("选项按钮预制体")]
	[SerializeField] private GameObject choiceButtonPrefab;

	[Header("导航按钮（可选）")]
	[Tooltip("上一条按钮（历史回看）")]
	[SerializeField] private Button prevButton;

	[Tooltip("下一条按钮（历史回看）")]
	[SerializeField] private Button nextButton;

	[Header("对话框按钮（可选）")]
	[Tooltip("对话框点击区域按钮。当前版本中不是必须字段，保留用于兼容旧场景结构。")]
	[SerializeField] private Button dialogueBoxButton;

	[Header("Root Interaction Lock")]
	[Tooltip("用于整体控制该对话 UI 区域是否可交互")]
	[SerializeField] private CanvasGroup rootCanvasGroup;

	/// <summary>
	/// 所属对话控制器
	/// </summary>
	private DialogueController _dialogueController;

	/// <summary>
	/// 当前对话正文上的打字机效果组件
	/// </summary>
	private TypewriterEffect _typewriterEffect;

	/// <summary>
	/// 当前动态生成的选项按钮实例列表
	/// 用于后续统一清理。
	/// </summary>
	private readonly List<GameObject> _currentChoiceButtons = new List<GameObject>();

	private void Awake()
	{
		// 获取同物体上的 DialogueController
		_dialogueController = GetComponent<DialogueController>();

		// 缓存打字机组件，避免 ShowDialogue / ClearDialogue 中反复 GetComponent
		if (dialogueText != null)
		{
			_typewriterEffect = dialogueText.GetComponent<TypewriterEffect>();
		}

		// 上一条按钮：绑定音效组件和点击事件
		if (prevButton != null)
		{
			if (prevButton.GetComponent<PlaySfxOnClick>() == null)
			{
				prevButton.gameObject.AddComponent<PlaySfxOnClick>();
			}

			prevButton.onClick.AddListener(OnPrevButtonClick);
		}

		// 下一条按钮：绑定音效组件和点击事件
		if (nextButton != null)
		{
			if (nextButton.GetComponent<PlaySfxOnClick>() == null)
			{
				nextButton.gameObject.AddComponent<PlaySfxOnClick>();
			}

			nextButton.onClick.AddListener(OnNextButtonClick);
		}

		// 对话框点击区域：推进下一句（无选项时）/ 打字机加速
		if (dialogueBoxButton != null)
		{
			if (dialogueBoxButton.GetComponent<PlaySfxOnClick>() == null)
			{
				dialogueBoxButton.gameObject.AddComponent<PlaySfxOnClick>();
			}

			dialogueBoxButton.onClick.AddListener(OnDialogueBoxClick);
		}

		// 初始化 UI 状态
		ClearDialogue();
	}

	private void OnDestroy()
	{
		// 解绑按钮事件，避免引用残留
		if (prevButton != null)
		{
			prevButton.onClick.RemoveListener(OnPrevButtonClick);
		}

		if (nextButton != null)
		{
			nextButton.onClick.RemoveListener(OnNextButtonClick);
		}

		if (dialogueBoxButton != null)
		{
			dialogueBoxButton.onClick.RemoveListener(OnDialogueBoxClick);
		}
	}

	/// <summary>
	/// 显示人物信息
	///
	/// 功能：
	/// - 设置人物名字
	/// - 设置人物头像
	/// - 若头像存在，则启用头像显示
	/// </summary>
	/// <param name="personName">人物名</param>
	/// <param name="portrait">人物头像</param>
	public void ShowPerson(string personName, Sprite portrait)
	{
		if (nameText != null)
		{
			nameText.text = personName ?? string.Empty;
		}

		if (portraitImage != null)
		{
			if (portrait != null)
			{
				portraitImage.sprite = portrait;
				portraitImage.enabled = true;
			}
			else
			{
				portraitImage.enabled = false;
			}
		}

		Debug.Log($"[DialogueUI] 显示人物：{personName}");
	}

	/// <summary>
	/// 显示对话文本
	///
	/// 处理流程：
	/// 1. 接收原始对话文本（纯文本）
	/// 2. 通过 DialogueTextPreprocessor 做文本预处理
	///    - 自动根据 CaseKeywordDatabase 将关键词构建为 TMP <link>
	/// 3. 根据 useTypewriter 决定：
	///    - 使用打字机显示
	///    - 或直接完整显示
	///
	/// 说明：
	/// - hasOptions 参数当前保留，用于与现有调用接口兼容
	/// - 若后续需要“有选项时禁用空白点击推进”等行为，可以在别处结合该参数使用
	/// </summary>
	/// <param name="text">原始对话文本（建议为纯文本）</param>
	/// <param name="hasOptions">该句是否带选项（当前仅保留接口语义）</param>
	/// <param name="useTypewriter">是否使用打字机效果。默认 true；历史浏览等场景可传 false</param>
	public void ShowDialogue(string text, bool hasOptions, bool useTypewriter = true)
	{
		if (dialogueText == null)
		{
			Debug.LogWarning("[DialogueUI] dialogueText 未配置，无法显示对话文本。");
			return;
		}

		string rawText = text ?? string.Empty;

		// 统一使用预处理器构建最终富文本
		string processedText = DialogueTextPreprocessor.Process(rawText, keywordDatabase);

		// 若需要打字机，并且组件存在，则走打字机
		if (_typewriterEffect != null && useTypewriter)
		{
			_typewriterEffect.SetText(processedText);
		}
		else
		{
			// 如果不使用打字机，则直接完整显示
			// 注意：不能用 processedText.Length 作为 maxVisibleCharacters
			// 因为富文本标签会影响字符串长度，但不会计入可见字符数
			if (_typewriterEffect != null)
			{
				// 先清理打字机状态，避免残留
				_typewriterEffect.Clear();
			}

			dialogueText.text = processedText;
			dialogueText.ForceMeshUpdate();

			// 这里使用 TMP 计算出的可见字符数，确保富文本也能完整显示
			dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
		}

		Debug.Log(
			$"[DialogueUI] 显示对话：{rawText.Substring(0, Mathf.Min(20, rawText.Length))}..." +
			$"（打字机={useTypewriter}，有选项={hasOptions}）");
	}

	/// <summary>
	/// 显示选项
	///
	/// 功能：
	/// - 清空旧选项
	/// - 根据 DialogueOption 列表动态生成按钮
	/// - 绑定按钮文本和点击事件
	/// - 支持历史浏览模式：历史模式下选项仅展示，不可点击
	/// </summary>
	/// <param name="options">选项列表</param>
	/// <param name="isHistoryView">是否为历史浏览模式。若为 true，则按钮不可点击</param>
	public void ShowOptions(List<DialogueOption> options, bool isHistoryView = false)
	{
		// 先清空旧选项，避免残留
		ClearOptions();

		if (options == null || options.Count == 0)
		{
			return;
		}

		if (choiceContainer == null)
		{
			Debug.LogWarning("[DialogueUI] choiceContainer 未配置，无法显示选项。");
			return;
		}

		if (choiceButtonPrefab == null)
		{
			Debug.LogWarning("[DialogueUI] choiceButtonPrefab 未配置，无法显示选项。");
			return;
		}

		for (int i = 0; i < options.Count; i++)
		{
			DialogueOption option = options[i];
			GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceContainer);
			_currentChoiceButtons.Add(buttonObj);

			// 设置按钮文本
			TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
			if (buttonText != null)
			{
				buttonText.text = option != null ? option.optionText : string.Empty;
			}

			// 设置按钮交互
			Button button = buttonObj.GetComponent<Button>();
			if (button != null)
			{
				button.interactable = !isHistoryView;

				if (!isHistoryView)
				{
					// 为选项按钮绑定点击音效组件
					PlaySfxOnClick playSfx = buttonObj.GetComponent<PlaySfxOnClick>();
					if (playSfx == null)
					{
						playSfx = buttonObj.AddComponent<PlaySfxOnClick>();
					}

					int optionIndex = i; // 闭包捕获
					button.onClick.AddListener(() => OnOptionClick(optionIndex));
				}
			}

			buttonObj.SetActive(true);
		}

		Debug.Log($"[DialogueUI] 显示 {options.Count} 个选项（历史浏览={isHistoryView}）");
	}

	/// <summary>
	/// 清空当前所有选项按钮
	///
	/// 说明：
	/// - 当前版本采用 Destroy 直接销毁
	/// - 若后续选项刷新非常频繁，可进一步优化为对象池
	/// </summary>
	public void ClearOptions()
	{
		for (int i = 0; i < _currentChoiceButtons.Count; i++)
		{
			if (_currentChoiceButtons[i] != null)
			{
				Destroy(_currentChoiceButtons[i]);
			}
		}

		_currentChoiceButtons.Clear();
	}

	/// <summary>
	/// 更新历史导航按钮状态
	/// </summary>
	/// <param name="canPrev">是否允许上一条</param>
	/// <param name="canNext">是否允许下一条</param>
	public void UpdateNavigationButtons(bool canPrev, bool canNext)
	{
		if (prevButton != null)
		{
			prevButton.interactable = canPrev;
		}

		if (nextButton != null)
		{
			nextButton.interactable = canNext;
		}
	}

	/// <summary>
	/// 清空对话区域
	///
	/// 功能：
	/// - 清空人物名字
	/// - 隐藏头像
	/// - 清空对话文本
	/// - 清空选项
	/// - 重置导航按钮状态
	/// </summary>
	public void ClearDialogue()
	{
		if (nameText != null)
		{
			nameText.text = string.Empty;
		}

		if (portraitImage != null)
		{
			portraitImage.enabled = false;
		}

		if (dialogueText != null)
		{
			if (_typewriterEffect != null)
			{
				_typewriterEffect.Clear();
			}
			else
			{
				dialogueText.text = string.Empty;
				dialogueText.maxVisibleCharacters = 0;
			}
		}

		ClearOptions();
		UpdateNavigationButtons(false, false);
	}

	/// <summary>
	/// 选项点击事件
	///
	/// 由选项按钮调用，最终交给 DialogueController 处理。
	/// </summary>
	/// <param name="optionIndex">选项索引</param>
	private void OnOptionClick(int optionIndex)
	{
		if (_dialogueController != null)
		{
			_dialogueController.SelectOption(optionIndex);
		}
		else
		{
			Debug.LogWarning("[DialogueUI] DialogueController 不存在，无法处理选项点击。");
		}
	}

	/// <summary>
	/// 上一条按钮点击事件
	///
	/// 用于历史对话浏览。
	/// </summary>
	private void OnPrevButtonClick()
	{
		if (_dialogueController != null)
		{
			_dialogueController.NavigateHistory(-1);
		}
		else
		{
			Debug.LogWarning("[DialogueUI] DialogueController 不存在，无法处理上一条导航。");
		}
	}

	/// <summary>
	/// 下一条按钮点击事件
	///
	/// 用于历史对话浏览。
	/// </summary>
	private void OnNextButtonClick()
	{
		if (_dialogueController != null)
		{
			_dialogueController.NavigateHistory(1);
		}
		else
		{
			Debug.LogWarning("[DialogueUI] DialogueController 不存在，无法处理下一条导航。");
		}
	}

	private void OnDialogueBoxClick()
	{
		if (_typewriterEffect != null && _typewriterEffect.HandleTypingClick())
		{
			return;
		}

		if (_dialogueController != null)
		{
			_dialogueController.NextDialogue();
		}
		else
		{
			Debug.LogWarning("[DialogueUI] DialogueController 不存在，无法处理对话框点击。");
		}
	}

	/// <summary>
	/// 设置整个对话 UI 是否可交互
	///
	/// 常见用途：
	/// - 插播演出时暂时锁住对话 UI
	/// - 特殊状态下禁用整块对话界面
	///
	/// 行为：
	/// - interactable 控制按钮/Selectable 交互
	/// - blocksRaycasts 控制是否拦截射线
	/// </summary>
	/// <param name="interactable">是否可交互</param>
	public void SetInteractable(bool interactable)
	{
		if (rootCanvasGroup == null)
		{
			Debug.LogWarning("[DialogueUI] rootCanvasGroup 未配置，无法设置交互锁。");
			return;
		}

		rootCanvasGroup.interactable = interactable;
		rootCanvasGroup.blocksRaycasts = interactable;

		// 如需视觉上半透明，可在这里扩展：
		// rootCanvasGroup.alpha = interactable ? 1f : 0.6f;
	}
}