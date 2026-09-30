namespace FactoryTD.Sim;

public enum BuildingType : byte
{
	Conveyor,
	BrickExtractor,
	PlasticExtractor,
	BatteryExtractor,
	Core,
	Splitter,
	Sorter,
	Assembler,
	SoldierFactory,
	FoamTower,
	Catapult,
	WaterTower,
	GolemWorkshop,
	CarFactory,
	LaserTower,
	Junction,
	Toolbox,
	Warehouse,
	CropField,
	Farmhouse,
	Kitchen,
}

/// <summary>Facing / output direction. Clockwise order, matching Godot's y-down rotation.</summary>
public enum Direction : byte
{
	East,
	South,
	West,
	North,
}

public static class DirectionExtensions
{
	public static Direction RotatedClockwise(this Direction d) => (Direction)(((int)d + 1) & 3);
	public static Direction RotatedCounterClockwise(this Direction d) => (Direction)(((int)d + 3) & 3);
	public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) & 3);

	public static int DX(this Direction d) => d switch { Direction.East => 1, Direction.West => -1, _ => 0 };
	public static int DY(this Direction d) => d switch { Direction.South => 1, Direction.North => -1, _ => 0 };
}

public enum PlaceError : byte
{
	None,
	NotFloor,
	Occupied,
	WrongResource,
	NotEnoughResources,
	OutsideZone,
}

/// <summary>
/// Base for everything placed on the grid. (X, Y) is the top-left tile; Facing is the main output side.
/// </summary>
public abstract class Building
{
	public BuildingType Type { get; }
	public int X { get; }
	public int Y { get; }
	public Direction Facing { get; }
	public int Owner { get; }

	/// <summary>Size in tiles. (X, Y) is the top-left tile.</summary>
	public int Width { get; }
	public int Height { get; }

	protected Building(BuildingType type, int x, int y, Direction facing, int owner)
	{
		Type = type;
		X = x;
		Y = y;
		Facing = facing;
		Owner = owner;
		(Width, Height) = BuildingRules.Size(type);
		Health = BuildingRules.MaxHealth(type);
	}

	public int Health { get; private set; }
	public int MaxHealth => BuildingRules.MaxHealth(Type);

	/// <summary>Centre of the footprint in sub-tile units (UnitStats.SubTile per tile).</summary>
	public int CenterX => X * UnitStats.SubTile + Width * UnitStats.SubTile / 2;
	public int CenterY => Y * UnitStats.SubTile + Height * UnitStats.SubTile / 2;

	/// <summary>Lowers health (not below 0). World removes buildings at 0; cores stay and end the match.</summary>
	public void TakeDamage(int amount) => Health = System.Math.Max(0, Health - amount);

	// ---- Construction: a placed building is a site until builders have put BuildTime work into it. ----

	/// <summary>Builder-ticks of work needed (one builder does 1 per tick).</summary>
	public int BuildTime => BuildingRules.BuildTime(Type);
	public int BuildWork { get; private set; }

	/// <summary>Only finished buildings run, take items and shoot.</summary>
	public bool IsBuilt => BuildWork >= BuildTime;

	internal void AddWork(int amount) => BuildWork = System.Math.Min(BuildTime, BuildWork + amount);
	internal void CompleteConstruction() => BuildWork = BuildTime;

	/// <summary>Whether a worker could drop this item off here right now (storage and kitchens).</summary>
	public virtual bool CanTake(ItemType item) => false;

	/// <summary>Hands an item to this building if it is finished and takes it. What belts and machines call.</summary>
	public bool Offer(ItemType item, Direction moving) => IsBuilt && TryAccept(item, moving);

	public int FrontX => X + Facing.DX();
	public int FrontY => Y + Facing.DY();

	public bool OutputsTo(int x, int y) => FrontX == x && FrontY == y;

	/// <summary>Whether items leaving this building can move in <paramref name="direction"/>. Used by the view for belt curves.</summary>
	public virtual bool OutputsToward(Direction direction) => direction == Facing;

	/// <summary>Advances this building one simulation tick.</summary>
	public virtual void Tick(World world) { }

	/// <summary>
	/// Offers an item that is moving in direction <paramref name="moving"/> into this building.
	/// Returns true if the building took it.
	/// </summary>
	public virtual bool TryAccept(ItemType item, Direction moving) => false;

	/// <summary>Buildings with a setting (sorter filter, assembler recipe) cycle it here. Returns false if there is none.</summary>
	public virtual bool CycleSetting() => false;

	/// <summary>Hands an item to the neighbouring tile in <paramref name="direction"/> (1x1 buildings).</summary>
	protected bool TryPush(World world, ItemType item, Direction direction)
	{
		var neighbour = world.GetBuilding(X + direction.DX(), Y + direction.DY());
		return neighbour != null && neighbour.Offer(item, direction);
	}

