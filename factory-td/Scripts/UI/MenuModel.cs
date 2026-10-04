using System;
using System.Collections.Generic;
using System.Linq;
using FactoryTD.Net;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>What the start menu remembers between runs. Never the password.</summary>
public sealed record MenuSettings(string Name = "Spelare", string Address = "", int Port = Protocol.DefaultPort,
	MapTheme Map = MapTheme.Nursery)
{
	public string ToText() => $"name={Name}\naddress={Address}\nport={Port}\nmap={Map}\n";

	public static MenuSettings FromText(string text)
	{
		var values = (text ?? "").Split('\n')
			.Select(line => line.Split('=', 2))
			.Where(parts => parts.Length == 2)
			.GroupBy(parts => parts[0].Trim())
			.ToDictionary(g => g.Key, g => g.Last()[1].Trim());
		var defaults = new MenuSettings();
		return new MenuSettings(
			values.TryGetValue("name", out var name) && name != "" ? name : defaults.Name,
			values.TryGetValue("address", out var address) ? address : defaults.Address,
			values.TryGetValue("port", out var port) && MenuModel.TryParsePort(port, out int p, out _) ? p : defaults.Port,
			values.TryGetValue("map", out var map) && System.Enum.TryParse(map, out MapTheme theme) && System.Enum.IsDefined(theme)
				? theme : defaults.Map);
	}
}

/// <summary>The start menu's rules and texts (Swedish), kept out of the Godot view so they can be tested.</summary>
public static class MenuModel
{
	public const int MinPort = 1024, MaxPort = 65535, MinPasswordLength = 4;

	/// <summary>The maps to choose from, in menu order.</summary>
	public static readonly (string Name, MapTheme Theme)[] Maps =
	{
		("Barnrummet", MapTheme.Nursery),
		("Trädgården", MapTheme.Garden),
	};

	/// <summary>The place of <paramref name="theme"/> in <see cref="Maps"/> (for the option buttons).</summary>
	public static int MapIndex(MapTheme theme) => Math.Max(0, Array.FindIndex(Maps, m => m.Theme == theme));

	public static readonly (string Name, int ArmyDelayTicks)[] Difficulties =
	{
		("Lätt", 20 * 60 * 3), // the bot's armies wait 3 minutes
		("Normal", 0),
	};

	public static bool TryParsePort(string text, out int port, out string error)
	{
		error = null;
		if (!int.TryParse((text ?? "").Trim(), out port))
			error = "Porten ska vara ett tal.";
		else if (port < MinPort || port > MaxPort)
			error = $"Porten ska vara mellan {MinPort} och {MaxPort}.";
		return error == null;
	}

	/// <summary>"ip" or "ip:port" (also a host name). Without a port the default one is used.</summary>
	public static bool TryParseAddress(string text, out string host, out int port, out string error)
	{
		host = (text ?? "").Trim();
		port = Protocol.DefaultPort;
		error = null;
		int colon = host.LastIndexOf(':');
		if (colon >= 0 && host.IndexOf(':') == colon)
		{
			if (!TryParsePort(host[(colon + 1)..], out port, out error))
				return false;
			host = host[..colon];
		}
		if (host.Length == 0)
			error = "Skriv värdens adress, t.ex. 192.168.1.20:7777.";
		else if (host.Any(c => char.IsWhiteSpace(c) || c == '/'))
			error = "Adressen ser konstig ut. Skriv t.ex. 192.168.1.20:7777.";
		return error == null;
	}

	/// <summary>Null when the password is fine, otherwise why not.</summary>
	public static string PasswordProblem(string password) =>
		(password ?? "").Length < MinPasswordLength ? $"Lösenordet ska ha minst {MinPasswordLength} tecken." : null;

	/// <summary>
	/// The addresses a friend can use: IPv4, not loopback. Home networks (192.168.x) first, then 10.x, Tailscale
	/// (100.64-127.x), 172.16-31.x (often a virtual adapter: WSL, Docker), VirtualBox's 192.168.56.x, others last.
	/// </summary>
	public static List<string> LanAddresses(IEnumerable<string> all) =>
		all.Where(a => a.Count(c => c == '.') == 3 && !a.StartsWith("127.", StringComparison.Ordinal)
				&& !a.StartsWith("169.254.", StringComparison.Ordinal))
			.OrderBy(Rank)
			.ToList();

	private static int Rank(string a)
	{
		var parts = a.Split('.');
		if (parts[0] == "192" && parts[1] == "168")
			return parts[2] == "56" ? 4 : 0;
		if (parts[0] == "10")
			return 1;
		if (IsTailscale(a))
			return 2;
		return parts[0] == "172" && int.TryParse(parts[1], out int second) && second is >= 16 and <= 31 ? 3 : 5;
	}

