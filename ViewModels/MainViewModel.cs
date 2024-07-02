using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Transparency.Support;
using Transparency.Helpers;
using Transparency.Services;
using Transparency.Controls;
using Transparency.Models;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using System.Runtime.CompilerServices;

namespace Transparency.ViewModels;

public class MainViewModel : ObservableRecipient
{
    #region [Props]
    static Uri _dialogImgUri = new Uri($"ms-appx:///Assets/Warning.png");
    static DispatcherTimer? _timer;

    // Only possible due to our System.Diagnostics.PerformanceCounter NuGet (sadly .NET Core does not offer the PerformanceCounter)
    PerformanceCounter? _perfCPU;
    SolidColorBrush _level1; SolidColorBrush _level2; SolidColorBrush _level3;
    SolidColorBrush _level4; SolidColorBrush _level5; SolidColorBrush _level6;
    System.Globalization.NumberFormatInfo _formatter;

    public ObservableCollection<NamedColor> NamedColors = new();

    NamedColor? _scrollToItem;
    public NamedColor? ScrollToItem
    {
        get => _scrollToItem;
        set => SetProperty(ref _scrollToItem, value);
    }

    Thickness _borderSize;
    public Thickness BorderSize
    {
        get => _borderSize;
        set => SetProperty(ref _borderSize, value);
    }

    double _opacity;
    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    bool _isBusy = false;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    SolidColorBrush _needleColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
    public SolidColorBrush NeedleColor
    {
        get => _needleColor;
        set => SetProperty(ref _needleColor, value);
    }

    string _status = "Loading…";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    int _maxCPU = 100;
    public int MaxCPU
    {
        get => _maxCPU;
        set => SetProperty(ref _maxCPU, value);
    }

    int _currentCPU = 0;
    public int CurrentCPU
    {
        get => _currentCPU;
        set => SetProperty(ref _currentCPU, value);
    }

    int _interval = 1000;
    /// <summary>
    /// This exists so the user can see the timer change real-time from the <see cref="ConfigWindow"/>.
    /// </summary>
    public int Interval
    {
        get => _interval;
        set 
        { 
            SetProperty(ref _interval, value);

            if (_timer == null)
            {
                ConfigureRefreshTimer(_interval);
            }
            else
            {
                Debug.WriteLine($"[INFO] Changing timer interval to {_interval} ms");
                _timer.Stop();
                _timer.Interval = TimeSpan.FromMilliseconds(_interval);
                _timer.Start();
            }

            // Update for saving when app is closed.
            if (App.LocalConfig != null)
                App.LocalConfig.msRefresh = _interval;
        }
    }

    public bool OpenOnWindowsStartup
    {
        get => Config.autoStart;
        set => SetProperty(ref Config.autoStart, value);
    }

    public Config? Config
    {
        get => App.LocalConfig;
    }
    #endregion

    public ICommand KeyDownCommand { get; }
    public ICommand OpenOnWindowsStartupCommand { get; }

    FileLogger? Logger = (FileLogger?)App.Current.Services.GetService<ILogger>();

    public MainViewModel()
    {
        Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name} [{DateTime.Now.ToString("hh:mm:ss.fff tt")}]");

        // https://learn.microsoft.com/en-us/dotnet/api/system.globalization.numberformatinfo?view=net-8.0
        _formatter = System.Globalization.NumberFormatInfo.CurrentInfo;

        if (App.LocalConfig is not null)
        {
            Interval = App.LocalConfig.msRefresh;
            _borderSize = new Thickness(App.LocalConfig.borderSize);
            _opacity = App.LocalConfig.opacity;
            if (App.LocalConfig.useHistogram)
                Status = "Loading, please wait…";
            else
                Status = "Loading…"; // we don't have much real estate w/r/t gauge
        }
        else
        {
            _borderSize = new Thickness(2);
            _opacity = 0.6;
        }

