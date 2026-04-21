using System;
using Godot;
using Vector2 = Godot.Vector2;

public partial class MapPreview : Control
{
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
	[Export] private TextureRect _gradientMagMapPreview;
	[Export] private TextureRect _gradientDirMapPreview;
	[Export] private TextureRect _humidityMapPreview;




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
	private Vector2 _windDirection;
	private CellNoiseHelper.CellMapData _cellMap;
	private Texture2D _cellTexture;
	private CellBiomeWfcHelper.BiomeCellResult _biomeResult;
	private Texture2D _biomeTexture;
	private float[,] _heightMap;
	private Texture2D _heightTexture;
	private float[,] _temperatureMap;
	private Texture2D _temperatureTexture;
	private float[,] _climateMap;
	private Texture2D _climateTexture;
	private float[,] _humidityMap;
	private Texture2D _humidityTexture;
	private float[,] _testMap;
	private Texture2D _testTexture;
	private (float dx, float dy)[,] _gradientDirMap;
	private Texture2D _gradientDirTexture;
	private float[,] _gradientMagMap;
	private Texture2D _gradientMagTexture;
	private PopupMenu _mapDisplayPopupMenu;
	private enum MapTypes
	{
		Test,
		Height,
		Temperature,
		Climate,
		Cell,
		Biome,
		GradientMag,
		GradientDir
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
		float heightOrientation = MapGenTools.NextRandomFloat();
		float temperatureOrientation = MapGenTools.NextRandomFloat();
		_windDirection = new Vector2(MapGenTools.NextRandomFloat(), MapGenTools.NextRandomFloat());
		GD.Print("Height Orientation: " + heightOrientation);
		GD.Print("Temperature Orientation: " + temperatureOrientation);


		(_heightMap, _heightTexture) = HeightMap.GenerateHeightMap(
			_mapSize,
			_seed,
			_baseFrequency,
			_detailFrequency,
			heightOrientation,
			_seaLevel,
			_coastThickness,
			_biomeLevel,
			_snowLevel
		);
		_heightMapPreview.Texture = _heightTexture;

		(_testMap, _testTexture) = TestMap.GenerateMountainOverlay(
			_mapSize,
			_seed
		);
		_testMapPreview.Texture = _testTexture;

		(_gradientMagMap, _gradientMagTexture) = HeightMap.GenerateGradientMagnitudeMap(_heightMap);
		_gradientMagMapPreview.Texture = _gradientMagTexture;

		(_gradientDirMap, _gradientDirTexture) = HeightMap.GenerateGradientDirectionMap(_heightMap);
		_gradientDirMapPreview.Texture = _gradientDirTexture;


		(_temperatureMap, _temperatureTexture) = TemperatureMap.GenerateTemperatureMap(
			_mapSize,
			_seed,
			_baseFrequency,
			heightOrientation,
			_heightMap,
			_seaLevel,
			_coastThickness,
			_biomeLevel,
			_snowLevel
		);
		_temperatureMapPreview.Texture = _temperatureTexture;

		(_humidityMap, _humidityTexture) = HumidityMap.GenerateHumidityMap(
			_mapSize,
			_seed,
			_baseFrequency,
			_windDirection,
			_heightMap,
			_temperatureMap,
			_seaLevel,
			_coastThickness,
			_biomeLevel,
			_snowLevel
		);
		_humidityMapPreview.Texture = _humidityTexture;



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
			(int)biomeSeed,
			_heightMap,
			_temperatureMap,
			_seaLevel
		);
		_biomeMapPreview.Texture = _biomeTexture;

		_mapDetailsForSliders =
		[
			new MapPreviewDetails("Test", _testMapPreview),
			new MapPreviewDetails("Height", _heightMapPreview),
			new MapPreviewDetails("Cell", _cellMapPreview),
			new MapPreviewDetails("Biome", _biomeMapPreview),
			new MapPreviewDetails("Temperature", _temperatureMapPreview),
			new MapPreviewDetails("GradientMag", _gradientMagMapPreview),
			new MapPreviewDetails("GradientDir", _gradientDirMapPreview),
			new MapPreviewDetails("humidity", _humidityMapPreview),
		];

		GD.Print($"Generated {_cellMap.Cells.Count} cells");

		AddReorderSliders(_mapDetailsForSliders);

		if (_testMapPreview != null)
			_testMapPreview.GuiInput += OnMapGuiInput;

		if (_reorderSliderContainer != null)
			_reorderSliderContainer.SliderReordered += OnSliderReordered;

		SetTileDetailsString(new Vector2(0f, 0f), _selectedTileDetailsLabel);
		SetTileDetailsString(new Vector2(0f, 0f), _currentTileDetailsLabel);

	}

	private void OnMapGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left &&
			mb.Pressed)
		{
			_selectedTileLocation = _testMapPreview.GetLocalMousePosition();
			SetTileDetailsString(_selectedTileLocation, _selectedTileDetailsLabel);
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
		SetTileDetailsString(_testMapPreview.GetLocalMousePosition(), _currentTileDetailsLabel);
	}

	private void SetTileDetailsString(Vector2 pos, Label labelToUpdate)
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

		// y =24tan(2.27x - 1.3)+19
		// y =5tan(2.95x - 1.51)+15
		// y =20tan(2.55x-1.36)+16.59
		float temperature = _temperatureMap[mapX, mapY];
		float temperatureDegrees = 20.0f * (float)Math.Tan(2.55f * temperature - 1.36f) + 16.59f;
		float altitude = _heightMap[mapX, mapY];
		float humidity = _humidityMap[mapX, mapY];
		float gradientMag = _gradientMagMap[mapX, mapY];
		(float dx, float dy) gradientDir = _gradientDirMap[mapX, mapY];

		string result =
			$"\n" +
			$"Y: {mapY}\n" +
			$"X: {mapX}\n\n" +
			$"Cell ID: {cellId}\n\n" +
			$"Cell Count: {_cellMap.Cells.Count}\n" +
			$"Biome: {biomeType}\n\n" +
			$"Temperature: {temperatureDegrees} / {temperature}\n" +
			$"Altitude: {altitude}\n" +
			$"Humidity: {humidity}\n" +
			$"Gradient Magnitude: {gradientMag}\n" +
			$"Gradient Direction: dx:{gradientDir.dx} dy:{gradientDir.dy}\n" +
			$"Test Value: {_testMap[mapX, mapY]}\n";


		labelToUpdate.Text = result;
	}
}
