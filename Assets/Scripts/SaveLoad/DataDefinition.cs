using UnityEngine;

public class DataDefinition : MonoBehaviour
{
	public string ID;
	public PresistentType presistentType;
	private void OnValidate()
	{
		if (presistentType == PresistentType.ReadWrite)
		{
			if (ID == string.Empty)
			{
				ID = System.Guid.NewGuid().ToString();
			}
		}
		else
		{
			ID = string.Empty;
		}
	}
}
