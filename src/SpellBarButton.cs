using Godot;

public partial class SpellBarButton : Control
{
	private int _slotIndex;
	[Export] private Label _indexLabel;

	private bool _hotkeyHeld = false;

	public override void _Ready()
	{
		if (_slotIndex != 9)
		{
			_indexLabel.Text = (_slotIndex + 1).ToString();
		}
		else
		{
			_indexLabel.Text = "0";
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("spellbar_slot_1_" + _indexLabel.Text))
		{
			_hotkeyHeld = true;
			UpdateVisualState();

			Activate();
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionReleased("spellbar_slot_1_" + _indexLabel.Text))
		{
			_hotkeyHeld = false;
			UpdateVisualState();
		}
	}

	private void Activate()
	{
		GD.Print($"Activated slot {_indexLabel.Text}");
		// Cast spell / trigger cooldown / notify spell bar here.
	}

	private void UpdateVisualState()
	{
		if (_hotkeyHeld)
		{
			Modulate = new Color(0.85f, 0.85f, 0.85f, 1f);
		}
		else
		{
			Modulate = Colors.White;
		}
	}

	public void SetSlotIndex(int index)
	{
		_slotIndex = index;
	}
}
