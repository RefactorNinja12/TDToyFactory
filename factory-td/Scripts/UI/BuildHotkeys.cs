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
/// Two-step build hotkeys: a number picks a menu category (1 = the first tab), then one of
/// Q W E R A S D F picks the building in that place of the category. While a category is chosen (and until
/// the letter is let go) those letters belong to the hotkeys, not to the camera or rotating. Esc cancels.
/// Keys are plain characters ('1', 'Q', Escape = '\u001b'), so this works without Godot.
/// </summary>
public sealed class BuildHotkeys
{
	public const string Letters = "QWERASDF";
	public const char Escape = '\u001b';

	private readonly BuildingType[][] _categories;
	private readonly HashSet<char> _held = new();

	public BuildHotkeys(IReadOnlyList<BuildingType[]> categories)
	{
		_categories = new BuildingType[categories.Count][];
		for (int i = 0; i < categories.Count; i++)
			_categories[i] = categories[i];
	}

	/// <summary>The category waiting for its letter, or -1.</summary>
	public int ArmedCategory { get; private set; } = -1;

	public bool Armed => ArmedCategory >= 0;

	/// <summary>The letter that picks a building (by its place in the category), e.g. 'Q' for the first.</summary>
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
			return default;
		_held.Add(key);
		var category = _categories[ArmedCategory];
		if (place >= category.Length)
			return new HotkeyResult(HotkeyOutcome.Swallowed, ArmedCategory); // no building there: still waiting
		int chosen = ArmedCategory;
		ArmedCategory = -1;
		return new HotkeyResult(HotkeyOutcome.Selected, chosen, category[place]);
	}

	public void Release(char key) => _held.Remove(char.ToUpperInvariant(key));

	/// <summary>Whether the camera (pan/zoom) should ignore this key right now.</summary>
	public bool BlocksCamera(char key)
	{
		key = char.ToUpperInvariant(key);
		return (Armed && Letters.IndexOf(key) >= 0) || _held.Contains(key);
	}
}
