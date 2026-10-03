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

	/// <summary>Width and height of a PNG (from its header), or (0, 0) if it is missing.</summary>
	public static (int Width, int Height) PngSize(string relative)
	{
		var path = Path.Combine(Sprites.Value, relative);
		if (!File.Exists(path))
			return (0, 0);
		var header = new byte[24];
		using (var file = File.OpenRead(path))
			file.ReadExactly(header);
		static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
		return (BigEndian(header, 16), BigEndian(header, 20));
	}
}
