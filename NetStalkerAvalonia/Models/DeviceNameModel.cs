using System.Net.NetworkInformation;

namespace NetStalkerAvalonia.Models;

public class DeviceNameModel
{
    public string Mac { get; set; }
    public string? Name { get; set; }

    public DeviceNameModel(string? mac, string? name)
    {
        Mac = mac;
        Name = name;
    }
}