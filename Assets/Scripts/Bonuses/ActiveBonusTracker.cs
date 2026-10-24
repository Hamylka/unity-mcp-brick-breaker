using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared list of the timers that are running right now, for the HUD. A running bonus calls
/// <see cref="Report"/> every frame it is alive; one that stops reporting simply drops off the list.
/// Each entry is one indicator. Entries that share a group are shown side by side under one label
/// (e.g. one entry per multi-ball extra, so each ball's own countdown is visible).
/// </summary>
public static class ActiveBonusTracker
{
    public struct Entry
    {
        public string Id;
        public string Group;
        public string Label;
        public Color Color;
        /// <summary>Seconds left, or a negative value when the bonus has no timer (until it breaks).</summary>
        public float Remaining;
        public float Total;
        public int Frame;

        public bool Timed => Remaining >= 0f && Total > 0f;
    }

    private static readonly List<Entry> entries = new();

    // Domain reload may be disabled in the editor: do not carry entries over from the last play session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => entries.Clear();

    /// <summary>
    /// Reports one running timer for this frame. <paramref name="id"/> must be unique per indicator,
    /// <paramref name="group"/> names the bonus. Pass a negative remaining time for "until broken".
    /// </summary>
    public static void Report(string id, string group, string label, Color color, float remaining, float total)
    {
        var entry = new Entry
        {
            Id = id, Group = group, Label = label, Color = color,
            Remaining = remaining, Total = total, Frame = Time.frameCount
        };

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id != id) continue;
            entries[i] = entry;
            return;
        }
        entries.Add(entry);
    }

    /// <summary>Fills <paramref name="result"/> with the entries reported this frame, in the order they first appeared.</summary>
    public static void GetActive(List<Entry> result)
    {
        result.Clear();
        int frame = Time.frameCount;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Frame < frame - 1) entries.RemoveAt(i); // stopped reporting: gone
        }
        result.AddRange(entries);
    }
}
