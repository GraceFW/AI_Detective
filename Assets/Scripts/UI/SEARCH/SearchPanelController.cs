using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 搜索面板控制器
/// 处理 /detect、/sniff 和 /attack 指令
/// 支持单框(Detect/Sniff)与双框(Attack)自动切换
/// </summary>
public class SearchPanelController : MonoBehaviour
{
	[Header("UI 组件")]
	[Tooltip("命令下拉框 (TMP_Dropdown)")]
	[SerializeField] private TMP_Dropdown commandDropdown;

	[Tooltip("Detect指令输入框 (TMP_InputField)")]
	[SerializeField] private TMP_InputField detectInput;

	[Tooltip("Attack指令输入框 - 线索名 (双框A)")]
	[SerializeField] private TMP_InputField attackInputA;

	[Tooltip("Attack指令输入框 - 秘钥 (双框B)")]
	[SerializeField] private TMP_InputField attackInputB;

	[Tooltip("结果显示文本 (TextMeshProUGUI)")]
	[SerializeField] private TextMeshProUGUI resultText;

	[Tooltip("滚动视图 (ScrollRect) - 用于自动滚动到底部")]
	[SerializeField] private ScrollRect scrollRect;

	[Header("数据")]
	[Tooltip("线索数据库")]
	[SerializeField] private ClueDatabaseSO clueDatabase;

	[Tooltip("关键词=>线索 索引库")]
	[SerializeField] private CaseKeywordDatabase caseKeyword;

	[Tooltip("\"执行中...\" 显示时长")]
	[SerializeField] private float executingDuration = 0.8f;

	[Tooltip("滚动到底部的延迟时间（秒），用于等待文本更新")]
	[SerializeField] private float scrollDelay = 0.05f;

	private Coroutine _displayCoroutine;
	private StringBuilder _historyLog = new StringBuilder();
	private string _lastDisplayedText = string.Empty;  // 记录上次显示的文本，用于追加模式
	private bool _shouldClearOnNextUpdate = false;  // 标记下次更新是否应该清空（Detect 命令）
	// private int _sniffUsageCount = 0;  // Sniff使用次数统计（场景级别，不存档）

	private enum CommandType
	{
		Detect = 0,
		Attack = 1,
		Sniff = 2
	}

	private void Start()
	{
		InitializeDropdown();

		// 绑定单框提交事件
		if (detectInput != null)
		{
			detectInput.onSubmit.AddListener(OnSubmit);
		}

		// 绑定双框提交事件
		if (attackInputA != null)
		{
			attackInputA.onSubmit.AddListener(OnAttackSubmitA);
		}
		if (attackInputB != null)
		{
			attackInputB.onSubmit.AddListener(OnAttackSubmitB);
		}

		// 绑定下拉栏变化事件
		if (commandDropdown != null)
		{
			commandDropdown.onValueChanged.AddListener(OnCommandDropdownChanged);
		}

		if (resultText != null)
		{
			// 检查是否有打字机效果组件
			TypewriterEffect typewriterEffect = resultText.GetComponent<TypewriterEffect>();
			if (typewriterEffect != null)
			{
				typewriterEffect.Clear();
			}
			else
			{
				resultText.text = string.Empty;
			}
			_lastDisplayedText = string.Empty;
		}

		// 如果没有手动指定 ScrollRect，尝试自动查找
		if (scrollRect == null)
		{
			scrollRect = GetComponentInParent<ScrollRect>();
			if (scrollRect == null && resultText != null)
			{
				scrollRect = resultText.GetComponentInParent<ScrollRect>();
			}
		}

		// 初始化输入框状态（默认Detect单框）
		SetInputFieldState(CommandType.Detect);
	}

	private void OnDestroy()
	{
		if (detectInput != null)
		{
			detectInput.onSubmit.RemoveListener(OnSubmit);
		}

		// 移除双框事件
		if (attackInputA != null)
		{
			attackInputA.onSubmit.RemoveListener(OnAttackSubmitA);
		}
		if (attackInputB != null)
		{
			attackInputB.onSubmit.RemoveListener(OnAttackSubmitB);
		}

		// 移除下拉栏事件
		if (commandDropdown != null)
		{
			commandDropdown.onValueChanged.RemoveListener(OnCommandDropdownChanged);
		}
	}

