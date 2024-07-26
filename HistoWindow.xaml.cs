// Ignore Spelling: Histo

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;

using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

using Transparency.Support;
using Transparency.ViewModels;
using Transparency.Services;

using CommunityToolkit.WinUI.Helpers;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Input;


namespace Transparency;

/// <summary>
/// Histogram Window
/// </summary>
/// <remarks>
/// Because a <see cref="Microsoft.UI.Xaml.Window"/> does not inherit <see cref="Microsoft.UI.Xaml.FrameworkElement"/>,
/// you cannot use <see cref="Microsoft.UI.Xaml.Data.IValueConverter"/>s directly from the XAML. One workaround is to 
/// use the <see cref="Microsoft.UI.Xaml.Window"/> to load a <see cref="Microsoft.UI.Xaml.Controls.Page"/> and then 
/// wire-up the converters inside the <see cref="Microsoft.UI.Xaml.Controls.Page"/>'s XAML.
/// </remarks>
public sealed partial class HistoWindow : Window
{
    ConfigWindow? cfgWin;
    MainViewModel? ViewModel = App.Current.Services.GetService<MainViewModel>();
    FileLogger? Logger = (FileLogger?)App.Current.Services.GetService<ILogger>();
    Windows.Win32.Foundation.HWND Handle;
    WINDOW_EX_STYLE WinExStyle
    {
        get => (WINDOW_EX_STYLE)PInvoke.GetWindowLong(Handle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        set => _ = PInvoke.SetWindowLong(Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)value);
    }

    #region [Dragging Props]
    int initialPointerX = 0;
    int initialPointerY = 0;
    int windowStartX = 0;
    int windowStartY = 0;
    bool isMoving = false;
    Microsoft.UI.Windowing.AppWindow appW;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetCursorPos(out Windows.Graphics.PointInt32 lpPoint);
    #endregion

    public HistoWindow()
    {
        Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name} [{DateTime.Now.ToString("hh:mm:ss.fff tt")}]");

        this.InitializeComponent();
        this.Activated += MainWindow_Activated;
        Title = App.GetCurrentAssemblyName();
        var hwnd = WindowNative.GetWindowHandle(this);
        Handle = new Windows.Win32.Foundation.HWND(hwnd);
        WinExStyle |= WINDOW_EX_STYLE.WS_EX_LAYERED; // We'll use WS_EX_LAYERED, not WS_EX_TRANSPARENT, for the effect.
        WinExStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW; // Prevent accidental Minimize/Maximize.
        SystemBackdrop = new TransparentBackdrop();
        Content.Background = new SolidColorBrush(Colors.Green);
        Content.Background = new SolidColorBrush(Colors.Transparent);
        //Content.Background = (SolidColorBrush)App.Current.Resources["ApplicationPageBackgroundThemeBrush"];

