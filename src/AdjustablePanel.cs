using System.Numerics;
using Godot;
using Vector2 = Godot.Vector2;

public partial class AdjustablePanel : Control
{
	[Signal]
	public delegate void OpenStateChangedEventHandler(bool isOpen);
	[Export] private Control _resizeHandleBR;
	[Export] private Control _resizeHandleBL;
	[Export] private Control _resizeHandleTR;
	[Export] private Control _resizeHandleTL;

	[Export] private Control _titleBar;
	[Export] private Label _titleLabel;
	[Export] private Button _closeButton;
	[Export] private Button _minMaxButton;
	[Export] private Button _pinButton;
	[Export] private Texture2D _maximizeIcon;
	[Export] private Texture2D _minimizeIcon;

	[Export] private Control _contentHost;
	[Export] private PackedScene _panelScene;

	[Export] public string _menuId;
	[Export] public Vector2 _defaultPos;
	[Export] private Vector2 _defaultSize = new(500, 350);
	[Export] private Vector2 _minimumSize = new(300, 180);
	[Export] private string _windowTitle = "Panel";

	public bool IsOpen => _isOpen;

	private bool _isOpen = false;
	private bool _isFullscreen = false;
	private bool _isPinned = false;
	public bool IsPinned => _isPinned;

	private bool _dragging = false;
	private Vector2 _dragOffset;

	private bool _resizing = false;
	private ResizeMode _resizeMode = ResizeMode.None;
	private Vector2 _resizeStartMouse;
	private Vector2 _resizeStartPos;
	private Vector2 _resizeStartSize;

	private enum ResizeMode
	{
		None,
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

	private PanelProps props;

	public override void _Ready()
	{
		AddToGroup("adjustable_panels");

		Visible = false;

		if (_titleLabel != null)
			_titleLabel.Text = _windowTitle;

		props = new();

		props.Size = _defaultSize;
		props.Pos = _defaultPos;
		props.layer = 0;
		props.id = _menuId;
		props.displayName = _windowTitle;

		Size = props.Size;
		Position = props.Pos;
		CustomMinimumSize = _minimumSize;

		ClampToParentBounds();

		if (_titleBar != null)
			_titleBar.GuiInput += OnTitleBarGuiInput;
		else
			props.PrintMessage("TitleBar failed to load!");

		if (_resizeHandleBR != null)
			_resizeHandleBR.GuiInput += OnBottomRightHandleGuiInput;
		else
			props.PrintMessage("ResizeHandle failed to load!");

		if (_resizeHandleBL != null)
			_resizeHandleBL.GuiInput += OnBottomLeftHandleGuiInput;

		if (_resizeHandleTR != null)
			_resizeHandleTR.GuiInput += OnTopRightHandleGuiInput;

		if (_resizeHandleTL != null)
			_resizeHandleTL.GuiInput += OnTopLeftHandleGuiInput;

		if (_closeButton != null)
			_closeButton.GuiInput += OnCloseButtonGuiInput;
		else
			props.PrintMessage("CloseButton failed to load!");

		if (_minMaxButton != null)
			_minMaxButton.GuiInput += OnMinMaxButtonGuiInput;

		if (_pinButton != null)
			_pinButton.GuiInput += OnPinButtonGuiInput;

		if (_panelScene != null)
		{
			var panel = _panelScene.Instantiate();
			_contentHost.AddChild(panel);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (_dragging && @event is InputEventMouseMotion dragMotion)
		{
			GlobalPosition = dragMotion.GlobalPosition + _dragOffset;
			ClampToParentBounds();

			props.Pos = Position;
			return;
		}

		if (_resizing && @event is InputEventMouseMotion mm)
		{
			UpdateResize(mm.GlobalPosition);
			return;
		}

		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left &&
			!mouseButton.Pressed)
		{
			_dragging = false;
			_resizing = false;
			_resizeMode = ResizeMode.None;

			ClampToParentBounds();

			props.Pos = Position;
			props.Size = Size;
		}
	}

	private void BringToFront()
	{
		MoveToFront();
		props.layer += 1;
	}

