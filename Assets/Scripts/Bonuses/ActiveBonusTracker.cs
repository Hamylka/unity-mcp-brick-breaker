using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared list of the bonuses that are active right now, for the HUD. A running bonus calls
/// <see cref="Report"/> every frame it is alive; one that stops reporting simply drops off the list.
/// Several reports with the same id in one frame (e.g. many multi-ball extras) keep the longest one.
/// </summary>
public static class ActiveBonusTracker
{
    public struct Entry
    {
        public string Id;
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

    /// <summary>Reports an active bonus for this frame. Pass a negative remaining time for "until broken".</summary>
    public static void Report(string id, string label, Color color, float remaining, float total)
    {
        int frame = Time.frameCount;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id != id) continue;

            Entry entry = entries[i];
            // Keep the longest report of this frame; a report from an older frame is replaced.
            if (entry.Frame == frame && entry.Remaining >= remaining) return;
            entry.Label = label;
            entry.Color = color;
            entry.Remaining = remaining;
            entry.Total = total;
            entry.Frame = frame;
            entries[i] = entry;
            return;
        }

        entries.Add(new Entry { Id = id, Label = label, Color = color, Remaining = remaining, Total = total, Frame = frame });
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
