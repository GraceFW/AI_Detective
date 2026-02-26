using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Clue/Camera Clue")]
public class CameraClueData : ClueData
{
    [Header("Default (for unspecified times)")]
    [Tooltip("If no frame matches the requested time, this image will be shown.")]
    public Sprite defaultImage;

    [Tooltip("Clickable areas used for unspecified times. Usually empty.")]
    public List<ClickableArea> defaultAreas = new List<ClickableArea>();

    [Header("Explicit Time Frames")]
    [Tooltip("Each entry represents one time point with its image and clickable areas.")]
    public List<CameraFrame> frames = new List<CameraFrame>();

    /// <summary>
    /// Returns true if there is an explicitly configured frame matching the given time exactly.
    /// </summary>
    public bool TryGetFrameExact(CameraTime time, out CameraFrame frame)
    {
        if (frames != null)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                if (f != null && f.time.Equals(time))
                {
                    frame = f;
                    return true;
                }
            }
        }

        frame = null;
        return false;
    }

    /// <summary>
    /// Returns an explicitly configured frame if exists; otherwise returns a fallback frame
    /// built from defaultImage/defaultAreas.
    /// </summary>
    public CameraFrameView GetFrameOrDefault(CameraTime time)
    {
        if (TryGetFrameExact(time, out var frame))
        {
            return new CameraFrameView(time, frame.image, frame.areas);
        }

        // Fallback (unspecified times)
        return new CameraFrameView(time, defaultImage, defaultAreas);
    }

    /// <summary>
    /// Optional helper: find nearest frame by absolute hour difference within the same month/day.
    /// If none on the same date, falls back to default.
    /// </summary>
    public CameraFrameView GetNearestFrameOrDefault(CameraTime time)
    {
        if (frames == null || frames.Count == 0)
            return new CameraFrameView(time, defaultImage, defaultAreas);

        int bestIndex = -1;
        int bestDiff = int.MaxValue;

        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            if (f == null) continue;

            // You can relax this matching rule later if needed.
            if (f.time.minute != time.minute || f.time.day != time.day)
                continue;

            int diff = Mathf.Abs(f.time.hour - time.hour);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            var best = frames[bestIndex];
            return new CameraFrameView(time, best.image, best.areas);
        }

        return new CameraFrameView(time, defaultImage, defaultAreas);
    }
}

[Serializable]
public class CameraFrame
{
    [Header("Time")]
    public CameraTime time;

    [Header("Visual")]
    public Sprite image;

    [Header("Clickable Areas")]
    public List<ClickableArea> areas = new List<ClickableArea>();
}

/// <summary>
/// Runtime-friendly view of a frame (either explicit or default).
/// Not a ScriptableObject; safe to construct on the fly.
/// </summary>
public readonly struct CameraFrameView
{
    public readonly CameraTime requestedTime;
    public readonly Sprite image;
    public readonly IReadOnlyList<ClickableArea> areas;

    public CameraFrameView(CameraTime requestedTime, Sprite image, IReadOnlyList<ClickableArea> areas)
    {
        this.requestedTime = requestedTime;
        this.image = image;
        this.areas = areas;
    }
}

/// <summary>
/// 摄像头时间结构体（日/时/分）
/// </summary>
[Serializable] // 确保能在 Inspector 面板显示
public struct CameraTime : IEquatable<CameraTime> // 实现强类型 Equals，性能更好
{
    // 字段顺序为「日→时→分」
    public int day;
    public int hour;
    public int minute;

    public CameraTime(int day, int hour, int minute)
    {
        this.day = day;
        this.hour = hour;
        this.minute = minute;
    }

    // 实现强类型 Equals（比 object 版本性能更高）
    public bool Equals(CameraTime other)
    {
        return day == other.day && hour == other.hour && minute == other.minute;
    }

    // 重写 object Equals（兼容旧代码）
    public override bool Equals(object obj)
    {
        return obj is CameraTime other && Equals(other);
    }

    // 修复4：哈希计算顺序和字段/Equals 一致（day→hour→minute），保证一致性
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + day;
            hash = hash * 31 + hour;
            hash = hash * 31 + minute;
            return hash;
        }
    }

    // 修复5：移除末尾多余空格，格式改为「00日 00:00」，更符合中文显示习惯
    public override string ToString() => $"{day:00}日 {hour:00}:{minute:00}";

    // 可选：重载 == 和 != 运算符，使用更便捷
    public static bool operator ==(CameraTime left, CameraTime right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CameraTime left, CameraTime right)
    {
        return !left.Equals(right);
    }
}

[Serializable]
public class ClickableArea
{
    [Tooltip("Normalized Rect (0�C1)")]
    public Rect rect;

    [Tooltip("Clue revealed when this area is clicked")]
    public ClueData reveals;
}
