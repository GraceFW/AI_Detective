using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 交互文本视图（最终收口版）
///
/// 核心定位：
/// - 本组件是“对话正文文本区域”的唯一点击入口
/// - 它统一处理：
///   1. 鼠标悬停（hover）
///   2. 鼠标点击（click）
///   3. 打字机进行中的点击转发
///   4. link 点击分发
///   5. 空白点击推进下一句
///
/// 架构职责边界：
/// - 本组件不负责构建富文本（那是 DialogueTextPreprocessor / InteractiveTextMarkupBuilder 的职责）
/// - 本组件不负责打字机内部逻辑（那是 TypewriterEffect 的职责）
/// - 本组件不负责具体业务逻辑（那是 ClueLinkHandler / 其他 handler 的职责）
/// - 本组件负责“输入路由”和“交互协调”
///
/// 统一点击规则：
/// 1. 若当前正在打字：任何点击优先交给 TypewriterEffect 处理（单击加速 / 双击跳过）
/// 2. 若打字结束且点到了 link：交给 handler 处理业务逻辑
/// 3. 若打字结束且没有点到 link：若允许，则推进下一句对话
///
/// 统一 hover 规则：
/// 1. 若启用 hover，并且当前没有打字（或允许打字时 hover）
/// 2. 检测当前鼠标是否位于某个 TMP link 上
/// 3. 若命中：
///    - 调用 hover 视觉层（变色 / 荧光笔底色）
///    - 修改光标
///    - 通知对应 handler 的 OnHoverEnter
/// 4. 若离开：
///    - 清理视觉层
///    - 恢复光标
///    - 通知 handler 的 OnHoverExit
///
/// 适配环境：
/// - 当前项目使用 TextMeshProUGUI
/// - 当前项目 Canvas 为 Screen Space - Overlay
/// - 因此 TMP_TextUtilities.FindIntersectingLink() 的 camera 参数统一传 null
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class InteractiveTextView : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
{
	[Header("References")]
	[Tooltip("目标 TMP 文本组件。通常与本脚本挂在同一对象上。")]
	[SerializeField] private TextMeshProUGUI tmp;

	[Tooltip("同对象上的打字机组件。用于打字中点击时执行加速/跳过逻辑。")]
	[SerializeField] private TypewriterEffect typewriter;

	[Tooltip("hover 视觉组件：负责变色、荧光笔底色等表现。")]
	[SerializeField] private InteractiveTextHoverVisual hoverVisual;

	[Tooltip("对话控制器。用于点击空白区域时推进下一句。")]
	[SerializeField] private DialogueController dialogueController;

	[Header("Hover Options")]
	[Tooltip("是否启用 hover 检测。")]
	[SerializeField] private bool enableHover = true;

	[Tooltip("打字过程中是否禁用 hover。建议开启，以避免打字中出现可点击误导。")]
	[SerializeField] private bool disableHoverWhileTyping = true;

	[Header("Click Options")]
	[Tooltip("打字结束且点击到空白区域时，是否允许推进下一句。")]
	[SerializeField] private bool enableBlankClickNextDialogue = true;

	[Header("Cursor Options")]
	[Tooltip("hover 到 link 时是否修改鼠标光标。")]
	[SerializeField] private bool changeCursorOnHover = true;

	[Tooltip("hover 时使用的手型光标贴图。若为空，则会退回系统默认光标。")]
	[SerializeField] private Texture2D handCursor;

	[Tooltip("手型光标热点（像素坐标）。")]
	[SerializeField] private Vector2 handCursorHotspot = new Vector2(6f, 0f);

	[Header("Handlers")]
	[Tooltip("交互 handler 列表。将所有实现了 IInteractiveLinkHandler 的组件拖入这里。")]
	[SerializeField] private List<MonoBehaviour> handlerBehaviours = new List<MonoBehaviour>();

	/// <summary>
	/// 运行时可用的交互 handler 列表。
	/// 从 handlerBehaviours 中提取实现了 IInteractiveLinkHandler 的组件。
	/// </summary>
	private readonly List<IInteractiveLinkHandler> _handlers = new List<IInteractiveLinkHandler>();

	/// <summary>
	/// 当前 hover 的 link 索引。
	/// -1 表示当前鼠标不在任何 link 上。
	/// </summary>
	private int _hoverLinkIndex = -1;

	/// <summary>
	/// 当前 hover 的 linkId。
	/// </summary>
	private string _hoverLinkId = null;

	private void Reset()
	{
		// 自动填充最常见的同对象依赖
		tmp = GetComponent<TextMeshProUGUI>();
		typewriter = GetComponent<TypewriterEffect>();
		hoverVisual = GetComponent<InteractiveTextHoverVisual>();
	}

	private void Awake()
	{
		// 防御式获取，避免 Inspector 漏配导致空引用
		if (tmp == null)
		{
			tmp = GetComponent<TextMeshProUGUI>();
		}

		if (typewriter == null)
		{
			typewriter = GetComponent<TypewriterEffect>();
		}

		if (hoverVisual == null)
		{
			hoverVisual = GetComponent<InteractiveTextHoverVisual>();
		}

		// 从 MonoBehaviour 列表中筛出实现了 IInteractiveLinkHandler 的组件
		RebuildHandlerCache();
	}

	/// <summary>
	/// 重建 handler 缓存。
	///
	/// 说明：
	/// - 当 Inspector 中 handlerBehaviours 变更时，建议重新执行一次
	/// - 当前版本在 Awake 时执行即可
	/// </summary>
	private void RebuildHandlerCache()
	{
		_handlers.Clear();

		for (int i = 0; i < handlerBehaviours.Count; i++)
		{
			MonoBehaviour mb = handlerBehaviours[i];
			if (mb is IInteractiveLinkHandler handler)
			{
				_handlers.Add(handler);
			}
		}
	}

	/// <summary>
	/// 鼠标在当前文本区域内移动时触发。
	///
	/// 处理流程：
	/// 1. 检查是否允许 hover
	/// 2. 若打字中且配置为禁用 hover，则清理当前 hover 状态并返回
	/// 3. 检测当前鼠标是否命中某个 link
	/// 4. 若命中变化，则更新 hover 状态
	/// </summary>
	/// <param name="eventData">UI PointerEventData</param>
	public void OnPointerMove(PointerEventData eventData)
	{
		if (!enableHover)
			return;

		if (tmp == null || eventData == null)
			return;

		// 若打字中禁用 hover，则强制清理当前 hover 状态
		if (disableHoverWhileTyping && typewriter != null && typewriter.IsTyping)
		{
			ClearHover();
			return;
		}

		// Overlay 模式：camera 传 null
		int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmp, eventData.position, null);

		// 若当前命中的 link 与之前相同，则无需重复刷新
		if (linkIndex == _hoverLinkIndex)
			return;

		// 没有命中任何 link，清理 hover
		if (linkIndex == -1)
		{
			ClearHover();
			return;
		}

		// 命中了新 link，读取 linkId 并切换 hover 状态
		TMP_LinkInfo linkInfo = tmp.textInfo.linkInfo[linkIndex];
		string linkId = linkInfo.GetLinkID();

		SetHover(linkIndex, linkId);
	}

	/// <summary>
	/// 鼠标离开当前文本区域时触发。
	/// 需要清理所有 hover 视觉与状态。
	/// </summary>
	/// <param name="eventData">UI PointerEventData</param>
	public void OnPointerExit(PointerEventData eventData)
	{
		ClearHover();
	}

	/// <summary>
	/// 文本区域统一点击入口（最终收口版）。
	///
	/// 点击规则：
	/// 1. 若正在打字，则本次点击优先交给 TypewriterEffect 处理（加速 / 双击跳过）
	/// 2. 若打字结束且点到了 link，则分发给对应 handler
	/// 3. 若打字结束且没有点到 link，则根据配置推进下一句
	/// </summary>
	/// <param name="eventData">UI PointerEventData</param>
	public void OnPointerClick(PointerEventData eventData)
	{
		if (tmp == null || eventData == null)
			return;

		// 1) 打字中：将点击优先交给打字机处理
		// HandleTypingClick() 返回 true 表示本次点击已被消费（用于加速/跳过）
		if (typewriter != null && typewriter.HandleTypingClick())
		{
			return;
		}

		// 2) 打字结束：检测是否点击到了 link
		tmp.ForceMeshUpdate();
		Canvas.ForceUpdateCanvases();

		int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmp, eventData.position, null);

		if (linkIndex != -1)
		{
			string linkId = tmp.textInfo.linkInfo[linkIndex].GetLinkID();

			InteractiveLinkContext ctx = new InteractiveLinkContext
			{
				view = this,
				tmp = tmp,
				pointerEventData = eventData
			};

			// 交给第一个能处理该 linkId 的 handler
			for (int i = 0; i < _handlers.Count; i++)
			{
				IInteractiveLinkHandler handler = _handlers[i];
				if (handler != null && handler.CanHandle(linkId))
				{
					handler.OnClick(linkId, ctx);
					return;
				}
			}

			// 命中了 link 但没有可处理的 handler：
			// 这里不推进下一句，避免用户点击交互词时出现误推进。
			return;
		}

		// 3) 打字结束且点击空白区域：推进下一句
		// 事实上，该脚本目前只用于Search界面和Ask界面，而这两个界面的功能有独立于对话系统的脚本控制
		// 于是这一行代码大抵是永远不会调用的
		// TODO：重构整个项目的对话系统，升级为条件于数据驱动的叙事框架，这样就可以在ASK、Search、对话系统三者间复用一套代码了
		// 道阻险长
		if (enableBlankClickNextDialogue && dialogueController != null)
		{
			dialogueController.NextDialogue();
		}
	}

	/// <summary>
	/// 设置当前 hover 状态。
	///
	/// 处理流程：
	/// 1. 若之前已有 hover，则先执行旧 link 的 hover exit
	/// 2. 更新当前 hover 索引与 linkId
	/// 3. 更新光标样式
	/// 4. 调用 hover 视觉层
	/// 5. 通知业务 handler 执行 OnHoverEnter
	/// </summary>
	/// <param name="linkIndex">当前命中的 TMP link 索引</param>
	/// <param name="linkId">当前命中的 linkId</param>
	private void SetHover(int linkIndex, string linkId)
	{
		// 若之前已有 hover，则先通知旧 hover 退出
		if (_hoverLinkIndex != -1)
		{
			NotifyHoverExit(_hoverLinkId);
		}

		_hoverLinkIndex = linkIndex;
		_hoverLinkId = linkId;

		// 修改光标
		if (changeCursorOnHover)
		{
			if (handCursor != null)
			{
				Cursor.SetCursor(handCursor, handCursorHotspot, CursorMode.Auto);
			}
			else
			{
				// 若未配置手型贴图，则恢复默认光标
				Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
			}
		}

		// 应用 hover 视觉表现（变色 / 荧光笔底色）
		if (hoverVisual != null)
		{
			hoverVisual.Apply(tmp, linkIndex);
		}

		// 通知 handler：hover enter
		NotifyHoverEnter(linkId);
	}

	/// <summary>
	/// 清理当前 hover 状态。
	///
	/// 处理内容：
	/// 1. 通知 handler 执行 OnHoverExit
	/// 2. 清空当前 hover 索引与 linkId
	/// 3. 恢复默认光标
	/// 4. 清除 hover 视觉表现
	/// </summary>
	private void ClearHover()
	{
		if (_hoverLinkIndex == -1)
			return;

		NotifyHoverExit(_hoverLinkId);

		_hoverLinkIndex = -1;
		_hoverLinkId = null;

		if (changeCursorOnHover)
		{
			Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
		}

		if (hoverVisual != null)
		{
			hoverVisual.Clear(tmp);
		}
	}

	/// <summary>
	/// 向所有可处理当前 linkId 的 handler 广播 hover enter。
	/// </summary>
	/// <param name="linkId">当前 hover 的 linkId</param>
	private void NotifyHoverEnter(string linkId)
	{
		if (string.IsNullOrEmpty(linkId))
			return;

		InteractiveLinkContext ctx = new InteractiveLinkContext
		{
			view = this,
			tmp = tmp,
			pointerEventData = null
		};

		for (int i = 0; i < _handlers.Count; i++)
		{
			IInteractiveLinkHandler handler = _handlers[i];
			if (handler != null && handler.CanHandle(linkId))
			{
				handler.OnHoverEnter(linkId, ctx);
			}
		}
	}

	/// <summary>
	/// 向所有可处理当前 linkId 的 handler 广播 hover exit。
	/// </summary>
	/// <param name="linkId">当前离开的 linkId</param>
	private void NotifyHoverExit(string linkId)
	{
		if (string.IsNullOrEmpty(linkId))
			return;

		InteractiveLinkContext ctx = new InteractiveLinkContext
		{
			view = this,
			tmp = tmp,
			pointerEventData = null
		};

		for (int i = 0; i < _handlers.Count; i++)
		{
			IInteractiveLinkHandler handler = _handlers[i];
			if (handler != null && handler.CanHandle(linkId))
			{
				handler.OnHoverExit(linkId, ctx);
			}
		}
	}

	/// <summary>
	/// 手动刷新 handler 缓存。
	///
	/// 使用场景：
	/// - 运行时动态增删 handler 后，可调用该方法同步缓存
	/// - 平时通常不需要外部主动调用
	/// </summary>
	public void RefreshHandlers()
	{
		RebuildHandlerCache();
	}

	/// <summary>
	/// 手动清理 hover 状态。
	///
	/// 使用场景：
	/// - 对话切句时主动清理上一个句子的 hover 状态
	/// - 文本内容刚被替换后，避免残留旧视觉
	/// </summary>
	public void ForceClearHover()
	{
		ClearHover();
	}
}