	/// <summary>One line about where the connection is, for the host/join screens.</summary>
	public static string Status(MatchSession session, bool isHost)
	{
		if (session == null)
			return "";
		return session.State switch
		{
			SessionState.Connecting => "Ansluter…",
			SessionState.Joining => "Kollar version och lösenord…",
			SessionState.Lobby when isHost && session is HostSession { CanStart: true } =>
				$"{Name(session.OpponentName)} är ansluten (ping {session.RttMs} ms). Starta när ni är redo!",
			SessionState.Lobby when isHost => "Väntar på motståndare…",
			SessionState.Lobby => $"Ansluten till {Name(session.OpponentName)} (ping {session.RttMs} ms). Väntar på att värden startar…",
			SessionState.Playing => "Matchen startar!",
			_ => EndText(session),
		};
	}

	/// <summary>Why a match or a connection attempt ended, in plain Swedish.</summary>
	public static string EndText(MatchSession session) =>
		EndText(session.EndReason, session.Rejection, session.DesyncStep, session.OpponentName);

	public static string EndText(EndReason reason, RejectReason? rejection, long desyncStep, string opponent) => reason switch
	{
		EndReason.Left => "Du lämnade matchen.",
		EndReason.OpponentLeft => $"{Name(opponent)} lämnade matchen.",
		EndReason.ConnectionLost => "Anslutningen bröts (inget svar på 10 sekunder).",
		EndReason.CouldNotConnect => "Ingen svarar på adressen. Kolla adress och port, och att värden väntar.",
		EndReason.Rejected => rejection switch
		{
			RejectReason.WrongPassword => "Fel lösenord.",
			RejectReason.WrongVersion => "Fel version av spelet: ni måste köra samma version.",
			RejectReason.Blocked => "Spärrad en stund efter för många försök. Vänta 30 sekunder.",
			RejectReason.Full => "Matchen är full.",
			_ => "Värden sa nej.",
		},
		EndReason.Desync => $"Spelen kom ur synk vid tick {desyncStep}. Matchen stoppades (en logg sparades).",
		_ => "",
	};

	/// <summary>
	/// How friends outside the home network get in. The game never opens ports in the router (that felt
	/// unsafe): a private virtual network (Tailscale) connects the two computers instead.
	/// </summary>
	public static string InternetHint(int port) =>
		$"Olika nätverk? Installera Tailscale (gratis) på båda datorerna; kompisen ansluter till din Tailscale-adress (100…):{port}. Inga portar öppnas i routern.";

	/// <summary>An address as shown to the host: "ip:port", marked when it is the Tailscale one.</summary>
	public static string AddressLine(string address, int port) =>
		IsTailscale(address) ? $"{address}:{port} (Tailscale)" : $"{address}:{port}";

	private static bool IsTailscale(string a)
	{
		var parts = a.Split('.');
		return parts.Length == 4 && parts[0] == "100" && int.TryParse(parts[1], out int second) && second is >= 64 and <= 127;
	}

	private static string Name(string name) => string.IsNullOrWhiteSpace(name) ? "Motståndaren" : name;
}

/// <summary>
/// Command-line start (after "--"), for the smoke test and quick testing:
/// --host [--port N] --password X | --join ip[:port] --password X, plus --name X, --bot (a bot plays for you),
/// --steps N --out FILE (write the checksum at step N, then quit), --seed N, --map nursery|garden.
/// </summary>
public sealed record LaunchArgs(bool Host, string Join, int Port, string Password, string Name, bool Bot,
	int Steps, string Out, int Seed, MapTheme Map = MapTheme.Nursery)
{
	public bool Online => Host || Join != null;

	public static LaunchArgs Parse(IReadOnlyList<string> args)
	{
		string Value(string key)
		{
			int at = args.ToList().IndexOf(key);
			return at >= 0 && at + 1 < args.Count ? args[at + 1] : null;
		}
		int Number(string key, int fallback) => int.TryParse(Value(key), out int n) ? n : fallback;
		return new LaunchArgs(args.Contains("--host"), Value("--join"), Number("--port", Protocol.DefaultPort),
			Value("--password") ?? "", Value("--name") ?? (args.Contains("--host") ? "Värd" : "Gäst"),
			args.Contains("--bot"), Number("--steps", 0), Value("--out") ?? "", Number("--seed", 1),
			Enum.TryParse(Value("--map") ?? "", ignoreCase: true, out MapTheme map) && Enum.IsDefined(map) ? map : MapTheme.Nursery);
	}
}
