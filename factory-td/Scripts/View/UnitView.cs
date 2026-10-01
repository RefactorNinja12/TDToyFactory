using System;
using System.Collections.Generic;
using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws all units with one MultiMesh per unit type (one draw call per type however many units there are),
/// interpolated between simulation ticks.
/// </summary>
public partial class UnitView : Node2D
{
	private const float SubTileToPixels = (float)BuildingVisuals.TileSize / UnitStats.SubTile;

	// Placeholder until the team colour shader: tint the enemy's units.
	private static readonly Color[] PlayerTints = { Colors.White, new(1f, 0.55f, 0.55f) };

	private static readonly Dictionary<UnitType, (string Texture, float Size)> Looks = new()
	{
		[UnitType.PlasticSoldier] = ("res://Assets/Sprites/Units/soldier.png", 48f),
		[UnitType.BrickGolem] = ("res://Assets/Sprites/Units/golem.png", 60f),
		[UnitType.RcCar] = ("res://Assets/Sprites/Units/rc_car.png", 44f),
		[UnitType.Builder] = ("res://Assets/Sprites/Units/builder.png", 40f),
		[UnitType.Farmer] = ("res://Assets/Sprites/Units/farmer.png", 40f),
		[UnitType.Scout] = ("res://Assets/Sprites/Units/scout.png", 40f),
		[UnitType.CheeseHunter] = ("res://Assets/Sprites/Units/cheese_hunter.png", 40f),
	};

	private readonly Dictionary<UnitType, MultiMesh> _meshes = new();
	private readonly Dictionary<UnitType, int> _counts = new();
	private readonly Dictionary<int, float> _angles = new();
	private World _world;

	public float Alpha { get; set; }

	/// <summary>Enemy units are only drawn while this player can see them.</summary>
	public int LocalPlayer { get; set; }

	public void Bind(World world) => _world = world;

	public override void _Ready()
	{
		foreach (var type in Enum.GetValues<UnitType>())
		{
			var (texture, size) = Looks[type];
			var mesh = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
				UseColors = true,
				// QuadMesh is y-up; a negative height flips it to match 2D textures.
				Mesh = new QuadMesh { Size = new Vector2(size, -size) },
			};
			AddChild(new MultiMeshInstance2D { Multimesh = mesh, Texture = GD.Load<Texture2D>(texture) });
			_meshes[type] = mesh;
		}
	}

	public override void _Process(double delta)
	{
		if (_world == null)
			return;

		var units = _world.Units;
		foreach (var type in _meshes.Keys)
			_counts[type] = 0;
		foreach (var unit in units)
			if (_world.CanSee(LocalPlayer, unit))
				_counts[unit.Type]++;

		foreach (var (type, mesh) in _meshes)
		{
			if (mesh.InstanceCount < _counts[type])
				mesh.InstanceCount = Mathf.Max(64, _counts[type] * 2); // grow in steps; resizing clears the buffer
			mesh.VisibleInstanceCount = _counts[type];
			_counts[type] = 0; // reused as the write index below
		}

		foreach (var unit in units)
		{
			if (!_world.CanSee(LocalPlayer, unit))
				continue;
			var position = new Vector2(
				Mathf.Lerp(unit.PrevX, unit.X, Alpha),
				Mathf.Lerp(unit.PrevY, unit.Y, Alpha)) * SubTileToPixels;

			// Keep facing the last direction the unit moved or aimed in (sprites face east at angle 0).
			if (unit.MoveX != 0 || unit.MoveY != 0)
				_angles[unit.Id] = Mathf.Atan2(unit.MoveY, unit.MoveX);
			_angles.TryGetValue(unit.Id, out float angle);

			var mesh = _meshes[unit.Type];
			int index = _counts[unit.Type]++;
			mesh.SetInstanceTransform2D(index, new Transform2D(angle, position));
			mesh.SetInstanceColor(index, PlayerTints[unit.Owner % PlayerTints.Length]);
		}

		// Forget the facing of dead units now and then.
		if (_angles.Count > units.Count * 2 + 64)
		{
			var alive = new HashSet<int>();
			foreach (var unit in units)
				alive.Add(unit.Id);
			foreach (var id in new List<int>(_angles.Keys))
				if (!alive.Contains(id))
					_angles.Remove(id);
		}
	}
}
