namespace FactoryTD.Sim;

public sealed record Recipe(ItemStack[] Inputs, ItemType Output, int Ticks);

/// <summary>
/// Tier 2 production. Takes the current recipe's inputs from any side, crafts, and pushes the result
/// out of its front. Players cycle the recipe by clicking it; switching throws away what is buffered.
/// </summary>
public sealed class Assembler : Building
{
	// ---- Balance: tier 2 recipes ----
	public static readonly Recipe[] Recipes =
	{
		new(new ItemStack[] { new(ItemType.Brick, 2), new(ItemType.Plastic, 1) }, ItemType.Gear, 40),
		new(new ItemStack[] { new(ItemType.Plastic, 2) }, ItemType.Spring, 30),
		new(new ItemStack[] { new(ItemType.Battery, 1), new(ItemType.Plastic, 1) }, ItemType.CircuitBoard, 60),
	};

	private const int MaxOutput = 5;

	private readonly Crafter _crafter = new();
	private int _recipeIndex;
	private int _output;

	public Recipe Recipe => Recipes[_recipeIndex];

	/// <summary>Buffered inputs and craft progress, e.g. for the info panel.</summary>
	public Crafter Crafter => _crafter;

	/// <summary>Finished items waiting to go out of the front.</summary>
	public int Finished => _output;

	public Assembler(int x, int y, Direction facing, int owner)
		: base(BuildingType.Assembler, x, y, facing, owner) { }

	// Output only goes out the front (the arrow on the sprite). Pushing to every side would put products
	// onto the belts feeding it and jam them.
	public override bool OutputsToward(Direction direction) => direction == Facing;

	public override bool CycleSetting()
	{
		_recipeIndex = (_recipeIndex + 1) % Recipes.Length;
		_crafter.Reset();
		return true;
	}

	public override bool TryAccept(ItemType item, Direction moving) => _crafter.TryAccept(Recipe.Inputs, item);

	public override void Tick(World world)
	{
		if (_output < MaxOutput && _crafter.Tick(Recipe.Inputs, Recipe.Ticks))
			_output++;
		if (_output > 0 && TryPush(world, Recipe.Output, Facing))
			_output--;
	}
}

/// <summary>
/// Shared input buffer + craft timer for recipe buildings. Buffers up to two crafts' worth of each input.
/// </summary>
public sealed class Crafter
{
	private readonly int[] _stock = new int[Items.All.Length + 1];
	private int _progress;
	private bool _crafting;

	public int Stock(ItemType item) => _stock[(int)item];

	/// <summary>0..ticks while crafting, 0 when idle.</summary>
	public int Progress => _progress;

	public void Reset()
	{
		System.Array.Clear(_stock);
		_progress = 0;
		_crafting = false;
	}

	public bool TryAccept(ItemStack[] inputs, ItemType item)
	{
		foreach (var input in inputs)
		{
			if (input.Type == item && _stock[(int)item] < input.Amount * 2)
			{
				_stock[(int)item]++;
				return true;
			}
		}
		return false;
	}

	/// <summary>Advances one tick. Returns true on the tick a craft completes.</summary>
	public bool Tick(ItemStack[] inputs, int ticks)
	{
		if (!_crafting)
		{
			foreach (var input in inputs)
				if (_stock[(int)input.Type] < input.Amount)
					return false;
			foreach (var input in inputs)
				_stock[(int)input.Type] -= input.Amount;
			_crafting = true;
			_progress = 0;
		}

		if (++_progress < ticks)
			return false;
		_crafting = false;
		_progress = 0;
		return true;
	}
}
