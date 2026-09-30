using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Fog of war over the map: a texture with one texel per tile (black where never seen, darkened where only
/// explored, clear where lit), stretched over the map with linear filtering so light has soft edges.
/// Also draws remembered enemy buildings as faded ghosts (under the fog) and car headlight cones.
/// </summary>
public partial class FogView : Node2D
{
	private const float T = BuildingVisuals.TileSize;
	private static readonly Color Ghost = new(1f, 1f, 1f, 0.55f);
	private static readonly Color ConeNear = new(1f, 0.93f, 0.6f, 0.30f);
	private static readonly Color ConeFar = new(1f, 0.93f, 0.6f, 0f);

	private World _world;
	private int _localPlayer;
	private byte[] _levels;
	private Image _image;
	private ImageTexture _texture;
	private Sprite2D _fog;
	private GhostLayer _ghosts;
	private long _lastUpdate = -1;

	/// <summary>Interpolation between the last two ticks, for the car cones (set by Game).</summary>
	public float Alpha { get; set; }

	public void Bind(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
		int w = world.Map.Width, h = world.Map.Height;
		_levels = new byte[w * h];
		_image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		_texture = ImageTexture.CreateFromImage(_image);
		_fog = new Sprite2D
		{
			Texture = _texture,
			Centered = false,
			Scale = new Vector2(T, T),
			TextureFilter = TextureFilterEnum.Linear,
			ZIndex = 3,
		};
		AddChild(_fog);
		_ghosts = new GhostLayer { View = this, ZIndex = 1 };
		AddChild(_ghosts);
		ZIndex = 2; // this node itself draws the car cones: above units, under the fog
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (_world == null)
			return;
		long stamp = _world.TickCount / VisionStats.VisionTicks;
		if (stamp != _lastUpdate)
		{
			_lastUpdate = stamp;
			Refresh();
		}
		QueueRedraw();
	}

	private void Refresh()
	{
		FogLevels.Build(_world, _localPlayer, _levels);
		int w = _world.Map.Width;
		for (int i = 0; i < _levels.Length; i++)
		{
			float alpha = _levels[i] switch { FogLevels.Unknown => 1f, FogLevels.Explored => 0.55f, _ => 0f };
			_image.SetPixel(i % w, i / w, new Color(0.03f, 0.02f, 0.08f, alpha));
		}
		_texture.Update(_image);
		_ghosts.QueueRedraw();
	}

	/// <summary>Headlight cones of the cars the player can see.</summary>
	public override void _Draw()
	{
		if (_world == null)
			return;
		foreach (var unit in _world.Units)
		{
			if (unit.Type != UnitType.RcCar || unit.LightDirection < 0 || !_world.CanSee(_localPlayer, unit))
				continue;
			var origin = new Vector2(Mathf.Lerp(unit.PrevX, unit.X, Alpha), Mathf.Lerp(unit.PrevY, unit.Y, Alpha)) / UnitStats.SubTile * T;
			float angle = unit.LightDirection * Mathf.Pi / 4f, spread = Mathf.DegToRad(35f), range = VisionStats.HeadlightRange * T;
			var left = origin + Vector2.FromAngle(angle - spread) * range;
			var right = origin + Vector2.FromAngle(angle + spread) * range;
			DrawPolygon(new[] { origin, left, right }, new[] { ConeNear, ConeFar, ConeFar });
		}
	}

	/// <summary>Remembered enemy buildings out of sight, drawn faded where they were last seen.</summary>
	private partial class GhostLayer : Node2D
	{
		public FogView View;

		public override void _Draw()
		{
			if (View?._world == null)
				return;
			foreach (var ghost in Knowledge.Ghosts(View._world, View._localPlayer))
			{
				var texture = BuildingVisuals.GetTexture(ghost.Type);
				var size = new Vector2(ghost.Width, ghost.Height) * T;
				var center = new Vector2(ghost.X, ghost.Y) * T + size / 2;
				DrawSetTransform(center, BuildingVisuals.Rotation(ghost.Facing));
				DrawTextureRect(texture, new Rect2(-size / 2, size), false, Ghost);
				DrawSetTransform(Vector2.Zero, 0);
			}
		}
	}
}
