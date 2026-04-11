using Godot;
using System;

public partial class PauseMenu : ColorRect
{

	[Export] private Button _resumeButton;
	private bool _isOpen = false;
	private bool _isVisible = false;

	public bool IsOpen => _isOpen;
	public override void _Ready()
	{
		Visible = false;
		_resumeButton.Pressed += OnMyButtonPressed;
	}

	private void OnMyButtonPressed()
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
}
