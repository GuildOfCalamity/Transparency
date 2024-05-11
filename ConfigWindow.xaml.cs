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
using Microsoft.Extensions.DependencyInjection;

using Transparency.ViewModels;
using Transparency.Services;
using Transparency.Support;

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
    Microsoft.UI.Windowing.AppWindow appWindow;
    MainViewModel? ViewModel = App.Current.Services.GetService<MainViewModel>();
    FileLogger? Logger = (FileLogger?)App.Current.Services.GetService<ILogger>();

    public ConfigWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.Title = "Settings";
        SetTitleBar(CustomTitleBar);
        this.Activated += ConfigWindow_Activated;
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

    void ConfigWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {

            if (ViewModel!.Config!.logging)
                Logger?.WriteLine($"The config window was {args.WindowActivationState}.", LogLevel.Debug);

            ShowMessage("You can adjust app settings here.", InfoBarSeverity.Informational);

            if (appWindow is not null)
                appWindow?.Resize(new Windows.Graphics.SizeInt32(350, 610));

            App.CenterWindow(this);
        }
        else
        {
            EndStoryboard();
        }
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
