using System.Net;
using System.Net.Sockets;

namespace LlmUtilityApi.Services;

/// <summary>
/// SSRF guard: classifies an IP as not-publicly-routable (loopback, private, link-local, CGNAT,
/// multicast/reserved, unspecified). <see cref="SafeFetchService"/> refuses to open a connection to
/// any blocked address — re-checked on every redirect hop — so a hostname that resolves (or 30x's)
/// to an internal address can't be used to reach LAN services or the cloud metadata endpoint.
/// </summary>
internal static class IpGuard
{
    public static bool IsBlocked(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] switch
            {
                0 => true,                                  // 0.0.0.0/8 "this network"
                10 => true,                                 // 10.0.0.0/8 private
                127 => true,                                // 127.0.0.0/8 loopback
                100 when b[1] is >= 64 and <= 127 => true,  // 100.64.0.0/10 CGNAT
                169 when b[1] == 254 => true,               // 169.254.0.0/16 link-local (incl. 169.254.169.254 metadata)
                172 when b[1] is >= 16 and <= 31 => true,   // 172.16.0.0/12 private
                192 when b[1] == 168 => true,               // 192.168.0.0/16 private
                >= 224 => true,                             // 224/4 multicast + 240/4 reserved
                _ => false,
            };
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
            if (ip.Equals(IPAddress.IPv6Any)) return true;                 // ::
            if (ip.IsIPv4MappedToIPv6) return IsBlocked(ip.MapToIPv4());
            return (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;              // fc00::/7 unique-local
        }

        return true; // unknown family — refuse
    }
}
