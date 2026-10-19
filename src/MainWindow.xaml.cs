using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace AutoPowerMeter;

public partial class MainWindow : FluentWindow
{
    private const double DesignWidth = 1180;
    private const double MinimumDashboardHeight = 336;
    private const int WmEnterSizeMove = 0x0231;
    private const int WmSizing = 0x0214;
    private const int BottomEdge = 6;
    private HwndSource? _source;
    private NativeRect _resizeStart;
    private double _resizeAspect = DesignWidth / 700;
    private double _lastLogicalHeight = 652;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(ResizeHook);
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(ResizeHook);
        base.OnClosed(e);
    }

    private void DashboardViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DashboardViewport.ActualWidth <= 0 || DashboardViewport.ActualHeight <= 0) return;

        // 统一比例缩放，不再让 Viewbox 在上下留下空白。
        // 窗口底边增高时，新增高度直接给日志；极宽窗口则限制缩放以保留最小布局高度。
        var scale = Math.Min(DashboardViewport.ActualWidth / DesignWidth,
            DashboardViewport.ActualHeight / MinimumDashboardHeight);
        var logicalHeight = DashboardViewport.ActualHeight / scale;
        Dashboard.Width = DashboardViewport.ActualWidth / scale;
        Dashboard.Height = logicalHeight;
        Dashboard.RenderTransform = new ScaleTransform(scale, scale);
        Dashboard.AddLogViewportHeight(logicalHeight - _lastLogicalHeight);
        _lastLogicalHeight = logicalHeight;
    }

    private IntPtr ResizeHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmEnterSizeMove && GetWindowRect(hwnd, out var start))
        {
            _resizeStart = start;
            if (start.Height > 0) _resizeAspect = (double)start.Width / start.Height;
        }
        if (message != WmSizing || lParam == IntPtr.Zero) return IntPtr.Zero;

        var edge = wParam.ToInt32();
        if (edge == BottomEdge || edge is < 1 or > 8) return IntPtr.Zero;
        var rect = Marshal.PtrToStructure<NativeRect>(lParam);
        if (rect.Width <= 0 || rect.Height <= 0 || _resizeAspect <= 0) return IntPtr.Zero;

        if (edge is 1 or 2) // 左、右边：按宽度同比例调整高度。
            rect.Bottom = rect.Top + (int)Math.Round(rect.Width / _resizeAspect);
        else if (edge == 3) // 上边：按高度同比例调整宽度。
            rect.Right = rect.Left + (int)Math.Round(rect.Height * _resizeAspect);
        else // 四个角：以拖动幅度较大的维度为准，保持开始拖动时的窗口比例。
        {
            var widthChange = _resizeStart.Width > 0
                ? Math.Abs((double)(rect.Width - _resizeStart.Width) / _resizeStart.Width) : 0;
            var heightChange = _resizeStart.Height > 0
                ? Math.Abs((double)(rect.Height - _resizeStart.Height) / _resizeStart.Height) : 0;
            if (widthChange >= heightChange)
            {
                var desiredHeight = (int)Math.Round(rect.Width / _resizeAspect);
                if (edge is 4 or 5) rect.Top = rect.Bottom - desiredHeight;
                else rect.Bottom = rect.Top + desiredHeight;
            }
            else
            {
                var desiredWidth = (int)Math.Round(rect.Height * _resizeAspect);
                if (edge is 4 or 7) rect.Left = rect.Right - desiredWidth;
                else rect.Right = rect.Left + desiredWidth;
            }
        }
        Marshal.StructureToPtr(rect, lParam, false);
        handled = true;
        return new IntPtr(1);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }
}
