using Godot;
using System;

public partial class InventorySlot : Control
{

	private int _slotIndex;
	private Vector2 _slotSize;
	public override void _Ready()
	{
		CustomMinimumSize = _slotSize;
		GD.Print("slot size in inv panel: " + _slotSize);
		GD.Print("actual Inv slot size: " + Size);


	}

	public void SetSlotData(int index, Vector2 slotSize)
	{
		_slotIndex = index;
		_slotSize = slotSize;
	}
}
