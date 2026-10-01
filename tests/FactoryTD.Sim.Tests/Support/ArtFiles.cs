using System;
using System.IO;

namespace FactoryTD.Sim.Tests.Support;

/// <summary>The game's sprites on disk (factory-td/Assets/Sprites), for tests that check art exists.</summary>
internal static class ArtFiles
{
	private static readonly Lazy<string> Sprites = new(() =>
	{
		for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
		{
			var path = Path.Combine(dir.FullName, "factory-td", "Assets", "Sprites");
			if (Directory.Exists(path))
				return path;
		}
		throw new DirectoryNotFoundException("factory-td/Assets/Sprites not found above the test folder");
	});

	public static bool Exists(string relative) => File.Exists(Path.Combine(Sprites.Value, relative));
}
