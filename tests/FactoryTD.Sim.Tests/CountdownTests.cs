using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class CountdownTests
{
	[Theory]
	[InlineData(0f, "3")]
	[InlineData(0.99f, "3")]
	[InlineData(1f, "2")]
	[InlineData(2.5f, "1")]
	[InlineData(3f, "Kör!")]
	[InlineData(3.7f, "Kör!")]
	[InlineData(4f, "")]
	public void CountsDown_ThenSaysGo(float elapsed, string text) => Assert.Equal(text, Countdown.Text(elapsed));

	[Fact]
	public void TheMatchWaits_UntilTheCountIsOver()
	{
		Assert.False(Countdown.Started(0));
		Assert.False(Countdown.Started(2.99f));
		Assert.True(Countdown.Started(3));
	}

	[Fact]
	public void Camera_FliesInFromTheRoomToTheToybox()
	{
		Assert.Equal(Countdown.WideZoom, Countdown.Zoom(0), 3);
		Assert.True(Countdown.Zoom(1) > Countdown.Zoom(0.5f));
		Assert.Equal(Countdown.CloseZoom, Countdown.Zoom(Countdown.Seconds), 3);
		Assert.Equal(Countdown.CloseZoom, Countdown.Zoom(10), 3);
		Assert.InRange(Countdown.Beat(1.25f), 0.24f, 0.26f);
	}
}