	private void InitializeDropdown()
	{
		if (commandDropdown == null)
		{
			return;
		}

		commandDropdown.ClearOptions();
		commandDropdown.AddOptions(new System.Collections.Generic.List<string>
		{
			"/Detect",
			"/Attack"
		});
		commandDropdown.value = 0;
		commandDropdown.RefreshShownValue();
	}

	/// <summary>
	/// 下拉栏选项变化时切换输入框状态
	/// </summary>
	private void OnCommandDropdownChanged(int index)
	{
		CommandType command = (CommandType)index;
		SetInputFieldState(command);
	}

	/// <summary>
	/// 控制单框/双框的显示/隐藏与激活
	/// </summary>
	private void SetInputFieldState(CommandType command)
	{
		bool isSingleMode = command == CommandType.Detect || command == CommandType.Sniff;
		bool isAttackMode = command == CommandType.Attack;

		// 单框状态
		if (detectInput != null)
		{
			detectInput.gameObject.SetActive(isSingleMode);
			if (isSingleMode)
			{
				detectInput.ActivateInputField();
				detectInput.text = string.Empty;
			}
		}

		// 双框状态
		if (attackInputA != null)
		{
			attackInputA.gameObject.SetActive(isAttackMode);
			if (isAttackMode)
			{
				attackInputA.ActivateInputField();
				attackInputA.text = string.Empty;
			}
		}
		if (attackInputB != null)
		{
			attackInputB.gameObject.SetActive(isAttackMode);
			if (isAttackMode)
			{
				attackInputB.text = string.Empty;
			}
		}
	}

	/// <summary>
	/// 当按下回车时调用（单框模式）
	/// </summary>
	private void OnSubmit(string inputText)
	{
		SubmitSingleInput(inputText, clearInputAfterSubmit: true, isManualSubmit: true);
	}

	/// <summary>
	/// 当在 Attack 的 A 输入框按下回车时调用。
	/// </summary>
	private void OnAttackSubmitA(string inputText)
	{
		SubmitAttackInput(raiseGuideSubmitForInputB: false);
	}

	/// <summary>
	/// 当在 Attack 的 B 输入框按下回车时调用。
	/// 不管秘钥是否正确，只要这是一次手动提交，都应该通知 guide 系统。
	/// </summary>
	private void OnAttackSubmitB(string inputText)
	{
		SubmitAttackInput(raiseGuideSubmitForInputB: true);
	}

	private void SubmitAttackInput(bool raiseGuideSubmitForInputB)
	{
		if (commandDropdown == null || clueDatabase == null)
		{
			Debug.LogError("SearchPanelController: commandDropdown 或 clueDatabase 未配置。");
			return;
		}

		var command = (CommandType)commandDropdown.value;
		if (command != CommandType.Attack)
		{
			return;
		}

		ExecuteCommand(command, string.Empty);

		if (raiseGuideSubmitForInputB)
		{
			string inputKey = attackInputB != null ? attackInputB.text : string.Empty;
			GuideInputSubmitEventBus.Raise(ResolveInputTargetKey(attackInputB), inputKey?.Trim() ?? string.Empty, true);
		}
	}

	/// <summary>
	/// 执行命令
	/// </summary>
	private void ExecuteCommand(CommandType command, string searchText)
	{
		if (_displayCoroutine != null)
		{
			StopCoroutine(_displayCoroutine);
		}

		_displayCoroutine = StartCoroutine(ExecuteCommandCoroutine(command, searchText));
	}

	/// <summary>
	/// 外部调用：提交当前单输入框命令。
	/// </summary>
	public bool SubmitCurrentSingleInput(bool clearInputAfterSubmit, bool isManualSubmit, string overrideTargetKey = null)
	{
		string currentText = detectInput != null ? detectInput.text : string.Empty;
		return SubmitSingleInput(currentText, clearInputAfterSubmit, isManualSubmit, overrideTargetKey);
	}

