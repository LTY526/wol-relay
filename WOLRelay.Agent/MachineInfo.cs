using System.Net.NetworkInformation;
using WOLRelay.Shared;

namespace WOLRelay.Agent;

public static class MachineInfo
{
    /// <summary>
    /// Builds the registration for this machine. The MAC is taken from the first
    /// operational, non-loopback adapter with a physical address — the same address
    /// that must be used to wake this machine.
    /// </summary>
    public static AgentRegistration Current() => new()
    {
        Hostname = Environment.MachineName,
        MacAddress = PrimaryMac() ?? "",
    };

    private static string? PrimaryMac()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0);

        foreach (var nic in candidates)
        {
            var bytes = nic.GetPhysicalAddress().GetAddressBytes();
            if (bytes.Length == 6)
                return string.Join(":", bytes.Select(b => b.ToString("X2")));
        }

        return null;
    }
}
