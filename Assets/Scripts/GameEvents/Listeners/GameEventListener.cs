using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
	// 事件通道：SO文件
	[SerializeField] protected GameEventSO gameEventSO;
	// 使用UnityEvent可以在inspector面板可视化编辑方法的注册与注销
	[SerializeField] protected UnityEvent response;

	private void OnEnable() => gameEventSO.OnEventRaised += OnEventRaised;

	private void OnDisable() => gameEventSO.OnEventRaised -= OnEventRaised;

	private void OnEventRaised() => response?.Invoke();
}

public abstract class GameEventListener<T> : MonoBehaviour
{
	[SerializeField] protected GameEventSO<T> gameEventSO;
	[SerializeField] protected UnityEvent<T> response;
	private void OnEnable() => gameEventSO.OnEventRaised += OnEventRaised;

	private void OnDisable() => gameEventSO.OnEventRaised -= OnEventRaised;

	private void OnEventRaised(T t) => response?.Invoke(t);
}

public abstract class GameEventListener<T1, T2> : MonoBehaviour
{
	[SerializeField] protected GameEventSO<T1, T2> gameEventSO;

	private void OnEnable() => gameEventSO.OnEventRaised += OnEventRaised;

	private void OnDisable() => gameEventSO.OnEventRaised -= OnEventRaised;

	protected abstract void OnEventRaised(T1 t1, T2 t2);
}

public abstract class GameEventListener<T1, T2, T3> : MonoBehaviour
{
	[SerializeField] protected GameEventSO<T1, T2, T3> gameEventSO;

	private void OnEnable() => gameEventSO.OnEventRaised += OnEventRaised;

	private void OnDisable() => gameEventSO.OnEventRaised -= OnEventRaised;

	protected abstract void OnEventRaised(T1 t1, T2 t2, T3 t3);
}