	private bool SubmitSingleInput(string inputText, bool clearInputAfterSubmit, bool isManualSubmit, string overrideTargetKey = null)
	{
		if (commandDropdown == null || clueDatabase == null)
		{
			Debug.LogError("SearchPanelController: commandDropdown 或 clueDatabase 未配置。");
			return false;
		}

		var command = (CommandType)commandDropdown.value;
		if (command == CommandType.Attack)
		{
			return false;
		}

		var searchText = inputText?.Trim() ?? string.Empty;
		ExecuteCommand(command, searchText);
		GuideInputSubmitEventBus.Raise(ResolveSingleInputTargetKey(overrideTargetKey), searchText, isManualSubmit);

		if (clearInputAfterSubmit && detectInput != null)
		{
			detectInput.text = string.Empty;
		}

		return true;
	}

	private IEnumerator ExecuteCommandCoroutine(CommandType command, string searchText)
	{
		//  Detect/Sniff/Attack 指令时清空文本窗当前显示的内容
		if (command == CommandType.Detect || command == CommandType.Sniff || command == CommandType.Attack)
		{
			_historyLog.Clear();
			_lastDisplayedText = string.Empty;
			_shouldClearOnNextUpdate = true;
			UpdateResultText();
		}

		string commandStr = command switch
		{
			CommandType.Detect => "/detect",
			CommandType.Sniff => "/sniff",
			CommandType.Attack => "/attack",
			_ => "/detect"
		};

		// 构建输入行（Attack显示双框输入）
		string inputLine;
		if (command == CommandType.Attack)
		{
			string clueName = attackInputA?.text?.Trim() ?? string.Empty;
			string key = attackInputB?.text?.Trim() ?? string.Empty;
			inputLine = $"> {commandStr} {clueName} {key}\n";
		}
		else
		{
			inputLine = $"> {commandStr} {searchText}\n";
		}

		// 添加输入行到历史
		_historyLog.Append(inputLine);

		// 显示 "执行中..."
		var executingLine = "执行中...\n";
		_historyLog.Append(executingLine);
		UpdateResultText();
		// 不再手动调用 ScrollToBottom，打字机效果（TypewriterEffect.cs）会自己处理滚动

		yield return new WaitForSeconds(executingDuration);

		// 搜索线索（Attack从双框A获取线索名）
		ClueData clue;
		if (command == CommandType.Attack)
		{
			string clueName = attackInputA?.text?.Trim() ?? string.Empty;
			clue = clueDatabase.SearchByDisplayName(clueName);
		}
		else
		{
			clue = clueDatabase.SearchByDisplayName(searchText);
		}

		// 执行命令逻辑
		string resultLine;

		if (command == CommandType.Attack)
		{
			// Attack单独校验输入
			resultLine = ExecuteAttack(clue);
		}
		else if (string.IsNullOrEmpty(searchText))
		{
			// 没有输入文本
			resultLine = "[结果]：未获得数据探针。\n\n";
		}
		else
		{
			resultLine = command switch
			{
				CommandType.Detect => ExecuteDetect(clue),
				// CommandType.Sniff => ExecuteSniff(clue),
				_ => "[结果]：未知命令。\n\n"
			};
		}

		_historyLog.Append(resultLine);
		UpdateResultText();
		// 不再手动调用 ScrollToBottom，打字机效果会自己处理滚动

		Debug.Log($"[Final TMP Text] {resultText.text}");

		_displayCoroutine = null;
	}

