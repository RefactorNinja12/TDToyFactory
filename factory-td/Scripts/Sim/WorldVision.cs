using System.Collections.Generic;

namespace FactoryTD.Sim;

// World partial: what each player knows (explored and visible tiles, remembered enemy buildings; see Vision).
public sealed partial class World
{
	/// <summary>
	/// Test switch: every tile counts as explored and visible for every player (no fog of war).
	/// Lets tests of other systems ignore scouting. Always false in real matches.
	/// </summary>
	internal bool FullVision { get; set; }

	/// <summary>Whether the player has ever lit this tile (fog of war).</summary>
	public bool IsExplored(int player, int x, int y) => FullVision || _vision.IsExplored(player, x, y);

	/// <summary>Whether the player knows where this unit is: its own, or standing in the player's light.</summary>
	public bool CanSee(int player, Unit unit) => unit.Owner == player || IsVisible(player, unit.TileX, unit.TileY);

	/// <summary>
	/// Enemy buildings as the player last saw them (with full vision: all of them, as they are now).
	/// Own buildings are always known and not listed.
	/// </summary>
	public IReadOnlyList<RememberedBuilding> RememberedBuildings(int player)
	{
		if (!FullVision)
			return _vision.Remembered(player);
		var all = new List<RememberedBuilding>();
		foreach (var b in _buildings)
			if (b.Owner != player)
				all.Add(new RememberedBuilding(b.Type, b.X, b.Y, b.Width, b.Height, b.Owner, b.Facing));
		return all;
	}

	/// <summary>Number of tiles the player has explored.</summary>
	public int ExploredCount(int player) => _vision.ExploredCount(player);

	/// <summary>Test helper: the player has seen the whole map (but still only sees what is lit now).</summary>
	internal void ExploreAll(int player) => _vision.ExploreAll(player);

	/// <summary>Whether the player's light reaches this tile right now.</summary>
	public bool IsVisible(int player, int x, int y) => FullVision || _vision.IsVisible(player, x, y);
}
