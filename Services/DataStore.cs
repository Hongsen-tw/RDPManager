// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RDPManager.Models;

namespace RDPManager.Services;

/// <summary>以 JSON 檔案儲存連線、設定與連線紀錄 (%AppData%\RDPManager)</summary>
public class DataStore
{
    /// <summary>
    /// 資料夾位置 (依序):環境變數 RDPMANAGER_DATA → 程式旁的 data 資料夾 (可攜模式,例如放在隨身碟)
    /// → %AppData%\RDPManager
    /// </summary>
    public static readonly string DataDir = ResolveDataDir();

    private static string ResolveDataDir()
    {
        var env = Environment.GetEnvironmentVariable("RDPMANAGER_DATA");
        if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
        var portable = Path.Combine(AppContext.BaseDirectory, "data");
        if (Directory.Exists(portable)) return portable;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RDPManager");
    }
    public static string BackupDir => Path.Combine(DataDir, "backups");
    private static string ConnectionsPath => Path.Combine(DataDir, "connections.json");
    private static string SettingsPath => Path.Combine(DataDir, "settings.json");
    private static string HistoryPath => Path.Combine(DataDir, "history.json");

    public const int MaxHistory = 1000;
    private const int MaxBackups = 30;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public List<RdpConnection> Connections { get; set; } = new();
    public List<HistoryEntry> History { get; private set; } = new();
    public AppSettings Settings { get; private set; } = new();
    public List<string> LoadWarnings { get; } = new();

    public void Load()
    {
        Directory.CreateDirectory(DataDir);
        Connections = Read<List<RdpConnection>>(ConnectionsPath) ?? new();
        History = Read<List<HistoryEntry>>(HistoryPath) ?? new();
        Settings = Read<AppSettings>(SettingsPath) ?? new();
    }

    private T? Read<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            var bad = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            try { File.Copy(path, bad, true); } catch { /* ignore */ }
            LoadWarnings.Add($"{Path.GetFileName(path)} 讀取失敗,已另存為 {Path.GetFileName(bad)}:{ex.Message}");
            return default;
        }
    }

    private static void Write<T>(string path, T data)
    {
        Directory.CreateDirectory(DataDir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(tmp, path, true); // 先寫暫存檔再取代,避免寫入中斷造成檔案損毀
    }

    public void SaveConnections() => Write(ConnectionsPath, Connections);
    public void SaveSettings() => Write(SettingsPath, Settings);
    public void SaveHistory() => Write(HistoryPath, History);

    public void AddHistory(HistoryEntry entry)
    {
        History.Insert(0, entry);
        if (History.Count > MaxHistory) History.RemoveRange(MaxHistory, History.Count - MaxHistory);
        try { SaveHistory(); } catch { /* 紀錄寫入失敗不影響連線 */ }
    }

    public void ClearHistory()
    {
        History.Clear();
        SaveHistory();
    }

    public IReadOnlyList<string> GetGroups() =>
        Connections.Select(c => c.Group.Trim())
                   .Where(g => g.Length > 0)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(g => g, StringComparer.CurrentCulture)
                   .ToList();

    public string CreateBackup()
    {
        Directory.CreateDirectory(BackupDir);
        var path = Path.Combine(BackupDir, $"connections-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(Connections, JsonOptions));
        foreach (var old in Directory.GetFiles(BackupDir, "connections-*.json")
                                     .OrderByDescending(f => f).Skip(MaxBackups))
        {
            try { File.Delete(old); } catch { /* ignore */ }
        }
        return path;
    }

    /// <summary>每天第一次啟動時自動備份</summary>
    public void AutoDailyBackup()
    {
        if (Connections.Count == 0) return;
        Directory.CreateDirectory(BackupDir);
        var today = $"connections-{DateTime.Now:yyyyMMdd}-";
        if (!Directory.GetFiles(BackupDir, today + "*.json").Any()) CreateBackup();
    }

    public static List<RdpConnection> ReadBackup(string path) =>
        JsonSerializer.Deserialize<List<RdpConnection>>(File.ReadAllText(path), JsonOptions) ?? new();
}