	/// <summary>
	/// 执行 /detect 命令（兼容Attack解锁内容）
	/// </summary>
	private string ExecuteDetect(ClueData clue)
	{
		if (clue == null)
		{
			return "[结果]：检定为低关联性信息。\n\n";
		}

		// 检查是否已收集（使用 clue.collected 字段）
		bool isRevealed = clue.collected;
		Debug.Log(clue.displayName + "线索收集状态：" + isRevealed);
		Debug.Log(clue.id + "线索ID：" + clue.id);
		Debug.Log(clue.detectable + "线索可搜索：" + clue.detectable);
		Debug.Log(clue.collectable + "线索可收集：" + clue.collectable);

		// 基础文本构建
		StringBuilder detectResult = new StringBuilder();
		if (isRevealed && ClueManager.instance.IsRevealed(clue.id))
		{
			// 已被收集的线索
			if (clue.detectable)
			{
				// 如果detectable，显示detailText
				// string detail = string.IsNullOrWhiteSpace(clue.detailText) ? clue.summary : clue.detailText;
				// detectResult.AppendLine($"\n{detail}");
				string detail_new = string.IsNullOrWhiteSpace(clue.Detail_Mark) ? clue.summary : clue.Detail_Mark;
				detectResult.AppendLine($"\n{detail_new}");

				// 追加已解锁的Attack内容（如果配置了直接展示）
				if (clue.isAttackContentUnlocked && clue.showAttackContentDirectly && !string.IsNullOrEmpty(clue.attackUnlockContent))
				{
					detectResult.AppendLine("\n【解锁的保护内容】：");
					detectResult.AppendLine(clue.attackUnlockContent);
				}
			}
			else
			{
				detectResult.AppendLine("[结果]：该线索已存在于档案中。");
			}
		}
		else
		{
			// 未收集的线索
			bool shouldShowDetail = clue.detectable;
			bool shouldCollect = clue.collectable;

			if (shouldShowDetail && shouldCollect)
			{
				// searchable且collectable：显示detailText + 收集 + 提示文本
				//string detail = string.IsNullOrWhiteSpace(clue.detailText) ? clue.summary : clue.detailText;
				//detectResult.AppendLine($"\n{detail}");
				string detail_test = string.IsNullOrWhiteSpace(clue.Detail_Mark) ? clue.summary : clue.Detail_Mark;
				detectResult.AppendLine($"\n{detail_test}");
				

				if (ClueManager.instance != null)
				{
					Debug.Log("RevealClue: " + clue.id);
					ClueManager.instance.RevealClue(clue.id);
				}
				else
				{
					Debug.LogWarning("SearchPanelController: ClueManager.instance 为空。");
				}

				// 追加已解锁的Attack内容（如果配置了直接展示）
				if (clue.isAttackContentUnlocked && clue.showAttackContentDirectly && !string.IsNullOrEmpty(clue.attackUnlockContent))
				{
					detectResult.AppendLine("\n【解锁的保护内容】：");
					detectResult.AppendLine(clue.attackUnlockContent);
					detectResult.AppendLine("\n");
				}

				detectResult.AppendLine("[结果]：采集到关联线索。");
			}
			else if (shouldShowDetail && !shouldCollect)
			{
				// 只detectable不collectable：显示detailText（不收集）
				//string detail = string.IsNullOrWhiteSpace(clue.detailText) ? clue.summary : clue.detailText;
				//detectResult.AppendLine($"\n{detail}");
				string detail_test = string.IsNullOrWhiteSpace(clue.Detail_Mark) ? clue.summary : clue.Detail_Mark;
				detectResult.AppendLine($"\n{detail_test}");

				// 这里没有写追加已解锁的Attack内容，因为根据设计，只有collectable的线索才会被RevealClue，从而可能解锁Attack内容
			}
			else if (!shouldShowDetail && shouldCollect)
			{
				// collectable但不detectable：收集 + 提示文本（不显示detailText）
				if (ClueManager.instance != null)
				{
					ClueManager.instance.RevealClue(clue.id);
				}
				else
				{
					Debug.LogWarning("SearchPanelController: ClueManager.instance 为空。");
				}

				detectResult.AppendLine("[结果]：采集到关联线索。");
			}
			else
			{
				// 两个都不满足：既不显示文字也不收集
				detectResult.AppendLine("[结果]：检定为低关联性信息。");
			}
		}

		// 补充换行符保持格式统一
		detectResult.AppendLine("\n");
		return detectResult.ToString();
	}

