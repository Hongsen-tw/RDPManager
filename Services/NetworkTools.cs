// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace RDPManager.Services;

public static class HostChecker
{
    /// <summary>以 TCP 連接 RDP 連接埠判斷主機是否可連線,並回傳延遲 (ms)</summary>
    public static async Task<(bool Online, long Ms)> CheckAsync(string host, int port, int timeoutMs)
    {
        if (string.IsNullOrWhiteSpace(host)) return (false, 0);
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(host.Trim(), port, cts.Token);
            return (true, sw.ElapsedMilliseconds);
        }
        catch
        {
            return (false, sw.ElapsedMilliseconds);
        }
    }
}

public static class WakeOnLan
{
    public static bool TryParseMac(string? mac, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var hex = Regex.Replace(mac ?? "", "[^0-9A-Fa-f]", "");
        if (hex.Length != 12) return false;
        bytes = Convert.FromHexString(hex);
        return true;
    }

    /// <summary>送出 Magic Packet (6 x FF + 16 x MAC) 到區網廣播位址</summary>
    public static void Send(byte[] mac)
    {
        var packet = new byte[6 + 16 * 6];
        for (int i = 0; i < 6; i++) packet[i] = 0xFF;
        for (int i = 0; i < 16; i++) Buffer.BlockCopy(mac, 0, packet, 6 + i * 6, 6);

        using var udp = new UdpClient { EnableBroadcast = true };
        udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
        udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 7));
    }
}
