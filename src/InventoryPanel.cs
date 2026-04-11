using Godot;

public partial class InventoryPanel : Control
{
	[Export] private HFlowContainer _invSlots;
	[Export] private PackedScene _invSlotScene;
	[Export] private ScrollContainer _invScrollContainer;
	[Export] private float _invSlotSize = 40.0f;
	[Export] private int _slotCount = 120;

	private Vector2 _slotSize;

	public override void _Ready()
	{
		_slotSize = new Vector2(_invSlotSize, _invSlotSize);
		GD.Print("slot size in inv panel: " + _slotSize);

		for (int i = 0; i < _slotCount; i++)
		{
			var invSlot = _invSlotScene.Instantiate<InventorySlot>();
			invSlot.SetSlotData(i, _slotSize);
			_invSlots.AddChild(invSlot);
		}
	}
}
