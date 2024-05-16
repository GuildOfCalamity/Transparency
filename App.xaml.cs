using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;

using Windows.UI.Popups;
using Windows.UI.ViewManagement;

using Transparency.Support;
using Transparency.Helpers;
using Transparency.ViewModels;
using Transparency.Services;

namespace Transparency;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    #region [Props]
    int m_width = 300;
    int m_height = 300;
    Window? m_window;
    static UISettings m_UISettings = new UISettings();

    public new static App Current => (App)Application.Current; // Gets the current app instance (for Dependency Injection).
    public IServiceProvider Services { get; }
    public static IntPtr WindowHandle { get; set; }
    public static FrameworkElement? MainRoot { get; set; }
    public static bool IsClosing { get; set; } = false;

    // https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/#advantages-and-disadvantages-of-packaging-your-app
#if IS_UNPACKAGED // We're using a custom PropertyGroup Condition we defined in the csproj to help us with the decision.
    public static bool IsPackaged { get => false; }
#else
    public static bool IsPackaged { get => true; }
#endif
    // We won't configure backing fields for these as the user could adjust them during app lifetime.
    public static bool TransparencyEffectsEnabled { get => m_UISettings.AdvancedEffectsEnabled; }
    public static bool AnimationsEffectsEnabled { get => m_UISettings.AnimationsEnabled; }
    public static double TextScaleFactor { get => m_UISettings.TextScaleFactor; }
    public static ElementTheme ThemeRequested
    {
        get
        {
            try
            {
                if (App.IsPackaged)
                    return (ElementTheme)Enum.Parse(typeof(ElementTheme), Application.Current.RequestedTheme.ToString());
                else
                    return App.MainRoot?.ActualTheme ?? ElementTheme.Default;
            }
            catch (Exception)
            {
                return ElementTheme.Default;
            }
        }
    }

    Windows.Globalization.DateTimeFormatting.DateTimeFormatter? _formatter;
    /// <summary>
    /// https://learn.microsoft.com/en-us/uwp/api/windows.globalization.datetimeformatting.datetimeformatter?view=winrt-22621#remarks
    /// </summary>
    public Windows.Globalization.DateTimeFormatting.DateTimeFormatter? Formatter
    {
        get 
        { 
            if (_formatter == null)
                _formatter = new Windows.Globalization.DateTimeFormatting.DateTimeFormatter("longdate longtime");
            
            return _formatter; 
        }
    }


    #endregion

    #region [Config]
    static bool _lastSave = false;
    static DateTime _lastMove = DateTime.Now;
    static Config? _localConfig;
    public static Config? LocalConfig
    {
        get => _localConfig;
        set => _localConfig = value;
    }

    public static Func<Config?, bool> SaveConfigFunc = (cfg) =>
    {
        if (cfg is not null)
        {
            cfg.firstRun = false;
            cfg.time = DateTime.Now;
            cfg.version = $"{GetCurrentAssemblyVersion()}";
            if (App.MainRoot is not null) { cfg.theme = $"{ThemeRequested}"; }
            if (string.IsNullOrEmpty(cfg.background)) { cfg.background = "001f1f1f"; }
            if (cfg.opacity == 0.0) { cfg.opacity = 0.5; }
            Process proc = Process.GetCurrentProcess();
            cfg.metrics = $"Process used {proc.PrivateMemorySize64/1024/1024}MB of memory and {proc.TotalProcessorTime.ToReadableString()} TotalProcessorTime on {Environment.ProcessorCount} possible cores.";
            try
            {
                _ = ConfigHelper.SaveConfig(cfg);
                return true;
            }
            catch (Exception) { return false; }
        }
        return false;
    };
    #endregion

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name} [{DateTime.Now.ToString("hh:mm:ss.fff tt")}]");
        App.Current.DebugSettings.FailFastOnErrors = false;

        #region [Exception handlers]
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomainFirstChanceException;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        UnhandledException += ApplicationUnhandledException;
        #endregion

        Services = ConfigureServices();

        this.InitializeComponent();

        Debug.WriteLine($"[INFO] {GetCurrentFullName()} ⇒ {Formatter?.Format(DateTimeOffset.Now)}");
        
        GetLanguageRecommendedFonts();
        TestCurrencyFormatter();
        TestPercentFormatter();
    }

    static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILogger, FileLogger>();
        services.AddTransient<MainViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        #region [Config]
        if (ConfigHelper.DoesConfigExist())
        {
            try
            {
                LocalConfig = ConfigHelper.LoadConfig();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] {nameof(ConfigHelper.LoadConfig)}: {ex.Message}");
            }
        }
        else
        {
            try
            {
                LocalConfig = new Config
                {
                    firstRun = true,
                    theme = $"{App.ThemeRequested}",
                    version = $"{App.GetCurrentAssemblyVersion()}",
                    time = DateTime.Now,
                    useHistogram = false,
                    metrics = "N/A",
                    borderSize = 3,
                    opacity = 0.6,
                    msRefresh = 2000,
                    windowW = m_width,
                    windowH = m_height,
                    background = "001F1FFF"
                };
                ConfigHelper.SaveConfig(LocalConfig);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] {nameof(ConfigHelper.SaveConfig)}: {ex.Message}");
            }
        }
        #endregion

        if (LocalConfig is not null && LocalConfig.useHistogram)
            m_window = new HistoWindow();
        else
            m_window = new MainWindow();

        var appWin = GetAppWindow(m_window);
        if (appWin != null)
        {
            // Gets or sets a value that indicates whether this window will appear in various system representations, such as ALT+TAB and taskbar.
            appWin.IsShownInSwitchers = false;

            // We don't have the Closing event exposed by default, so we'll use the AppWindow to compensate.
            appWin.Closing += (s, e) =>
            {
                App.IsClosing = true;
                Debug.WriteLine($"[INFO] Application closing detected at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
                _lastSave = SaveConfigFunc(LocalConfig);
            };

            // Destroying is always called, but Closing is only called when the application is shutdown normally.
            appWin.Destroying += (s, e) =>
            {
                Debug.WriteLine($"[INFO] Application destroying detected at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
                if (!_lastSave) // prevent redundant calls
                    SaveConfigFunc(LocalConfig);
            };

            // The changed event holds a bunch of juicy info that we can extrapolate.
            appWin.Changed += (s, args) =>
            {
                if (args.DidPositionChange)
                {
                    // Add debounce in scenarios where this event could be hammered.
                    var idleTime = DateTime.Now - _lastMove;
                    if (idleTime.TotalSeconds > 1.01d && LocalConfig != null)
                    {
                        _lastMove = DateTime.Now;
                        if (s.Position.X > 0 && s.Position.Y > 0)
                        {
                            // This property is initially null. Once a window has been shown it always has a
                            // presenter applied, either one applied by the platform or applied by the app itself.
                            if (s.Presenter is not null && s.Presenter is OverlappedPresenter op)
                            {
                                if (op.State == OverlappedPresenterState.Minimized)
                                {
                                    appWin.IsShownInSwitchers = true;
                                }
                                else if (op.State != OverlappedPresenterState.Maximized)
                                {
                                    appWin.IsShownInSwitchers = false;
                                    Debug.WriteLine($"[INFO] Updating window position to {s.Position.X},{s.Position.Y} and size to {s.Size.Width},{s.Size.Height}");
                                    LocalConfig.windowX = s.Position.X;
                                    LocalConfig.windowY = s.Position.Y;
                                    LocalConfig.windowH = s.Size.Height;
                                    LocalConfig.windowW = s.Size.Width;
                                }
                                else
                                {
                                    appWin.IsShownInSwitchers = false;
                                    Debug.WriteLine($"[INFO] Ignoring position saving (window maximized)");
                                }
                            }
                        }
                    }
                }
            };

            if (IsPackaged)
                appWin.SetIcon(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, $"Assets/WinTransparent.ico"));
            else
                appWin.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, $"Assets/WinTransparent.ico"));

            appWin.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
            
            // Going forward we'll handle from config load.
            //appWin?.Resize(new Windows.Graphics.SizeInt32(m_width, m_height));
            //CenterWindow(m_window);
        }

        m_window.Activate();

        // Save the FrameworkElement for any future content dialogs.
        MainRoot = m_window.Content as FrameworkElement;

        #region [Window placement]
        if (LocalConfig == null || LocalConfig.firstRun)
        {
            Debug.WriteLine($"[INFO] Moving window to center screen");
            appWin?.Resize(new Windows.Graphics.SizeInt32(m_width, m_height));
            CenterWindow(m_window);
        }
        else
        {
            Debug.WriteLine($"[INFO] Moving window to previous position {LocalConfig.windowX},{LocalConfig.windowY} with size {LocalConfig.windowW},{LocalConfig.windowH}");
            //appWin?.Move(new Windows.Graphics.PointInt32(LocalConfig.windowX, LocalConfig.windowY));
            if (LocalConfig.useHistogram)
                appWin?.MoveAndResize(new Windows.Graphics.RectInt32(LocalConfig.windowX, LocalConfig.windowY, LocalConfig.windowW >= 160 ? LocalConfig.windowW : 160, LocalConfig.windowH >= 220 ? LocalConfig.windowH : 220 ), Microsoft.UI.Windowing.DisplayArea.Primary);
            else
                appWin?.MoveAndResize(new Windows.Graphics.RectInt32(LocalConfig.windowX, LocalConfig.windowY, LocalConfig.windowW, LocalConfig.windowH), Microsoft.UI.Windowing.DisplayArea.Primary);
        }
        #endregion
    }

    #region [Font Recommendations]
    /* [FontWeight Explained] https://learn.microsoft.com/en-us/uwp/api/windows.ui.text.fontweights?view=winrt-22621#remarks
        ExtraBlack... 950
        Black........ 900
        ExtraBold.... 800
        Bold......... 700
        SemiBold..... 600
        Medium....... 500
        Normal....... 400
        SimiLight.... 350
        Light........ 300
        ExtraLight... 200
        Thin......... 100
    */
    Dictionary<ushort, string> _fontWeights = new()
    { 
        { 100, "Thin"       },
        { 200, "ExtraLight" },
        { 300, "Light"      },
        { 350, "SemiLight"  },
        { 400, "Normal"     },
        { 500, "Medium"     },
        { 600, "SemiBold"   },
        { 700, "Bold"       },
        { 800, "ExtraBold"  },
        { 900, "Black"      },
        { 950, "ExtraBlack" },
    };

    /// <summary>
    /// Get the recommended fonts based on the language.
    /// </summary>
    /// <remarks>
    /// https://learn.microsoft.com/en-us/uwp/api/windows.globalization.fonts.languagefont?view=winrt-22621
    /// </remarks>
    public void GetLanguageRecommendedFonts(string langTag = "en-US")
    {
        if (string.IsNullOrEmpty(langTag))
            return;

        var fonts = new Windows.Globalization.Fonts.LanguageFontGroup(langTag);

        // The FontWeight is not the same as the FontStyle. FontStyle can be Normal, Oblique or Italic.
        // Oblique & Italic are not the same ⇒ https://en.wikipedia.org/wiki/Oblique_type

        var fixedWidthFont = fonts.FixedWidthTextFont;
        Debug.WriteLine($"[INFO] Fixed width font family ⇒ {fixedWidthFont.FontFamily}");
        Debug.WriteLine($"[INFO] Fixed width font weight ⇒ {_fontWeights[fixedWidthFont.FontWeight.Weight]}");
        var fixedWidthFontScale = fixedWidthFont.ScaleFactor == 0 ? 100 : fixedWidthFont.ScaleFactor;

        var uiTextFont = fonts.UITextFont;
        Debug.WriteLine($"[INFO] UI text font family ⇒ {uiTextFont.FontFamily}");
        Debug.WriteLine($"[INFO] UI text font weight ⇒ {_fontWeights[uiTextFont.FontWeight.Weight]}");
        var uiTextFontFontScale = uiTextFont.ScaleFactor == 0 ? 100 : uiTextFont.ScaleFactor;

        var traditionalDocumentFont = fonts.TraditionalDocumentFont;
        Debug.WriteLine($"[INFO] Traditional document font family ⇒ {traditionalDocumentFont.FontFamily}");
        Debug.WriteLine($"[INFO] Traditional document font weight ⇒ {_fontWeights[traditionalDocumentFont.FontWeight.Weight]}");
        var traditionalDocumentFontScale = traditionalDocumentFont.ScaleFactor == 0 ? 100 : traditionalDocumentFont.ScaleFactor;

        var modernDocumentFont = fonts.ModernDocumentFont;
        Debug.WriteLine($"[INFO] Modern document font family ⇒ {modernDocumentFont.FontFamily}");
        Debug.WriteLine($"[INFO] Modern document font weight ⇒ {_fontWeights[modernDocumentFont.FontWeight.Weight]}");
        var modernDocumentFontScale = modernDocumentFont.ScaleFactor == 0 ? 100 : modernDocumentFont.ScaleFactor;
    }
    #endregion

    #region [Globalization Testing]
    /// <summary>
    /// Some portions of this method are from https://github.com/microsoft/Windows-universal-samples/blob/main/Samples/NumberFormatting/cs/Scenario3_CurrencyFormatting.xaml.cs
    /// </summary>
    /// <remarks>
    /// Other formatting examples https://github.com/microsoft/Windows-universal-samples/tree/main/Samples/NumberFormatting/cs
    /// </remarks>
    public void TestCurrencyFormatter()
    {
        double amount = 56789.01;
        string currency = Windows.System.UserProfile.GlobalizationPreferences.Currencies[0]; // the current user's default currency
        StringBuilder results = new StringBuilder();

        Windows.Globalization.NumberFormatting.CurrencyFormatter defaultFormatter = new Windows.Globalization.NumberFormatting.CurrencyFormatter(currency);
        Windows.Globalization.NumberFormatting.CurrencyFormatter usdFormatter = new Windows.Globalization.NumberFormatting.CurrencyFormatter(Windows.Globalization.CurrencyIdentifiers.USD);
        Windows.Globalization.NumberFormatting.CurrencyFormatter eurFormatter = new Windows.Globalization.NumberFormatting.CurrencyFormatter(Windows.Globalization.CurrencyIdentifiers.EUR);

        results.AppendLine("Original value: " + amount);
        results.AppendLine("With user's default currency: " + defaultFormatter.Format(amount));
        results.AppendLine("Formatted US Dollar: " + usdFormatter.Format(amount));

        usdFormatter.FractionDigits = 2;
        results.AppendLine("Formatted US Dollar (with two fractional digits): " + usdFormatter.Format(amount));

        usdFormatter.IsGrouped = true;
        results.AppendLine("Formatted US Dollar (with grouping separators): " + usdFormatter.Format(amount));

        usdFormatter.FractionDigits = 2;
        results.AppendLine("Formatted Euro (with two fractional digits): " + eurFormatter.Format(amount));

        eurFormatter.IsGrouped = true;
        results.AppendLine("Formatted Euro (with grouping separators): " + eurFormatter.Format(amount));

        Debug.WriteLine($"[INFO] Currency Formatting Test ⇒");
        Debug.WriteLine($"{results}");
    }

    public void TestPercentFormatter()
    {
        float amount = 5f;

        Windows.Globalization.NumberFormatting.PercentFormatter formatter = new();
        formatter.SignificantDigits = 1;
        formatter.FractionDigits = 1;
        formatter.IntegerDigits = 1;
        formatter.IsDecimalPointAlwaysDisplayed = false;
        formatter.IsGrouped = false;
        var result = formatter.Format(amount/100);
        Debug.WriteLine($"[INFO] Windows.Globalization ⇒ {result}");

        System.Globalization.NumberFormatInfo percentageFormat = new() { PercentPositivePattern = 1, PercentNegativePattern = 1 };
        string val = (amount/100).ToString("P0", percentageFormat);
        Debug.WriteLine($"[INFO] System.Globalization ⇒ {val}");

        System.Globalization.NumberFormatInfo nfi1 = System.Globalization.NumberFormatInfo.CurrentInfo;
        System.Globalization.NumberFormatInfo nfi2 = System.Globalization.CultureInfo.CurrentCulture.NumberFormat;
        PropertyInfo[] props = nfi1.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Debug.WriteLine("[INFO] Properties of NumberFormat.CurrentInfo: ");
        foreach (var prop in props)
        {
            if (prop.PropertyType.IsArray)
            {
                Array? arr = prop.GetValue(nfi1) as Array;
                Debug.Write(string.Format("   {0}: ", prop.Name) + "{ ");
                if (arr is null) { continue; }
                int ctr = 0;
                foreach (var item in arr)
                {
                    Debug.Write(string.Format("{0}{1}", item, ctr == arr.Length - 1 ? " }" : ", "));
                    ctr++;
                }
                Debug.WriteLine("");
            }
            else
            {
                Debug.WriteLine(string.Format("   {0}: {1}", prop.Name, prop.GetValue(nfi1)));
            }
        }
    }

    /// <summary>
    /// This enumeration supports a bitwise combination of its member values.
    /// https://learn.microsoft.com/en-us/dotnet/api/system.globalization.numberstyles?view=net-8.0#fields
    /// </summary>
    public void TestNumberStyles()
    {
        // Parse the string as a hex value and display the value as a decimal.
        var num = "A";
        int val = int.Parse(num, System.Globalization.NumberStyles.HexNumber);
        Debug.WriteLine(string.Format("{0} in hex = {1} in decimal.", num, val));

        // Parse the string, allowing a leading sign, and ignoring leading and trailing white spaces.
        num = "    -45   ";
        val = int.Parse(num, System.Globalization.NumberStyles.AllowLeadingSign |
                             System.Globalization.NumberStyles.AllowLeadingWhite | 
                             System.Globalization.NumberStyles.AllowTrailingWhite);
        Debug.WriteLine(string.Format("'{0}' parsed to an int is '{1}'.", num, val));

        // Parse the string, allowing parentheses, and ignoring leading and trailing white spaces.
        num = "    (37)   ";
        val = int.Parse(num, System.Globalization.NumberStyles.AllowParentheses | 
                             System.Globalization.NumberStyles.AllowLeadingSign | 
                             System.Globalization.NumberStyles.AllowLeadingWhite | 
                             System.Globalization.NumberStyles.AllowTrailingWhite);
        Debug.WriteLine(string.Format("'{0}' parsed to an int is '{1}'.", num, val));
    }
    #endregion

    #region [Window Helpers]
    /// <summary>
    /// This code example demonstrates how to retrieve an AppWindow from a WinUI3 window.
    /// The AppWindow class is available for any top-level HWND in your app.
    /// AppWindow is available only to desktop apps (both packaged and unpackaged), it's not available to UWP apps.
    /// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/windowing/windowing-overview
    /// https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.create?view=windows-app-sdk-1.3
    /// </summary>
    public Microsoft.UI.Windowing.AppWindow? GetAppWindow(object window)
    {
        // Retrieve the window handle (HWND) of the current (XAML) WinUI3 window.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        // For other callers, e.g. P/Invoke.
        App.WindowHandle = hWnd;

        // Retrieve the WindowId that corresponds to hWnd.
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);

        // Lastly, retrieve the AppWindow for the current (XAML) WinUI3 window.
        Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        return appWindow;
    }

    /// <summary>
    /// Centers a <see cref="Microsoft.UI.Xaml.Window"/> based on the <see cref="Microsoft.UI.Windowing.DisplayArea"/>.
    /// </summary>
    /// <remarks>This must be run on the UI thread.</remarks>
    public static void CenterWindow(Window window)
    {
        try
        {
            System.IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            if (Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId) is Microsoft.UI.Windowing.AppWindow appWindow &&
                Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest) is Microsoft.UI.Windowing.DisplayArea displayArea)
            {
                Windows.Graphics.PointInt32 CenteredPosition = appWindow.Position;
                CenteredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                CenteredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(CenteredPosition);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] {MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// The <see cref="Microsoft.UI.Windowing.DisplayArea"/> exposes properties such as:
    /// OuterBounds     (Rect32)
    /// WorkArea.Width  (int)
    /// WorkArea.Height (int)
    /// IsPrimary       (bool)
    /// DisplayId.Value (ulong)
    /// </summary>
    /// <param name="window"></param>
    /// <returns><see cref="DisplayArea"/></returns>
    public Microsoft.UI.Windowing.DisplayArea? GetDisplayArea(Window window)
    {
        try
        {
            System.IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var da = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            return da;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] {MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region [Domain Events]
    void ApplicationUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
        Exception? ex = e.Exception;
        Debug.WriteLine($"[UnhandledException]: {ex?.Message}");
        Debug.WriteLine($"Unhandled exception of type {ex?.GetType()}: {ex}");
        DebugLog($"Unhandled Exception StackTrace: {Environment.StackTrace}");
        DebugLog($"{ex?.DumpFrames()}");
        e.Handled = true;
    }

    void CurrentDomainOnProcessExit(object? sender, EventArgs e)
    {
        if (!IsClosing)
            IsClosing = true;

        if (sender is null)
            return;

        if (sender is AppDomain ad)
        {
            Debug.WriteLine($"[OnProcessExit]", $"{nameof(App)}");
            Debug.WriteLine($"DomainID: {ad.Id}", $"{nameof(App)}");
            Debug.WriteLine($"FriendlyName: {ad.FriendlyName}", $"{nameof(App)}");
            Debug.WriteLine($"BaseDirectory: {ad.BaseDirectory}", $"{nameof(App)}");
        }
    }

    void CurrentDomainFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        Debug.WriteLine($"[ERROR] First chance exception from {sender?.GetType()}: {e.Exception.Message}");
        DebugLog($"First chance exception from {sender?.GetType()}: {e.Exception.Message}");
        if (e.Exception.InnerException != null)
            DebugLog($"  ⇨ InnerException: {e.Exception.InnerException.Message}");
        DebugLog($"First chance exception StackTrace: {Environment.StackTrace}");
        DebugLog($"{e.Exception.DumpFrames()}");
    }

    void CurrentDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
    {
        Exception? ex = e.ExceptionObject as Exception;
        Debug.WriteLine($"[ERROR] Thread exception of type {ex?.GetType()}: {ex}");
        DebugLog($"Thread exception of type {ex?.GetType()}: {ex}");
        DebugLog($"{ex?.DumpFrames()}");
    }

    void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception is AggregateException aex)
        {
            aex?.Flatten().Handle(ex =>
            {
                Debug.WriteLine($"[ERROR] Unobserved task exception: {ex?.Message}");
                DebugLog($"Unobserved task exception: {ex?.Message}");
                DebugLog($"{ex?.DumpFrames()}");
                return true;
            });
        }
        e.SetObserved(); // suppress and handle manually
    }
    #endregion

    #region [Reflection Helpers]
    /// <summary>
    /// Returns the declaring type's namespace.
    /// </summary>
    public static string? GetCurrentNamespace() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace;

    /// <summary>
    /// Returns the declaring type's full name.
    /// </summary>
    public static string? GetCurrentFullName() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Assembly.FullName;

    /// <summary>
    /// Returns the declaring type's assembly name.
    /// </summary>
    public static string? GetCurrentAssemblyName() => System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

    /// <summary>
    /// Returns the AssemblyVersion, not the FileVersion.
    /// </summary>
    public static Version GetCurrentAssemblyVersion() => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
    #endregion

    #region [Dialog Helpers]
    static SemaphoreSlim semaSlim = new SemaphoreSlim(1, 1);
    /// <summary>
    /// The <see cref="Windows.UI.Popups.MessageDialog"/> does not look as nice as the
    /// <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> and is not part of the native Microsoft.UI.Xaml.Controls.
    /// The <see cref="Windows.UI.Popups.MessageDialog"/> offers the <see cref="Windows.UI.Popups.UICommandInvokedHandler"/> 
    /// callback, but this could be replaced with actions. Both can be shown asynchronously.
    /// </summary>
    /// <remarks>
    /// You'll need to call <see cref="WinRT.Interop.InitializeWithWindow.Initialize"/> when using the <see cref="Windows.UI.Popups.MessageDialog"/>,
    /// because the <see cref="Microsoft.UI.Xaml.XamlRoot"/> does not exist and an owner must be defined.
    /// </remarks>
    public static async Task ShowMessageBox(string title, string message, string yesText, string noText, Action? yesAction, Action? noAction)
    {
        if (App.WindowHandle == IntPtr.Zero) { return; }

        // Create the dialog.
        var messageDialog = new MessageDialog($"{message}");
        messageDialog.Title = title;

        if (!string.IsNullOrEmpty(yesText))
        {
            messageDialog.Commands.Add(new UICommand($"{yesText}", (opt) => { yesAction?.Invoke(); }));
            messageDialog.DefaultCommandIndex = 0;
        }

        if (!string.IsNullOrEmpty(noText))
        {
            messageDialog.Commands.Add(new UICommand($"{noText}", (opt) => { noAction?.Invoke(); }));
            messageDialog.DefaultCommandIndex = 1;
        }

        // We must initialize the dialog with an owner.
        WinRT.Interop.InitializeWithWindow.Initialize(messageDialog, App.WindowHandle);
        // Show the message dialog. Our DialogDismissedHandler will deal with what selection the user wants.
        await messageDialog.ShowAsync();
        // We could force the result in a separate timer...
        //DialogDismissedHandler(new UICommand("time-out"));
    }

    /// <summary>
    /// The <see cref="Windows.UI.Popups.MessageDialog"/> does not look as nice as the
    /// <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> and is not part of the native Microsoft.UI.Xaml.Controls.
    /// The <see cref="Windows.UI.Popups.MessageDialog"/> offers the <see cref="Windows.UI.Popups.UICommandInvokedHandler"/> 
    /// callback, but this could be replaced with actions. Both can be shown asynchronously.
    /// </summary>
    /// <remarks>
    /// You'll need to call <see cref="WinRT.Interop.InitializeWithWindow.Initialize"/> when using the <see cref="Windows.UI.Popups.MessageDialog"/>,
    /// because the <see cref="Microsoft.UI.Xaml.XamlRoot"/> does not exist and an owner must be defined.
    /// </remarks>
    public static async Task ShowMessageBox(string title, string message, string primaryText, string cancelText)
    {
        // Create the dialog.
        var messageDialog = new MessageDialog($"{message}");
        messageDialog.Title = title;

        if (!string.IsNullOrEmpty(primaryText))
        {
            messageDialog.Commands.Add(new UICommand($"{primaryText}", new UICommandInvokedHandler(DialogDismissedHandler)));
            messageDialog.DefaultCommandIndex = 0;
        }

        if (!string.IsNullOrEmpty(cancelText))
        {
            messageDialog.Commands.Add(new UICommand($"{cancelText}", new UICommandInvokedHandler(DialogDismissedHandler)));
            messageDialog.DefaultCommandIndex = 1;
        }
        // We must initialize the dialog with an owner.
        WinRT.Interop.InitializeWithWindow.Initialize(messageDialog, App.WindowHandle);
        // Show the message dialog. Our DialogDismissedHandler will deal with what selection the user wants.
        await messageDialog.ShowAsync();

        // We could force the result in a separate timer...
        //DialogDismissedHandler(new UICommand("time-out"));
    }

    /// <summary>
    /// Callback for the selected option from the user.
    /// </summary>
    static void DialogDismissedHandler(IUICommand command) => Debug.WriteLine($"[INFO] UICommand.Label ⇨ {command.Label}");

    /// <summary>
    /// The <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> looks much better than the
    /// <see cref="Windows.UI.Popups.MessageDialog"/> and is part of the native Microsoft.UI.Xaml.Controls.
    /// The <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> does not offer a <see cref="Windows.UI.Popups.UICommandInvokedHandler"/>
    /// callback, but in this example was replaced with actions. Both can be shown asynchronously.
    /// </summary>
    /// <remarks>
    /// There is no need to call <see cref="WinRT.Interop.InitializeWithWindow.Initialize"/> when using the <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/>,
    /// but a <see cref="Microsoft.UI.Xaml.XamlRoot"/> must be defined since it inherits from <see cref="Microsoft.UI.Xaml.Controls.Control"/>.
    /// The <see cref="SemaphoreSlim"/> was added to prevent "COMException: Only one ContentDialog can be opened at a time."
    /// </remarks>
    public static async Task ShowDialogBox(string title, string message, string primaryText, string cancelText, Action? onPrimary, Action? onCancel, Uri? imageUri)
    {
        if (App.MainRoot?.XamlRoot == null) { return; }

        await semaSlim.WaitAsync();

        #region [Initialize Assets]
        double fontSize = 16;
        Microsoft.UI.Xaml.Media.FontFamily fontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas");

        if (App.Current.Resources["FontSizeMedium"] is not null)
            fontSize = (double)App.Current.Resources["FontSizeMedium"];

        if (App.Current.Resources["PrimaryFont"] is not null)
            fontFamily = (Microsoft.UI.Xaml.Media.FontFamily)App.Current.Resources["PrimaryFont"];

        StackPanel panel = new StackPanel()
        {
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical,
            Spacing = 10d
        };

        if (imageUri is not null)
        {
            panel.Children.Add(new Image
            {
                Margin = new Thickness(1, -45, 1, 1), // Move the image into the title area.
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                Width = 42,
                Height = 42,
                Source = new BitmapImage(imageUri)
            });
        }

        panel.Children.Add(new TextBlock()
        {
            Text = message,
            FontSize = fontSize,
            FontFamily = fontFamily,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        });

        var tb = new TextBox()
        {
            Text = message,
            FontSize = fontSize,
            FontFamily = fontFamily,
            TextWrapping = TextWrapping.Wrap
        };
        tb.Loaded += (s, e) => { tb.SelectAll(); };
        #endregion

        // NOTE: Content dialogs will automatically darken the background.
        ContentDialog contentDialog = new ContentDialog()
        {
            Title = title,
            PrimaryButtonText = primaryText,
            CloseButtonText = cancelText,
            Content = panel,
            XamlRoot = App.MainRoot?.XamlRoot,
            RequestedTheme = App.MainRoot?.ActualTheme ?? ElementTheme.Default
        };

        try
        {
            ContentDialogResult result = await contentDialog.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    onPrimary?.Invoke();
                    break;
                //case ContentDialogResult.Secondary:
                //    onSecondary?.Invoke();
                //    break;
                case ContentDialogResult.None: // Cancel
                    onCancel?.Invoke();
                    break;
                default:
                    Debug.WriteLine($"Dialog result not defined.");
                    break;
            }
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Debug.WriteLine($"[ERROR] ShowDialogBox: {ex.Message}");
        }
        finally
        {
            semaSlim.Release();
        }
    }

    /// <summary>
    /// The <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> looks much better than the
    /// <see cref="Windows.UI.Popups.MessageDialog"/> and is part of the native Microsoft.UI.Xaml.Controls.
    /// The <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> does not offer a <see cref="Windows.UI.Popups.UICommandInvokedHandler"/>
    /// callback, but in this example was replaced with actions. Both can be shown asynchronously.
    /// </summary>
    /// <remarks>
    /// There is no need to call <see cref="WinRT.Interop.InitializeWithWindow.Initialize"/> when using the <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/>,
    /// but a <see cref="Microsoft.UI.Xaml.XamlRoot"/> must be defined since it inherits from <see cref="Microsoft.UI.Xaml.Controls.Control"/>.
    /// The <see cref="SemaphoreSlim"/> was added to prevent "COMException: Only one ContentDialog can be opened at a time."
    /// </remarks>
    public static async Task ShowDialogBox(Microsoft.UI.Xaml.XamlRoot root, string title, string message, string primaryText, string cancelText, Action? onPrimary, Action? onCancel, Uri? imageUri)
    {
        if (root == null) { return; }

        await semaSlim.WaitAsync();

        #region [Initialize Assets]
        double fontSize = 16;
        Microsoft.UI.Xaml.Media.FontFamily fontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas");

        if (App.Current.Resources["FontSizeMedium"] is not null)
            fontSize = (double)App.Current.Resources["FontSizeMedium"];

        if (App.Current.Resources["PrimaryFont"] is not null)
            fontFamily = (Microsoft.UI.Xaml.Media.FontFamily)App.Current.Resources["PrimaryFont"];

        StackPanel panel = new StackPanel()
        {
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical,
            Spacing = 10d
        };

        if (imageUri is not null)
        {
            panel.Children.Add(new Image
            {
                Margin = new Thickness(1, -45, 1, 1), // Move the image into the title area.
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                Width = 42,
                Height = 42,
                Source = new BitmapImage(imageUri)
            });
        }

        panel.Children.Add(new TextBlock()
        {
            Text = message,
            FontSize = fontSize,
            FontFamily = fontFamily,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        });

        var tb = new TextBox()
        {
            Text = message,
            FontSize = fontSize,
            FontFamily = fontFamily,
            TextWrapping = TextWrapping.Wrap
        };
        tb.Loaded += (s, e) => { tb.SelectAll(); };
        #endregion

        // NOTE: Content dialogs will automatically darken the background.
        ContentDialog contentDialog = new ContentDialog()
        {
            Title = title,
            PrimaryButtonText = primaryText,
            CloseButtonText = cancelText,
            Content = panel,
            XamlRoot = root,
            RequestedTheme = ElementTheme.Default
        };

        try
        {
            ContentDialogResult result = await contentDialog.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    onPrimary?.Invoke();
                    break;
                //case ContentDialogResult.Secondary:
                //    onSecondary?.Invoke();
                //    break;
                case ContentDialogResult.None: // Cancel
                    onCancel?.Invoke();
                    break;
                default:
                    Debug.WriteLine($"Dialog result not defined.");
                    break;
            }
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Debug.WriteLine($"[ERROR] ShowDialogBox: {ex.Message}");
        }
        finally
        {
            semaSlim.Release();
        }
    }
    #endregion

    /// <summary>
    /// Simplified debug logger for app-wide use.
    /// </summary>
    /// <param name="message">the text to append to the file</param>
    public static void DebugLog(string message)
    {
        try
        {
            if (App.IsPackaged)
                System.IO.File.AppendAllText(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Debug.log"), $"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt")}] {message}{Environment.NewLine}");
            else
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Debug.log"), $"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt")}] {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            Debug.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt")}] {message}");
        }
    }
}