	private bool TryGetParentBounds(out Vector2 parentSize)
	{
		if (GetParent() is Control parentControl)
		{
			parentSize = parentControl.Size;
			return true;
		}

		parentSize = Vector2.Zero;
		return false;
	}

	private void ClampToParentBounds()
	{
		if (!TryGetParentBounds(out Vector2 parentSize))
			return;

		Vector2 newSize = Size;
		Vector2 newPos = Position;

		// Never allow a panel larger than its parent.
		newSize.X = Mathf.Min(newSize.X, parentSize.X);
		newSize.Y = Mathf.Min(newSize.Y, parentSize.Y);

		// Never allow a panel smaller than its minimum.
		newSize.X = Mathf.Max(newSize.X, _minimumSize.X);
		newSize.Y = Mathf.Max(newSize.Y, _minimumSize.Y);

		// If parent is smaller than minimum, fit the parent.
		newSize.X = Mathf.Min(newSize.X, parentSize.X);
		newSize.Y = Mathf.Min(newSize.Y, parentSize.Y);

		// Clamp position so all edges stay onscreen.
		float minX = 0f;
		float minY = 0f;
		float maxX = parentSize.X - newSize.X;
		float maxY = parentSize.Y - newSize.Y;

		newPos.X = Mathf.Clamp(newPos.X, minX, maxX);
		newPos.Y = Mathf.Clamp(newPos.Y, minY, maxY);

		Size = newSize;
		Position = newPos;
	}

	private void ToggleFullscreen()
	{
		if (_isFullscreen == false)
		{
			if (GetParent() is Control parentControl)
			{
				props.LastPanelSize = props.Size;
				props.LastPanelPos = props.Pos;

				props.Size = parentControl.Size;
				props.Pos = Vector2.Zero;

				Size = props.Size;
				Position = props.Pos;

				_isFullscreen = true;
				_minMaxButton.Icon = _minimizeIcon;
			}
			else
			{
				props.PrintMessage("Parent node is not a Control node!");
			}
		}
		else
		{
			if (Mathf.IsEqualApprox(Size.X, props.LastPanelSize.X)
				&& Mathf.IsEqualApprox(Size.Y, props.LastPanelSize.Y))
			{
				props.LastPanelPos = new Vector2(props.LastPanelPos.X + 20.0f, props.LastPanelPos.Y + 20.0f);
				props.LastPanelSize = new Vector2(props.LastPanelSize.X - 40.0f, props.LastPanelSize.Y - 40.0f);
			}
			Size = props.LastPanelSize;
			Position = props.LastPanelPos;

			ClampToParentBounds();

			props.Size = Size;
			props.Pos = Position;

			_isFullscreen = false;
			_minMaxButton.Icon = _maximizeIcon;
		}
	}

