using System;
using Godot;
using Vector2 = Godot.Vector2;

public partial class MapPreview : Control
{
	[Export] private MenuButton _mapDisplayMenu;
	[Export] private Label _currentTileDetailsLabel;
	[Export] private Label _selectedTileDetailsLabel;
	[Export] private Label _mapIdLabel;
	[Export] private SliderContainer _reorderSliderContainer;
	[Export] private PanelContainer _mapPreviewContainer;
	[Export] private PackedScene _reorderSliderScene;
	[Export] private TextureRect _cellMapPreview;
	[Export] private TextureRect _heightMapPreview;
	[Export] private TextureRect _testMapPreview;
	[Export] private TextureRect _biomeMapPreview;
	[Export] private TextureRect _temperatureMapPreview;


	[ExportGroup("Map Settings")]
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


	private Vector2 _selectedTileLocation = Vector2.Zero;

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
	private string _currentMapType = "Test";

	private MapPreviewDetails[] _mapDetailsForSliders;
	public class MapPreviewDetails
	{
		public string Id { get; set; }
		public TextureRect LinkedMapPreview { get; set; }

		public MapPreviewDetails(string id, TextureRect linkedMapPreview)
		{
			Id = id;
			LinkedMapPreview = linkedMapPreview;
		}
	}

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

		if (_mapDisplayPopupMenu != null)
			_mapDisplayPopupMenu.IdPressed += OnMapDisplayMenuChoice;

		if (_testMapPreview != null)
			_testMapPreview.GuiInput += OnMapGuiInput;

		(_heightMap, _heightTexture) = HeightMap.GenerateHeightMap(
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
		_heightMapPreview.Texture = _heightTexture;

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
		_testMapPreview.Texture = _testTexture;


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
		_temperatureMapPreview.Texture = _temperatureTexture;


		(_cellMap, _cellTexture) = CellNoiseHelper.GenerateCellMapAndTexture(
			_mapSize,
			_mapSize,
			_cellCount,
			_seed
		);
		_cellMapPreview.Texture = _cellTexture;


		long biomeSeed = _seed ^ 0x9E3779B9;

		(_biomeResult, _biomeTexture) = CellBiomeWfcHelper.GenerateBiomesAndTexture(
			_cellMap,
			(int)biomeSeed
		);
		_biomeMapPreview.Texture = _biomeTexture;

		_mapDetailsForSliders =
		[
			new MapPreviewDetails("Test", _testMapPreview),
			new MapPreviewDetails("Height", _heightMapPreview),
			new MapPreviewDetails("Cell", _cellMapPreview),
			new MapPreviewDetails("Biome", _biomeMapPreview),
			new MapPreviewDetails("Temperature", _temperatureMapPreview),
		];

		foreach (var pair in _biomeResult.CellBiomes)
		{
			int cellId = pair.Key;
			BiomeRulesHelper.BiomeType biome = pair.Value;
			// GD.Print($"Cell {cellId} => {biome}");
		}

		GD.Print($"Generated {_cellMap.Cells.Count} cells");

		OnMapDisplayMenuChoice(0);

		AddReorderSliders(_mapDetailsForSliders);

		if (_reorderSliderContainer != null)
			_reorderSliderContainer.SliderReordered += OnSliderReordered;

		GetTileDetailsString(new Vector2(0f, 0f), _selectedTileDetailsLabel);
		GetTileDetailsString(new Vector2(0f, 0f), _currentTileDetailsLabel);

	}

	private void OnMapGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left &&
			mb.Pressed)
		{
			_selectedTileLocation = _testMapPreview.GetLocalMousePosition();
			GetTileDetailsString(_selectedTileLocation, _selectedTileDetailsLabel);
		}
	}

	private void AddReorderSliders(MapPreviewDetails[] mapPreviewDetails)
	{
		int numOfSliders = Enum.GetNames(typeof(MapTypes)).Length;
		_reorderSliderContainer.Size = new Vector2(_reorderSliderContainer.GetParent<MarginContainer>().Size.X, numOfSliders * 20);

		foreach (var item in mapPreviewDetails)
		{
			var slider = _reorderSliderScene.Instantiate<MapPreviewSlider>();
			slider.SetSliderData(item.Id, _reorderSliderContainer.Size.X, item.LinkedMapPreview);
			_reorderSliderContainer.AddChild(slider);
		}
	}

	private void OnSliderReordered(MapPreviewSlider slider, int newIndex)
	{
		if (slider.LinkedMapPreview == null)
			return;

		_mapPreviewContainer.MoveChild(slider.LinkedMapPreview, newIndex);
	}

	public override void _Process(double delta)
	{
		GetTileDetailsString(_testMapPreview.GetLocalMousePosition(), _currentTileDetailsLabel);
	}

	private void GetTileDetailsString(Vector2 pos, Label labelToUpdate)
	{
		Rect2 bounds = new Rect2(Vector2.Zero, _testMapPreview.Size);

		if (!bounds.HasPoint(pos))
		{
			return;
		}

		if (_testMapPreview.Size.X <= 0 || _testMapPreview.Size.Y <= 0)
			return;

		// Convert from TextureRect space to map array space
		int mapX = Mathf.Clamp(
			Mathf.FloorToInt(pos.X / _testMapPreview.Size.X * _biomeResult.CellMap.Width),
			0,
			_biomeResult.CellMap.Width - 1
		);

		int mapY = Mathf.Clamp(
			Mathf.FloorToInt(pos.Y / _testMapPreview.Size.Y * _biomeResult.CellMap.Height),
			0,
			_biomeResult.CellMap.Height - 1
		);

		int cellId = _biomeResult.CellMap.CellIdMap[mapX, mapY];
		BiomeRulesHelper.BiomeType biomeType = _biomeResult.CellBiomes[cellId];

		string result =
			$"\n" +
			$"Y: {mapY}\n" +
			$"X: {mapX}\n\n" +
			$"Cell ID: {cellId}\n\n" +
			$"Biome: {biomeType}";

		labelToUpdate.Text = result;
	}

	private void OnMapDisplayMenuChoice(long id)
	{
		switch ((int)id)
		{
			case (int)MapTypes.Height:
				_mapIdLabel.Text = "Height";
				_currentMapType = "Height";
				_currentMap = _heightMap;
				break;
			case (int)MapTypes.Temperature:
				_mapIdLabel.Text = "Temperature";
				_currentMapType = "Temperature";
				_currentMap = _temperatureMap;
				break;
			case (int)MapTypes.Climate:
				_mapIdLabel.Text = "Cliamte";
				_currentMapType = "Climate";
				// _currentMap = _climateMap;
				break;
			case (int)MapTypes.Test:
				_mapIdLabel.Text = "Test";
				_currentMapType = "Test";
				_currentMap = _testMap;
				break;
			case (int)MapTypes.Cell:
				_mapIdLabel.Text = "Cell";
				_currentMapType = "Cell";
				// _currentMap = new float[,];
				break;
			case (int)MapTypes.Biome:
				_mapIdLabel.Text = "Biome";
				_currentMapType = "Biome";
				// _currentMap = new float[,];
				break;
		}

		GD.Print($"{_currentMapType} Map Selected");

		return;
	}
}
