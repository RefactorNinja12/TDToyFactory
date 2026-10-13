using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the shadows UI/ShadowCaster works out from the light sources: one soft disc stretched and turned
/// per shadow, with one MultiMesh each for the big toys/plants, buildings and units (three draw calls). Sits over
/// the floor and the plants' soil beds and under the buildings, units and toys; never painted into sprites, so it doesn't turn with them.
/// </summary>
public partial class ShadowView : Node2D
{
	private const int Disc = 64; // pixels of the generated soft disc

	private readonly ShadowCaster _caster = new();
	private World _world;
	private int _localPlayer;
	private MultiMesh _obstacles, _buildings, _units;

	public void Bind(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
		var texture = BuildingVisuals.SoftDisc(Disc, 0.55f, 0.8f);
		_obstacles = AddMesh(texture);
		_buildings = AddMesh(texture);
		_units = AddMesh(texture);
	}

	public override void _Process(double delta)
	{
		if (_world == null)
			return;
		_caster.Update(_world, _localPlayer, (float)delta);
		Fill(_obstacles, _caster.Obstacles);
		Fill(_buildings, _caster.Buildings);
		Fill(_units, _caster.Units);
	}

	private static void Fill<T>(MultiMesh mesh, List<(T Thing, Shadow Shadow)> shadows)
	{
		if (mesh.InstanceCount < shadows.Count)
			mesh.InstanceCount = shadows.Count * 2;
		mesh.VisibleInstanceCount = shadows.Count;
		const float ts = BuildingVisuals.TileSize;
		for (int i = 0; i < shadows.Count; i++)
		{
			var s = shadows[i].Shadow;
			var scale = new Vector2(s.Length * ts / Disc, s.Width * ts / Disc);
			mesh.SetInstanceTransform2D(i, new Transform2D(s.Angle, scale, 0, new Vector2(s.X * ts, s.Y * ts)));
			mesh.SetInstanceColor(i, new Color(0, 0, 0, s.Alpha));
		}
	}

	private MultiMesh AddMesh(Texture2D texture)
	{
		var mesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
			UseColors = true,
			Mesh = new QuadMesh { Size = new Vector2(Disc, Disc) },
			InstanceCount = 64,
			VisibleInstanceCount = 0,
		};
		AddChild(new MultiMeshInstance2D { Multimesh = mesh, Texture = texture });
		return mesh;
	}
}