        #region [Dragging]
        //IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Microsoft.UI.WindowId WndID = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        appW = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(WndID);
        rootGrid.PointerPressed += RootGrid_PointerPressed;
        rootGrid.PointerMoved += RootGrid_PointerMoved;
        rootGrid.PointerReleased += RootGrid_PointerReleased;
        #endregion
    }

    void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            if (ViewModel!.Config!.logging)
                Logger?.WriteLine($"The histo window was {args.WindowActivationState}.", LogLevel.Debug);

            SetIsAlwaysOnTop(this, true);

            if (ViewModel.Config.ctrlRowBottom)
                SetBottomControlRow();
            else
                SetTopControlRow();
        }
    }

    void SetTopControlRow()
    {
        rootGrid.RowDefinitions.Clear();
        rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40, GridUnitType.Pixel) });
        rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(ctrlGrid, 0);
        Grid.SetRow(lvHisto, 0);
        Grid.SetRowSpan(lvHisto, 2);
    }

    void SetBottomControlRow()
    {
        rootGrid.RowDefinitions.Clear();
        rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40, GridUnitType.Pixel) });
        Grid.SetRow(ctrlGrid, 1);
        Grid.SetRow(lvHisto, 0);
        Grid.SetRowSpan(lvHisto, 2);
    }

    void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        //var btn = sender as Button;
        //if (btn != null)
        //    btn.Content = "x";
        Application.Current.Exit();
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (cfgWin is null)
        {
            cfgWin = new();
            cfgWin?.Activate();
            cfgWin?.BeginStoryboard();
        }
        else
        {
            cfgWin?.Close();
            cfgWin = new();
            cfgWin?.Activate();
            cfgWin?.BeginStoryboard();
        }
    }

    #region [AlwaysOnTop Helpers]
    /// <summary>
    /// Configures whether the window should always be displayed on top of other windows or not
    /// </summary>
    /// <remarks>The presenter must be an overlapped presenter.</remarks>
    /// <exception cref="NotSupportedException">Throw if the AppWindow Presenter isn't an overlapped presenter.</exception>
    /// <param name="window"><see cref="Microsoft.UI.Xaml.Window"/></param>
    /// <param name="enable">true to set always on top, false otherwise</param>
    void SetIsAlwaysOnTop(Microsoft.UI.Xaml.Window window, bool enable) => UpdateOverlappedPresenter(window, (op) => op.IsAlwaysOnTop = enable);
    void UpdateOverlappedPresenter(Microsoft.UI.Xaml.Window window, Action<Microsoft.UI.Windowing.OverlappedPresenter> action)
    {
        if (window is null)
            throw new ArgumentNullException(nameof(window));

        var appwindow = GetAppWindow(window);

        if (appwindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter overlapped)
            action(overlapped);
        else
            throw new NotSupportedException($"Not supported with a {appwindow.Presenter.Kind} presenter.");
    }

    /// <summary>
    /// Gets the <see cref="Microsoft.UI.Windowing.AppWindow"/> for the window.
    /// </summary>
    /// <param name="window"><see cref="Microsoft.UI.Xaml.Window"/></param>
    /// <returns><see cref="Microsoft.UI.Windowing.AppWindow"/></returns>
    Microsoft.UI.Windowing.AppWindow GetAppWindow(Microsoft.UI.Xaml.Window window) => GetAppWindowFromWindowHandle(WindowNative.GetWindowHandle(window));

    /// <summary>
    /// Gets the <see cref="Microsoft.UI.Windowing.AppWindow"/> from an HWND.
    /// </summary>
    /// <param name="hwnd"><see cref="IntPtr"/> of the window</param>
    /// <returns><see cref="Microsoft.UI.Windowing.AppWindow"/></returns>
    Microsoft.UI.Windowing.AppWindow GetAppWindowFromWindowHandle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentNullException(nameof(hwnd));

        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
    }
    #endregion

    #region [Drag Events]
    void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] {((Grid)sender).Name} PointerPressed");
        ((UIElement)sender).CapturePointer(e.Pointer);
        var currentPoint = e.GetCurrentPoint((UIElement)sender);
        if (currentPoint.Properties.IsLeftButtonPressed)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);
            windowStartX = appW.Position.X;
            windowStartY = appW.Position.Y;
            Windows.Graphics.PointInt32 pt;
            GetCursorPos(out pt); // user32.dll
            initialPointerX = pt.X;
            initialPointerY = pt.Y;
            isMoving = true;
        }
        else if (currentPoint.Properties.IsRightButtonPressed)
        {
            e.Handled = true;
            Application.Current.Exit();
        }
    }

    void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] {((Grid)sender).Name} PointerReleased");
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
        isMoving = false;
    }

    void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var currentPoint = e.GetCurrentPoint((UIElement)sender);
        if (currentPoint.Properties.IsLeftButtonPressed)
        {
            Windows.Graphics.PointInt32 pt;
            GetCursorPos(out pt);
            if (isMoving)
                appW.Move(new Windows.Graphics.PointInt32(windowStartX + (pt.X - initialPointerX), windowStartY + (pt.Y - initialPointerY)));
        }
    }
    #endregion
}