	private void OnPinButtonGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (!mouseButton.Pressed)
				_isPinned = !_isPinned;
		}
	}

	private void OnMinMaxButtonGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (!mouseButton.Pressed)
				ToggleFullscreen();
		}
	}

	private void OnCloseButtonGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (!mouseButton.Pressed)
				TogglePanel();
		}
	}

	private void OnTitleBarGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				BringToFront();
				_dragging = true;
				_resizing = false;

				_dragOffset = GlobalPosition - mouseButton.GlobalPosition;
			}
			else
			{
				_dragging = false;
				ClampToParentBounds();
				props.Pos = Position;
			}
		}
	}

	private void BeginResize(ResizeMode mode, Vector2 mouseGlobalPos)
	{
		BringToFront();
		_resizeMode = mode;
		_resizing = true;
		_dragging = false;

		_resizeStartMouse = mouseGlobalPos;
		_resizeStartPos = Position;
		_resizeStartSize = Size;
	}

	private void UpdateResize(Vector2 mouseGlobalPos)
	{
		if (!TryGetParentBounds(out Vector2 parentSize))
			return;

		Vector2 delta = mouseGlobalPos - _resizeStartMouse;

		float left = _resizeStartPos.X;
		float top = _resizeStartPos.Y;
		float right = _resizeStartPos.X + _resizeStartSize.X;
		float bottom = _resizeStartPos.Y + _resizeStartSize.Y;

		switch (_resizeMode)
		{
			case ResizeMode.TopLeft:
				left += delta.X;
				top += delta.Y;
				break;

			case ResizeMode.TopRight:
				right += delta.X;
				top += delta.Y;
				break;

			case ResizeMode.BottomLeft:
				left += delta.X;
				bottom += delta.Y;
				break;

			case ResizeMode.BottomRight:
				right += delta.X;
				bottom += delta.Y;
				break;
		}

		// Clamp to parent edges first.
		left = Mathf.Clamp(left, 0f, parentSize.X);
		right = Mathf.Clamp(right, 0f, parentSize.X);
		top = Mathf.Clamp(top, 0f, parentSize.Y);
		bottom = Mathf.Clamp(bottom, 0f, parentSize.Y);

		float minWidth = _minimumSize.X;
		float minHeight = _minimumSize.Y;

		if (right - left < minWidth)
		{
			if (_resizeMode == ResizeMode.TopLeft || _resizeMode == ResizeMode.BottomLeft)
				left = right - minWidth;
			else
				right = left + minWidth;
		}

		if (bottom - top < minHeight)
		{
			if (_resizeMode == ResizeMode.TopLeft || _resizeMode == ResizeMode.TopRight)
				top = bottom - minHeight;
			else
				bottom = top + minHeight;
		}

		// Clamp again after min-size correction.
		if (left < 0f)
		{
			float shift = -left;
			left += shift;
			right += shift;
		}
		if (top < 0f)
		{
			float shift = -top;
			top += shift;
			bottom += shift;
		}
		if (right > parentSize.X)
		{
			float shift = right - parentSize.X;
			left -= shift;
			right -= shift;
		}
		if (bottom > parentSize.Y)
		{
			float shift = bottom - parentSize.Y;
			top -= shift;
			bottom -= shift;
		}

		Position = new Vector2(left, top);
		Size = new Vector2(right - left, bottom - top);

		ClampToParentBounds();

		props.Pos = Position;
		props.Size = Size;
	}

	private void OnBottomRightHandleGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left &&
			mb.Pressed)
		{
			if (_isFullscreen)
			{
				props.LastPanelSize = Size;
				props.LastPanelPos = Position;
				ToggleFullscreen();
			}
			BeginResize(ResizeMode.BottomRight, mb.GlobalPosition);
		}
	}

	private void OnBottomLeftHandleGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left &&
			mb.Pressed)
		{
			if (_isFullscreen)
			{
				props.LastPanelSize = Size;
				props.LastPanelPos = Position;
				ToggleFullscreen();
			}
			BeginResize(ResizeMode.BottomLeft, mb.GlobalPosition);
		}
	}

	private void OnTopRightHandleGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left &&
			mb.Pressed)
		{
			if (_isFullscreen)
			{
				props.LastPanelSize = Size;
				props.LastPanelPos = Position;
				ToggleFullscreen();
			}
			BeginResize(ResizeMode.TopRight, mb.GlobalPosition);
		}
	}

	private void OnTopLeftHandleGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left &&
			mb.Pressed)
		{
			if (_isFullscreen)
			{
				props.LastPanelSize = Size;
				props.LastPanelPos = Position;
				ToggleFullscreen();
			}
			BeginResize(ResizeMode.TopLeft, mb.GlobalPosition);
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("toggle_" + props.id))
		{
			TogglePanel();
			GetViewport().SetInputAsHandled();
		}
	}

	public void OpenPanel()
	{
		if (_isOpen)
			return;

		_isOpen = true;
		Visible = true;
		BringToFront();
		ClampToParentBounds();
		EmitSignal(SignalName.OpenStateChanged, _isOpen);
	}

	public void ClosePanel()
	{
		if (!_isOpen)
			return;

		_isOpen = false;
		Visible = false;
		EmitSignal(SignalName.OpenStateChanged, _isOpen);
	}

	public void TogglePanel()
	{
		if (_isOpen)
			ClosePanel();
		else
			OpenPanel();
	}
}
