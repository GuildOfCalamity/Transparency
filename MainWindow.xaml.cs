using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

using Transparency.Support;
using Transparency.ViewModels;
using Transparency.Services;

using CommunityToolkit.WinUI.Helpers;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Hosting;

namespace Transparency;

/// <summary>
/// Main Window (gauge)
/// ✖ &#10006; ✔️
/// ✕ &#x2715; ✓ &#x2713;
/// ✖ &#x2716; ✔ &#x2714;
/// ✕ &#10005; Saltires: 🞭🞭🞬🞫🞪🞩🞨
/// </summary>
/// <remarks>
/// Because a <see cref="Microsoft.UI.Xaml.Window"/> does not inherit <see cref="Microsoft.UI.Xaml.FrameworkElement"/>,
/// you cannot use <see cref="Microsoft.UI.Xaml.Data.IValueConverter"/>s directly from the XAML. One workaround is to 
/// use the <see cref="Microsoft.UI.Xaml.Window"/> to load a <see cref="Microsoft.UI.Xaml.Controls.Page"/> and then 
/// wire-up the converters inside the <see cref="Microsoft.UI.Xaml.Controls.Page"/>'s XAML.
/// </remarks>
public sealed partial class MainWindow : Window
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

    #region [Rounded Window Test]
    // PInvoke declarations
    [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    private static extern IntPtr CreateRoundRectRgn(
        int nLeftRect,     // x-coordinate of upper-left corner
        int nTopRect,      // y-coordinate of upper-left corner
        int nRightRect,    // x-coordinate of lower-right corner
        int nBottomRect,   // y-coordinate of lower-right corner
        int nWidthEllipse, // height of ellipse
        int nHeightEllipse // width of ellipse
    );

    [DllImport("user32.dll", EntryPoint = "SetWindowRgn")]
    private static extern int SetWindowRgn(
        IntPtr hWnd,       // Handle to the window
        IntPtr hRgn,       // Handle to the region
        bool bRedraw       // Boolean to redraw the window
    );

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    #endregion

    public MainWindow()
    {
        Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name} [{DateTime.Now.ToString("hh:mm:ss.fff tt")}]");

        this.InitializeComponent();
        this.Activated += MainWindow_Activated;
        Title = App.GetCurrentAssemblyName();
        var hwnd = WindowNative.GetWindowHandle(this);
        Handle = new Windows.Win32.Foundation.HWND(hwnd);
        WinExStyle |= WINDOW_EX_STYLE.WS_EX_LAYERED; // We'll use WS_EX_LAYERED, not WS_EX_TRANSPARENT, for the effect.
        SystemBackdrop = new TransparentBackdrop();
        Content.Background = new SolidColorBrush(Colors.Green);
        Content.Background = new SolidColorBrush(Colors.Transparent);
        //Content.Background = (SolidColorBrush)App.Current.Resources["ApplicationPageBackgroundThemeBrush"];

        #region [just experimenting]
        //ApplyRoundedCorners(hwnd);
        //CreateGradientBackdrop();
        #endregion

        if (App.IsPackaged)
        {
            Logger?.WriteLine($"The app is running as Packaged.", LogLevel.Debug);
            Debug.WriteLine($"[INFO] ApplicationName............ {SystemInformation.Instance.ApplicationName}            ");
            Debug.WriteLine($"[INFO] ApplicationVersion......... {SystemInformation.Instance.ApplicationVersion}         ");
            Debug.WriteLine($"[INFO] AppUptime.................. {SystemInformation.Instance.AppUptime}                  ");
            Debug.WriteLine($"[INFO] AvailableMemory............ {SystemInformation.Instance.AvailableMemory}            ");
            Debug.WriteLine($"[INFO] Culture.................... {SystemInformation.Instance.Culture}                    ");
            Debug.WriteLine($"[INFO] DeviceFamily............... {SystemInformation.Instance.DeviceFamily}               ");
            Debug.WriteLine($"[INFO] DeviceManufacturer......... {SystemInformation.Instance.DeviceManufacturer}         ");
            Debug.WriteLine($"[INFO] DeviceModel................ {SystemInformation.Instance.DeviceModel}                ");
            Debug.WriteLine($"[INFO] FirstUseTime............... {SystemInformation.Instance.FirstUseTime}               ");
            Debug.WriteLine($"[INFO] IsAppUpdated............... {SystemInformation.Instance.IsAppUpdated}               ");
            Debug.WriteLine($"[INFO] IsFirstRun................. {SystemInformation.Instance.IsFirstRun}                 ");
            Debug.WriteLine($"[INFO] LaunchTime................. {SystemInformation.Instance.LaunchTime}                 ");
            Debug.WriteLine($"[INFO] LastLaunchTime............. {SystemInformation.Instance.LastLaunchTime}             ");
            Debug.WriteLine($"[INFO] LaunchCount................ {SystemInformation.Instance.LaunchCount}                ");
            Debug.WriteLine($"[INFO] OperatingSystem............ {SystemInformation.Instance.OperatingSystem}            ");
            Debug.WriteLine($"[INFO] OperatingSystemArchitecture {SystemInformation.Instance.OperatingSystemArchitecture}");
            Debug.WriteLine($"[INFO] OperatingSystemVersion..... {SystemInformation.Instance.OperatingSystemVersion}     ");
            Debug.WriteLine($"[INFO] TotalLaunchCount........... {SystemInformation.Instance.TotalLaunchCount}           ");
        }
        else
        {
            Logger?.WriteLine($"The app is running as Unpackaged.", LogLevel.Debug);
        }
    }

    void CreateGradientBackdrop(FrameworkElement fe)
    {
        // Get the FrameworkElement's compositor.
        var compositor = ElementCompositionPreview.GetElementVisual(fe).Compositor;
        if (compositor == null) { return; }
        var gb = compositor.CreateLinearGradientBrush();

        // Define gradient stops.
        var gradientStops = gb.ColorStops;

        if (App.Current.Resources.TryGetValue("GC1", out object clr))
        {
            var clr1 = (Windows.UI.Color)App.Current.Resources["GC1"];
            var clr2 = (Windows.UI.Color)App.Current.Resources["GC2"];
            var clr3 = (Windows.UI.Color)App.Current.Resources["GC3"];
            var clr4 = (Windows.UI.Color)App.Current.Resources["GC4"];
            gradientStops.Insert(0, compositor.CreateColorGradientStop(0.0f, clr1));
            gradientStops.Insert(1, compositor.CreateColorGradientStop(0.3f, clr2));
            gradientStops.Insert(2, compositor.CreateColorGradientStop(0.6f, clr3));
            gradientStops.Insert(3, compositor.CreateColorGradientStop(1.0f, clr4));
        }
        else
        {
            gradientStops.Insert(0, compositor.CreateColorGradientStop(0.0f, Windows.UI.Color.FromArgb(55, 255, 0, 0)));   // Red
            gradientStops.Insert(1, compositor.CreateColorGradientStop(0.3f, Windows.UI.Color.FromArgb(55, 255, 216, 0))); // Yellow
            gradientStops.Insert(2, compositor.CreateColorGradientStop(0.6f, Windows.UI.Color.FromArgb(55, 0, 255, 0)));   // Green
            gradientStops.Insert(3, compositor.CreateColorGradientStop(1.0f, Windows.UI.Color.FromArgb(55, 0, 0, 255)));   // Blue
        }

        // Set the direction of the gradient.
        gb.StartPoint = new System.Numerics.Vector2(0, 0);
        gb.EndPoint = new System.Numerics.Vector2(1, 1);

        // Create a sprite visual and assign the gradient brush.
        var spriteVisual = Compositor.CreateSpriteVisual();
        spriteVisual.Brush = gb;

        // Set the size of the sprite visual to cover the entire window.
        spriteVisual.Size = new System.Numerics.Vector2((float)fe.ActualSize.X, (float)fe.ActualSize.Y);

        // Handle the SizeChanged event to adjust the size of the sprite visual when the window is resized.
        fe.SizeChanged += (s, e) =>
        {
            spriteVisual.Size = new System.Numerics.Vector2((float)fe.ActualWidth, (float)fe.ActualHeight);
        };

        // Set the sprite visual as the background of the FrameworkElement.
        ElementCompositionPreview.SetElementChildVisual(fe, spriteVisual);
    }

    /// <summary>
    /// I don't believe this is compatible with a LAYERED window.
    /// </summary>
    void ApplyRoundedCorners(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return;

        int radius = 20;
        //IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        // Create a rounded rectangle region
        IntPtr hrgn = CreateRoundRectRgn(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN), radius, radius);
        SetWindowRgn(hWnd, hrgn, true);
    }

    void SetTopControlRow()
    {
        rootGrid.RowDefinitions.Clear();
        rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40, GridUnitType.Pixel) });
        rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(ctrlGrid, 0);
        Grid.SetRow(cpuGauge, 0);
        Grid.SetRowSpan(cpuGauge, 2);
    }

    void SetBottomControlRow()
    {
        rootGrid.RowDefinitions.Clear();
        rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40, GridUnitType.Pixel) });
        Grid.SetRow(ctrlGrid, 1);
        Grid.SetRow(cpuGauge, 0);
        Grid.SetRowSpan(cpuGauge, 2);
    }

    void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            if (ViewModel!.Config!.logging)
                Logger?.WriteLine($"The main window was {args.WindowActivationState}.", LogLevel.Debug);

            SetIsAlwaysOnTop(this, true);

            if (ViewModel.Config.ctrlRowBottom)
                SetBottomControlRow();
            else
                SetTopControlRow();
        }
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
}
