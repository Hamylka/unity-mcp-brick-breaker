using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Runtime state of one power-up card on the choice screen. Built and animated by <see cref="PowerUpScreen"/>.</summary>
public class CardView : MonoBehaviour, IPointerClickHandler
{
    public int Index;
    public Action<int> Clicked;

    public RectTransform Rect;
    public CanvasGroup Group;
    public Image Halo;
    public Image Border;
    public PowerUpRarity Rarity;
    public Color RarityColor;

    public float Width;
    public float BaseX;
    public float SelectT;
    public float Appear;
    public float EmitAccumulator;

    public void OnPointerClick(PointerEventData eventData)
    {
        Clicked?.Invoke(Index);
    }
}