	/// <summary>Tries the front first, then the other sides clockwise.</summary>
	protected bool TryPushAnySide(World world, ItemType item)
	{
		var direction = Facing;
		for (int i = 0; i < 4; i++, direction = direction.RotatedClockwise())
			if (TryPush(world, item, direction))
				return true;
		return false;
	}
}

public static class BuildingRules
{
	/// <summary>What players can build from the menu, in menu order. The core is placed by the match setup.</summary>
	public static readonly BuildingType[] Buildable =
	{
		BuildingType.Conveyor,
		BuildingType.Splitter,
		BuildingType.Sorter,
		BuildingType.Junction,
		BuildingType.BrickExtractor,
		BuildingType.PlasticExtractor,
		BuildingType.BatteryExtractor,
		BuildingType.Assembler,
		BuildingType.Toolbox,
		BuildingType.Warehouse,
		BuildingType.CropField,
		BuildingType.Farmhouse,
		BuildingType.Kitchen,
		BuildingType.SoldierFactory,
		BuildingType.GolemWorkshop,
		BuildingType.CarFactory,
		BuildingType.FoamTower,
		BuildingType.Catapult,
		BuildingType.WaterTower,
		BuildingType.LaserTower,
	};

	// ---- Balance ------------------------------------------------------------------------
	// An extractor makes 30 items/min, so a brick extractor pays for itself in ~20 s.
	// The starting stock covers a brick extractor, a plastic melter and the belts from both to the
	// core (the deposits are ~5 tiles away), with some left over for a second extractor.
	// Recipes are in Assembler.cs / Unit.cs, tower stats in Tower.cs.

	public static readonly ItemStack[] StartingStock =
	{
		new(ItemType.Brick, 50),
		new(ItemType.Plastic, 50),
	};

	private static readonly ItemStack[] Free = { };
	private static readonly ItemStack[] ConveyorCost = { new(ItemType.Plastic, 1) };
	private static readonly ItemStack[] SplitterCost = { new(ItemType.Plastic, 3) };
	private static readonly ItemStack[] SorterCost = { new(ItemType.Plastic, 3), new(ItemType.Brick, 2) };
	private static readonly ItemStack[] JunctionCost = { new(ItemType.Plastic, 2), new(ItemType.Brick, 1) };
	private static readonly ItemStack[] BrickExtractorCost = { new(ItemType.Brick, 10) };
	private static readonly ItemStack[] PlasticExtractorCost = { new(ItemType.Brick, 10), new(ItemType.Plastic, 5) };
	private static readonly ItemStack[] BatteryExtractorCost = { new(ItemType.Brick, 15), new(ItemType.Plastic, 15) };
	private static readonly ItemStack[] AssemblerCost = { new(ItemType.Brick, 15), new(ItemType.Plastic, 10) };
	private static readonly ItemStack[] SoldierFactoryCost = { new(ItemType.Brick, 30), new(ItemType.Plastic, 20) };
	private static readonly ItemStack[] FoamTowerCost = { new(ItemType.Brick, 15), new(ItemType.Plastic, 15) };
	private static readonly ItemStack[] CatapultCost = { new(ItemType.Brick, 30), new(ItemType.Plastic, 10) };
	private static readonly ItemStack[] WaterTowerCost = { new(ItemType.Brick, 20), new(ItemType.Plastic, 20), new(ItemType.Battery, 5) };
	private static readonly ItemStack[] LaserTowerCost = { new(ItemType.Brick, 20), new(ItemType.Plastic, 15), new(ItemType.Battery, 10) };
	private static readonly ItemStack[] GolemWorkshopCost = { new(ItemType.Brick, 40), new(ItemType.Plastic, 20) };
	private static readonly ItemStack[] CarFactoryCost = { new(ItemType.Brick, 30), new(ItemType.Plastic, 20), new(ItemType.Battery, 10) };
	private static readonly ItemStack[] ToolboxCost = { new(ItemType.Brick, 25), new(ItemType.Plastic, 15) };
	private static readonly ItemStack[] WarehouseCost = { new(ItemType.Brick, 30), new(ItemType.Plastic, 20) };
	private static readonly ItemStack[] CropFieldCost = { new(ItemType.Brick, 5), new(ItemType.Plastic, 5) };
	private static readonly ItemStack[] FarmhouseCost = { new(ItemType.Brick, 25), new(ItemType.Plastic, 15) };
	private static readonly ItemStack[] KitchenCost = { new(ItemType.Brick, 15), new(ItemType.Plastic, 10) };

