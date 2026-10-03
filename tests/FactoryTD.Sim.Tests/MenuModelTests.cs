using System.Linq;
using FactoryTD.Net;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class MenuModelTests
{
	[Theory]
	[InlineData("7777", true)]
	[InlineData(" 1024 ", true)]
	[InlineData("65535", true)]
	[InlineData("80", false)]
	[InlineData("0", false)]
	[InlineData("70000", false)]
	[InlineData("sju", false)]
	[InlineData("", false)]
	public void Port_MustBeANumberInRange(string text, bool ok)
	{
		Assert.Equal(ok, MenuModel.TryParsePort(text, out _, out string error));
		Assert.Equal(ok, error == null);
	}

	[Theory]
	[InlineData("192.168.1.20", "192.168.1.20", 7777)]
	[InlineData("192.168.1.20:7790", "192.168.1.20", 7790)]
	[InlineData(" min-dator.local:2000 ", "min-dator.local", 2000)]
	public void Address_WithOrWithoutPort(string text, string host, int port)
	{
		Assert.True(MenuModel.TryParseAddress(text, out string h, out int p, out _));
		Assert.Equal((host, port), (h, p));
	}

	[Theory]
	[InlineData("")]
	[InlineData("192.168.1.20:80")]
	[InlineData("192.168.1.20:abc")]
	[InlineData("http://x/y")]
	[InlineData("två ord")]
	public void Address_BadOnesSayWhy(string text)
	{
		Assert.False(MenuModel.TryParseAddress(text, out _, out _, out string error));
		Assert.False(string.IsNullOrEmpty(error));
	}

	[Theory]
	[InlineData(null, false)]
	[InlineData("abc", false)]
	[InlineData("abcd", true)]
	public void Password_AtLeastFourCharacters(string password, bool ok) =>
		Assert.Equal(ok, MenuModel.PasswordProblem(password) == null);

	[Fact]
	public void Settings_RoundTrip_AndNeverHoldThePassword()
	{
		var settings = new MenuSettings("Ricky", "192.168.1.20:7790", 7790);
		Assert.Equal(settings, MenuSettings.FromText(settings.ToText()));
		Assert.DoesNotContain("password", settings.ToText());
		Assert.DoesNotContain(typeof(MenuSettings).GetProperties(), p => p.Name.Contains("Password"));
		Assert.Equal(new MenuSettings(), MenuSettings.FromText("junk\nport=99\n"));
	}

	[Fact]
	public void LanAddresses_HomeNetworkFirst_VirtualAdaptersLater()
	{
		var shown = MenuModel.LanAddresses(new[] { "127.0.0.1", "fe80::1", "85.10.2.3", "169.254.3.3", "172.28.224.1",
			"192.168.56.1", "10.0.0.7", "100.101.2.3", "192.168.0.27", "172.40.0.1" });
		Assert.Equal(new[] { "192.168.0.27", "10.0.0.7", "100.101.2.3", "172.28.224.1", "192.168.56.1", "85.10.2.3", "172.40.0.1" }, shown);
	}

	[Fact]
	public void EveryEndAndRejection_HasItsOwnText()
	{
		var texts = System.Enum.GetValues<EndReason>().Where(r => r is not EndReason.None and not EndReason.Rejected)
			.Select(r => MenuModel.EndText(r, null, 120, "Kompis"))
			.Concat(System.Enum.GetValues<RejectReason>().Select(r => MenuModel.EndText(EndReason.Rejected, r, -1, "")))
			.ToList();
		Assert.DoesNotContain("", texts);
		Assert.Equal(texts.Count, texts.Distinct().Count());
		Assert.Contains(texts, t => t.Contains("tick 120"));
		Assert.Contains(texts, t => t.StartsWith("Kompis lämnade"));
		var net = new Support.FakeNetwork();
		Assert.Equal("Väntar på motståndare…", MenuModel.Status(new HostSession(net.Host, "pass", "v", seed => null), isHost: true));
		Assert.Equal("Ansluter…", MenuModel.Status(new ClientSession(net.Connect("x"), "pass", "x", seed => null), isHost: false));
	}

	[Fact]
	public void Internet_GoesThroughTailscale_NeverTheRouter()
	{
		var hint = MenuModel.InternetHint(7790);
		Assert.Contains("Tailscale", hint);
		Assert.Contains(":7790", hint);
		Assert.Equal("100.101.2.3:7790 (Tailscale)", MenuModel.AddressLine("100.101.2.3", 7790));
		Assert.Equal("192.168.0.27:7790", MenuModel.AddressLine("192.168.0.27", 7790));
		Assert.Equal("100.20.2.3:7790", MenuModel.AddressLine("100.20.2.3", 7790)); // outside Tailscale's range
	}

	[Fact]
	public void LaunchArgs_HostAndJoin()
	{
		var host = LaunchArgs.Parse("--host --port 7790 --password test --bot --steps 1200 --out a.txt".Split(' '));
		Assert.True(host.Host && host.Online && host.Bot);
		Assert.Equal((7790, "test", 1200, "a.txt"), (host.Port, host.Password, host.Steps, host.Out));
		var join = LaunchArgs.Parse("--join 127.0.0.1:7790 --password test".Split(' '));
		Assert.Equal("127.0.0.1:7790", join.Join);
		Assert.False(join.Host || LaunchArgs.Parse(new string[0]).Online);
	}
}
