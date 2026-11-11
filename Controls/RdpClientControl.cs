// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Runtime.InteropServices;

namespace RDPManager.Controls;

/// <summary>
/// Windows 內建遠端桌面 ActiveX (mstscax.dll, MsRdpClient9NotSafeForScripting) 的 AxHost 包裝。
/// 以 late binding (dynamic) 存取屬性,因此不需要用 aximp 產生 Interop 組件。
/// </summary>
public class RdpClientControl : AxHost
{
    private const string RdpClientClsid = "8B918B82-7985-4C24-89DF-C33AD2BBFBCD";

    private ConnectionPointCookie? _cookie;
    private RdpEventSink? _sink;

    public event EventHandler? Connecting;
    public event EventHandler? Connected;
    public event EventHandler? LoginComplete;
    public event EventHandler<int>? Disconnected;
    public event EventHandler<int>? FatalError;
    /// <summary>ContainerHandledFullScreen 模式下,控制項要求容器進入全螢幕 (例如遠端按 Ctrl+Alt+Break)</summary>
    public event EventHandler? RequestGoFullScreen;
    public event EventHandler? RequestLeaveFullScreen;
    public event EventHandler? RequestContainerMinimize;

    public RdpClientControl() : base(RdpClientClsid) { }

    /// <summary>ActiveX 物件 (IDispatch),以 dynamic 存取</summary>
    public dynamic Ocx => GetOcx() ?? throw new InvalidOperationException("RDP 元件尚未建立。");

    protected override void CreateSink()
    {
        try
        {
            _sink = new RdpEventSink(this);
            _cookie = new ConnectionPointCookie(GetOcx()!, _sink, typeof(IMsTscAxEvents));
        }
        catch
        {
            _cookie = null; // 事件無法掛載時仍可連線,只是無法得知中斷狀態
        }
    }

    protected override void DetachSink()
    {
        try { _cookie?.Disconnect(); } catch { /* ignore */ }
        _cookie = null;
    }

    internal void RaiseConnecting() => Connecting?.Invoke(this, EventArgs.Empty);
    internal void RaiseConnected() => Connected?.Invoke(this, EventArgs.Empty);
    internal void RaiseLoginComplete() => LoginComplete?.Invoke(this, EventArgs.Empty);
    internal void RaiseDisconnected(int reason) => Disconnected?.Invoke(this, reason);
    internal void RaiseFatalError(int code) => FatalError?.Invoke(this, code);
    internal void RaiseRequestGoFullScreen() => RequestGoFullScreen?.Invoke(this, EventArgs.Empty);
    internal void RaiseRequestLeaveFullScreen() => RequestLeaveFullScreen?.Invoke(this, EventArgs.Empty);
    internal void RaiseRequestContainerMinimize() => RequestContainerMinimize?.Invoke(this, EventArgs.Empty);
}

/// <summary>mstscax 事件介面 (dispinterface IMsTscAxEvents)</summary>
[ComImport]
[Guid("336D5562-EFA8-482E-8CB3-C5C0FC7A7DB6")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IMsTscAxEvents
{
    [DispId(1)] void OnConnecting();
    [DispId(2)] void OnConnected();
    [DispId(3)] void OnLoginComplete();
    [DispId(4)] void OnDisconnected(int discReason);
    [DispId(8)] void OnRequestGoFullScreen();
    [DispId(9)] void OnRequestLeaveFullScreen();
    [DispId(10)] void OnFatalError(int errorCode);
    [DispId(14)] void OnRequestContainerMinimize();
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class RdpEventSink : IMsTscAxEvents
{
    private readonly RdpClientControl _owner;
    public RdpEventSink(RdpClientControl owner) => _owner = owner;

    public void OnConnecting() => _owner.RaiseConnecting();
    public void OnConnected() => _owner.RaiseConnected();
    public void OnLoginComplete() => _owner.RaiseLoginComplete();
    public void OnDisconnected(int discReason) => _owner.RaiseDisconnected(discReason);
    public void OnFatalError(int errorCode) => _owner.RaiseFatalError(errorCode);
    public void OnRequestGoFullScreen() => _owner.RaiseRequestGoFullScreen();
    public void OnRequestLeaveFullScreen() => _owner.RaiseRequestLeaveFullScreen();
    public void OnRequestContainerMinimize() => _owner.RaiseRequestContainerMinimize();
}
