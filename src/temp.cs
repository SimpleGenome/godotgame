using Godot;

public partial class WorldMapPanelTEMP : Control
{
    [Export] private Control _clipper;
    [Export] private TextureRect _mapTexture;

    [Export] private float _zoomStep = 0.15f;
    [Export] private float _minZoom = 0.5f;
    [Export] private float _maxZoom = 4.0f;

    [Export] private float _defaultZoom = 1.0f;

    // This is the map's starting offset from the centered position.
    // (0, 0) means perfectly centered.
    [Export] private Vector2 _defaultOffset = Vector2.Zero;

    private float _zoom = 1.0f;
    private Vector2 _mapOffset = Vector2.Zero;
    private Vector2 _baseMapSize = Vector2.Zero;

    private bool _dragging = false;
    private Vector2 _dragOffset;

    public override void _Ready()
    {
        _clipper.ClipContents = true;

        _mapTexture.MouseFilter = MouseFilterEnum.Ignore;
        _clipper.MouseFilter = MouseFilterEnum.Stop;
        _clipper.GuiInput += OnClipperGuiInput;

        if (_mapTexture.Texture == null)
        {
            GD.PushError("WorldMapPanel: MapTexture has no texture assigned.");
            return;
        }

        _baseMapSize = _mapTexture.Texture.GetSize();

        // Start from your chosen defaults.
        ResetView();

        // Re-center correctly if the clipper changes size.
        _clipper.Resized += ApplyMapTransform;
    }

    public override void _Input(InputEvent @event)
    {
        if (_dragging && @event is InputEventMouseMotion dragMotion)
        {
            GlobalPosition = dragMotion.GlobalPosition + _dragOffset;
            _mapTexture.Position = Position;
            return;
        }

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            !mouseButton.Pressed)
        {
            _dragging = false;
            _mapTexture.Position = Position;
        }
    }


    private void OnClipperGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mb || !mb.Pressed)
            return;

        if (mb.ButtonIndex == MouseButton.WheelUp)
        {
            SetZoom(_zoom + _zoomStep);
            GetViewport().SetInputAsHandled();
        }
        else if (mb.ButtonIndex == MouseButton.WheelDown)
        {
            SetZoom(_zoom - _zoomStep);
            GetViewport().SetInputAsHandled();
        }
    }

    public void ResetView()
    {
        _zoom = Mathf.Clamp(_defaultZoom, _minZoom, _maxZoom);
        _mapOffset = _defaultOffset;
        ApplyMapTransform();
    }

    private void SetZoom(float value)
    {
        float newZoom = Mathf.Clamp(value, _minZoom, _maxZoom);

        if (Mathf.IsEqualApprox(newZoom, _zoom))
            return;

        float zoomRatio = newZoom / _zoom;
        _zoom = newZoom;

        // Keep zoom centered on the clipper center.
        _mapOffset *= zoomRatio;

        ApplyMapTransform();
    }

    private void ApplyMapTransform()
    {
        if (_mapTexture.Texture == null)
            return;

        Vector2 newMapSize = _baseMapSize * _zoom;
        _mapTexture.Size = newMapSize;

        // Center the map inside the clipper.
        Vector2 centeredPosition = (_clipper.Size - newMapSize) / 2f;

        // Then apply any extra offset.
        _mapTexture.Position = centeredPosition + _mapOffset;
    }
}
