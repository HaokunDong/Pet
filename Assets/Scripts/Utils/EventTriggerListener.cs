using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
public class EventTriggerListener : EventTrigger
{
	public delegate void VoidDelegate(GameObject go);
	public VoidDelegate onClick;
	public VoidDelegate onDown;
	public VoidDelegate onEnter;
	public VoidDelegate onExit;
	public VoidDelegate onUp;
	public VoidDelegate onSelect;
	public VoidDelegate onUpdateSelect;
	public VoidDelegate onBeginDrag;

	// Flag to track whether a drag operation is in progress.
	// When true, OnPointerClick will be suppressed.
	private bool _isDragging = false;

	static public EventTriggerListener Get(GameObject go)
	{
		EventTriggerListener listener = go.GetComponent<EventTriggerListener>();
		if (listener == null) listener = go.AddComponent<EventTriggerListener>();
		return listener;
	}
	public override void OnPointerClick(PointerEventData eventData)
	{
		// Suppress click if a drag just occurred
		if (_isDragging)
		{
			_isDragging = false;
			return;
		}
		if (onClick != null) onClick(gameObject);
	}
	public override void OnPointerDown(PointerEventData eventData)
	{
		if (onDown != null) onDown(gameObject);
	}
	public override void OnPointerEnter(PointerEventData eventData)
	{
		if (onEnter != null) onEnter(gameObject);
	}
	public override void OnPointerExit(PointerEventData eventData)
	{
		if (onExit != null) onExit(gameObject);
	}
	public override void OnPointerUp(PointerEventData eventData)
	{
		if (onUp != null) onUp(gameObject);
	}
	public override void OnSelect(BaseEventData eventData)
	{
		if (onSelect != null) onSelect(gameObject);
	}
	public override void OnUpdateSelected(BaseEventData eventData)
	{
		if (onUpdateSelect != null) onUpdateSelect(gameObject);
	}
	public override void OnBeginDrag(PointerEventData eventData)
	{
		_isDragging = true;
		if (onBeginDrag != null) onBeginDrag(gameObject);
	}

	public override void OnEndDrag(PointerEventData eventData)
	{
		// Note: _isDragging is reset in OnPointerClick (which fires after OnEndDrag).
	}

	public override void OnDrag(PointerEventData eventData)
	{
		// Intentionally empty – drag movement is handled by BlackHoleDragHandler.
		// Unity EventSystem calls all IDragHandler components on the same GameObject,
		// so BlackHoleDragHandler.OnDrag will be invoked automatically.
	}
}