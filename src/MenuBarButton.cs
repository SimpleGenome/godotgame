using Godot;

public partial class MenuBarButton : Control
{
	private string _slotIndexHotkey;
	private string _slotIndexId;

	[Export] private Label _menuHotkeyLabel;
	private AdjustablePanel _linkedPanel;

	private bool _hotkeyHeld = false;
	private bool _panelOpen = false;

	public override void _Ready()
	{
		if (_menuHotkeyLabel != null)
			_menuHotkeyLabel.Text = _slotIndexHotkey;

		if (_linkedPanel != null)
			_linkedPanel.OpenStateChanged += OnPanelOpenStateChanged;

		MouseFilter = MouseFilterEnum.Stop;

		if (_menuHotkeyLabel != null)
		{
			_menuHotkeyLabel.Text = _slotIndexHotkey;
			_menuHotkeyLabel.MouseFilter = MouseFilterEnum.Ignore;
		}

		if (_linkedPanel != null)
			_linkedPanel.OpenStateChanged += OnPanelOpenStateChanged;
	}

	public override void _ExitTree()
	{
		if (_linkedPanel != null)
			_linkedPanel.OpenStateChanged -= OnPanelOpenStateChanged;
	}

	public override void _Input(InputEvent @event)
	{
		if (string.IsNullOrEmpty(_slotIndexId))
			return;

		string actionName = "toggle_" + _slotIndexId;

		if (@event.IsActionPressed(actionName))
		{
			_hotkeyHeld = true;
			UpdateVisualState();
		}
		else if (@event.IsActionReleased(actionName))
		{
			_hotkeyHeld = false;
			UpdateVisualState();
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			GD.Print($"MenuBarButton click event, pressed = {mouseButton.Pressed}");
		}

		if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left &&
			!mb.Pressed)
		{
			GD.Print("Toggling linked panel from menu button");
			_linkedPanel?.TogglePanel();
			AcceptEvent();
		}
	}

	private void OnPanelOpenStateChanged(bool isOpen)
	{
		_panelOpen = isOpen;
		UpdateVisualState();
	}

	private void UpdateVisualState()
	{
		if (_hotkeyHeld)
		{
			Modulate = new Color(0.85f, 0.85f, 0.85f, 1f);
		}
		else if (_panelOpen)
		{
			Modulate = new Color(0.70f, 0.90f, 1.00f, 1f);
		}
		else
		{
			Modulate = Colors.White;
		}
	}

	public void SetSlotData(string hotkey, string id, AdjustablePanel panel)
	{
		_slotIndexHotkey = hotkey;
		_slotIndexId = id;
		_linkedPanel = panel;

		if (_menuHotkeyLabel != null)
			_menuHotkeyLabel.Text = _slotIndexHotkey;
	}
}
