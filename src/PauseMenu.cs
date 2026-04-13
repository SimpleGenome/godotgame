using Godot;
using System;

public partial class PauseMenu : ColorRect
{

	[Export] private Button _resumeButton;
	private bool _isOpen = false;

	public bool IsOpen => _isOpen;
	public override void _Ready()
	{
		Visible = false;
		_resumeButton.Pressed += OnResumeButtonPressed;
	}

	private void OnResumeButtonPressed()
	{
		ClosePanel();
	}

	public void OpenPanel()
	{
		_isOpen = true;
		Visible = true;
	}

	public void ClosePanel()
	{
		_isOpen = false;
		Visible = false;
	}

	public void TogglePanel()
	{
		if (_isOpen)
		{
			_isOpen = false;
			Visible = false;
		}
		else
		{
			_isOpen = true;
			Visible = true;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		GD.Print($"Pause menu Open: {_isOpen}");
		if (_isOpen)
		{
			if (@event.IsActionPressed("toggle_menu"))
				return;

			GetViewport().SetInputAsHandled();
		}
		else
		{
			return;
		}

	}
}
