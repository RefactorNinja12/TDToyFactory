using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the fighting on top of everything in the world: projectiles, tower ammo bars
/// (with a warning when a tower is empty) and health bars on damaged units.
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
			DrawTextureRect(BuildingVisuals.GetItemTexture(unit.Carrying), new Rect2(at + new Vector2(-2, -30), new Vector2(22, 22)), tile: false);
		}
		int max = UnitStats.MaxHealth(unit.Type);
		if (unit.Health >= max)
			return;
		var p = Interpolate(unit.PrevX, unit.PrevY, unit.X, unit.Y);
		var bar = new Rect2(p.X - 16, p.Y - 28, 32, 4);
		DrawRect(bar, BarBack);
		DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Max(0, unit.Health) / max, bar.Size.Y)), HealthColor);
	}

	private void DrawProjectile(Projectile shot)
	{
		// Progress through the flight, 0 at the tower and 1 on impact.
		float t = Mathf.Clamp((shot.TotalTicks - shot.TicksLeft + Alpha) / shot.TotalTicks, 0f, 1f);
		var from = new Vector2(shot.FromX, shot.FromY) * SubTileToPixels;
		var to = new Vector2(shot.ToX, shot.ToY) * SubTileToPixels;
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
