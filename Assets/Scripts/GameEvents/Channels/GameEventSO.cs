using UnityEngine;
using UnityEngine.Events;

public class GameEventSO : ScriptableObject
{
	public UnityAction OnEventRaised;
	public void RaiseEvent() => OnEventRaised?.Invoke();
}
public class GameEventSO<T> : ScriptableObject
{
	public UnityAction<T> OnEventRaised;
	public void RaiseEvent(T t) => OnEventRaised?.Invoke(t);
}

public class GameEventSO<T1, T2> : ScriptableObject
{
	public UnityAction<T1, T2> OnEventRaised;
	public void RaiseEvent(T1 t1, T2 t2) => OnEventRaised?.Invoke(t1, t2);
}

public class GameEventSO<T1, T2, T3> : ScriptableObject
{
	public UnityAction<T1, T2, T3> OnEventRaised;
	public void RaiseEvent(T1 t1, T2 t2, T3 t3) => OnEventRaised?.Invoke(t1, t2, t3);
}