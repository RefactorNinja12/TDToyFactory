using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Fog of war over the map: a texture with one texel per tile (black where never seen, darkened where only
/// explored, clear where lit), drawn over the map by Shaders/fog.gdshader as small fog pixels with a
/// stepped gradient from light to dark.
/// Walls are marked in the texture's red channel so the shader can draw their outlines through the fog.
/// Also draws remembered enemy buildings as faded ghosts (under the fog), and the glow of light sources
/// (warm lanterns and units, cold electric pylons) plus car headlight cones, added on top of the map.
/// </summary>
public partial class FogView : Node2D
{
	private const float T = BuildingVisuals.TileSize;
	private static readonly Color Ghost = new(1f, 1f, 1f, 0.55f);
	// Each light is drawn twice: a tint (normal blending, so blue really looks blue on the warm floor) and
	// a brightening (additive). Colours: [tint, add].
	private static readonly Color[] Warm = { new(1f, 0.68f, 0.25f, 0.28f), new(1f, 0.8f, 0.5f, 0.2f) };
	private static readonly Color[] Cold = { new(0.15f, 0.45f, 1f, 0.5f), new(0.45f, 0.72f, 1f, 0.15f) };
	private static readonly Color[] Cone = { new(1f, 0.75f, 0.3f, 0.3f), new(1f, 0.85f, 0.55f, 0.35f) };

	private World _world;
	private int _localPlayer;
	private byte[] _levels;
	private Image _image;
	private ImageTexture _texture;
	private Sprite2D _fog;
	private GhostLayer _ghosts;
	private LightLayer _tint;
	private LightLayer _lights;
	private bool[] _walls;
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
			// Small fog pixels with a stepped soft edge instead of one big block per tile.
			Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/fog.gdshader") },
		};
		AddChild(_fog);
		_ghosts = new GhostLayer { View = this, ZIndex = 1 };
		AddChild(_ghosts);
		// Light glows are added onto what is under them (above units, under the fog).
		_tint = new LightLayer { View = this, Layer = 0, ZIndex = 2 };
		AddChild(_tint);
		_lights = new LightLayer { View = this, Layer = 1, ZIndex = 2, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
		AddChild(_lights);
		_walls = new bool[w * h];
		for (int i = 0; i < _walls.Length; i++)
			_walls[i] = world.Map[i % w, i / w] == TileType.Wall;
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
		_tint.QueueRedraw();
		_lights.QueueRedraw();
	}

	private void Refresh()
	{
		FogLevels.Build(_world, _localPlayer, _levels);
		int w = _world.Map.Width;
		for (int i = 0; i < _levels.Length; i++)
		{
			float alpha = _levels[i] switch { FogLevels.Unknown => 1f, FogLevels.Explored => 0.62f, _ => 0f };
			_image.SetPixel(i % w, i / w, new Color(_walls[i] ? 1f : 0f, 0f, 0f, alpha));
		}
		_texture.Update(_image);
		_ghosts.QueueRedraw();
	}

	/// <summary>
	/// Glows of the light sources the player can see (UI.LightSources decides which and how warm), and car
	/// headlight cones. Drawn with additive blending, so light brightens and tints what is under it.
	/// </summary>
	private partial class LightLayer : Node2D
	{
		public FogView View;
		/// <summary>0 = tint (normal blending), 1 = brighten (additive).</summary>
		public int Layer;
		private Texture2D _glow;

		public override void _Ready() => _glow = BuildingVisuals.SoftDisc(128, 0.45f, 0.45f);

		public override void _Draw()
		{
			var world = View?._world;
			if (world == null || _glow == null)
				return;
			// Electric light hums a little.
			float hum = 0.9f + 0.1f * Mathf.Sin((float)Time.GetTicksMsec() / 180f);
			foreach (var light in LightSources.For(world, View._localPlayer))
			{
				var center = new Vector2(light.X, light.Y) * T;
				float radius = light.Radius * T;
				var colour = light.Tone == LightTone.Cold ? Cold[Layer] with { A = Cold[Layer].A * hum } : Warm[Layer];
				colour.A *= light.Strength;
				if (light.Flicker)
				{
					// A small torch: uneven flicker (three sines out of step), the flame also grows and shrinks a bit.
					float t = (float)Time.GetTicksMsec() / 1000f + light.Seed * 1.7f;
					float flicker = 0.75f + 0.12f * Mathf.Sin(t * 9.1f) + 0.08f * Mathf.Sin(t * 23.7f) + 0.05f * Mathf.Sin(t * 41.3f);
					colour.A *= flicker;
					radius *= 0.92f + 0.08f * flicker;
					colour = colour.Lerp(new Color(1f, 0.5f, 0.15f, colour.A), 0.35f); // more orange, like a flame
				}
				DrawTextureRect(_glow, new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2, radius * 2)), false, colour);
			}

			foreach (var unit in world.Units)
			{
				if (unit.Type != UnitType.RcCar || unit.LightDirection < 0 || !world.CanSee(View._localPlayer, unit))
					continue;
				var origin = new Vector2(Mathf.Lerp(unit.PrevX, unit.X, View.Alpha), Mathf.Lerp(unit.PrevY, unit.Y, View.Alpha)) / UnitStats.SubTile * T;
				float angle = unit.LightDirection * Mathf.Pi / 4f, spread = Mathf.DegToRad(35f), range = VisionStats.HeadlightRange * T;
				var left = origin + Vector2.FromAngle(angle - spread) * range;
				var right = origin + Vector2.FromAngle(angle + spread) * range;
				var far = Cone[Layer] with { A = 0 };
				DrawPolygon(new[] { origin, left, right }, new[] { Cone[Layer], far, far });
			}
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
