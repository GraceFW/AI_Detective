using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Clue/Camera Clue")]
public class CameraClueData : ClueData
{
    public CameraTime time;
    public Sprite image;
    public List<ClickableArea> areas;
}

[System.Serializable]
public struct CameraTime
{
    public int month;
    public int day;
    public int hour;
}

[System.Serializable]
public class ClickableArea
{
    [Tooltip("Normalized Rect (0¨C1)")]
    public Rect rect;
    public ClueData reveals;
}

