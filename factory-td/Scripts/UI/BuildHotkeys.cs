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
/// Two-step build hotkeys: a number picks a menu category (1 = the first tab), then one of Z X C F G T
/// picks the building in that place of the category. None of those letters is used by anything else
/// (WASD pans, Q/E zoom, R rotates, V shows the power grid), so the camera never has to give way. Esc
/// cancels. Keys are plain characters ('1', 'Z', Escape = '\u001b'), so this works without Godot.
/// </summary>
public sealed class BuildHotkeys
{
	public const string Letters = "ZXCFGT";
	public const char Escape = '\u001b';

	private readonly BuildingType[][] _categories;

	public BuildHotkeys(IReadOnlyList<BuildingType[]> categories)
	{
		_categories = new BuildingType[categories.Count][];
		for (int i = 0; i < categories.Count; i++)
			_categories[i] = categories[i];
	}

	/// <summary>The category waiting for its letter, or -1.</summary>
	public int ArmedCategory { get; private set; } = -1;

	public bool Armed => ArmedCategory >= 0;

	/// <summary>The letter that picks a building by its place in the category, e.g. 'Z' for the first.</summary>
	public static char LetterFor(int place) => place < Letters.Length ? Letters[place] : ' ';

	public HotkeyResult Press(char key)
	{
		key = char.ToUpperInvariant(key);
		if (key >= '1' && key <= '9' && key - '1' < _categories.Length)
		{
			ArmedCategory = key - '1';
			return new HotkeyResult(HotkeyOutcome.CategoryOpened, ArmedCategory);
		}
		if (!Armed)
			return default;
		if (key == Escape)
		{
			ArmedCategory = -1;
			return new HotkeyResult(HotkeyOutcome.Cancelled);
		}
		int place = Letters.IndexOf(key);
		if (place < 0)
			return default; // anything else (WASD, Q/E, R...) keeps working while a category waits
		var category = _categories[ArmedCategory];
		if (place >= category.Length)
			return new HotkeyResult(HotkeyOutcome.Swallowed, ArmedCategory); // no building there: still waiting
		int chosen = ArmedCategory;
		ArmedCategory = -1;
		return new HotkeyResult(HotkeyOutcome.Selected, chosen, category[place]);
	}
}
