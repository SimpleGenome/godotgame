using System;
using Godot;
using Vector2 = Godot.Vector2;

public partial class WorldMapTexture : PanelContainer
{
	[Export] private TextureRect _mapTexture;
	[Export] private Control _mapClipper;
	[Export] private Label _zoomLabel;

	[Export] private float _zoomStep = 0.10f;
	[Export] private float _minZoom = 0.2f;
	[Export] private float _maxZoom = 4.0f;
	[Export] private float _defaultZoom = 1.0f;

	private float _zoom = 1.0f;
	private Vector2 _baseMapSize = Vector2.Zero;

	private bool _dragging = false;

	public override void _Ready()
	{
		_baseMapSize = _mapTexture.Texture.GetSize();
		_zoom = _defaultZoom;

		Size = _baseMapSize * _zoom;

		// Start centered, then clamp in case the map is smaller/larger than the clipper.
		Position = (_mapClipper.Size - Size) / 2f;
		ClampToClipper();

		UpdateZoomLabel();

		if (_mapTexture != null)
			_mapTexture.GuiInput += OnMapTextureGuiInput;

		// If the clipper is resized later, keep the map valid.
		_mapClipper.Resized += ClampToClipper;
	}

	public override void _Input(InputEvent @event)
	{
		if (_dragging && @event is InputEventMouseMotion motion)
		{
			Position += motion.Relative;
			ClampToClipper();
			return;
		}

		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left &&
			!mouseButton.Pressed)
		{
			_dragging = false;
		}
	}

	private void OnMapTextureGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			_dragging = mouseButton.Pressed;
		}

		if (@event is not InputEventMouseButton mb || !mb.Pressed)
			return;

		Vector2 mouseInClipper = mb.GlobalPosition - _mapClipper.GlobalPosition;

		if (mb.ButtonIndex == MouseButton.WheelUp)
		{
			SetZoomAtPoint(_zoom + _zoomStep, mouseInClipper);
			GetViewport().SetInputAsHandled();
		}
		else if (mb.ButtonIndex == MouseButton.WheelDown)
		{
			SetZoomAtPoint(_zoom - _zoomStep, mouseInClipper);
			GetViewport().SetInputAsHandled();
		}
	}

	private void SetZoomAtPoint(float value, Vector2 zoomPointInClipper)
	{
		float newZoom = Mathf.Clamp(value, _minZoom, _maxZoom);

		if (Mathf.IsEqualApprox(newZoom, _zoom))
			return;

		float oldZoom = _zoom;

		// Which point on the map is currently under the mouse?
		Vector2 mapPointUnderMouse = (zoomPointInClipper - Position) / oldZoom;

		// Apply new zoom.
		_zoom = newZoom;
		Size = _baseMapSize * _zoom;

		// Reposition so the same map point stays under the mouse.
		Position = zoomPointInClipper - mapPointUnderMouse * _zoom;

		// If that would push the map past an edge, clamp it back.
		ClampToClipper();

		UpdateZoomLabel();
	}

	private void ClampToClipper()
	{
		Vector2 clipperSize = _mapClipper.Size;
		Vector2 newPos = Position;

		// Horizontal clamp
		if (Size.X <= clipperSize.X)
		{
			// Map is smaller than the viewport: keep it centered.
			newPos.X = (clipperSize.X - Size.X) / 2f;
		}
		else
		{
			// Map is larger than the viewport: don't allow empty space at the edges.
			float minX = clipperSize.X - Size.X;
			float maxX = 0f;
			newPos.X = Mathf.Clamp(newPos.X, minX, maxX);
		}

		// Vertical clamp
		if (Size.Y <= clipperSize.Y)
		{
			newPos.Y = (clipperSize.Y - Size.Y) / 2f;
		}
		else
		{
			float minY = clipperSize.Y - Size.Y;
			float maxY = 0f;
			newPos.Y = Mathf.Clamp(newPos.Y, minY, maxY);
		}

		Position = newPos;
	}

	private void UpdateZoomLabel()
	{
		if (_zoomLabel != null)
			_zoomLabel.Text = $"{Math.Round(_zoom * 100)}%";
	}
}
