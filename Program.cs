// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using RDPManager.Forms;

namespace RDPManager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "RDPManager_SingleInstance_7C1E2A", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show("RDP 遠端連線管理員已在執行中。", "RDP Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show("發生未預期的錯誤:\n" + e.Exception.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);

        // 用法:RDPManager.exe [已儲存的連線名稱 或 IP[:連接埠]]
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
