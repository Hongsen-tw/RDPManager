// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Text;
using System.Text.Json;
using RDPManager.Models;

namespace RDPManager.Services;

public static class ImportExport
{
    private static readonly string[] CsvHeaders =
        { "Name", "Host", "Port", "Username", "Domain", "Group", "Notes", "MacAddress", "Favorite" };

    // ---------- JSON ----------
    /// <summary>匯出 JSON (不含密碼,因為 DPAPI 加密的密碼無法在其他電腦解密)</summary>
    public static void ExportJson(string path, IEnumerable<RdpConnection> list)
    {
        var copy = list.Select(c => { var x = c.Clone(); x.EncryptedPassword = null; return x; }).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(copy, DataStore.JsonOptions), Encoding.UTF8);
    }

    public static List<RdpConnection> ImportJson(string path) =>
        JsonSerializer.Deserialize<List<RdpConnection>>(File.ReadAllText(path), DataStore.JsonOptions) ?? new();

    // ---------- CSV ----------
    public static void ExportCsv(string path, IEnumerable<RdpConnection> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", CsvHeaders));
        foreach (var c in list)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                c.Name, c.Host, c.Port.ToString(), c.Username, c.Domain, c.Group, c.Notes, c.MacAddress,
                c.IsFavorite ? "1" : "0",
            }.Select(Esc)));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); // 含 BOM,Excel 開啟中文不亂碼
    }

    public static List<RdpConnection> ImportCsv(string path)
    {
        var rows = ParseCsv(File.ReadAllText(path));
        var result = new List<RdpConnection>();
        if (rows.Count < 2) return result;

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => header.FindIndex(h => names.Contains(h));
        int iName = Col("name", "名稱"), iHost = Col("host", "ip", "address", "主機", "位址"),
            iPort = Col("port", "連接埠"), iUser = Col("username", "user", "使用者"),
            iDomain = Col("domain", "網域"), iGroup = Col("group", "群組"), iNotes = Col("notes", "備註"),
            iMac = Col("macaddress", "mac"), iFav = Col("favorite", "我的最愛");
        if (iHost < 0) throw new InvalidDataException("CSV 缺少 Host 欄位");

        string Get(List<string> r, int i) => i >= 0 && i < r.Count ? r[i].Trim() : "";

        foreach (var r in rows.Skip(1))
        {
            var hostText = Get(r, iHost);
            if (!AddressParser.TryParse(hostText, out var host, out var port)) continue;
            if (int.TryParse(Get(r, iPort), out var p) && p is > 0 and < 65536) port = p;
            var name = Get(r, iName);
            result.Add(new RdpConnection
            {
                Name = name.Length > 0 ? name : host,
                Host = host,
                Port = port,
                Username = Get(r, iUser),
                Domain = Get(r, iDomain),
                Group = Get(r, iGroup),
                Notes = Get(r, iNotes),
                MacAddress = Get(r, iMac),
                IsFavorite = Get(r, iFav) is "1" or "true" or "True" or "是",
            });
        }
        return result;
    }

    private static string Esc(string? v)
    {
        v ??= "";
        return v.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = false;
                }
                else sb.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(sb.ToString()); sb.Clear(); }
            else if (ch == '\r') { /* skip */ }
            else if (ch == '\n') { row.Add(sb.ToString()); sb.Clear(); rows.Add(row); row = new List<string>(); }
            else sb.Append(ch);
        }
        if (sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row); }
        return rows.Where(r => r.Any(c => c.Length > 0)).ToList();
    }

    // ---------- .rdp ----------
    public static RdpConnection ImportRdpFile(string path)
    {
        var c = new RdpConnection { Name = Path.GetFileNameWithoutExtension(path) };
        int? serverPort = null;
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split(':', 3);
            if (parts.Length < 3) continue;
            var key = parts[0].Trim().ToLowerInvariant();
            var val = parts[2].Trim();
            int.TryParse(val, out var n);
            switch (key)
            {
                case "full address":
                    if (AddressParser.TryParse(val, out var h, out var p)) { c.Host = h; c.Port = p; }
                    break;
                case "server port": serverPort = n; break;
                case "username":
                    var idx = val.IndexOf('\\');
                    if (idx > 0) { c.Domain = val[..idx]; c.Username = val[(idx + 1)..]; }
                    else c.Username = val;
                    break;
                case "screen mode id": c.ScreenMode = n == 2 ? ScreenMode.FullScreen : ScreenMode.Custom; break;
                case "desktopwidth": if (n > 0) c.DesktopWidth = n; break;
                case "desktopheight": if (n > 0) c.DesktopHeight = n; break;
                case "session bpp": if (n is 15 or 16 or 24 or 32) c.ColorDepth = n; break;
                case "smart sizing": c.SmartSizing = n == 1; break;
                case "use multimon": c.MultiMonitor = n == 1; break;
                case "redirectclipboard": c.RedirectClipboard = n == 1; break;
                case "redirectprinters": c.RedirectPrinters = n == 1; break;
                case "drivestoredirect": c.RedirectDrives = val.Length > 0; break;
                case "audiomode": if (n is >= 0 and <= 2) c.AudioMode = (AudioMode)n; break;
                case "administrative session": c.AdminSession = n == 1; break;
            }
        }
        if (serverPort is > 0 and < 65536 && c.Port == AddressParser.DefaultPort) c.Port = serverPort.Value;
        if (string.IsNullOrWhiteSpace(c.Name)) c.Name = c.Host;
        return c;
    }
}
