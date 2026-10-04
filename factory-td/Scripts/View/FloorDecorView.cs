using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Floor variation (UI/FloorDecor): patches of bare soil on the garden's lawn, rugs on the nursery's floor. Drawn
/// once, flat over the floor tiles and the clover, under the deposits and everything else.
/// </summary>
public partial class FloorDecorView : Node2D
{
	private const float T = BuildingVisuals.TileSize;
	private const int DirtSize = 192; // one patch on Tiles/dirt.png

	// Held here: a texture only loaded in _Draw would be freed after drawing (a white box).
	private readonly Dictionary<DecorKind, Texture2D> _textures = new();
	private List<Decor> _decor = new();

	public void Bind(MapLayout map)
	{
		_decor = FloorDecor.For(map);
		TextureFilter = TextureFilterEnum.Nearest;
		foreach (var d in _decor)
			if (!_textures.ContainsKey(d.Kind))
				_textures[d.Kind] = GD.Load<Texture2D>($"res://Assets/Sprites/Tiles/{Picture(d.Kind)}.png");
		QueueRedraw();
	}

	private static string Picture(DecorKind kind) => kind switch
	{
		DecorKind.Dirt => "dirt",
		DecorKind.RoundRug => "rug_round",
		DecorKind.StripedRug => "rug_striped",
		_ => "rug_playmat",
	};

	public override void _Draw()
	{
		foreach (var d in _decor)
		{
			var rect = new Rect2(d.X * T, d.Y * T, d.Width * T, d.Height * T);
			var texture = _textures[d.Kind];
			if (d.Kind == DecorKind.Dirt)
				DrawTextureRectRegion(texture, rect, new Rect2(d.Variant * DirtSize, 0, DirtSize, DirtSize));
			else
				DrawTextureRect(texture, rect, false);
		}
	}
}
