using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws every item on every conveyor, interpolated between simulation ticks so movement is smooth.
/// </summary>
public partial class ItemView : Node2D
{
	private const float ItemSize = 40f;
	private const float UnitsToPixels = (float)BuildingVisuals.TileSize / Conveyor.Length;

	private World _world;

	/// <summary>How far we are between the last tick and the next, 0..1. Set by Game every frame.</summary>
	public float Alpha { get; set; }

	public void Bind(World world) => _world = world;

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (_world == null)
			return;

		var half = new Vector2(ItemSize / 2f, ItemSize / 2f);
		var size = new Vector2(ItemSize, ItemSize);
		foreach (var building in _world.Buildings)
		{
			if (building is not Conveyor conveyor)
				continue;
			foreach (var item in conveyor.Items)
			{
				float progress = Mathf.Lerp(item.PrevProgress, item.Progress, Alpha);
				var position = PositionOnBelt(conveyor, item.EntryDirection, progress);
				DrawTextureRect(BuildingVisuals.GetItemTexture(item.Type), new Rect2(position - half, size), tile: false);
			}
		}
	}

	/// <summary>
	/// First half of the tile: from the entry edge to the centre, moving in the entry direction.
	/// Second half: from the centre to the front edge. On a turn this traces the corner.
	/// </summary>
	private static Vector2 PositionOnBelt(Conveyor conveyor, Direction entry, float progress)
	{
		var center = BuildingVisuals.CellCenter(conveyor.X, conveyor.Y);
		float middle = Conveyor.Length / 2f;
		return progress < middle
			? center - BuildingVisuals.ToVector(entry) * (middle - progress) * UnitsToPixels
			: center + BuildingVisuals.ToVector(conveyor.Facing) * (progress - middle) * UnitsToPixels;
	}
}
