using Godot;
using Godot.Collections;

public partial class AdjustablePanelContainer : Control
{

	[Export] private PauseMenu _pausePanel;

	public override void _Input(InputEvent @event)
	{
		HandleToggleMenuPressed(@event);
		HandleBringPanelToFront(@event);
	}

	private void HandleBringPanelToFront(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mb)
			return;

		if (mb.ButtonIndex != MouseButton.Left || !mb.Pressed)
			return;

		for (int i = GetChildCount() - 1; i >= 0; i--)
		{
			Node child = GetChild(i);

			if (child is not AdjustablePanel panel || !panel.Visible)
				continue;

			if (panel.GetGlobalRect().HasPoint(mb.GlobalPosition))
			{
				panel.MoveToFront();
				break;
			}
		}
	}

	private void HandleToggleMenuPressed(InputEvent @event)
	{
		if (!@event.IsActionPressed("toggle_menu"))
		{
			return;
		}
		else
		{
			Array<Node> panels = GetTree().GetNodesInGroup("adjustable_panels");

			bool anyOpen = false;

			foreach (Node node in panels)
			{
				if (node is AdjustablePanel panel && panel.IsOpen && !panel.IsPinned)
				{
					anyOpen = true;
					break;
				}
			}

			if (anyOpen)
			{
				foreach (Node node in panels)
				{
					if (node is AdjustablePanel panel && !panel.IsPinned)
					{
						panel.ClosePanel();
					}
				}
			}
			else
			{
				if (_pausePanel.IsOpen)
				{
					_pausePanel.ClosePanel();
				}
				else
				{
					_pausePanel.OpenPanel();
				}
			}

			GetViewport().SetInputAsHandled();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("toggle_menu"))
		{
			GetTree().CallGroup("adjustable_panels", "ClosePanel");
			GetViewport().SetInputAsHandled();
		}
	}
}
