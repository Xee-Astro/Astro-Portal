using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace AsiairContainer;

public sealed class EmbeddedView : HwndHost
{
    public bool SuppressActivation { get; set; }
    private nint host, child, owner;
    public EmbeddedView() { IsVisibleChanged += (_, _) => ResizeChild(); }
    protected override HandleRef BuildWindowCore(HandleRef parent)
    {
        host = CreateWindowEx(0, "static", "Astro - Portal display host", 0x50000000, 0, 0, 1, 1, parent.Handle, 0, 0, 0);
        if (host == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        return new(this, host);
    }
    protected override void DestroyWindowCore(HandleRef handle) { Detach(); DestroyWindow(handle.Handle); host = 0; }
    public void Attach(nint window)
    {
        if (host == 0) throw new InvalidOperationException("The Windows display host is not ready.");
        child = window; owner = new WindowInteropHelper(Window.GetWindow(this)).Handle;
        long style = GetWindowLongPtr(child, -16).ToInt64();
        style &= ~(0x40000000L | 0x00C00000L | 0x00040000L | 0x00080000L);
        style |= 0x90000000L;
        SetWindowLongPtr(child, -16, new nint(style));
        long extended = GetWindowLongPtr(child, -20).ToInt64();
        SetWindowLongPtr(child, -20, new nint((extended & ~0x00040000L) | 0x00000080L));
        // Keep SDL as an owned top-level surface: reparenting it as WS_CHILD breaks
        // its native Windows keyboard/IME focus handling.
        SetWindowLongPtr(child, -8, owner);
        ResizeChild(); ShowWindow(child, 4); FocusAndroid();
    }
    public void SetInputEnabled(bool enabled) { if (IsAttached) EnableWindow(child, enabled); }
    [DllImport("user32.dll")] private static extern bool EnableWindow(nint handle, bool enabled);
    public void FocusAndroid()
    {
        if (!IsAttached || !IsVisible) return;
        uint current = GetCurrentThreadId(), target = GetWindowThreadProcessId(child, out _);
        bool attached = target != current && AttachThreadInput(current, target, true);
        try { Keyboard.ClearFocus(); if (!SuppressActivation) SetForegroundWindow(child); SetFocus(child); }
        finally { if (attached) AttachThreadInput(current, target, false); }
    }
    protected override bool TabIntoCore(TraversalRequest request) { if (!IsAttached) return false; FocusAndroid(); return true; }
    protected override bool TranslateAcceleratorCore(ref MSG message, ModifierKeys modifiers)
    {
        if (IsAttached && (GetFocus() == child || GetFocus() == host || IsKeyboardFocusWithin) && message.hwnd != child && message.message is 0x0100 or 0x0101 or 0x0102 or 0x0104 or 0x0105)
        { PostMessage(child, (uint)message.message, message.wParam, message.lParam); return true; }
        return base.TranslateAcceleratorCore(ref message, modifiers);
    }
    protected override nint WndProc(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x0007 && child != 0) SetFocus(child);
        return base.WndProc(hwnd, message, wparam, lparam, ref handled);
    }
    private nint ActivateKeyboardProbe()
    {
        nint previous = GetForegroundWindow();
        uint current = GetCurrentThreadId(), active = GetWindowThreadProcessId(previous, out _);
        bool attached = current != active && AttachThreadInput(current, active, true);
        try { SetForegroundWindow(child); SetFocus(child); }
        finally { if (attached) AttachThreadInput(current, active, false); }
        if (GetForegroundWindow() != child) throw new InvalidOperationException("Keyboard probe could not activate the Android view.");
        return previous;
    }
    private static void SendKey(ushort key, bool up)
    {
        var input = new NativeInput { Type = 1, Data = new NativeInputUnion { Keyboard = new NativeKeyboard { VirtualKey = key, Flags = up ? 2u : 0u } } };
        if (SendInput(1, [input], Marshal.SizeOf<NativeInput>()) != 1) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    public async Task BackspaceForVerification()
    {
        nint previous = ActivateKeyboardProbe();
        try { SendKey(8, false); SendKey(8, true); await Task.Delay(150); }
        finally { if (GetForegroundWindow() == child && previous != child) SetForegroundWindow(previous); }
    }
    public async Task TypeForVerification(string text)
    {
        nint previous = ActivateKeyboardProbe(); bool shifted = false;
        await Task.Delay(350); // Let SDL activate text input after native focus changes.
        try
        {
            foreach (char character in text)
            {
                if (GetForegroundWindow() != child) throw new InvalidOperationException("Keyboard probe interrupted by a focus change.");
                short mapping = VkKeyScan(character); if (mapping == -1) throw new InvalidOperationException("Test character unavailable on the Windows keyboard layout.");
                ushort key = (ushort)(mapping & 255); shifted = (mapping & 256) != 0;
                if (shifted) SendKey(16, false);
                SendKey(key, false); await Task.Delay(30); SendKey(key, true);
                if (shifted) { SendKey(16, true); shifted = false; }
                await Task.Delay(30);
            }
            await Task.Delay(150);
        }
        finally { if (shifted) SendKey(16, true); if (GetForegroundWindow() == child && previous != child) SetForegroundWindow(previous); }
    }
    public void Detach()
    {
        if (child != 0 && IsWindow(child)) { ShowWindow(child, 0); SetWindowLongPtr(child, -8, 0); }
        child = owner = 0;
    }
    public bool IsAttached => host != 0 && child != 0 && GetWindowLongPtr(child, -8) == owner && IsWindow(child);
    public void Fit() => ResizeChild();
    public void ClickGuest(double x, double y, double guestWidth, double guestHeight)
    {
        VerifySize(); GetClientRect(child, out var area);
        double scale = Math.Min(area.Right / guestWidth, area.Bottom / guestHeight);
        int px = (int)((area.Right - guestWidth * scale) / 2 + x * scale);
        int py = (int)((area.Bottom - guestHeight * scale) / 2 + y * scale);
        nint point = new((py << 16) | (px & 0xFFFF));
        PostMessage(child, 0x0200, 0, point);
        PostMessage(child, 0x0201, 1, point);
        PostMessage(child, 0x0202, 0, point);
    }
    public void ClickGuestWithWindowsMouse(double x, double y, double guestWidth, double guestHeight)
    {
        VerifySize(); ActivateKeyboardProbe(); GetClientRect(child, out var area);
        double scale = Math.Min(area.Right / guestWidth, area.Bottom / guestHeight);
        var point = new NativePoint { X = (int)((area.Right - guestWidth * scale) / 2 + x * scale), Y = (int)((area.Bottom - guestHeight * scale) / 2 + y * scale) };
        ClientToScreen(child, ref point);
        SetCursorPos(point.X, point.Y);
        Thread.Sleep(100);
        var down = new NativeInput { Type = 0, Data = new NativeInputUnion { Mouse = new NativeMouse { Flags = 2 } } };
        var up = new NativeInput { Type = 0, Data = new NativeInputUnion { Mouse = new NativeMouse { Flags = 4 } } };
        if (SendInput(1, [down], Marshal.SizeOf<NativeInput>()) != 1) throw new Win32Exception(Marshal.GetLastPInvokeError());
        Thread.Sleep(80);
        if (SendInput(1, [up], Marshal.SizeOf<NativeInput>()) != 1) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    public void VerifySize()
    {
        if (!IsAttached || !GetClientRect(host, out var size) || size.Right < 100 || size.Bottom < 100)
            throw new InvalidOperationException("The Android display was not embedded at a usable size.");
    }
    protected override void OnWindowPositionChanged(Rect rectangle) { base.OnWindowPositionChanged(rectangle); ResizeChild(); }
    private void ResizeChild()
    {
        if (host == 0 || child == 0 || !IsWindow(child)) return;
        var parentWindow = Window.GetWindow(this);
        if (!IsVisible || parentWindow is null || parentWindow.WindowState == WindowState.Minimized || !parentWindow.IsEnabled || !IsWindowEnabled(owner)) { ShowWindow(child, 0); return; }
        if (!GetWindowRect(host, out var rectangle)) return;
        if ((GetWindowLongPtr(child, -20).ToInt64() & 8) != 0) SetWindowPos(child, new nint(-2), 0, 0, 0, 0, 0x0013);
        MoveWindow(child, rectangle.Left, rectangle.Top, Math.Max(1, rectangle.Right - rectangle.Left), Math.Max(1, rectangle.Bottom - rectangle.Top), true);
        if (!IsWindowVisible(child) && (GetForegroundWindow() == owner || GetForegroundWindow() == child)) ShowWindow(child, 4);
    }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public NativeInputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct NativeInputUnion { [FieldOffset(0)] public NativeKeyboard Keyboard; [FieldOffset(0)] public NativeMouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeKeyboard { public ushort VirtualKey, ScanCode; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScan(char character);
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(int extended, string className, string name, int style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool MoveWindow(nint window, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern nint GetFocus();
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetParent(nint window);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wparam, nint lparam);
}
