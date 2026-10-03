using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

public enum HotkeyOutcome { None, CategoryOpened, Selected, Cancelled, Swallowed }

/// <summary>What a key press did. Anything but None means the key was used up by the build hotkeys.</summary>
public readonly record struct HotkeyResult(HotkeyOutcome Outcome, int Category = -1, BuildingType Type = default)
{
	public bool Consumed => Outcome != HotkeyOutcome.None;
}

/// <summary>
/// Build hotkeys with numbers only (the hand stays on the number row): a number opens a menu category
/// (1 = the first tab), then a number picks the building in that place of the category (1 = the first card).
/// The category stays open, so more numbers pick other buildings in it. Space steps back: first what is being
/// placed (left to the build controller: a belt's start, then the building), then the category, after which
/// numbers pick categories again. Esc is left to the pause menu. Letters are never taken (WASD pans, Q/E zoom,
/// R rotates, V shows the power grid). Keys are plain characters ('1', Back = ' '), so this works without Godot.
/// </summary>
public sealed class BuildHotkeys
{
	/// <summary>The step-back key: space.</summary>
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

	/// <param name="hasSelection">A building is picked for placing: space then belongs to the build controller.</param>
	public HotkeyResult Press(char key, bool hasSelection = false)
	{
		if (key == Back)
		{
			if (hasSelection || !Armed)
				return default;
			ArmedCategory = -1;
			return new HotkeyResult(HotkeyOutcome.Cancelled);
		}
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
		return new HotkeyResult(HotkeyOutcome.Selected, ArmedCategory, category[number]);
	}
}
