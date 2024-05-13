using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;

using CommunityToolkit.Mvvm.ComponentModel;

using Transparency.Support;
using Transparency.Helpers;
using Transparency.Services;

using CommunityToolkit.Mvvm.Input;
using static System.Net.Mime.MediaTypeNames;
using Transparency.Controls;
using Transparency.Models;
using System.Collections.ObjectModel;

namespace Transparency.ViewModels;

public class MainViewModel : ObservableRecipient
{
    #region [Props]
    static DispatcherTimer? _timer;

    // Only possible due to our System.Diagnostics.PerformanceCounter NuGet (sadly .NET Core does not offer the PerformanceCounter)
    PerformanceCounter? _perfCPU;
    SolidColorBrush _level1;
    SolidColorBrush _level2;
    SolidColorBrush _level3;
    SolidColorBrush _level4;
    SolidColorBrush _level5;
    SolidColorBrush _level6;


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

    public Config? Config
    {
        get => App.LocalConfig;
    }
    #endregion

    public ICommand KeyDownCommand { get; }

    FileLogger? Logger = (FileLogger?)App.Current.Services.GetService<ILogger>();

    public MainViewModel()
    {
        Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name} [{DateTime.Now.ToString("hh:mm:ss.fff tt")}]");

        if (App.LocalConfig is not null)
        {
            Interval = App.LocalConfig.msRefresh;
            _borderSize = new Thickness(App.LocalConfig.borderSize);
            _opacity = App.LocalConfig.opacity;
        }
        else
        {
            _borderSize = new Thickness(2);
            _opacity = 0.6;
        }

        KeyDownCommand = new RelayCommand<object>(async (obj) =>
        {
            IsBusy = true;
            #region [KeyDownTriggerBehavior Testing]
            // It's considered poor form to play with UI controls from the ViewModel, but
            // this was a fun exercise and there are use-cases where this might be desired.
            // This is currently used as a validation step to demo the KeyDownTriggerBehavior.
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
                                            await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                                        break;
                                    case string name when name.ToLower().Contains("color"):
                                        if (App.LocalConfig is not null && !string.IsNullOrEmpty(tb.Text))
                                            App.LocalConfig.background = tb.Text.Trim().Replace("#", "");
                                        else
                                            await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                                        break;
                                    case string name when name.ToLower().Contains("border"):
                                        if (App.LocalConfig is not null && int.TryParse(tb.Text, out int size))
                                        {
                                            App.LocalConfig.borderSize = size;
                                            BorderSize = new Thickness(size);
                                        }
                                        else
                                            await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                                        break;
                                    case string name when name.ToLower().Contains("opacity"):
                                        if (App.LocalConfig is not null && double.TryParse(tb.Text, out double opac))
                                            App.LocalConfig.opacity = Opacity = opac;
                                        else
                                            await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
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
                            else
                            {
                                Debug.WriteLine($"[INFO] 📢 Found {uie.GetType().Name} of base type {uie.GetType().BaseType?.Name}");
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
                    else
                    {
                        Debug.WriteLine($"[INFO] 📢 Found {uie.GetType().Name} of base type {uie.GetType().BaseType?.Name}");
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
                                await App.ShowDialogBox(tb.XamlRoot, "Updated", $"⚙️ {name} value is now \"{tb.Text}\"", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                            }
                            else
                                await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                            break;
                        case string name when name.ToLower().Contains("color"):
                            if (App.LocalConfig is not null && !string.IsNullOrEmpty(tb.Text))
                            {
                                App.LocalConfig.background = tb.Text.Trim().Replace("#","");
                                await App.ShowDialogBox(tb.XamlRoot, "Updated", $"⚙️ {name} value is now \"{tb.Text}\"", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                            }
                            else
                                await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                            break;
                        case string name when name.ToLower().Contains("border"):
                            if (App.LocalConfig is not null && int.TryParse(tb.Text, out int size))
                            {
                                App.LocalConfig.borderSize = size;
                                BorderSize = new Thickness(size);
                                await App.ShowDialogBox(tb.XamlRoot, "Updated", $"⚙️ {name} value is now \"{tb.Text}\"", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                            }
                            else
                                await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                            break;
                        case string name when name.ToLower().Contains("opacity"):
                            if (App.LocalConfig is not null && double.TryParse(tb.Text, out double opac))
                            {
                                App.LocalConfig.opacity = opac;
                                await App.ShowDialogBox(tb.XamlRoot, "Updated", $"⚙️ {name} value is now \"{tb.Text}\"", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
                            }
                            else
                                await App.ShowDialogBox(tb.XamlRoot, "Warning", $"⚙️ {name} value is invalid", "OK", "", null, null, new Uri($"ms-appx:///Assets/WinTransparent.png"));
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

    #region [PerfCounter]
    int _lastValue = -1;
    bool _useLogarithm = true;
    float GetCPU(int maxWidth = 200)
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

            float width = 0;

            // Simple duplicate checking.
            if ((int)newValue != _lastValue)
            {
                _lastValue = (int)newValue;

                // Auto-size rectangle graphic.
                if (_useLogarithm)
                {
                    //width = ScaleValueLog(newValue);
                    width = ScaleValueLog10(newValue);
                }
                else
                {
                    width = AmplifyLinear(newValue);
                }


                // Opacity is not carried over from the SolidColorBrush color property accessors.
                var clr = NeedleColor.Color;
                var opac = App.LocalConfig != null ? App.LocalConfig.opacity : 0.75;

                // Add entry for histogram.
                NamedColors.Insert(0, new NamedColor { Height = (double)width, Amount = $"{(int)newValue}%", Time = $"{DateTime.Now.ToString("h:mm:ss tt")}", Color = clr, Opacity = opac });

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
    /// </summary>
    float AmplifyLinear(float number, int maxClamp = 200)
    {
        if (number < 10)
            return ((number + 1f) * 6f).Clamp(1, maxClamp);
        else if (number < 20)
            return ((number + 1f) * 5f).Clamp(1, maxClamp);
        else if (number < 40)
            return ((number + 1f) * 4f).Clamp(1, maxClamp);
        else if (number < 60)
            return ((number + 1f) * 3f).Clamp(1, maxClamp);
        else if (number < 80)
            return ((number + 1f) * 2.5f).Clamp(1, maxClamp);
        else
            return ((number + 1f) * 2f).Clamp(1, maxClamp);
    }

    float ScaleValueLog10(float value)
    {
        // Clamp value between 1 and 100
        value = Math.Clamp(value, 1f, 100f);

        // Scale the value logarithmically to a range between 1 and 200
        float scaledValue = (float)(1 + (199 * Math.Log10(1 + (10 * value / 100)) / Math.Log10(11)));

        return scaledValue;
    }

    #endregion
}
