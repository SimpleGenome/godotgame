using Godot;

public partial class SpellBar : Control
{
	[Export] private HBoxContainer _slots;
	[Export] private PackedScene _slotScene;
	private int _slotCount = 10;

	public override void _Ready()
	{
		for (int i = 0; i < _slotCount; i++)
		{
			var slot = _slotScene.Instantiate<SpellBarButton>();
			slot.SetSlotIndex(i);
			_slots.AddChild(slot);
		}
	}
}
