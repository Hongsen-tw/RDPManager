// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Runtime.InteropServices;

namespace RDPManager.Services;

/// <summary>
/// 將帳密寫入 Windows 認證管理員 (TERMSRV/host),讓外部 mstsc 自動登入。
/// 使用 CRED_PERSIST_SESSION,登出 Windows 後即自動清除。
/// </summary>
public static class CredentialManager
{
    private const int CRED_TYPE_DOMAIN_PASSWORD = 2;
    private const int CRED_PERSIST_SESSION = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    public static void WriteRdpCredential(string host, string userName, string password)
    {
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_DOMAIN_PASSWORD,
                TargetName = "TERMSRV/" + host,
                UserName = userName,
                CredentialBlob = blob,
                CredentialBlobSize = password.Length * 2,
                Persist = CRED_PERSIST_SESSION,
                Comment = "RDP Manager",
            };
            if (!CredWrite(ref cred, 0))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(blob);
        }
    }

    public static void DeleteRdpCredential(string host) =>
        CredDelete("TERMSRV/" + host, CRED_TYPE_DOMAIN_PASSWORD, 0);
}
