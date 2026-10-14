using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the fighting on top of everything in the world: projectiles, tower ammo bars
/// (with a warning when a tower is empty), health bars on damaged units and the shock ring of a golem's slam.
/// </summary>
public partial class CombatView : Node2D
{
	private const float SubTileToPixels = (float)BuildingVisuals.TileSize / UnitStats.SubTile;

	private static readonly Color FoamColor = new(1f, 0.55f, 0.1f);
	private static readonly Color WaterColor = new(0.45f, 0.75f, 1f, 0.9f);
	private static readonly Color BarBack = new(0f, 0f, 0f, 0.6f);
	private static readonly Color AmmoColor = new(1f, 0.85f, 0.2f);
	private static readonly Color HealthColor = new(0.3f, 0.95f, 0.3f);
	private static readonly Color EmptyColor = new(1f, 0.25f, 0.2f);

	private World _world;

	public float Alpha { get; set; }

	public void Bind(World world) => _world = world;

	/// <summary>Only what this player can see is drawn.</summary>
	public int LocalPlayer { get; set; }

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (_world == null)
			return;

		foreach (var building in _world.Buildings)
		{
			if (!Knowledge.ShowBuilding(_world, LocalPlayer, building))
				continue;
			if (building is Tower tower)
				DrawAmmo(tower);
			DrawBuildingHealth(building);
		}

		foreach (var impact in _world.RecentImpacts)
			if (_world.IsVisible(LocalPlayer, impact.X / UnitStats.SubTile, impact.Y / UnitStats.SubTile))
			{
				if (impact.Bolt)
					DrawBolt(impact);
				else
					DrawSlam(impact);
			}

		foreach (var unit in _world.Units)
			if (_world.CanSee(LocalPlayer, unit))
				DrawHealth(unit);

