using Godot;
using System;

public partial class PauseMenu : ColorRect
{
	private bool _isOpen = false;
	private bool _isVisible = false;

	public bool IsOpen => _isOpen;
	public override void _Ready()
	{

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
}
