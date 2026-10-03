using System;
using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws all units with one MultiMesh per unit type (one draw call per type however many units there are),
/// interpolated between simulation ticks. Each instance shows one cell of its type's sprite sheet (UI/UnitSheets),
/// picked by a small shader from the instance's custom data (column, row, mirrored), as UI/UnitAnimation says:
/// upright units stand on their feet at their position and are never turned (sorted by y, front ones on top);
/// the RC car is turned the way it drives.
/// </summary>
public partial class UnitView : Node2D
{
	private const float SubTileToPixels = (float)BuildingVisuals.TileSize / UnitStats.SubTile;

	// Placeholder until the team colour shader: tint the enemy's units.
	/// <summary>Team colours (also used for the mouse running on a treadmill).</summary>
	internal static readonly Color[] PlayerTints = { Colors.White, new(1f, 0.55f, 0.55f) };

	private const string CellShader = @"
shader_type canvas_item;
uniform vec2 cells = vec2(7.0, 3.0);
varying vec4 cell;
varying vec4 tint;
void vertex() { cell = INSTANCE_CUSTOM; tint = COLOR; }
void fragment() {
	// COLOR here already holds the whole texture sampled at UV: use the instance colour from the vertex instead.
	vec2 uv = UV;
	if (cell.z > 0.5) uv.x = 1.0 - uv.x;
	COLOR = texture(TEXTURE, (cell.xy + uv) / cells) * tint;
}";

	private readonly Dictionary<UnitType, MultiMesh> _meshes = new();
	private readonly Dictionary<UnitType, List<(float Y, Unit Unit, Vector2 Position)>> _visible = new();
	private readonly Dictionary<int, float> _angles = new();
	private readonly UnitAnimation _animation = new();
	private World _world;

	public float Alpha { get; set; }

	/// <summary>Enemy units are only drawn while this player can see them.</summary>
	public int LocalPlayer { get; set; }

	public void Bind(World world) => _world = world;

	public override void _Ready()
	{
		var shader = new Shader { Code = CellShader };
		foreach (var type in Enum.GetValues<UnitType>())
		{
			float size = UnitSheets.CellSize(type);
			var mesh = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
				UseColors = true,
				UseCustomData = true,
				// QuadMesh is y-up; a negative height flips it to match 2D textures.
				Mesh = new QuadMesh { Size = new Vector2(size, -size) },
			};
			var (columns, rows) = UnitSheets.IsUpright(type) ? (UnitSheets.Columns, UnitSheets.Rows) : (UnitSheets.VehicleFrames, 1);
			var material = new ShaderMaterial { Shader = shader };
			material.SetShaderParameter("cells", new Vector2(columns, rows));
			AddChild(new MultiMeshInstance2D
			{
				Multimesh = mesh,
				Texture = BuildingVisuals.GetUnitSheet(type),
				Material = material,
				TextureFilter = TextureFilterEnum.Nearest,
			});
			_meshes[type] = mesh;
			_visible[type] = new List<(float, Unit, Vector2)>();
		}
	}

	public override void _Process(double delta)
	{
		if (_world == null)
			return;
		_animation.Observe(_world);

		foreach (var list in _visible.Values)
			list.Clear();
		foreach (var unit in _world.Units)
		{
			if (!_world.CanSee(LocalPlayer, unit))
				continue;
			var position = new Vector2(
				Mathf.Lerp(unit.PrevX, unit.X, Alpha),
				Mathf.Lerp(unit.PrevY, unit.Y, Alpha)) * SubTileToPixels;
			_visible[unit.Type].Add((position.Y, unit, position));
		}

		foreach (var (type, mesh) in _meshes)
		{
			var list = _visible[type];
			if (mesh.InstanceCount < list.Count)
				mesh.InstanceCount = Mathf.Max(64, list.Count * 2); // grow in steps; resizing clears the buffer
			mesh.VisibleInstanceCount = list.Count;
			bool upright = UnitSheets.IsUpright(type);
			if (upright)
				list.Sort((a, b) => a.Y.CompareTo(b.Y)); // the ones further down the screen are in front
														 // Upright sprites stand on their feet: lift the quad so the feet line sits on the position.
			var lift = new Vector2(0, UnitSheets.CellSize(type) / 2f - UnitSheets.FeetY(type));
			for (int i = 0; i < list.Count; i++)
			{
				var (_, unit, position) = list[i];
				var frame = _animation.FrameOf(unit);
				if (upright)
				{
					var (column, row, mirror) = UnitSheets.Cell(frame);
					mesh.SetInstanceTransform2D(i, new Transform2D(0, position + lift));
					mesh.SetInstanceCustomData(i, new Color(column, row, mirror ? 1 : 0, 0));
				}
				else
				{
					// Vehicles face east at angle 0 and keep the last direction they moved or aimed in.
					if (unit.MoveX != 0 || unit.MoveY != 0)
						_angles[unit.Id] = Mathf.Atan2(unit.MoveY, unit.MoveX);
					_angles.TryGetValue(unit.Id, out float angle);
					int wheel = frame.Kind == PoseKind.Walk ? frame.Step % UnitSheets.VehicleFrames : 0;
					mesh.SetInstanceTransform2D(i, new Transform2D(angle, position));
					mesh.SetInstanceCustomData(i, new Color(wheel, 0, 0, 0));
				}
				mesh.SetInstanceColor(i, PlayerTints[unit.Owner % PlayerTints.Length]);
			}
		}

		// Forget the facing of dead vehicles now and then.
		if (_angles.Count > 64)
		{
			var alive = new HashSet<int>();
			foreach (var unit in _world.Units)
				alive.Add(unit.Id);
			foreach (var id in new List<int>(_angles.Keys))
				if (!alive.Contains(id))
					_angles.Remove(id);
		}
	}
}
