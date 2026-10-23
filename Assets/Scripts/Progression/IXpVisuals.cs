using System;
using UnityEngine;

/// <summary>
/// Lets something visual (the HUD) carry earned XP from a world position to the XP bar, as one or more orbs.
/// <paramref name="onArrive"/> is called once per orb, with the XP that orb carried, when it reaches the bar.
/// </summary>
public interface IXpVisuals
{
    void Deliver(Vector3 worldPosition, int amount, Action<int> onArrive);
}
