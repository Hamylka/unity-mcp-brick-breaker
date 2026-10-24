using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Minimal tap button: raises <see cref="Clicked"/> and reports whether it is being pressed.</summary>
public class UiButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool Interactable { get; set; } = true;
    public bool IsPressed { get; private set; }
    public event Action Clicked;
    /// <summary>Raised when the button is tapped while not interactable (for a "denied" sound or shake).</summary>
    public event Action DeniedClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Interactable) Clicked?.Invoke();
        else DeniedClick?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = Interactable;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsPressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsPressed = false;
    }
}
