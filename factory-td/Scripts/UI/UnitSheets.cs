using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>
/// How the unit sprite sheets are laid out (Assets/Sprites/Units/&lt;name&gt;_sheet.png, drawn by
/// tools/art/units). Upright units: rows towards / away / side (left = the side row mirrored), columns
/// idle, walk 1-4, act 1-2. The RC car is a vehicle: drawn from above and turned, one row of wheel frames.
/// </summary>
public static class UnitSheets
{
	public const int Columns = 7;
	public const int Rows = 3;
	public const int WalkColumn = 1, ActColumn = 5;

	public const int VehicleFrames = 2;

	/// <summary>File name in Assets/Sprites/Units: &lt;name&gt;_sheet.png (animation) and &lt;name&gt;.png (UI icon).</summary>
	public static string FileName(UnitType type) => type switch
	{
		UnitType.PlasticSoldier => "pirate",
		UnitType.BrickGolem => "golem",
		UnitType.RcCar => "rc_car",
		UnitType.Builder => "builder",
		UnitType.Farmer => "farmer",
		UnitType.Scout => "scout",
		UnitType.CheeseHunter => "cheese_hunter",
		_ => type.ToString().ToLowerInvariant(),
	};

	/// <summary>Sheet size in pixels: 7x3 cells for upright units, a row of wheel frames for vehicles.</summary>
	public static (int Width, int Height) SheetSize(UnitType type) => IsUpright(type)
		? (Columns * CellSize(type), Rows * CellSize(type))
		: (VehicleFrames * CellSize(type), CellSize(type));

	/// <summary>Drawn upright from the front (not turned); everything else is a vehicle.</summary>
	public static bool IsUpright(UnitType type) => type != UnitType.RcCar;

	/// <summary>Pixels per cell of the sheet.</summary>
	public static int CellSize(UnitType type) => type switch
	{
		UnitType.BrickGolem => 64,
		_ => 48,
	};

	/// <summary>Where the feet are in a cell, from its top: the sprite stands on the unit's position.</summary>
	public static int FeetY(UnitType type) => CellSize(type) - 6;

	/// <summary>The cell for a frame, and whether to mirror it (looking left).</summary>
	public static (int Column, int Row, bool Mirror) Cell(UnitFrame frame)
	{
		int column = frame.Kind switch
		{
			PoseKind.Walk => WalkColumn + frame.Step % 4,
			PoseKind.Act => ActColumn + frame.Step % 2,
			_ => 0,
		};
		int row = frame.Facing switch
		{
			Facing.Toward => 0,
			Facing.Away => 1,
			_ => 2,
		};
		return (column, row, frame.Facing == Facing.Left);
	}
}
