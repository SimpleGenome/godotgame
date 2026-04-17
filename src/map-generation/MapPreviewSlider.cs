using Godot;
using System;

public partial class MapPreviewSlider : PanelContainer
{
	[Export] private Label _sliderTitleLabel;
	[Export] private HSlider _slider;
	[Export] private Button _dragHandle;
	[Export] private Button _visibleButton;
	[Export] private Texture2D _notVisibleIcon;
	[Export] private Texture2D _visibleIcon;

	private string _sliderId;
	private TextureRect _linkedMapPreview;

	private double _sliderValue = 0;
	public double SliderValue => _sliderValue;

	public TextureRect LinkedMapPreview => _linkedMapPreview;
	public string SliderId => _sliderId;

	public override void _Ready()
	{
		_slider.MinValue = 0.0;
		_slider.MaxValue = 1.0;
		_slider.Step = 0.01;
		_slider.Value = 1.0;
		_slider.ValueChanged += OnSliderValueChanged;

		_sliderTitleLabel.MouseFilter = MouseFilterEnum.Ignore;

		if (_dragHandle != null)
			_dragHandle.GuiInput += OnDragHandleGuiInput;

		if (_visibleButton != null)
			_visibleButton.Pressed += OnVisibleButtonPressed;
	}

	private void OnVisibleButtonPressed()
	{
		if (_linkedMapPreview == null)
		{
			return;
		}
		else
		{
			if (_linkedMapPreview.Visible == true)
			{
				_linkedMapPreview.Visible = false;
				_visibleButton.Icon = _notVisibleIcon;
			}
			else
			{
				_linkedMapPreview.Visible = true;
				_visibleButton.Icon = _visibleIcon;
			}
		}
	}

	private void OnSliderValueChanged(double value)
	{
		_sliderValue = value;

		if (_linkedMapPreview == null)
			return;

		Color modulate = _linkedMapPreview.SelfModulate;
		modulate.A = (float)value;
		_linkedMapPreview.SelfModulate = modulate;
	}

	private void OnDragHandleGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left &&
			mouseButton.Pressed)
		{
			CallDeferred(nameof(BeginDrag));
		}
	}

	private void BeginDrag()
	{
		ForceDrag(this, CreateDragPreview());
	}

	private Control CreateDragPreview()
	{
		var preview = new PanelContainer();
		preview.CustomMinimumSize = Size;

		var label = new Label();
		label.Text = _sliderTitleLabel.Text;
		preview.AddChild(label);

		return preview;
	}

	public void SetSliderData(string id, float size, TextureRect mapPreview)
	{
		_sliderId = id;
		_sliderTitleLabel.Text = id;
		Size = new Vector2(size, 40.0f);
		_linkedMapPreview = mapPreview;

	}
}