	#region Sniff功能（已删）
	/// <summary>
	/// 执行 /sniff 命令
	/// </summary>
	//private string ExecuteSniff(ClueData clue)
	//{
	//	if (clue == null)
	//	{
	//		return "[结果]：未获得数据探针。\n\n";
	//	}

	//	// 只对searchable的线索有效
	//	if (!clue.detectable)
	//	{
	//		return "[结果]：未关联到高置信度结果。\n\n";
	//	}

	//	// 增加Sniff使用次数统计
	//	_sniffUsageCount++;

	//	// 检查是否有Detail_Mark
	//	if (!string.IsNullOrWhiteSpace(clue.Detail_Mark))
	//	{
	//		// 有Detail_Mark：显示Detail_Mark
	//		return $"\n{clue.Detail_Mark}\n\n";
	//	}
	//	else
	//	{
	//		// 没有Detail_Mark：输出提示文本
	//		return "[结果]：该线索暂无标记文本。\n\n";
	//	}
	//}
	/// <summary>
	/// 获取Sniff使用次数（场景级别，不存档）
	/// </summary>
	//public int GetSniffUsageCount()
	//{
	//	return _sniffUsageCount;
	//}
	#endregion

	/// <summary>
	/// 执行 /attack 命令（密码验证+解锁新内容）
	/// </summary>
	private string ExecuteAttack(ClueData clue)
	{
		if (clue == null)
		{
			return "[结果]：未找到对应线索，无法执行骇入指令。\n\n";
		}

		// 从双框B获取秘钥
		string inputKey = attackInputB?.text?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(inputKey))
		{
			return "[结果]：未输入相关秘钥，无法验证权限。\n\n";
		}

		// 线索是否支持Attack校验
		if (string.IsNullOrEmpty(clue.attackKey) || string.IsNullOrEmpty(clue.attackUnlockContent))
		{
			return "[结果]：未找到骇入端点。\n\n";
		}

		// 密码验证
		if (inputKey != clue.attackKey)
		{
			return "[结果]：权限验证无效，骇入失败，无法解锁加密内容。\n\n";
		}

