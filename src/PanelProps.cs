using Godot;
using System;
using System.Dynamic;
using System.Numerics;

public class PanelProps
{
	public int layer
	{ get; set; }
	public string id
	{ get; set; }
	public string displayName
	{ get; set; }
	public Godot.Vector2 Pos
	{ get; set; }
	public Godot.Vector2 LastPanelSize
	{ get; set; }
	public Godot.Vector2 LastPanelPos
	{ get; set; }
	public Godot.Vector2 Size
	{ get; set; }
	public void PrintMessage(string message)
	{
		GD.Print(displayName + ": ", message);
	}
	public void AdjustLayer()
	{
		return;
	}

}
