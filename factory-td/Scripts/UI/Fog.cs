using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>What the local player may be shown, under the fog of war.</summary>
public static class Knowledge
{
	/// <summary>Own buildings always; enemy ones while any of their tiles is lit.</summary>
	public static bool ShowBuilding(World world, int player, Building building)
	{
		if (building.Owner == player)
			return true;
		for (int y = building.Y; y < building.Y + building.Height; y++)
			for (int x = building.X; x < building.X + building.Width; x++)
				if (world.IsVisible(player, x, y))
					return true;
		return false;
	}

	/// <summary>Remembered enemy buildings that are out of sight now: drawn as faded ghosts.</summary>
	public static List<RememberedBuilding> Ghosts(World world, int player)
	{
		var ghosts = new List<RememberedBuilding>();
		foreach (var r in world.RememberedBuildings(player))
		{
			bool inSight = false;
			for (int y = r.Y; y < r.Y + r.Height && !inSight; y++)
				for (int x = r.X; x < r.X + r.Width && !inSight; x++)
					inSight = world.IsVisible(player, x, y);
			if (!inSight)
				ghosts.Add(r);
		}
		return ghosts;
	}
}

/// <summary>Warm light (lanterns, lamps, the toybox, units) or cold electric light (pylons, chargers).</summary>
public enum LightTone { Warm, Cold }

/// <summary>
/// A glowing light to draw: centre and radius in tiles, strength 0..1, and whether it flickers like a
/// small torch (workers). <paramref name="Seed"/> keeps each flickering light out of step with the others.
/// </summary>
public readonly record struct LightSource(float X, float Y, float Radius, LightTone Tone, float Strength = 1f, bool Flicker = false, int Seed = 0);

/// <summary>
/// Which things glow. Every building lights the map (Vision), but only real light sources glow, or every
/// belt would shine: the toybox and lamps (warm), pylons and chargers (cold electric blue) and units
/// (a small warm glow; cars also have their headlight cones). Workers only carry a weak, flickering torch.
/// </summary>
public static class LightSources
{
	/// <summary>Glow strength of pylons (and chargers) and of fighting units; workers are weaker still.</summary>
	public const float PowerStrength = 0.55f, UnitStrength = 0.55f, WorkerStrength = 0.4f;

	/// <summary>
	/// Units on the same 2x2 tiles share one light, a little stronger per extra unit up to this many times
	/// one unit's, so a crowd doesn't add up to a blinding spot.
	/// </summary>
	public const float MaxCrowdBoost = 1.4f, BoostPerExtraUnit = 0.1f;

	/// <summary>The small glow around a shot fired by a unit (soldiers, cars): radius in tiles, strength.</summary>
	public const float ShotRadius = 1.2f, ShotStrength = 0.6f;

	public static List<LightSource> For(World world, int player)
	{
		var lights = new List<LightSource>();
		foreach (var b in world.Buildings)
		{
			if (!b.IsBuilt || !Knowledge.ShowBuilding(world, player, b))
				continue;
			var (tone, radius) = b.Type switch
			{
				BuildingType.Core => (LightTone.Warm, VisionStats.CoreRadius * 0.7f),
				BuildingType.Lamp => (LightTone.Warm, (float)VisionStats.LampRadius),
				BuildingType.Pylon => (LightTone.Cold, (float)PowerStats.PylonRadius),
				BuildingType.BatteryCharger => (LightTone.Cold, VisionStats.SmallBuildingRadius * 1.3f),
				_ => (LightTone.Warm, 0f),
			};
			if (radius > 0)
				lights.Add(new LightSource(Tile(b.CenterX), Tile(b.CenterY), radius, tone, tone == LightTone.Cold ? PowerStrength : 1f));
		}
		// Units grouped per 2x2 tiles (workers and fighters apart: torches flicker, the others don't).
		var crowds = new Dictionary<(int X, int Y, bool Worker), (float SumX, float SumY, float Radius, int Count, int Seed)>();
		var order = new List<(int X, int Y, bool Worker)>();
		foreach (var unit in world.Units)
		{
			if (!world.CanSee(player, unit))
				continue;
			bool worker = UnitStats.IsWorker(unit.Type);
			var key = (unit.TileX / 2, unit.TileY / 2, worker);
			float radius = VisionStats.UnitRadius(unit.Type) * (worker ? 0.45f : 0.6f);
			if (crowds.TryGetValue(key, out var crowd))
				crowds[key] = (crowd.SumX + Tile(unit.X), crowd.SumY + Tile(unit.Y), System.Math.Max(crowd.Radius, radius), crowd.Count + 1, crowd.Seed);
			else
			{
				crowds[key] = (Tile(unit.X), Tile(unit.Y), radius, 1, unit.Id);
				order.Add(key);
			}
		}
		// Shots fired by units carry a small light while they fly (tower shots don't).
		foreach (var shot in world.Projectiles)
		{
			if (shot.Kind != DamageKind.Bullet)
				continue;
			float t = shot.TotalTicks == 0 ? 1f : 1f - shot.TicksLeft / (float)shot.TotalTicks;
			float x = Tile(shot.FromX) + (Tile(shot.ToX) - Tile(shot.FromX)) * t;
			float y = Tile(shot.FromY) + (Tile(shot.ToY) - Tile(shot.FromY)) * t;
			if (world.IsVisible(player, (int)x, (int)y))
				lights.Add(new LightSource(x, y, ShotRadius, LightTone.Warm, ShotStrength));
		}

		foreach (var key in order)
		{
			var crowd = crowds[key];
			float boost = System.Math.Min(MaxCrowdBoost, 1f + BoostPerExtraUnit * (crowd.Count - 1));
			float strength = (key.Worker ? WorkerStrength : UnitStrength) * boost;
			lights.Add(new LightSource(crowd.SumX / crowd.Count, crowd.SumY / crowd.Count, crowd.Radius, LightTone.Warm,
				strength, Flicker: key.Worker, Seed: crowd.Seed));
		}
		return lights;
	}

