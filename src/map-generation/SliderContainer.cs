using Godot;
using System;

public partial class SliderContainer : VBoxContainer
{
	[Signal]
	public delegate void SliderReorderedEventHandler(MapPreviewSlider slider, int newIndex);

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		var draggedSlider = data.AsGodotObject() as MapPreviewSlider;
		if (draggedSlider == null)
			return false;

		return draggedSlider.GetParent() == this;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		var draggedSlider = data.AsGodotObject() as MapPreviewSlider;
		if (draggedSlider == null)
			return;

		int targetIndex = GetDropIndex(atPosition.Y);
		int sourceIndex = draggedSlider.GetIndex();

		if (sourceIndex < targetIndex)
			targetIndex--;

		MoveChild(draggedSlider, targetIndex);
		EmitSignal(SignalName.SliderReordered, draggedSlider, targetIndex);
	}

	private int GetDropIndex(float localY)
	{
		for (int i = 0; i < GetChildCount(); i++)
		{
			if (GetChild(i) is not Control child)
				continue;

			float midpoint = child.Position.Y + child.Size.Y * 0.5f;
			if (localY < midpoint)
				return i;
		}

		return GetChildCount() - 1;
	}
}