		foreach (var shot in _world.Projectiles)
			if (_world.IsVisible(LocalPlayer, shot.ToX / UnitStats.SubTile, shot.ToY / UnitStats.SubTile) ||
				_world.IsVisible(LocalPlayer, shot.FromX / UnitStats.SubTile, shot.FromY / UnitStats.SubTile))
				DrawProjectile(shot);
	}

	private void DrawAmmo(Tower tower)
	{
		var center = BuildingVisuals.CellCenter(tower.X, tower.Y);
		var bar = new Rect2(center.X - 24, center.Y + 26, 48, 5);
		DrawRect(bar, BarBack);
		if (tower.Shots > 0)
		{
			DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * tower.Shots / tower.Stats.MaxShots, bar.Size.Y)), AmmoColor);
			return;
		}

		// Out of ammo: blinking "!" and a small picture of the ammo it wants.
		if ((Time.GetTicksMsec() / 400) % 2 == 0)
		{
			DrawCircle(center + new Vector2(20, -20), 10, EmptyColor);
			DrawString(ThemeDB.FallbackFont, center + new Vector2(16, -13), "!", fontSize: 18, modulate: Colors.White);
		}
		DrawTextureRect(BuildingVisuals.GetItemTexture(tower.Stats.Ammo), new Rect2(center + new Vector2(-30, -30), new Vector2(22, 22)), tile: false, modulate: new Color(1, 1, 1, 0.8f));
	}

	private void DrawBuildingHealth(Building building)
	{
		if (!building.IsBuilt)
		{
			DrawConstruction(building);
			return;
		}
		if (building.Health >= building.MaxHealth)
			return;
		var center = BuildingVisuals.FootprintCenter(building);
		float width = building.Width * BuildingVisuals.TileSize * 0.75f;
		float top = center.Y - building.Height * BuildingVisuals.TileSize / 2f - 2;
		var bar = new Rect2(center.X - width / 2, top, width, 6);
		DrawRect(bar, BarBack);
		DrawRect(new Rect2(bar.Position, new Vector2(width * building.Health / building.MaxHealth, bar.Size.Y)), EmptyColor);
	}

	private static readonly Color ConstructionColor = new(0.35f, 0.75f, 1f);

	/// <summary>Build progress bar across the middle of a construction site.</summary>
	private void DrawConstruction(Building building)
	{
		var center = BuildingVisuals.FootprintCenter(building);
		float width = building.Width * BuildingVisuals.TileSize * 0.7f;
		var bar = new Rect2(center.X - width / 2, center.Y - 4, width, 8);
		DrawRect(bar, BarBack);
		float done = building.BuildTime == 0 ? 1f : (float)building.BuildWork / building.BuildTime;
		DrawRect(new Rect2(bar.Position, new Vector2(width * done, bar.Size.Y)), ConstructionColor);
	}

	private void DrawHealth(Unit unit)
	{
		if (unit.CarryAmount > 0)
		{
			var at = Interpolate(unit.PrevX, unit.PrevY, unit.X, unit.Y);
			// In the arms of the upright mouse, at the side.
			DrawTextureRect(BuildingVisuals.GetItemTexture(unit.Carrying), new Rect2(at + new Vector2(4, -26), new Vector2(18, 18)), tile: false);
		}
		int max = UnitStats.MaxHealth(unit.Type);
		if (unit.Health >= max)
			return;
		var p = Interpolate(unit.PrevX, unit.PrevY, unit.X, unit.Y) -
			new Vector2(0, UnitAnimation.Lift(unit, (int)(Alpha * 1000)) * SubTileToPixels); // up in the air with it
																							 // Above the head: upright units stand on their position, vehicles are centred on it.
		float top = UnitSheets.IsUpright(unit.Type) ? UnitSheets.FeetY(unit.Type) + 2 : 28;
		var bar = new Rect2(p.X - 16, p.Y - top, 32, 4);
		DrawRect(bar, BarBack);
		DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Max(0, unit.Health) / max, bar.Size.Y)), HealthColor);
	}

	private const float BulletHeight = 16f;

	private const float SlamRingTicks = 12f;
	private const int SlamDust = 10;
	private static readonly Color SlamColor = new(0.95f, 0.85f, 0.6f);

	/// <summary>The slam: a ring running out over the floor to the slam's reach, and dust puffs thrown outwards.</summary>
	private void DrawSlam(ImpactMark slam)
	{
		float age = (_world.TickCount - slam.Tick + Alpha) / SlamRingTicks;
		if (age >= 1)
			return;
		var at = new Vector2(slam.X, slam.Y) * SubTileToPixels;
		float reach = GolemStats.SlamRadius * SubTileToPixels;
		float radius = reach * (0.3f + 0.7f * Mathf.Sqrt(age));
		float fade = 1 - age;
		DrawArc(at, radius, 0, Mathf.Tau, 40, new Color(SlamColor, 0.3f * fade), 10);
		DrawArc(at, radius, 0, Mathf.Tau, 40, new Color(SlamColor, 0.9f * fade), 3);
		for (int i = 0; i < SlamDust; i++)
		{
			float angle = Mathf.Tau * (i + 0.5f * (i % 2)) / SlamDust;
			var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f);
			DrawCircle(at + direction * reach * (0.2f + 0.6f * age), 4 + 5 * age, new Color(0.75f, 0.68f, 0.55f, 0.6f * fade));
		}
	}

	private static readonly Color BoltGlow = new(0.45f, 0.75f, 1f);
	private static readonly Color BoltCore = new(1f, 1f, 0.92f);

	/// <summary>A thunderbolt that struck: the jagged pillar falling onto the spot, a flash, sparks over a small ring.</summary>
	private void DrawBolt(ImpactMark bolt)
	{
		float age = _world.TickCount - bolt.Tick + Alpha;
		var at = new Vector2(bolt.X, bolt.Y) * SubTileToPixels;
		if (BoltLook.Striking(age))
		{
			DrawPillar(at, bolt.Tick + (long)age, age, BoltLook.StrikeFade(age));
			DrawCircle(at, 16, new Color(BoltCore, 0.7f * BoltLook.StrikeFade(age)));
		}
		float fade = BoltLook.SparkFade(age);
		if (fade <= 0)
			return;
		float radius = WizardStats.BoltRadius * SubTileToPixels;
		DrawArc(at, radius * (0.5f + 0.5f * (1 - fade)), 0, Mathf.Tau, 28, new Color(BoltGlow, 0.7f * fade), 2);
		foreach (var spark in BoltLook.Sparks(at.X, at.Y, age, radius))
		{
			var points = new Vector2[spark.Length];
			for (int i = 0; i < spark.Length; i++)
				points[i] = new Vector2(spark[i].X, spark[i].Y);
			DrawPolyline(points, new Color(BoltGlow, fade), 3);
			DrawPolyline(points, new Color(BoltCore, fade), 1);
		}
	}

	/// <summary>The bolt's pillar from the sky down to the spot: a wide blue glow round a bright core.</summary>
	private void DrawPillar(Vector2 at, long tick, float age, float brightness)
	{
		var pillar = BoltLook.Pillar(at.X, at.Y, tick, age);
		var points = new Vector2[pillar.Count];
		for (int i = 0; i < pillar.Count; i++)
			points[i] = new Vector2(pillar[i].X, pillar[i].Y);
		DrawPolyline(points, new Color(BoltGlow, 0.35f * brightness), 12);
		DrawPolyline(points, new Color(BoltGlow, 0.8f * brightness), 6);
		DrawPolyline(points, new Color(BoltCore, brightness), 2.5f);
	}

	private void DrawProjectile(Projectile shot)
	{
		// Progress through the flight, 0 at the tower and 1 on impact.
		float t = Mathf.Clamp((shot.TotalTicks - shot.TicksLeft + Alpha) / shot.TotalTicks, 0f, 1f);
		var from = new Vector2(shot.FromX, shot.FromY) * SubTileToPixels;
		var to = new Vector2(shot.ToX, shot.ToY) * SubTileToPixels;
		if (shot.Kind == DamageKind.Bullet)
		{
			// Units stand on their position: bullets fly at hand height, not along the floor.
			from.Y -= BulletHeight;
			to.Y -= BulletHeight;
		}
		var p = from.Lerp(to, t);

		switch (shot.Kind)
		{
			case DamageKind.Area:
				// A brick thrown in an arc: bigger at the top of the arc.
				float lift = 4f * t * (1f - t);
				var size = new Vector2(24, 24) * (1f + lift * 0.8f);
				DrawTextureRect(BuildingVisuals.GetItemTexture(ItemType.Brick), new Rect2(p - size / 2 - new Vector2(0, lift * 40), size), tile: false);
				if (shot.TicksLeft <= 1)
					DrawArc(to, shot.AreaRadius * SubTileToPixels, 0, Mathf.Tau, 24, new Color(1, 0.8f, 0.3f, 0.8f), 3);
				break;
			case DamageKind.Bullet:
				DrawCircle(p, 3, new Color(1f, 0.95f, 0.5f));
				break;
			case DamageKind.Laser:
				// A beam for the whole (short) flight: a wide glow and a bright core.
				DrawLine(from, to, new Color(1f, 0.2f, 0.2f, 0.35f), 7);
				DrawLine(from, to, new Color(1f, 0.85f, 0.85f), 2);
				break;
			case DamageKind.Punch:
				// A chunk of brick knocked towards the target, with a flash on impact.
				DrawRect(new Rect2(p - new Vector2(4, 4), new Vector2(8, 8)), new Color(0.9f, 0.2f, 0.25f));
				if (shot.TicksLeft <= 1)
					DrawCircle(to, 10, new Color(1f, 0.9f, 0.6f, 0.6f));
				break;
			case DamageKind.Slash:
				// The sword's sweep: a bright arc round the samurai through the unit it cuts.
				var (radius, start, end) = SlashLook.Arc(from.X, from.Y, to.X, to.Y);
				float fade = SlashLook.Fade(t);
				DrawArc(from, radius, start, end, 10, new Color(1f, 1f, 1f, 0.35f * fade), 7);
				DrawArc(from, radius, start, end, 10, new Color(1f, 1f, 1f, fade), 2);
				break;
			case DamageKind.Lightning:
				// Gathering over the spot: crackles on the floor that grow until it strikes (a single bolt at a building
				// strikes in its last tick here; the area one is drawn from the world's impacts).
				float w = BoltLook.Warning(shot.TotalTicks, shot.TicksLeft, Alpha);
				float r = Mathf.Max(shot.AreaRadius * SubTileToPixels, 20) * (0.35f + 0.65f * w);
				for (int i = 0; i < 6; i++)
				{
					float a = Mathf.Tau * i / 6 + (_world.TickCount % 6) * 0.5f;
					DrawArc(to, r, a, a + 0.5f, 4, new Color(BoltGlow, 0.3f + 0.6f * w), 2);
				}
				DrawCircle(to - new Vector2(0, 26), 4 + 8 * w, new Color(BoltGlow, 0.25f * w));
				if (shot.AreaRadius == 0 && shot.TicksLeft <= 1)
					DrawPillar(to, _world.TickCount, 1, 1);
				break;
			case DamageKind.Water:
				DrawCircle(p, 5, WaterColor);
				DrawCircle(from.Lerp(to, Mathf.Max(0, t - 0.15f)), 3, WaterColor);
				break;
			default:
				var direction = (to - from).Normalized() * 7;
				DrawLine(p - direction, p + direction, FoamColor, 4);
				DrawCircle(p + direction, 2.5f, new Color(0.2f, 0.45f, 0.9f));
				break;
		}
	}

	private Vector2 Interpolate(int prevX, int prevY, int x, int y) =>
		new Vector2(Mathf.Lerp(prevX, x, Alpha), Mathf.Lerp(prevY, y, Alpha)) * SubTileToPixels;
}
