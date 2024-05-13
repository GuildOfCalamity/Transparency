using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.Extensions.DependencyInjection;

using Transparency.ViewModels;
using Transparency.Services;
using Transparency.Support;

using WinRT; // required to support Window.As<ICompositionSupportsSystemBackdrop>()

namespace Transparency;

/// <summary>
/// Settings Window
/// </summary>
/// <remarks>
/// Because a <see cref="Microsoft.UI.Xaml.Window"/> does not inherit <see cref="Microsoft.UI.Xaml.FrameworkElement"/>,
/// you cannot use <see cref="Microsoft.UI.Xaml.Data.IValueConverter"/>s directly from the XAML. One workaround is to 
/// use the <see cref="Microsoft.UI.Xaml.Window"/> to load a <see cref="Microsoft.UI.Xaml.Controls.Page"/> and then 
/// wire-up the converters inside the <see cref="Microsoft.UI.Xaml.Controls.Page"/>'s XAML.
/// </remarks>
public sealed partial class ConfigWindow : Window
{
    SystemBackdropConfiguration? _configurationSource;
    DesktopAcrylicController? _acrylicController;
    Microsoft.UI.Windowing.AppWindow? appWindow;
    MainViewModel? ViewModel = App.Current.Services.GetService<MainViewModel>();
    FileLogger? Logger = (FileLogger?)App.Current.Services.GetService<ILogger>();

    public ConfigWindow()
    {
        Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name} [{DateTime.Now.ToString("hh:mm:ss.fff tt")}]");

        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.Title = "Settings";
        SetTitleBar(CustomTitleBar);
        this.Activated += ConfigWindow_Activated;
        this.Closed += ConfigWindow_Closed;
        #region [Resize, Center and Icon]
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this); // Retrieve the window handle (HWND) of the current (XAML) WinUI3 window.
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd); // Retrieve the WindowId that corresponds to hWnd.
        appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId); // Lastly, retrieve the AppWindow for the current (XAML) WinUI3 window.
        if (appWindow is not null)
        {
            if (App.IsPackaged)
                appWindow?.SetIcon(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, $"Assets/WinTransparent.ico"));
            else
                appWindow?.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, $"Assets/WinTransparent.ico"));
        }
        #endregion

        // https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/system-backdrop-controller
        if (DesktopAcrylicController.IsSupported())
        {
            // Hook up the policy object.
            _configurationSource = new SystemBackdropConfiguration();
            // Create the desktop controller.
            _acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();
            _acrylicController.TintOpacity = 0.4f; // Lower value may be too translucent vs light background.
            _acrylicController.LuminosityOpacity = 0.1f;
            _acrylicController.TintColor = Microsoft.UI.Colors.Gray;
            // Fall-back color is only used when the window state becomes deactivated.
            _acrylicController.FallbackColor = Microsoft.UI.Colors.Transparent;
            // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
            _acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
        }
    }

    void ConfigWindow_Closed(object sender, WindowEventArgs args)
    {
        // Make sure the Acrylic controller is disposed
        // so it doesn't try to access a closed window.
        if (_acrylicController is not null)
        {
            _acrylicController.Dispose();
            _acrylicController = null;
        }
    }
    void ConfigWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {

            if (ViewModel!.Config!.logging)
                Logger?.WriteLine($"The config window was {args.WindowActivationState}.", LogLevel.Debug);

            ShowMessage("You can adjust app settings here.", InfoBarSeverity.Informational);

            appWindow?.Resize(new Windows.Graphics.SizeInt32(350, 710));

            App.CenterWindow(this);
        }
        else
        {
            EndStoryboard();
        }
    }

    public void BeginStoryboard()
    {
        if (App.AnimationsEffectsEnabled)
            OpacityStoryboard.Begin();
    }

    public void EndStoryboard()
    {
        if (App.AnimationsEffectsEnabled)
            OpacityStoryboard.SkipToFill(); //OpacityStoryboard.Stop();
    }

    /// <summary>
    /// Thread-safe helper for <see cref="Microsoft.UI.Xaml.Controls.InfoBar"/>.
    /// </summary>
    /// <param name="message">text to show</param>
    /// <param name="severity"><see cref="Microsoft.UI.Xaml.Controls.InfoBarSeverity"/></param>
    public void ShowMessage(string message, InfoBarSeverity severity)
    {
        infoBar.DispatcherQueue?.TryEnqueue(() =>
        {
            infoBar.IsOpen = true;
            infoBar.Severity = severity;
            infoBar.Message = $"{message}";
        });
    }
}