        KeyDownCommand = new RelayCommand<object>(async (obj) =>
        {
            IsBusy = true;
            #region [Transparency.Behaviors.KeyDownTriggerBehavior Testing]
            // If we got here then the user has pressed the [Enter] key.
            // It's considered poor form to play with UI controls from the VM, but this
            // was a fun exercise and there are use-cases where this might be desired.
            // In this example we want an ICommand tied to the key press while the control has focus.
            // This is currently used as a validation step to demo the KeyDownTriggerBehavior.
            // You could also perform the validation in the code-behind for the ConfigWindow.
            if (obj is Microsoft.UI.Xaml.Controls.Grid grid)
            {
                foreach (var uie in grid.Children)
                {
                    if (uie.GetType() == typeof(AutoCloseInfoBar))
                    {
                        ((AutoCloseInfoBar)uie).Message = "Settings have been updated.";
                        ((AutoCloseInfoBar)uie).IsOpen = true;
                    }
                    // Our TextBox controls are inside a StackPanel, which lives inside a Grid.
                    else if (uie.GetType() == typeof(Microsoft.UI.Xaml.Controls.StackPanel))
                    {
                        var sp = (Microsoft.UI.Xaml.Controls.StackPanel)uie;
                        if (sp == null) { continue; }
                        foreach (var e in sp.Children)
                        {
                            if (e.GetType() == typeof(Microsoft.UI.Xaml.Controls.TextBox))
                            {
                                var tb = (Microsoft.UI.Xaml.Controls.TextBox)e;
                                if (tb == null) { continue; }
                                switch (tb.Name)
                                {
                                    case string name when name.ToLower().Contains("refresh"):
                                        if (App.LocalConfig is not null && int.TryParse(tb.Text, out int refresh))
                                            App.LocalConfig.msRefresh = refresh;
                                        else
                                            await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, _dialogImgUri);
                                        break;
                                    case string name when name.ToLower().Contains("color"):
                                        if (App.LocalConfig is not null && !string.IsNullOrEmpty(tb.Text))
                                            App.LocalConfig.background = tb.Text.Trim().Replace("#", "");
                                        else
                                            await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, _dialogImgUri);
                                        break;
                                    case string name when name.ToLower().Contains("border"):
                                        if (App.LocalConfig is not null && int.TryParse(tb.Text, out int size))
                                        {
                                            App.LocalConfig.borderSize = size;
                                            BorderSize = new Thickness(size);
                                        }
                                        else
                                            await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, _dialogImgUri);
                                        break;
                                    case string name when name.ToLower().Contains("opacity"):
                                        if (App.LocalConfig is not null && double.TryParse(tb.Text, out double opac))
                                            App.LocalConfig.opacity = Opacity = opac;
                                        else
                                            await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, _dialogImgUri);
                                        break;
                                    default:
                                        Debug.WriteLine($"[INFO] 📢 No switch case defined for \"{tb.Name}\"");
                                        break;
                                }
                            }
                            else if (e.GetType() == typeof(Microsoft.UI.Xaml.Controls.ToggleSwitch))
                            {
                                var ts = (Microsoft.UI.Xaml.Controls.ToggleSwitch)e;
                                if (ts == null) { continue; }
                                Debug.WriteLine($"[INFO] 📢 ToggleSwitch \"{ts.Name}\"");
                            }
                            else if (e.GetType() == typeof(Microsoft.UI.Xaml.Controls.StackPanel))
                            {
                                var isp = (Microsoft.UI.Xaml.Controls.StackPanel)e;
                                foreach (var sub in isp.Children)
                                {
                                    Debug.WriteLine($"[INFO] 📢 Found sub element {sub.GetType().Name}");
                                }
                            }
                            else if (e.GetType() == typeof(AutoCloseInfoBar))
                            {
                                ((AutoCloseInfoBar)e).Message = "Settings have been updated.";
                                ((AutoCloseInfoBar)e).IsOpen = true;
                            }
                            else
                            {
                                Debug.WriteLine($"[INFO] 📢 Found inner {e.GetType().Name} of base type {e.GetType().BaseType?.Name}");
                            }
                        }
                    }
                    else if (uie.GetType() == typeof(Microsoft.UI.Xaml.Controls.TextBox))
                    {
                        var tb = (Microsoft.UI.Xaml.Controls.TextBox)uie;
                        if (tb == null) { continue; }
                        switch (tb.Name)
                        {
                            case string name when name.ToLower().Contains("refresh"):
                                if (App.LocalConfig is not null && int.TryParse(tb.Text, out int refresh))
                                    App.LocalConfig.msRefresh = refresh;
                                break;
                            case string name when name.ToLower().Contains("color"):
                                if (App.LocalConfig is not null)
                                    App.LocalConfig.background = tb.Text.Trim().Replace("#", "");
                                break;
                            case string name when name.ToLower().Contains("border"):
                                if (App.LocalConfig is not null && int.TryParse(tb.Text, out int size))
                                {
                                    App.LocalConfig.borderSize = size;
                                    BorderSize = new Thickness(size);
                                }
                                break;
                            case string name when name.ToLower().Contains("opacity"):
                                if (App.LocalConfig is not null && double.TryParse(tb.Text, out double opac))
                                    App.LocalConfig.opacity = Opacity = opac;
                                break;
                            default:
                                Debug.WriteLine($"[INFO] 📢 No switch case defined for \"{tb.Name}\"");
                                break;
                        }
                    }
                    else if (uie.GetType() == typeof(Microsoft.UI.Xaml.Controls.Grid))
                    {
                        var g = (Microsoft.UI.Xaml.Controls.Grid)uie;
                        foreach (var ge in g.Children)
                        {
                            Debug.WriteLine($"[INFO] 📢 Found sub element {ge.GetType().Name}");
                            if (ge.GetType() == typeof(Microsoft.UI.Xaml.Controls.StackPanel))
                            {
                                var isp = (Microsoft.UI.Xaml.Controls.StackPanel)ge;
                                foreach (var sub in isp.Children)
                                {
                                    Debug.WriteLine($"[INFO] 📢 Found sub-sub element {sub.GetType().Name}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[INFO] 📢 Found outer {uie.GetType().Name} of base type {uie.GetType().BaseType?.Name}");
                    }
                }

                //var objs = grid.FindDescendants();
                //foreach (DependencyObject dp in objs)
                //{
                //    var txt = dp.FindDescendant<Microsoft.UI.Xaml.Controls.TextBox>();
                //    if (txt != null)
                //        Debug.WriteLine($"[INFO] Found TextBox: {txt.Text}");
                //
                //    var ibar = dp.FindDescendant<AutoCloseInfoBar>();
                //    if (ibar != null)
                //        Debug.WriteLine($"[INFO] Found InfoBar");
                //}
            }
            else if (obj is Microsoft.UI.Xaml.Controls.TextBox tb)
            {
                if (tb is not null && !string.IsNullOrEmpty(tb.Text) && !string.IsNullOrEmpty(tb.Name))
                {
                    switch (tb.Name)
                    {
                        case string name when name.ToLower().Contains("refresh"):
                            if (App.LocalConfig is not null && int.TryParse(tb.Text, out int refresh))
                            {
                                App.LocalConfig.msRefresh = refresh;
                                await App.ShowDialogBox(tb.XamlRoot, "Updated", $"⚙️ {name} value is now \"{tb.Text}\"", "OK", "", null, null, _dialogImgUri);
                            }
                            else
                                await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, _dialogImgUri);
                            break;
                        case string name when name.ToLower().Contains("color"):
                            if (App.LocalConfig is not null && !string.IsNullOrEmpty(tb.Text))
                            {
                                App.LocalConfig.background = tb.Text.Trim().Replace("#","");
                                await App.ShowDialogBox(tb.XamlRoot, "Updated", $"⚙️ {name} value is now \"{tb.Text}\"", "OK", "", null, null, _dialogImgUri);
                            }
                            else
                                await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, _dialogImgUri);
                            break;
                        case string name when name.ToLower().Contains("border"):
                            if (App.LocalConfig is not null && int.TryParse(tb.Text, out int size))
                            {
                                App.LocalConfig.borderSize = size;
                                BorderSize = new Thickness(size);
                                await App.ShowDialogBox(tb.XamlRoot, "Updated", $"⚙️ {name} value is now \"{tb.Text}\"", "OK", "", null, null, _dialogImgUri);
                            }
                            else
                                await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, _dialogImgUri);
                            break;
                        case string name when name.ToLower().Contains("opacity"):
                            if (App.LocalConfig is not null && double.TryParse(tb.Text, out double opac))
                            {
                                App.LocalConfig.opacity = opac;
                                await App.ShowDialogBox(tb.XamlRoot, "Updated", $"⚙️ {name} value is now \"{tb.Text}\"", "OK", "", null, null, _dialogImgUri);
                            }
                            else
                                await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, _dialogImgUri);
                            break;
                        default:
                            Debug.WriteLine($"[INFO] 📢 No switch case defined for \"{tb.Name}\"");
                            break;
                    }
                }
            }
            else if (obj is Microsoft.Xaml.Interactions.Core.InvokeCommandAction cmd)
            {
                if (cmd != null)
                    Debug.WriteLine($"[INFO] 📢 Got Behavior InvokeCommandAction");
            }
            else if (obj is Microsoft.UI.Xaml.Controls.Slider sld)
            {
                if (sld != null)
                    Debug.WriteLine($"[INFO] 📢 Got Slider value \"{sld.Value}\"");
            }
            else if (obj is Microsoft.UI.Xaml.Window win)
            {
                if (win != null)
                    Debug.WriteLine($"[INFO] 📢 Got Window \"{win.Title}\"");
            }
            else
            {
                if (obj != null)
                    Debug.WriteLine($"[WARNING] 📢 No action defined for type '{obj?.GetType()}', name '{obj?.GetType().Name}', and base type {obj?.GetType().BaseType?.Name}");
            }
            #endregion
            await Task.Delay(1000); // for spinners
            IsBusy = false;
        });

        OpenOnWindowsStartupCommand = new RelayCommand<bool>(async (obj) => 
        { 
            await OpenOnWindowsStartupAsync(obj); 
        });
        _ = DetectOpenAtStartupAsync();

        #region [PerfCounter Init]
        // Instantiating a PerformanceCounter can take up to 20+ seconds, so we'll queue this on another thread.
        Task.Run(() =>
        {
            if (_perfCPU == null)
            {
                if (Config!.logging)
                    Logger?.WriteLine($"Creating PerformanceCounter", LogLevel.Debug);

                // There's a bazillion categories of performance counters available.
                // To learn more about them check out my other repo ⇒ https://github.com/GuildOfCalamity/ResourceMonitor
                _perfCPU = new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Time", "_Total", true);
                Debug.WriteLine(_perfCPU.ToStringDump());
                
                if (Config!.logging)
                    Logger?.WriteLine($"PerformanceCounter Created", LogLevel.Debug);
            }
        });

        // CPU usage with RadialGauge.
        _level6 = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed) { Opacity = 0.7 };
        _level5 = new SolidColorBrush(Microsoft.UI.Colors.Orange) { Opacity = 0.7 };
        _level4 = new SolidColorBrush(Microsoft.UI.Colors.Yellow) { Opacity = 0.6 };
        _level3 = new SolidColorBrush(Microsoft.UI.Colors.YellowGreen) { Opacity = 0.7 };
        _level2 = new SolidColorBrush(Microsoft.UI.Colors.SpringGreen) { Opacity = 0.7 };
        _level1 = new SolidColorBrush(Microsoft.UI.Colors.RoyalBlue) { Opacity = 0.7 };

        // This should have happened already, but we'll add another check here in
        // the event that config could not be loaded from the Application class.
        if (_timer == null)
        {
            ConfigureRefreshTimer(App.LocalConfig != null ? App.LocalConfig.msRefresh : 2000);
        }
        #endregion
    }

    #region [SyncContext]
    Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext? _context;
    void TestDispatcherQueueSynchronizationContext(FrameworkElement fe)
    {
        var dis = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (dis is not null)
        {
            _context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(dis);
            SynchronizationContext.SetSynchronizationContext(_context);

            // SynchronizationContext's Post() is the asynchronous method.
            _context?.Post(o => fe.Height = 40, null); // Marshal the delegate to the UI thread

            // SynchronizationContext's Send() is the synchronous method.
            _context?.Send(_ => fe.Height = 40, null); // Marshal the delegate to the UI thread
        }
        else
        {
            // You could also use the control's dispatcher for UI calls.
            fe.DispatcherQueue.TryEnqueue(() => fe.Height = 40);
        }
    }
    #endregion

    #region [PerfCounter]
    int _lastValue = -1;
    float GetCPU()
    {
        float newValue = 0;

        if (_perfCPU == null)
            return newValue;

        if (!string.IsNullOrEmpty(Status))
            Status = "";

        try
        {
            newValue = _perfCPU.NextValue();
            switch (newValue)
            {
                case float f when f > 80:
                    NeedleColor = _level6;
                    break;
                case float f when f > 70:
                    NeedleColor = _level5;
                    break;
                case float f when f > 40:
                    NeedleColor = _level4;
                    break;
                case float f when f > 20:
                    NeedleColor = _level3;
                    break;
                case float f when f > 10:
                    NeedleColor = _level2;
                    break;
                default:
                    NeedleColor = _level1;
                    break;
            }

            float height = 0;

            // Simple duplicate checking.
            if ((int)newValue != _lastValue)
            {
                _lastValue = (int)newValue;

                // Auto-size rectangle graphic.
                height = ScaleValueLog10(newValue);

                // Opacity is not carried over from the SolidColorBrush color property accessors.
                var clr = NeedleColor.Color;
                var opac = App.LocalConfig != null ? App.LocalConfig.opacity : 0.75;

                // Add entry for histogram.
                NamedColors.Insert(0, new NamedColor { Height = (double)height, Amount = (newValue / 100).ToString("P0", _formatter), Time = $"{DateTime.Now.ToString("h:mm:ss tt")}", Color = clr, Opacity = opac });

                // NOTE: A percent sign (%) in a format string causes a number to be multiplied by 100 before it is formatted.
                // The localized percent symbol is inserted in the number at the location where the % appears in the format string.
                // This is why you'll see the (newValue/100) before it is assigned.
                // https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#percent-format-specifier-p
                // https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--custom-specifier-3

                // Monitor memory consumption.
                if (NamedColors.Count > 100)
                    NamedColors.RemoveAt(NamedColors.Count - 1);

            }
        }
        catch (Exception ex)
        {
            if (Config!.logging)
                Logger?.WriteLine($"PerformanceCounter.NextValue(): {ex.Message}", LogLevel.Error);
        }
        return newValue;
    }

    /// <summary>
    /// Smaller values will be harder to see on the graph, so we'll scale them up to be more visible.
    /// A linear model wouldn't work as well with small amounts, e.g. 1%.
    /// </summary>
    float ScaleValueLog10(float value)
    {
        // Clamp value between 1 and 100
        value = Math.Clamp(value, 1f, 100f);

        // Scale the value logarithmically to a range between 1 and 150
        float scaledValue = (float)(1 + (149 * Math.Log10(1 + (10 * value / 100)) / Math.Log10(11)));

        return scaledValue;
    }

    #endregion

    void ConfigureRefreshTimer(int interval)
    {
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(interval);
        _timer.Tick += (_, _) =>
        {
            if (!App.IsClosing)
                CurrentCPU = (int)GetCPU();
            else
                _timer?.Stop();
        };
        _timer?.Start();
    }

    #region [StartupTask]
    public void ToggleSwitchOnToggled(object sender, RoutedEventArgs e)
    {
        var ts = sender as ToggleSwitch;
        if (ts != null)
        {
            OpenOnWindowsStartupCommand.Execute(ts.IsOn);
        }
    }

    public async Task OpenOnWindowsStartupAsync(bool toggleState)
    {
        var stateMode = await ReadState();

        bool state = stateMode switch
        {
            StartupTaskState.Enabled => true,
            StartupTaskState.EnabledByPolicy => true,
            StartupTaskState.Disabled => false,
            StartupTaskState.DisabledByUser => false,
            StartupTaskState.DisabledByPolicy => false,
            _ => false,
        };

        Config.autoStart = toggleState;

        try
        {
            if (App.IsPackaged)
            {
                StartupTask startupTask = await StartupTask.GetAsync("3AA55462-A5FA-DEAD-BEEF-712D0B6CDEBB");
                if (toggleState)
                    await startupTask.RequestEnableAsync();
                else
                    startupTask.Disable();
            }
            else
            {
                if (toggleState)
                    EnableRegistryStartup();
                else
                    DisableRegistryStartup();
            }
        }
        catch (RuntimeWrappedException rwe) // catch any non-CLS exceptions
        {
            String? s = rwe.WrappedException as String;
            if (s != null)
                Debug.WriteLine($"[WARNING] {s}");
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"StartupTask.GetAsync: {ex.Message}", LogLevel.Error);
        }

        await DetectOpenAtStartupAsync();
    }

    public async Task DetectOpenAtStartupAsync()
    {
        var stateMode = await ReadState();

        switch (stateMode)
        {
            case StartupTaskState.Disabled:
                Config.autoStart = false;
                break;
            case StartupTaskState.Enabled:
                Config.autoStart = true;
                break;
            case StartupTaskState.DisabledByPolicy:
                Config.autoStart = false;
                break;
            case StartupTaskState.DisabledByUser:
                Config.autoStart = false;
                break;
            case StartupTaskState.EnabledByPolicy:
                Config.autoStart = true;
                break;
        }
    }

    /// <summary>
    /// <para>
    /// In regards to auto-start apps, a packaged app has different requirements than an unpackaged app.
    /// For packaged apps the registry's Windows Store AppID contains two keys a "State" and a "UserEnabledStartupOnce".
    /// If "State is set to 2 then the app will be invoked at logon.
    /// If "State is set to 0 then the app will not be invoked at logon.
    /// <b>HKCU\SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData</b>
    /// </para>
    /// <para>
    /// For unpackaged apps the path to the assembly must be added to the following location in the registry:
    /// <b>HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run</b>
    /// At logon, if the assembly path exists, the app will be invoked.
    /// <i>The unpackaged app must have enough rights to access the registry.</i>
    /// </para>
    /// </summary>
    /// <returns><see cref="Windows.ApplicationModel.StartupTaskState"/></returns>
    public async Task<Windows.ApplicationModel.StartupTaskState> ReadState()
    {
        try
        {
            if (App.IsPackaged)
            {
                var state = await StartupTask.GetAsync("3AA55462-A5FA-DEAD-BEEF-712D0B6CDEBB");
                return state.State;
            }
            else
            {
                switch (CheckRegistryStartup())
                {
                    case false: 
                        return StartupTaskState.Disabled;
                    case true: 
                        return StartupTaskState.Enabled;
                }
            }
        }
        catch (RuntimeWrappedException rwe) // catch any non-CLS exceptions
        {
            String? s = rwe.WrappedException as String;
            if (s != null)
                Debug.WriteLine($"[WARNING] {s}");
            return StartupTaskState.Disabled;
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"StartupTask.GetAsync: {ex.Message}", LogLevel.Error);
            return StartupTaskState.Disabled;
        }
    }

    public bool CheckRegistryStartup()
    {
        bool result = false;
        try
        {
            object? regVal = null;
            string fallback = string.Empty;
            if (App.IsPackaged)
                fallback = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
            else
                fallback = System.AppContext.BaseDirectory; // -or- Directory.GetCurrentDirectory()
            var procName = Process.GetCurrentProcess()?.MainModule?.FileName ?? System.IO.Path.Combine(fallback, $"{App.GetCurrentAssemblyName()}.exe");
            using (var view64 = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
            {
                using (var clsid64 = view64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    regVal = clsid64?.GetValue($"AutoStart{App.GetCurrentAssemblyName()}");
                    if (regVal == null)
                    {
                        Logger?.WriteLine($"'{App.GetCurrentAssemblyName()}' does not exist in registry startup.", LogLevel.Info);
                        result = false;
                    }
                    else
                    {
                        Logger?.WriteLine($"'{App.GetCurrentAssemblyName()}' already exists in registry startup.", LogLevel.Info);
                        result = true;
                    }
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"Error during registry startup check: {ex.Message}", LogLevel.Error);
            return false;
        }
    }

    public bool EnableRegistryStartup()
    {
        bool result = false;
        try
        {
            object? regVal = null;

            string fallback = string.Empty;
            if (App.IsPackaged)
                fallback = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
            else
                fallback = System.AppContext.BaseDirectory; // -or- Directory.GetCurrentDirectory()
            var procName = Process.GetCurrentProcess()?.MainModule?.FileName ?? System.IO.Path.Combine(fallback, $"{App.GetCurrentAssemblyName()}.exe");
            using (var view64 = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
            {
                using (var clsid64 = view64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    regVal = clsid64?.GetValue($"AutoStart{App.GetCurrentAssemblyName()}");
                    if (regVal == null)
                    {
                        clsid64?.SetValue($"AutoStart{App.GetCurrentAssemblyName()}", procName, Microsoft.Win32.RegistryValueKind.String);
                        Logger?.WriteLine($"Added '{App.GetCurrentAssemblyName()}' to registry startup.", LogLevel.Info);
                        result = true;
                    }
                    else if (regVal != null)
                    {
                        Logger?.WriteLine($"'{App.GetCurrentAssemblyName()}' already exists in registry startup.", LogLevel.Info);
                        result = true;
                    }
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"Error during registry startup check: {ex.Message}", LogLevel.Error);
            return false;
        }
    }

    public bool DisableRegistryStartup()
    {
        bool result = false;
        try
        {
            object? regVal = null;

            string fallback = string.Empty;
            if (App.IsPackaged)
                fallback = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
            else
                fallback = System.AppContext.BaseDirectory; // -or- Directory.GetCurrentDirectory()
            var procName = Process.GetCurrentProcess()?.MainModule?.FileName ?? System.IO.Path.Combine(fallback, $"{App.GetCurrentAssemblyName()}.exe");
            using (var view64 = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
            {
                using (var clsid64 = view64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    regVal = clsid64?.GetValue($"AutoStart{App.GetCurrentAssemblyName()}");
                    if (regVal != null)
                    {
                        clsid64?.DeleteValue($"AutoStart{App.GetCurrentAssemblyName()}", false);
                        Logger?.WriteLine($"Removed '{App.GetCurrentAssemblyName()}' from registry startup.", LogLevel.Info);
                        result = true;
                    }
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"Error during registry startup check: {ex.Message}", LogLevel.Error);
            return false;
        }
    }
    #endregion
}
