using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>
/// The moving treads drawn on the belts' rubber by the belt shader: how fast they move (exactly as fast as the
/// items: Conveyor.Speed progress per tick, Conveyor.Length per tile) and where the rubber is in the sprite
/// (tools/art/logistics/belts.py draws it there: straight y 18-45, curve r 18-45 round the corner (0, 64)).
/// </summary>
public static class BeltLook
{
	public const int TileSize = 64;

	/// <summary>Pixels per second the items (and so the treads) move along a belt.</summary>
	public const float PixelsPerSecond = (float)Conveyor.Speed * World.TicksPerSecond * TileSize / Conveyor.Length;

	/// <summary>Pixels between two tread ridges, and how wide a ridge is.</summary>
	public const float TreadSpacing = 12f, TreadWidth = 2f;

	/// <summary>The rubber between the rails, in sprite pixels from the belt's edge (straight) or the curve's corner.</summary>
	public const float RubberFrom = 18f, RubberTo = 46f;

	/// <summary>Where the ridges are after <paramref name="seconds"/>: 0..1 along one spacing, at a point <paramref name="along"/> the belt.</summary>
	public static float Phase(float along, float seconds)
	{
		float s = (along - seconds * PixelsPerSecond) / TreadSpacing;
		return s - System.MathF.Floor(s);
	}
}
