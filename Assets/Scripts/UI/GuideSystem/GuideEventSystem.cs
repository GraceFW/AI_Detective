using System;

public static class GuideEventSystem
{
	public static Action OnClick;

	public static void TriggerClick()
	{
		OnClick?.Invoke();
	}
}