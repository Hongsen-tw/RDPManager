// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

namespace RDPManager.Services;

public static class AddressParser
{
    public const int DefaultPort = 3389;
    public const int DefaultVncPort = 5900;

    /// <summary>快速連線:「vnc://host[:port]」或「vnc:host」視為 VNC</summary>
    public static bool StripVncPrefix(string input, out string rest)
    {
        var s = input.Trim();
        foreach (var p in new[] { "vnc://", "vnc:" })
        {
            if (s.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                rest = s[p.Length..];
                return true;
            }
        }
        rest = s;
        return false;
    }

    /// <summary>解析 host、host:port、[IPv6]:port、IPv6</summary>
    public static bool TryParse(string? input, out string host, out int port, int defaultPort = DefaultPort)
    {
        host = "";
        port = defaultPort;
        var s = (input ?? "").Trim();
        if (s.Length == 0) return false;

        if (s.StartsWith('['))
        {
            var end = s.IndexOf(']');
            if (end < 0) return false;
            host = s[1..end];
            var rest = s[(end + 1)..];
            if (rest.StartsWith(':') && !int.TryParse(rest[1..], out port)) return false;
        }
        else if (s.Count(ch => ch == ':') == 1)
        {
            var i = s.IndexOf(':');
            host = s[..i];
            if (!int.TryParse(s[(i + 1)..], out port)) return false;
        }
        else
        {
            host = s;
        }

        host = host.Trim();
        return host.Length > 0 && port is > 0 and < 65536;
    }

    public static string Format(string host, int port, int defaultPort = DefaultPort)
    {
        var h = host.Contains(':') && !host.StartsWith('[') ? $"[{host}]" : host;
        return port == defaultPort ? h : $"{h}:{port}";
    }
}
