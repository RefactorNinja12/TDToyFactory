using System.Threading.Tasks;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View.Net;

/// <summary>
/// Asks the router (UPnP) to forward the host's UDP port, so friends outside the home network can connect,
/// and finds the public address to give them. Discovery takes a couple of seconds, so it runs in the
/// background; the mapping is removed again when the match is over.
/// </summary>
public sealed class PortOpener
{
	/// <summary>The router drops the forwarding by itself after this, should the game crash before removing it.</summary>
	private const int LeaseSeconds = 2 * 60 * 60;

	private Upnp _upnp;
	private int _port;
	private volatile PortState _state = PortState.Pending;
	private volatile bool _closed;

	public PortState State => _state;
	public string PublicAddress { get; private set; } = "";

	public static PortOpener Open(int port)
	{
		var opener = new PortOpener { _port = port };
		Task.Run(opener.Try);
		return opener;
	}

	private void Try()
	{
		var upnp = new Upnp();
		if (upnp.Discover(2000, 2, "InternetGatewayDevice") != (int)Upnp.UpnpResult.Success
			|| upnp.GetGateway() is not { } gateway || !gateway.IsValidGateway()
			|| !Map(upnp))
		{
			_state = PortState.Failed;
			return;
		}
		_upnp = upnp;
		if (_closed)
		{
			upnp.DeletePortMapping(_port, "UDP"); // the host gave up while the router was answering
			return;
		}
		PublicAddress = upnp.QueryExternalAddress();
		_state = PortState.Opened;
	}

	// Some routers only take permanent forwardings: then ask for one (Close still removes it).
	private bool Map(Upnp upnp) =>
		upnp.AddPortMapping(_port, _port, "Leksakskrig", "UDP", LeaseSeconds) == (int)Upnp.UpnpResult.Success
		|| upnp.AddPortMapping(_port, _port, "Leksakskrig", "UDP", 0) == (int)Upnp.UpnpResult.Success;

	/// <summary>Removes the forwarding (if it was made).</summary>
	public void Close()
	{
		_closed = true;
		if (_state == PortState.Opened)
			_upnp?.DeletePortMapping(_port, "UDP");
		_state = PortState.Failed;
	}
}
