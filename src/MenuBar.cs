using Godot;
using Vector2 = Godot.Vector2;

public partial class MenuBar : Control
{
	[Export] private HBoxContainer _slots;
	[Export] private PackedScene _menuButtonScene;
	[Export] private Vector2 _buttonSize = new Vector2(20.0f, 20.0f);
	[Export] private MarginContainer _slotMarginContainer;
	[Export] private int _slotPadding = 4;

	[ExportGroup("Panel Assignments")]
	[Export] private AdjustablePanel _inventoryPanel;
	[Export] private AdjustablePanel _characterPanel;
	[Export] private AdjustablePanel _skillsPanel;
	[Export] private AdjustablePanel _mapPanel;
	[Export] private AdjustablePanel _questLogPanel;
	private PanelShortcut[] _hotkeyIds;

	public class PanelShortcut
	{
		public string Hotkey { get; set; }
		public string Id { get; set; }
		public AdjustablePanel LinkedPanel { get; set; }

		public PanelShortcut(string hotkey, string id, AdjustablePanel linkedPanel)
		{
			Hotkey = hotkey;
			Id = id;
			LinkedPanel = linkedPanel;
		}
	}

	public override void _Ready()
	{
		_hotkeyIds =
		[
			new PanelShortcut("B", "inventory", _inventoryPanel),
			new PanelShortcut("C", "character", _characterPanel),
			new PanelShortcut("K", "skills", _skillsPanel),
			new PanelShortcut("M", "map", _mapPanel),
			new PanelShortcut("L", "questlog", _questLogPanel),
		];

		foreach (var item in _hotkeyIds)
		{
			var slot = _menuButtonScene.Instantiate<MenuBarButton>();
			slot.SetSlotData(item.Hotkey, item.Id, item.LinkedPanel);
			_slots.AddChild(slot);
		}

		if (_slotMarginContainer != null)
		{
			_slotMarginContainer.AddThemeConstantOverride("margin_left", _slotPadding);
			_slotMarginContainer.AddThemeConstantOverride("margin_right", _slotPadding);
			_slotMarginContainer.AddThemeConstantOverride("margin_top", _slotPadding);
			_slotMarginContainer.AddThemeConstantOverride("margin_bottom", _slotPadding);
		}

		_slots.AddThemeConstantOverride("separation", _slotPadding);

		float menuBarLength = _hotkeyIds.Length * (_buttonSize.X + _slotPadding) + (_slotPadding * 2);
		float menuBarHeight = _buttonSize.Y + _slotPadding + (_slotPadding * 2);

		Size = new Vector2(menuBarLength, menuBarHeight);

		SetAnchorsPreset(LayoutPreset.BottomRight, true);
		Position = new Vector2(Position.X - menuBarLength, Position.Y - menuBarHeight);
	}
}
