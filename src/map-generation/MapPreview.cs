using System;
using System.Linq.Expressions;
using Godot;

public partial class MapPreview : Control
{
	[Export] private TextureRect _mapPreview;

	[Export] private int _mapSize = 512;
	[Export] private int _seed = 12345;

	[Export] private float _baseFrequency = 0.008f;
	[Export] private float _detailFrequency = 0.025f;

	[Export] private MenuButton _mapDisplayMenu;

	private PopupMenu _mapDisplayPopupMenu;
	private float[,] _heightMap;
	private Texture2D _heightTexture;
	private float[,] _temperatureMap;
	private Texture2D _temperatureTexture;
	private float[,] _climateMap;
	private Texture2D _climateTexture;
	private float[,] _testMap;
	private Texture2D _testTexture;
	private enum MapTypes
	{
		Test,
		Height,
		Temperature,
		Climate
	}

	public override void _Ready()
	{
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
			_detailFrequency
		);

		(_testMap, _testTexture) = TestMap.GenerateHeightMap(
			_mapSize,
			_seed,
			_baseFrequency,
			_detailFrequency
		);
	}

	private void OnMapDisplayMenuChoice(long id)
	{
		switch ((int)id)
		{
			case (int)MapTypes.Height:
				GD.Print($"Height Map Selected");
				_mapPreview.Texture = _heightTexture;
				break;
			case (int)MapTypes.Temperature:
				GD.Print($"Temperature Map Selected");
				_mapPreview.Texture = _temperatureTexture;
				break;
			case (int)MapTypes.Climate:
				GD.Print($"Climate Map Selected");
				_mapPreview.Texture = _climateTexture;
				break;
			case (int)MapTypes.Test:
				GD.Print($"Test Map Selected");
				_mapPreview.Texture = _testTexture;
				break;
		}

		return;
	}
}