	private static float Tile(int subTile) => subTile / (float)UnitStats.SubTile;
}

/// <summary>Per tile: 0 = never seen (black), 1 = explored but dark, 2 = lit now. For the fog texture.</summary>
public static class FogLevels
{
	public const byte Unknown = 0, Explored = 1, Lit = 2;

	public static void Build(World world, int player, byte[] into)
	{
		int width = world.Map.Width;
		for (int i = 0; i < into.Length; i++)
		{
			int x = i % width, y = i / width;
			into[i] = world.IsVisible(player, x, y) ? Lit : world.IsExplored(player, x, y) ? Explored : Unknown;
		}
	}
}

/// <summary>
/// The minimap: one pixel per tile. The low bits say what to show, <see cref="LitBit"/> whether it is
/// lit now (drawn bright) or only remembered (drawn darker).
/// </summary>
public static class Minimap
{
	public const byte Unknown = 0, Wall = 1, Floor = 2, Bricks = 3, Plastic = 4, Batteries = 5,
		OwnBuilding = 6, EnemyBuilding = 7, OwnUnit = 8, EnemyUnit = 9, Toy = 10;
	public const byte LitBit = 0x10;

	public static byte Kind(byte cell) => (byte)(cell & 0x0F);
	public static bool IsLit(byte cell) => (cell & LitBit) != 0;

	public static void Build(World world, int player, byte[] into)
	{
		var map = world.Map;
		int width = map.Width;
		for (int i = 0; i < into.Length; i++)
		{
			int x = i % width, y = i / width;
			if (!world.IsExplored(player, x, y))
			{
				into[i] = Unknown;
				continue;
			}
			byte kind = map[x, y] == TileType.Wall ? Wall : map[x, y] == TileType.Obstacle ? Toy : map.GetResource(x, y) switch
			{
				ResourceType.Brick => Bricks,
				ResourceType.Plastic => Plastic,
				ResourceType.Battery => Batteries,
				_ => Floor,
			};
			into[i] = (byte)(kind | (world.IsVisible(player, x, y) ? LitBit : 0));
		}

		foreach (var r in world.RememberedBuildings(player))
			Paint(into, width, r.X, r.Y, r.Width, r.Height, EnemyBuilding);
		foreach (var b in world.Buildings)
			if (b.Owner == player)
				Paint(into, width, b.X, b.Y, b.Width, b.Height, OwnBuilding);
		foreach (var unit in world.Units)
		{
			if (!world.CanSee(player, unit))
				continue;
			into[unit.TileY * width + unit.TileX] = (byte)((unit.Owner == player ? OwnUnit : EnemyUnit) | LitBit);
		}
	}

	/// <summary>A point on the minimap (<paramref name="scale"/> pixels per tile) as a map position in tiles.</summary>
	public static (float X, float Y) ToTiles(float px, float py, float scale) => (px / scale, py / scale);

	/// <summary>The camera's view (in tiles) as a rectangle on the minimap, clamped to the map.</summary>
	public static (float X, float Y, float W, float H) CameraRect(float left, float top, float right, float bottom, int mapWidth, int mapHeight, float scale)
	{
		float x0 = System.Math.Clamp(left, 0, mapWidth), y0 = System.Math.Clamp(top, 0, mapHeight);
		float x1 = System.Math.Clamp(right, 0, mapWidth), y1 = System.Math.Clamp(bottom, 0, mapHeight);
		return (x0 * scale, y0 * scale, (x1 - x0) * scale, (y1 - y0) * scale);
	}

	private static void Paint(byte[] into, int width, int x, int y, int w, int h, byte kind)
	{
		for (int ty = y; ty < y + h; ty++)
			for (int tx = x; tx < x + w; tx++)
			{
				int i = ty * width + tx;
				into[i] = (byte)(kind | (into[i] & LitBit));
			}
	}
}
