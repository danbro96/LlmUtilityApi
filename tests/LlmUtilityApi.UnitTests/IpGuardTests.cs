using System.Net;
using LlmUtilityApi.Services;
using Xunit;

namespace LlmUtilityApi.UnitTests;

/// <summary>
/// The SSRF guard is the security boundary of the fetch tool: it must reject every
/// not-publicly-routable address (loopback, private, link-local incl. the cloud metadata IP,
/// CGNAT, multicast, unique-local) and allow ordinary public addresses.
/// </summary>
public class IpGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]      // loopback
    [InlineData("10.0.0.5")]       // private 10/8
    [InlineData("172.16.0.1")]     // private 172.16/12
    [InlineData("172.31.255.255")] // private 172.16/12 upper
    [InlineData("192.168.1.10")]   // private 192.168/16
    [InlineData("169.254.169.254")]// link-local / cloud metadata
    [InlineData("100.64.0.1")]     // CGNAT
    [InlineData("0.0.0.0")]        // this-network
    [InlineData("224.0.0.1")]      // multicast
    [InlineData("::1")]            // IPv6 loopback
    [InlineData("fe80::1")]        // IPv6 link-local
    [InlineData("fc00::1")]        // IPv6 unique-local
    [InlineData("::ffff:10.0.0.1")] // IPv4-mapped private
    public void Blocks_non_public_addresses(string ip)
        => Assert.True(IpGuard.IsBlocked(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]  // example.com
    [InlineData("2606:4700:4700::1111")] // public IPv6 (Cloudflare)
    public void Allows_public_addresses(string ip)
        => Assert.False(IpGuard.IsBlocked(IPAddress.Parse(ip)));
}
