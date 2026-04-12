using Godot;
using System;

public partial class InventorySlot : Control
{

	private int _slotIndex;
	private Vector2 _slotSize;
	public override void _Ready()
	{
		CustomMinimumSize = _slotSize;
	}

	public void SetSlotData(int index, Vector2 slotSize)
	{
		_slotIndex = index;
		_slotSize = slotSize;
	}
}
