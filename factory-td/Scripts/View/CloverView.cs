using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Clover lying on the garden's lawn, in the patches UI/Lawn picks: drawn once over the grass tiles (under the
/// deposits, buildings and everything else). Nothing on other maps.
/// </summary>
public partial class CloverView : Node2D
{
	// Held here: a texture only loaded in _Draw would be freed after drawing (a white box).
	private Texture2D _sheet;
	private System.Collections.Generic.List<Clover> _clovers = new();

	public void Bind(MapLayout map)
	{
		_clovers = Lawn.Clovers(map);
		_sheet = _clovers.Count > 0 ? GD.Load<Texture2D>("res://Assets/Sprites/Tiles/clover.png") : null;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_sheet == null)
			return;
		const int size = Lawn.SpriteSize;
		foreach (var clover in _clovers)
			DrawTextureRectRegion(_sheet, new Rect2(clover.X - size / 2, clover.Y - size / 2, size, size),
				new Rect2(clover.Sprite * size, 0, size, size));
	}
}
