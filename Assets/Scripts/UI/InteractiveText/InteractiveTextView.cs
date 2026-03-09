using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 交互文本视图（工业级交互层）
///
/// 职责：
/// 1) 使用 TMP_TextUtilities 命中 link（hover / click）
/// 2) 管理 hover 状态（当前 hover 的 linkIndex / linkId）
/// 3) 调用视觉层（InteractiveTextHoverVisual）实现：变色 + 荧光笔底色
/// 4) 分发事件给业务层 handler（IInteractiveLinkHandler）
/// 5) 支持与打字机协作：打字中禁用 hover / click link（可开关）
///
/// 注意：
/// - 当前的 Canvas 是 Screen Space - Overlay，因此 FindIntersectingLink 的 camera 传 null 最稳。
/// - 该脚本不修改 tmp.text，不插富文本标签，避免索引错乱/GC/富文本嵌套爆炸。
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class InteractiveTextView : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
{
	[Header("References")]
	[Tooltip("目标 TMP 文字组件（Overlay 模式下命中 link 用它）")]
	[SerializeField] private TextMeshProUGUI tmp;

	[Tooltip("可选：同物体的打字机脚本。用于打字中禁用 hover/click link")]
	[SerializeField] private TypewriterEffect typewriter;

	[Tooltip("Hover 视觉表现：变色 + 荧光笔底色")]
	[SerializeField] private InteractiveTextHoverVisual hoverVisual;

	[Header("Hover Options")]
	[Tooltip("是否启用 hover 高亮")]
	[SerializeField] private bool enableHover = true;

	[Tooltip("是否在打字机运行时禁用 hover（建议开启：避免用户在打字时误以为可以点 link）")]
	[SerializeField] private bool disableHoverWhileTyping = true;

	[Header("Cursor Options")]
	[Tooltip("是否在 hover link 时变更鼠标光标")]
	[SerializeField] private bool changeCursorOnHover = true;

	[Tooltip("hover link 时使用的手型光标贴图（不配置也可以，仍可使用默认光标）")]
	[SerializeField] private Texture2D handCursor;

	[Tooltip("手型光标热点（像素坐标：通常靠左上更自然）")]
	[SerializeField] private Vector2 handCursorHotspot = new Vector2(6, 0);

	[Header("Handlers (Business Logic)")]
	[Tooltip("可插拔的交互处理器（例如：线索、百科、NPC等）。把实现了 IInteractiveLinkHandler 的脚本拖进来。")]
	[SerializeField] private List<MonoBehaviour> handlerBehaviours = new List<MonoBehaviour>();

	private readonly List<IInteractiveLinkHandler> _handlers = new List<IInteractiveLinkHandler>();

	// 当前 hover 的 link 状态（-1 表示不在任何 link 上）
	private int _hoverLinkIndex = -1;
	private string _hoverLinkId = null;

	private void Reset()
	{
		tmp = GetComponent<TextMeshProUGUI>();
		typewriter = GetComponent<TypewriterEffect>();
		hoverVisual = GetComponent<InteractiveTextHoverVisual>();
	}

	private void Awake()
	{
		if (tmp == null) tmp = GetComponent<TextMeshProUGUI>();
		if (typewriter == null) typewriter = GetComponent<TypewriterEffect>();
		if (hoverVisual == null) hoverVisual = GetComponent<InteractiveTextHoverVisual>();

		// 收集 handlers（解耦业务逻辑）
		_handlers.Clear();
		foreach (var mb in handlerBehaviours)
		{
			if (mb is IInteractiveLinkHandler h)
				_handlers.Add(h);
		}
	}

	/// <summary>
	/// 鼠标在该 UI 上移动时触发
	/// </summary>
	public void OnPointerMove(PointerEventData eventData)
	{
		if (!enableHover || tmp == null || eventData == null)
			return;

		// 如果打字中禁用 hover，则这里直接清理高亮并返回
		if (disableHoverWhileTyping && typewriter != null && typewriter.IsTyping)
		{
			ClearHover();
			return;
		}

		// Overlay 模式 camera 传 null（最稳）
		int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmp, eventData.position, null);

		// hover 没变，不需要重复刷新（避免多余 ForceMeshUpdate）
		if (linkIndex == _hoverLinkIndex)
			return;

		if (linkIndex == -1)
		{
			ClearHover();
			return;
		}

		// 读取 linkId（用于派发给 handler）
		var linkInfo = tmp.textInfo.linkInfo[linkIndex];
		string linkId = linkInfo.GetLinkID();

		SetHover(linkIndex, linkId);
	}

	/// <summary>
	/// 鼠标离开该 UI 区域时触发
	/// </summary>
	public void OnPointerExit(PointerEventData eventData)
	{
		ClearHover();
	}

	/// <summary>
	/// 点击事件：打字中不处理 link；打字结束命中 link 则交给 handler
	/// </summary>
	public void OnPointerClick(PointerEventData eventData)
	{
		if (tmp == null || eventData == null)
			return;

		// 与你的 TypewriterEffect 规则一致：打字中不允许点 link
		if (typewriter != null && typewriter.IsTyping)
			return;

		int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmp, eventData.position, null);
		if (linkIndex == -1)
			return;

		string linkId = tmp.textInfo.linkInfo[linkIndex].GetLinkID();

		// 组装上下文（以后你想做 tooltip、打开面板、播放音效都可以用 ctx）
		var ctx = new InteractiveLinkContext
		{
			view = this,
			tmp = tmp,
			pointerEventData = eventData
		};

		// 找到第一个能处理该 linkId 的 handler 并执行
		foreach (var h in _handlers)
		{
			if (h != null && h.CanHandle(linkId))
			{
				h.OnClick(linkId, ctx);
				return;
			}
		}
	}

	/// <summary>
	/// 设置 hover 到某个 link：更新状态、更新光标、更新视觉、通知 handler
	/// </summary>
	private void SetHover(int linkIndex, string linkId)
	{
		// 先退出旧 hover（通知 handler）
		if (_hoverLinkIndex != -1)
			NotifyHoverExit(_hoverLinkId);

		_hoverLinkIndex = linkIndex;
		_hoverLinkId = linkId;

		// 光标变手型
		if (changeCursorOnHover)
		{
			if (handCursor != null)
				Cursor.SetCursor(handCursor, handCursorHotspot, CursorMode.Auto);
			else
				Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
		}

		// 视觉：变色 + 荧光笔底色
		if (hoverVisual != null)
			hoverVisual.Apply(tmp, linkIndex);

		// 通知 handler：hover enter（例如显示 tooltip）
		NotifyHoverEnter(linkId);
	}

	/// <summary>
	/// 清理 hover：恢复光标、恢复视觉、通知 handler
	/// </summary>
	private void ClearHover()
	{
		if (_hoverLinkIndex == -1)
			return;

		NotifyHoverExit(_hoverLinkId);

		_hoverLinkIndex = -1;
		_hoverLinkId = null;

		if (changeCursorOnHover)
			Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

		if (hoverVisual != null)
			hoverVisual.Clear(tmp);
	}

	private void NotifyHoverEnter(string linkId)
	{
		if (string.IsNullOrEmpty(linkId)) return;

		var ctx = new InteractiveLinkContext { view = this, tmp = tmp, pointerEventData = null };
		foreach (var h in _handlers)
		{
			if (h != null && h.CanHandle(linkId))
				h.OnHoverEnter(linkId, ctx);
		}
	}

	private void NotifyHoverExit(string linkId)
	{
		if (string.IsNullOrEmpty(linkId)) return;

		var ctx = new InteractiveLinkContext { view = this, tmp = tmp, pointerEventData = null };
		foreach (var h in _handlers)
		{
			if (h != null && h.CanHandle(linkId))
				h.OnHoverExit(linkId, ctx);
		}
	}
}

/// <summary>
/// 交互 link 的上下文信息
/// - TODO：tooltip、打开 UI、播音效、记录埋点，都可以从这里取信息
/// </summary>
public struct InteractiveLinkContext
{
	public InteractiveTextView view;
	public TextMeshProUGUI tmp;
	public PointerEventData pointerEventData;
}

/// <summary>
/// 可插拔的交互处理器接口（业务层）
/// - 例如：ClueLinkHandler、GlossaryLinkHandler、NpcLinkHandler...
/// </summary>
public interface IInteractiveLinkHandler
{
	bool CanHandle(string linkId);
	void OnHoverEnter(string linkId, InteractiveLinkContext ctx);
	void OnHoverExit(string linkId, InteractiveLinkContext ctx);
	void OnClick(string linkId, InteractiveLinkContext ctx);
}