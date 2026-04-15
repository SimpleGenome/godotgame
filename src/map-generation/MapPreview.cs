using System;
using System.Linq.Expressions;
using Godot;

public partial class MapPreview : Control
{
	[Export] private MenuButton _mapDisplayMenu;
	[Export] private Label _mouseXPosLabel;
	[Export] private Label _mouseYPosLabel;
	[Export] private Label _heightLabel;

	[ExportGroup("Map Settings")]
	[Export] private TextureRect _mapPreview;
	[Export] private int _mapSize;
	[Export] private int _seed = 12345;
	[Export] private int _cellCount;

	[ExportSubgroup("Base Height Levels")]
	[Export] private float _seaLevel = 0.5f;
	[Export] private float _coastThickness = 0.02f;
	[Export] private float _biomeLevel = 0.72f;
	[Export] private float _snowLevel = 0.89f;
	[Export] private float _baseFrequency = 0.008f;
	[Export] private float _detailFrequency = 0.025f;


	private float _orientation;
	private CellNoiseHelper.CellMapData _cellMap;
	private Texture2D _cellTexture;
	private CellBiomeWfcHelper.BiomeCellResult _biomeResult;
	private float[,] _currentMap;
	private Texture2D _biomeTexture;
	private float[,] _heightMap;
	private Texture2D _heightTexture;
	private float[,] _temperatureMap;
	private Texture2D _temperatureTexture;
	private float[,] _climateMap;
	private Texture2D _climateTexture;
	private float[,] _testMap;
	private Texture2D _testTexture;
	private PopupMenu _mapDisplayPopupMenu;
	private enum MapTypes
	{
		Test,
		Height,
		Temperature,
		Climate,
		Cell,
		Biome
	}
	private string _currentMapType;


	public override void _Ready()
	{
		MapGenTools.InitRandom(_seed);
		_orientation = MapGenTools.NextRandomFloat();
		GD.Print("Orientation: " + _orientation);

		_mapDisplayPopupMenu = _mapDisplayMenu.GetPopup();

		foreach (MapTypes mapType in Enum.GetValues<MapTypes>())
		{
			_mapDisplayPopupMenu.AddItem(mapType.ToString(), (int)mapType);
		}

		_mapDisplayPopupMenu.IdPressed += OnMapDisplayMenuChoice;

		(_heightMap, _heightTexture) = HeightMap.GenerateHeightMap(
			_mapSize,
			_seed,
			_baseFrequency,
			_detailFrequency,
			_seaLevel,
			_coastThickness,
			_biomeLevel,
			_snowLevel
		);

		(_testMap, _testTexture) = TestMap.GenerateHeightMap(
			_mapSize,
			_seed,
			_baseFrequency,
			_detailFrequency,
			_orientation,
			_seaLevel,
			_coastThickness,
			_biomeLevel,
			_snowLevel
		);

		(_temperatureMap, _temperatureTexture) = TemperatureMap.GenerateTemperatureMap(
			_mapSize,
			_seed,
			_baseFrequency,
			_detailFrequency,
			_orientation,
			_testMap,
			_seaLevel,
			_coastThickness,
			_biomeLevel,
			_snowLevel
		);

		(_cellMap, _cellTexture) = CellNoiseHelper.GenerateCellMapAndTexture(
			_mapSize,
			_mapSize,
			_cellCount,
			_seed
		);

		long biomeSeed = _seed ^ 0x9E3779B9;

		(_biomeResult, _biomeTexture) = CellBiomeWfcHelper.GenerateBiomesAndTexture(
			_cellMap,
			(int)biomeSeed
		);

		foreach (var pair in _biomeResult.CellBiomes)
		{
			int cellId = pair.Key;
			BiomeRulesHelper.BiomeType biome = pair.Value;
			// GD.Print($"Cell {cellId} => {biome}");
		}

		GD.Print($"Generated {_cellMap.Cells.Count} cells");

		OnMapDisplayMenuChoice(0);

		_mouseXPosLabel.Text = "X:0";
		_mouseYPosLabel.Text = "Y:0";

	}

	public override void _Process(double delta)
	{
		// Mouse position relative to the target control
		Vector2 localMousePos = _mapPreview.GetLocalMousePosition();
		int lmX = (int)localMousePos.X;
		int lmY = (int)localMousePos.Y;

		// The control's local bounds
		Rect2 bounds = new Rect2(Vector2.Zero, _mapPreview.Size);

		if (bounds.HasPoint(localMousePos))
		{
			_mouseXPosLabel.Text = $"X:{lmX}";
			_mouseYPosLabel.Text = $"Y:{lmY}";
			_heightLabel.Text = $"{_currentMapType}:{_currentMap[lmX, lmY]}";
		}
	}

	private void OnMapDisplayMenuChoice(long id)
	{
		switch ((int)id)
		{
			case (int)MapTypes.Height:
				_currentMapType = "Height";
				_mapPreview.Texture = _heightTexture;
				_currentMap = _heightMap;
				break;
			case (int)MapTypes.Temperature:
				_currentMapType = "Temperature";
				_mapPreview.Texture = _temperatureTexture;
				_currentMap = _temperatureMap;
				break;
			case (int)MapTypes.Climate:
				_currentMapType = "Climate";
				_mapPreview.Texture = _climateTexture;
				_currentMap = _climateMap;
				break;
			case (int)MapTypes.Test:
				_currentMapType = "Test";
				_mapPreview.Texture = _testTexture;
				_currentMap = _testMap;
				break;
			case (int)MapTypes.Cell:
				_currentMapType = "Cell";
				_mapPreview.Texture = _cellTexture;
				// _currentMap = new float[,];
				break;
			case (int)MapTypes.Biome:
				_currentMapType = "Biome";
				_mapPreview.Texture = _biomeTexture;
				// _currentMap = new float[,];
				break;
		}

		GD.Print($"{_currentMapType} Map Selected");

		return;
	}
}
