using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>How a unit is drawn.</summary>
public enum UnitLayout
{
	TopDown, // seen from above, nose east, turned the way it goes: one row (idle, walk 1-4, act 1-2) - the mice
	Upright, // seen from the front, never turned: rows towards / away / side (left = side mirrored) - the golem
	Vehicle, // seen from above and turned, one row of wheel frames - the RC car
}

/// <summary>
/// How the unit sprite sheets are laid out (Assets/Sprites/Units/&lt;name&gt;_sheet.png, drawn by
/// tools/art/units): columns idle, walk 1-4, act 1-2 (vehicles: wheel frames); rows per UnitLayout.
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

	/// <summary>Sheet size in pixels: 7x3 cells upright, 7x1 top-down, a row of wheel frames for vehicles.</summary>
	public static (int Width, int Height) SheetSize(UnitType type) => Layout(type) switch
	{
		UnitLayout.Upright => (Columns * CellSize(type), Rows * CellSize(type)),
		UnitLayout.TopDown => (Columns * CellSize(type), CellSize(type)),
		_ => (VehicleFrames * CellSize(type), CellSize(type)),
	};

	public static UnitLayout Layout(UnitType type) => type switch
	{
		UnitType.BrickGolem => UnitLayout.Upright,
		UnitType.RcCar => UnitLayout.Vehicle,
		_ => UnitLayout.TopDown,
	};

	/// <summary>Drawn upright from the front (not turned, stands on its feet).</summary>
	public static bool IsUpright(UnitType type) => Layout(type) == UnitLayout.Upright;

	/// <summary>The column of a frame in a one-row sheet (top-down units): idle, walk 1-4, act 1-2.</summary>
	public static int Column(UnitFrame frame) => frame.Kind switch
	{
		PoseKind.Walk => WalkColumn + frame.Step % 4,
		PoseKind.Act => ActColumn + frame.Step % 2,
		_ => 0,
	};

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
		int column = Column(frame);
		int row = frame.Facing switch
		{
			Facing.Toward => 0,
			Facing.Away => 1,
			_ => 2,
		};
		return (column, row, frame.Facing == Facing.Left);
	}
}
