// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Security.Cryptography;
using System.Text;

namespace RDPManager.Services;

/// <summary>
/// 使用 Windows DPAPI 加密密碼:只有同一台電腦上的同一個 Windows 使用者能解密。
/// </summary>
public static class PasswordProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RDPManager.v1.entropy");

    public static string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(data);
    }

    public static string Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return "";
        try
        {
            var data = ProtectedData.Unprotect(Convert.FromBase64String(cipher), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return ""; // 來自其他電腦/使用者的資料無法解密
        }
    }
}