		// 密码正确：解锁新内容
		if (clue.collected && !clue.isAttackContentUnlocked)
		{
			clue.isAttackContentUnlocked = true;
			Debug.Log($"线索 [{clue.displayName}] 的Attack内容已解锁");
			return $"[结果]：骇入成功！已解锁新内容：\n{clue.attackUnlockContent}\n\n";
		}
		else if (clue.collected && clue.isAttackContentUnlocked && clue.showAttackContentDirectly)
		{
			// 已解锁，直接展示
			return $"[结果]：该线索加密内容已解锁，内容如下：\n{clue.attackUnlockContent}\n\n";
		}
		else
		{
			clue.isAttackContentUnlocked = true; // 即使之前未收集，只要密码正确也解锁内容（但不展示）
			return "[结果]：骇入成功！\n使用/Detect收集线索以获得完整内容。\n\n";
		}
	}

	private void UpdateResultText()
	{
		if (resultText != null)
		{
			string newText = _historyLog.ToString();

			// 检查是否有打字机效果组件
			TypewriterEffect typewriterEffect = resultText.GetComponent<TypewriterEffect>();
			if (typewriterEffect != null)
			{
				// 使用打字机效果
				if (_shouldClearOnNextUpdate || string.IsNullOrEmpty(_lastDisplayedText))
				{
					// 清空或首次显示：使用 SetText
					typewriterEffect.SetProcessedText(newText, caseKeyword);
					_shouldClearOnNextUpdate = false;
				}
				else
				{
					// 追加模式：计算新增文本
					if (newText.Length > _lastDisplayedText.Length && newText.StartsWith(_lastDisplayedText))
					{
						string addedText = newText.Substring(_lastDisplayedText.Length);
						typewriterEffect.AppendProcessedText(addedText, caseKeyword);
					}
					else
					{
						// 如果文本不匹配（可能是被外部修改），使用 SetText
						typewriterEffect.SetProcessedText(newText, caseKeyword);
					}
				}
			}
			else
			{
				// 没有打字机效果：直接设置文本（兼容旧代码）
				resultText.text = newText;
				resultText.ForceMeshUpdate();
			}

			// 更新最后显示的文本
			_lastDisplayedText = newText;
		}
	}

	/// <summary>
	/// 滚动到底部，显示最新内容
	/// </summary>
	private void ScrollToBottom()
	{
		if (scrollRect == null)
		{
			return;
		}

		// 使用协程延迟滚动，确保文本已更新
		StartCoroutine(ScrollToBottomCoroutine());
	}

	private IEnumerator ScrollToBottomCoroutine()
	{
		// 等待一帧，确保 TextMeshPro 已更新 preferredHeight
		yield return null;

		// 再等待一小段时间，确保布局更新完成
		if (scrollDelay > 0f)
		{
			yield return new WaitForSeconds(scrollDelay);
		}

		// 强制更新 Canvas
		Canvas.ForceUpdateCanvases();

		// 滚动到底部（verticalNormalizedPosition = 0 表示底部）
		if (scrollRect != null)
		{
			scrollRect.verticalNormalizedPosition = 0f;
		}
	}

	/// <summary>
	/// 清空历史记录
	/// </summary>
	public void ClearHistory()
	{
		_historyLog.Clear();
		_lastDisplayedText = string.Empty;
		_shouldClearOnNextUpdate = true;

		// 如果有打字机效果，使用 Clear 方法
		if (resultText != null)
		{
			TypewriterEffect typewriterEffect = resultText.GetComponent<TypewriterEffect>();
			if (typewriterEffect != null)
			{
				typewriterEffect.Clear();
			}
			else
			{
				resultText.text = string.Empty;
			}
		}

		ScrollToBottom();
	}

	/// <summary>
	/// 外部调用：设置搜索输入框的文本（用于拖拽功能）
	/// </summary>
	public void SetSearchText(string text)
	{
		if (commandDropdown == null) return;

		CommandType currentCommand = (CommandType)commandDropdown.value;
		if (currentCommand == CommandType.Detect || currentCommand == CommandType.Sniff)
		{
			if (detectInput != null)
			{
				detectInput.text = text;
				detectInput.ActivateInputField();
			}
		}
		else if (currentCommand == CommandType.Attack)
		{
			// Attack状态下拖拽文本设置到线索名输入框
			if (attackInputA != null)
			{
				attackInputA.text = text;
				attackInputA.ActivateInputField();
			}
		}
	}

	private string ResolveSingleInputTargetKey(string overrideTargetKey)
	{
		if (!string.IsNullOrWhiteSpace(overrideTargetKey))
		{
			return overrideTargetKey;
		}

		if (detectInput == null)
		{
			return string.Empty;
		}

		return ResolveInputTargetKey(detectInput);
	}

	private string ResolveInputTargetKey(TMP_InputField inputField)
	{
		if (inputField == null)
		{
			return string.Empty;
		}

		GuideTarget guideTarget = inputField.GetComponent<GuideTarget>();
		if (guideTarget == null)
		{
			guideTarget = inputField.GetComponentInParent<GuideTarget>();
		}

		return guideTarget != null ? guideTarget.key : string.Empty;
	}

	/// <summary>
	/// 获取搜索输入框的 RectTransform（用于拖拽检测）
	/// </summary>
	public RectTransform GetSearchInputRect()
	{
		if (commandDropdown == null) return null;

		CommandType currentCommand = (CommandType)commandDropdown.value;
		if (currentCommand == CommandType.Detect || currentCommand == CommandType.Sniff)
		{
			return detectInput != null ? detectInput.GetComponent<RectTransform>() : null;
		}
		else
		{
			return attackInputA != null ? attackInputA.GetComponent<RectTransform>() : null;
		}
	}
}
