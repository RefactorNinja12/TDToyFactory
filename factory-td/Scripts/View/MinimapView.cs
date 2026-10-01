using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Minimap in the top right corner: what the player has explored (UI.Minimap decides the colours), the
/// camera's view as a rectangle, and a click (or drag) moves the camera there.
/// </summary>
public partial class MinimapView : CanvasLayer
{
	private const float PixelsPerTile = 2f;
	private const double RefreshSeconds = 0.25;

	private static readonly Color[] Colours =
	{
		new(0.03f, 0.06f, 0.19f),   // unknown: the fog's dark blue
		new(0.35f, 0.30f, 0.40f),   // wall
		new(0.42f, 0.29f, 0.22f),   // floor (dark wood)
		new(0.90f, 0.30f, 0.25f),   // bricks
		new(0.30f, 0.65f, 0.95f),   // plastic
		new(0.35f, 0.85f, 0.45f),   // batteries
		new(0.35f, 0.65f, 1.00f),   // own building
		new(1.00f, 0.40f, 0.35f),   // enemy building
		new(0.75f, 0.95f, 1.00f),   // own unit
		new(1.00f, 0.15f, 0.15f),   // enemy unit
		new(0.62f, 0.44f, 0.30f),   // big toy
		new(0.95f, 0.78f, 0.30f),   // cheese
	};

	private World _world;
	private int _localPlayer;
	private Camera2D _camera;
	private byte[] _cells;
	private Image _image;
	private ImageTexture _texture;
	private MapPanel _panel;
	private double _refresh;

	public void Bind(World world, int localPlayer, Camera2D camera)
	{
		_world = world;
		_localPlayer = localPlayer;
		_camera = camera;
		int w = world.Map.Width, h = world.Map.Height;
		_cells = new byte[w * h];
		_image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		_texture = ImageTexture.CreateFromImage(_image);

		var frame = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -w * PixelsPerTile - 20, OffsetRight = -8, OffsetTop = 8 };
		AddChild(frame);
		_panel = new MapPanel { View = this, CustomMinimumSize = new Vector2(w * PixelsPerTile, h * PixelsPerTile), TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
		frame.AddChild(_panel);
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (_world == null)
			return;
		_refresh -= delta;
		if (_refresh <= 0)
		{
			_refresh = RefreshSeconds;
			Refresh();
		}
		_panel.QueueRedraw(); // the camera rectangle follows every frame
	}

	private void Refresh()
	{
		Minimap.Build(_world, _localPlayer, _cells);
		int w = _world.Map.Width;
		for (int i = 0; i < _cells.Length; i++)
		{
			byte cell = _cells[i];
			var colour = Colours[Minimap.Kind(cell)];
			if (Minimap.Kind(cell) != Minimap.Unknown && !Minimap.IsLit(cell))
				colour = colour.Darkened(0.45f);
			_image.SetPixel(i % w, i / w, colour);
		}
		_texture.Update(_image);
	}

	private void MoveCamera(Vector2 local)
	{
		var (x, y) = Minimap.ToTiles(local.X, local.Y, PixelsPerTile);
		_camera.Position = new Vector2(x, y) * BuildingVisuals.TileSize;
	}

	private partial class MapPanel : Control
	{
		public MinimapView View;

		public override void _Draw()
		{
			if (View?._texture == null)
				return;
			DrawTextureRect(View._texture, new Rect2(Vector2.Zero, Size), false);
			var camera = View._camera;
			if (camera == null)
				return;
			var viewSize = camera.GetViewportRect().Size / camera.Zoom / BuildingVisuals.TileSize;
			var center = camera.GetScreenCenterPosition() / BuildingVisuals.TileSize;
			var (x, y, w, h) = Minimap.CameraRect(center.X - viewSize.X / 2, center.Y - viewSize.Y / 2,
				center.X + viewSize.X / 2, center.Y + viewSize.Y / 2, View._world.Map.Width, View._world.Map.Height, PixelsPerTile);
			DrawRect(new Rect2(x, y, w, h), Colors.White, false, 1.5f);
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
			{
				View.MoveCamera(click.Position);
				AcceptEvent();
			}
			else if (@event is InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } drag)
			{
				View.MoveCamera(drag.Position);
				AcceptEvent();
			}
		}
	}
}
