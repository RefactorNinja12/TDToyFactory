using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

public enum HotkeyOutcome { None, CategoryOpened, Selected, Exited, Swallowed }

/// <summary>What a key press did. Anything but None means the key was used up by the build hotkeys.</summary>
public readonly record struct HotkeyResult(HotkeyOutcome Outcome, int Category = -1, BuildingType Type = default)
{
	public bool Consumed => Outcome != HotkeyOutcome.None;
}

/// <summary>
/// Build hotkeys with numbers only (the hand stays on the number row): a number opens a menu category
/// (1 = the first tab), then a number picks the building in that place of the category (1 = the first card).
/// The category stays open, so more numbers pick other buildings in it. Space, or the number of the building
/// already picked once more, exits at once: the building is dropped and the category closed, so the next
/// number opens a category. Esc is left to the pause menu. Letters are never taken (WASD pans, Q/E zoom,
/// R rotates, V shows the power grid). Keys are plain characters ('1', Back = ' '), so this works without Godot.
/// </summary>
public sealed class BuildHotkeys
{
	/// <summary>The exit key: space.</summary>
	public const char Back = ' ';

	private readonly BuildingType[][] _categories;

	public BuildHotkeys(IReadOnlyList<BuildingType[]> categories)
	{
		_categories = new BuildingType[categories.Count][];
		for (int i = 0; i < categories.Count; i++)
			_categories[i] = categories[i];
	}

	/// <summary>The open category whose buildings the numbers pick, or -1 (numbers pick categories).</summary>
	public int ArmedCategory { get; private set; } = -1;

	public bool Armed => ArmedCategory >= 0;

	/// <summary>The key shown on a card for its place in the category: '1' for the first.</summary>
	public static char KeyFor(int place) => place < 9 ? (char)('1' + place) : ' ';

	/// <param name="selected">The building picked for placing right now (by key or mouse), if any.</param>
	public HotkeyResult Press(char key, BuildingType? selected = null)
	{
		if (key == Back)
			return Armed || selected != null ? Exit() : default;
		if (key < '1' || key > '9')
			return default;
		int number = key - '1';
		if (!Armed)
		{
			if (number >= _categories.Length)
				return default;
			ArmedCategory = number;
			return new HotkeyResult(HotkeyOutcome.CategoryOpened, ArmedCategory);
		}
		var category = _categories[ArmedCategory];
		if (number >= category.Length)
			return new HotkeyResult(HotkeyOutcome.Swallowed, ArmedCategory); // no building there: nothing happens
		if (category[number] == selected)
			return Exit(); // the same number again: done with it
		return new HotkeyResult(HotkeyOutcome.Selected, ArmedCategory, category[number]);
	}

	private HotkeyResult Exit()
	{
		ArmedCategory = -1;
		return new HotkeyResult(HotkeyOutcome.Exited);
	}
}
