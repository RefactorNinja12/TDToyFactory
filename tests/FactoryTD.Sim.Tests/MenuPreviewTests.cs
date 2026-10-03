using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class MenuPreviewTests
{
	[Fact]
	public void Camera_StaysOverTheMap_GoesFromSideToSide_AndLoops()
	{
		const float w = 160 * 64, h = 62 * 64;
		float left = w, right = 0;
		for (float t = 0; t < MenuPreview.Period; t += 0.5f)
		{
			var (x, y, zoom) = MenuPreview.CameraAt(t, w, h);
			Assert.InRange(x, 0, w);
			Assert.InRange(y, 0, h);
			Assert.InRange(zoom, MenuPreview.Zoom - MenuPreview.ZoomBreath, MenuPreview.Zoom + MenuPreview.ZoomBreath);
			left = System.Math.Min(left, x);
			right = System.Math.Max(right, x);
		}
		Assert.True(left < w * 0.25f && right > w * 0.75f, "it visits both bases");
		var (x0, y0, z0) = MenuPreview.CameraAt(3, w, h);
		var (x1, y1, z1) = MenuPreview.CameraAt(3 + MenuPreview.Period, w, h);
		Assert.Equal(x0, x1, 1);
		Assert.Equal(y0, y1, 1);
		Assert.Equal(z0, z1, 4);
	}
}
