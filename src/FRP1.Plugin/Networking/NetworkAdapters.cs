using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FRP1.Plugin.Networking;

public sealed record LocalNetworkAddress(string AdapterName, IPAddress Address);

public static class NetworkAdapters
{
    // Local interface enumeration only: no DNS, connectivity probes or outside requests.
    public static IReadOnlyList<LocalNetworkAddress> EnumerateLocalAddresses()
    {
        var result = new List<LocalNetworkAddress> { new("Loopback", IPAddress.Loopback), new("Loopback IPv6", IPAddress.IPv6Loopback) };
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var address in adapter.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6) ||
                    address.Address.IsIPv6Multicast || address.Address.Equals(IPAddress.Any) || address.Address.Equals(IPAddress.IPv6Any)) continue;
                if (!result.Any(item => item.Address.Equals(address.Address))) result.Add(new(adapter.Name, address.Address));
                if (result.Count >= 64) return result.AsReadOnly();
            }
        }
        return result.AsReadOnly();
    }
}
