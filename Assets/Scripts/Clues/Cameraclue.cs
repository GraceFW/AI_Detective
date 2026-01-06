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
            if (f.time.month != time.month || f.time.day != time.day)
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

[Serializable]
public struct CameraTime
{
    public int month;
    public int day;
    public int hour;

    public CameraTime(int month, int day, int hour)
    {
        this.month = month;
        this.day = day;
        this.hour = hour;
    }

    public override bool Equals(object obj)
    {
        if (obj is CameraTime other)
            return month == other.month && day == other.day && hour == other.hour;
        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + month;
            hash = hash * 31 + day;
            hash = hash * 31 + hour;
            return hash;
        }
    }

    public override string ToString() => $"{month:00}/{day:00} {hour:00}:00";
}

[Serializable]
public class ClickableArea
{
    [Tooltip("Normalized Rect (0¨C1)")]
    public Rect rect;

    [Tooltip("Clue revealed when this area is clicked")]
    public ClueData reveals;
}