/// <summary>
/// 交互 link 的上下文数据。
///
/// 用途：
/// - 在 handler 中获取当前文本视图、TMP 组件、点击事件等上下文信息
/// - 便于后续扩展 tooltip、UI 跳转、埋点、音效等逻辑
/// </summary>
public struct InteractiveLinkContext
{
	/// <summary>
	/// 当前交互文本视图
	/// </summary>
	public InteractiveTextView view;

	/// <summary>
	/// 当前目标 TMP 文本组件
	/// </summary>
	public TextMeshProUGUI tmp;

	/// <summary>
	/// 当前点击事件数据
	/// - hover enter/exit 时通常为 null
	/// - click 时有效
	/// </summary>
	public PointerEventData pointerEventData;
}

/// <summary>
/// 交互 link 处理器接口。
///
/// 说明：
/// - 不同类型的 link（如 clue / npc / glossary）都可以通过实现该接口接入系统
/// - InteractiveTextView 不关心具体业务，只负责将 linkId 分发给能处理它的 handler
///
/// 建议：
/// - 每种 link 类型单独写一个 handler
/// - 例如：ClueLinkHandler / NpcLinkHandler / GlossaryLinkHandler
/// </summary>
public interface IInteractiveLinkHandler
{
	/// <summary>
	/// 判断当前 handler 是否能处理指定 linkId。
	/// </summary>
	/// <param name="linkId">待处理的 linkId</param>
	/// <returns>能处理则返回 true，否则返回 false</returns>
	bool CanHandle(string linkId);

	/// <summary>
	/// 鼠标进入该 link 时调用。
	/// </summary>
	/// <param name="linkId">当前 hover 的 linkId</param>
	/// <param name="ctx">交互上下文</param>
	void OnHoverEnter(string linkId, InteractiveLinkContext ctx);

	/// <summary>
	/// 鼠标离开该 link 时调用。
	/// </summary>
	/// <param name="linkId">当前离开的 linkId</param>
	/// <param name="ctx">交互上下文</param>
	void OnHoverExit(string linkId, InteractiveLinkContext ctx);

	/// <summary>
	/// 点击该 link 时调用。
	/// </summary>
	/// <param name="linkId">当前点击的 linkId</param>
	/// <param name="ctx">交互上下文</param>
	void OnClick(string linkId, InteractiveLinkContext ctx);
}