	public static ItemStack[] Cost(BuildingType type) => type switch
	{
		BuildingType.Conveyor => ConveyorCost,
		BuildingType.Splitter => SplitterCost,
		BuildingType.Sorter => SorterCost,
		BuildingType.Junction => JunctionCost,
		BuildingType.BrickExtractor => BrickExtractorCost,
		BuildingType.PlasticExtractor => PlasticExtractorCost,
		BuildingType.BatteryExtractor => BatteryExtractorCost,
		BuildingType.Assembler => AssemblerCost,
		BuildingType.SoldierFactory => SoldierFactoryCost,
		BuildingType.FoamTower => FoamTowerCost,
		BuildingType.Catapult => CatapultCost,
		BuildingType.WaterTower => WaterTowerCost,
		BuildingType.LaserTower => LaserTowerCost,
		BuildingType.GolemWorkshop => GolemWorkshopCost,
		BuildingType.CarFactory => CarFactoryCost,
		BuildingType.Toolbox => ToolboxCost,
		BuildingType.Warehouse => WarehouseCost,
		BuildingType.CropField => CropFieldCost,
		BuildingType.Farmhouse => FarmhouseCost,
		BuildingType.Kitchen => KitchenCost,
		_ => Free,
	};
	// -------------------------------------------------------------------------------------

	// ---- Balance: building health (a soldier does 4 damage per second) ----
	public static int MaxHealth(BuildingType type) => type switch
	{
		BuildingType.Core => 1000,
		BuildingType.Conveyor => 20,
		BuildingType.Splitter or BuildingType.Sorter or BuildingType.Junction or BuildingType.CropField => 40,
		BuildingType.BrickExtractor or BuildingType.PlasticExtractor or BuildingType.BatteryExtractor => 60,
		BuildingType.Assembler or BuildingType.Kitchen => 80,
		BuildingType.SoldierFactory or BuildingType.GolemWorkshop or BuildingType.CarFactory or BuildingType.Toolbox or BuildingType.Warehouse or BuildingType.Farmhouse => 150,
		BuildingType.FoamTower or BuildingType.WaterTower or BuildingType.LaserTower => 100,
		BuildingType.Catapult => 120,
		_ => 50,
	};

	// ---- Balance: build time, in builder-ticks (one builder: 20 per second; two builders halve it) ----
	public static int BuildTime(BuildingType type) => type switch
	{
		BuildingType.Core => 0,
		BuildingType.Conveyor => 10,
		BuildingType.Splitter or BuildingType.Sorter or BuildingType.Junction => 30,
		BuildingType.CropField => 40,
		BuildingType.BrickExtractor or BuildingType.PlasticExtractor or BuildingType.BatteryExtractor => 60,
		BuildingType.Assembler or BuildingType.Kitchen => 100,
		BuildingType.FoamTower or BuildingType.WaterTower or BuildingType.LaserTower => 120,
		BuildingType.Catapult => 140,
		BuildingType.Toolbox or BuildingType.Warehouse or BuildingType.Farmhouse => 160,
		BuildingType.SoldierFactory or BuildingType.GolemWorkshop or BuildingType.CarFactory => 240,
		_ => 60,
	};
	// -------------------------------------------------------------------------------------

	public static (int Width, int Height) Size(BuildingType type) => type switch
	{
		BuildingType.Core => (2, 2),
		BuildingType.SoldierFactory or BuildingType.GolemWorkshop or BuildingType.CarFactory or BuildingType.Toolbox or BuildingType.Warehouse or BuildingType.Farmhouse => (2, 2),
		_ => (1, 1),
	};

	/// <summary>The deposit an extractor has to stand on, or None if it can go on any floor.</summary>
	public static ResourceType RequiredResource(BuildingType type) => type switch
	{
		BuildingType.BrickExtractor => ResourceType.Brick,
		BuildingType.PlasticExtractor => ResourceType.Plastic,
		BuildingType.BatteryExtractor => ResourceType.Battery,
		_ => ResourceType.None,
	};

	public static Building Create(BuildingType type, int x, int y, Direction facing, int owner, World world) => type switch
	{
		BuildingType.Conveyor => new Conveyor(x, y, facing, owner),
		BuildingType.Core => new Core(x, y, facing, owner, world.Players[owner]),
		BuildingType.Splitter => new Splitter(x, y, facing, owner),
		BuildingType.Sorter => new Sorter(x, y, facing, owner),
		BuildingType.Junction => new Junction(x, y, facing, owner),
		BuildingType.Assembler => new Assembler(x, y, facing, owner),
		BuildingType.SoldierFactory => new UnitFactory(type, x, y, facing, owner, UnitType.PlasticSoldier),
		BuildingType.GolemWorkshop => new UnitFactory(type, x, y, facing, owner, UnitType.BrickGolem),
		BuildingType.CarFactory => new UnitFactory(type, x, y, facing, owner, UnitType.RcCar),
		BuildingType.Toolbox => new UnitFactory(type, x, y, facing, owner, UnitType.Builder),
		BuildingType.Warehouse => new Warehouse(x, y, facing, owner, world.Players[owner]),
		BuildingType.CropField => new CropField(x, y, facing, owner),
		BuildingType.Farmhouse => new UnitFactory(type, x, y, facing, owner, UnitType.Farmer),
		BuildingType.Kitchen => new Kitchen(x, y, facing, owner),
		BuildingType.FoamTower or BuildingType.Catapult or BuildingType.WaterTower or BuildingType.LaserTower
			=> new Tower(type, x, y, facing, owner),
		_ => new Extractor(type, x, y, facing, owner),
	};
}
