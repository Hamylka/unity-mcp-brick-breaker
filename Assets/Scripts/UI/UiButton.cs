using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Minimal tap button: raises <see cref="Clicked"/> and reports whether it is being pressed.</summary>
public class UiButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool Interactable { get; set; } = true;
    public bool IsPressed { get; private set; }
    public event Action Clicked;
    /// <summary>Raised on touch-down, before the tap is released (for actions that must beat other input, like pausing).</summary>
    public event Action Pressed;
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
        if (Interactable) Pressed?.Invoke();
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
