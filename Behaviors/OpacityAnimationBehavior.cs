using System;
using System.Diagnostics;
using System.Numerics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Xaml.Interactivity;

using Windows.Foundation;

namespace Transparency.Behaviors;


/// <summary>
/// This <see cref="Microsoft.Xaml.Interactivity.Behavior"/> pattern still needs some fine tuning.
/// When the <see cref="FrameworkElement"/> receives the focus an opacity and translation animation will be performed.
/// </summary>
public class OpacityAnimationBehavior : Behavior<FrameworkElement>
{
    #region [Props]
    DispatcherTimer? _timer;

    /// <summary>
    /// Identifies the <see cref="Seconds"/> property for the animation.
    /// </summary>
    public static readonly DependencyProperty SecondsProperty = DependencyProperty.Register(
        nameof(Seconds),
        typeof(double),
        typeof(SlideAnimationBehavior),
        new PropertyMetadata(1.25d));

    /// <summary>
    /// Gets or sets the <see cref="TimeSpan"/> to run the animation for.
    /// </summary>
    public double Seconds
    {
        get => (double)GetValue(SecondsProperty);
        set => SetValue(SecondsProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Final"/> property for the animation.
    /// </summary>
    public static readonly DependencyProperty FinalProperty = DependencyProperty.Register(
        nameof(Final),
        typeof(double),
        typeof(SlideAnimationBehavior),
        new PropertyMetadata(1d));

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public double Final
    {
        get => (double)GetValue(FinalProperty);
        set => SetValue(FinalProperty, value);
    }

    #endregion

    protected override void OnAttached()
    {
        base.OnAttached();

        if (!App.AnimationsEffectsEnabled)
            return;

        AssociatedObject.Loaded += AssociatedObject_Loaded;
        AssociatedObject.Unloaded += AssociatedObject_Unloaded;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (!App.AnimationsEffectsEnabled)
            return;

        AssociatedObject.Loaded -= AssociatedObject_Loaded;
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
    }

    void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] {sender.GetType().Name} loaded at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");

        var obj = sender as FrameworkElement;
        if (obj is null) { return; }

        if (obj.ActualHeight != double.NaN && obj.ActualHeight != 0)
            Debug.WriteLine($"[INFO] Reported {sender.GetType().Name} height is {obj.ActualHeight} pixels");

        Debug.WriteLine($"[INFO] Opacity animation will run for {Seconds} seconds.");
        AnimateUIElementOpacity(0, Final, TimeSpan.FromSeconds(Seconds), (UIElement)sender);
    }

    /// <summary>
    /// Mock disposal routine.
    /// </summary>
    void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] {sender.GetType().Name} unloaded at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
    }

    #region [Animations]
    /// <summary>
    /// Opacity animation using <see cref="Microsoft.UI.Composition.ScalarKeyFrameAnimation"/>
    /// </summary>
    void AnimateUIElementOpacity(double from, double to, TimeSpan duration, UIElement target, Microsoft.UI.Composition.AnimationDirection direction = Microsoft.UI.Composition.AnimationDirection.Normal)
    {
        var targetVisual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = targetVisual.Compositor;
        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Direction = direction;
        opacityAnimation.Duration = duration;
        opacityAnimation.Target = "Opacity";
        opacityAnimation.InsertKeyFrame(0.0f, (float)from);
        opacityAnimation.InsertKeyFrame(1.0f, (float)to);
        targetVisual.StartAnimation("Opacity", opacityAnimation);
    }

    /// <summary>
    /// Offset animation using <see cref="Microsoft.UI.Composition.Vector3KeyFrameAnimation"/>
    /// </summary>
    void AnimateUIElementOffset(Point to, TimeSpan duration, UIElement target, Microsoft.UI.Composition.AnimationDirection direction = Microsoft.UI.Composition.AnimationDirection.Normal)
    {
        var targetVisual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = targetVisual.Compositor;
        var linear = compositor.CreateLinearEasingFunction();
        var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Direction = direction;
        offsetAnimation.Duration = duration;
        offsetAnimation.Target = "Offset";
        offsetAnimation.InsertKeyFrame(0.0f, new Vector3((float)to.X, (float)to.Y, 0), linear);
        offsetAnimation.InsertKeyFrame(1.0f, new Vector3(0), linear);
        targetVisual.StartAnimation("Offset", offsetAnimation);
    }

    /// <summary>
    /// Scale animation using <see cref="Microsoft.UI.Composition.Vector3KeyFrameAnimation"/>
    /// </summary>
    void AnimateUIElementScale(double to, TimeSpan duration, UIElement target, Microsoft.UI.Composition.AnimationDirection direction = Microsoft.UI.Composition.AnimationDirection.Normal)
    {
        var targetVisual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = targetVisual.Compositor;
        var linear = compositor.CreateLinearEasingFunction();
        var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Direction = direction;
        scaleAnimation.Duration = duration;
        scaleAnimation.Target = "Scale";
        scaleAnimation.InsertKeyFrame(0.0f, new Vector3(0), linear);
        scaleAnimation.InsertKeyFrame(1.0f, new Vector3((float)to), linear);
        targetVisual.StartAnimation("Scale", scaleAnimation);
    }
    #endregion
}


/// <summary>
/// This <see cref="Microsoft.Xaml.Interactivity.Behavior"/> pattern still needs some fine tuning.
/// When the <see cref="FrameworkElement"/> receives the focus an opacity and translation animation will be performed.
/// </summary>
public class OpacityAnimationBehaviorExperimental : Behavior<FrameworkElement>
{
    DispatcherTimer? _timer;
    static bool _hasFocus = false;
    Storyboard? _storyboardOn;
    Storyboard? _storyboardOff;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (!App.AnimationsEffectsEnabled)
            return;

        AssociatedObject.Loaded += AssociatedObject_Loaded;
        AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        AssociatedObject.GotFocus += AssociatedObject_GotFocus;
        AssociatedObject.LostFocus += AssociatedObject_LostFocus;
        AssociatedObject.GettingFocus += AssociatedObject_GettingFocus;
        AssociatedObject.LosingFocus += AssociatedObject_LosingFocus;

        _timer = new DispatcherTimer();
        _timer.Tick += Timer_Tick;
        _timer.Interval = TimeSpan.FromMilliseconds(1000);
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (!App.AnimationsEffectsEnabled)
            return;

        AssociatedObject.Loaded -= AssociatedObject_Loaded;
        AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
        AssociatedObject.GotFocus -= AssociatedObject_GotFocus;
        AssociatedObject.LostFocus -= AssociatedObject_LostFocus;
        AssociatedObject.GettingFocus -= AssociatedObject_GettingFocus;
        AssociatedObject.LosingFocus -= AssociatedObject_LosingFocus;

        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }
    }

    void Timer_Tick(object? sender, object e)
    {
        if (!_hasFocus)
        {
            Debug.WriteLine($"[INFO] Running StoryboardOff at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
            _storyboardOff?.Begin();
        }
        _timer?.Stop();
    }

    void AssociatedObject_GettingFocus(UIElement sender, Microsoft.UI.Xaml.Input.GettingFocusEventArgs args)
    {
        Debug.WriteLine($"[INFO] Getting focus at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
        _hasFocus = true;
    }

    void AssociatedObject_LosingFocus(UIElement sender, Microsoft.UI.Xaml.Input.LosingFocusEventArgs args)
    {
        Debug.WriteLine($"[INFO] Losing focus at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");

        if (_timer != null && _timer.IsEnabled)
            return;

        _hasFocus = false;
        _timer?.Start();
    }

    void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
    {
        var obj = sender as FrameworkElement;
        if (obj is null) { return; }

        //AnimateUIElementScale(0.2, TimeSpan.FromSeconds(0.1), (UIElement)sender);

        var daOn = new DoubleAnimation
        {
            From = 0.2d,
            To = 1d,
            AutoReverse = false,
            Duration = TimeSpan.FromSeconds(1.3),
            EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut }
        };

        var daOff = new DoubleAnimation
        {
            From = 1d,
            To = 0.2d,
            AutoReverse = false,
            Duration = TimeSpan.FromSeconds(1.3),
            EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut }
        };

        _storyboardOn = new Storyboard();
        Storyboard.SetTarget(daOn, obj);
        Storyboard.SetTargetName(daOn, obj.Name);
        Storyboard.SetTargetProperty(daOn, "Opacity");
        _storyboardOn.Children.Add(daOn);

        _storyboardOff = new Storyboard();
        Storyboard.SetTarget(daOff, obj);
        Storyboard.SetTargetName(daOff, obj.Name);
        Storyboard.SetTargetProperty(daOff, "Opacity");
        _storyboardOff.Children.Add(daOff);
        _storyboardOff.Completed += StoryboardOffCompleted;
    }

    void StoryboardOffCompleted(object? sender, object e)
    {
        _timer?.Stop();
    }

    void AssociatedObject_GotFocus(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] {sender.GetType()} got focus.");

        if (_timer != null && _timer.IsEnabled)
        {
            Debug.WriteLine($"[INFO] Skipping StoryBoardOn since timer is running.");
            return;
        }

        var obj = sender as FrameworkElement;
        if (obj is null) { return; }

        if (obj.Visibility == Visibility.Visible)
            _storyboardOn?.Begin();
        else
            _storyboardOn?.SkipToFill(); //_storyboard.Stop();

        //AnimateUIElementOpacity(0.1, 1.0, TimeSpan.FromSeconds(2.0), obj);
        //AnimateUIElementScale(1.0, TimeSpan.FromSeconds(1.0), (UIElement)sender);
        if (obj.ActualHeight != double.NaN && obj.ActualHeight != 0)
            AnimateUIElementOffset(new Point(0, obj.ActualHeight), TimeSpan.FromSeconds(0.8), (UIElement)sender);
        else
            AnimateUIElementOffset(new Point(0, 600), TimeSpan.FromSeconds(0.8), (UIElement)sender);
    }

    void AssociatedObject_LostFocus(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] {sender.GetType()} lost focus.");

        //var obj = sender as FrameworkElement;
        //if (obj is null) { return; }
        //
        //if (obj.Visibility == Visibility.Visible)
        //    _storyboardOff?.Begin();
        //else
        //    _storyboardOff?.SkipToFill();
    }

    void AssociatedObject_Activated(object sender, WindowActivatedEventArgs args)
    {
    }

    /// <summary>
    /// Mock disposal routine.
    /// </summary>
    void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_storyboardOn != null)
        {
            _storyboardOn.Stop();
            _storyboardOn = null;
        }

        if (_storyboardOff != null)
        {
            _storyboardOff.Stop();
            _storyboardOff = null;
        }

        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }
    }

    /// <summary>
    /// Opacity animation using <see cref="Microsoft.UI.Composition.ScalarKeyFrameAnimation"/>
    /// </summary>
    void AnimateUIElementOpacity(double from, double to, TimeSpan duration, UIElement target, Microsoft.UI.Composition.AnimationDirection direction = Microsoft.UI.Composition.AnimationDirection.Normal)
    {
        var targetVisual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = targetVisual.Compositor;
        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Direction = direction;
        opacityAnimation.Duration = duration;
        opacityAnimation.Target = "Opacity";
        opacityAnimation.InsertKeyFrame(0.0f, (float)from);
        opacityAnimation.InsertKeyFrame(1.0f, (float)to);
        targetVisual.StartAnimation("Opacity", opacityAnimation);
    }

    /// <summary>
    /// Offset animation using <see cref="Microsoft.UI.Composition.Vector3KeyFrameAnimation"/>
    /// </summary>
    void AnimateUIElementOffset(Point to, TimeSpan duration, UIElement target)
    {
        var targetVisual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = targetVisual.Compositor;
        var linear = compositor.CreateLinearEasingFunction();
        var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = duration;
        offsetAnimation.Target = "Offset";
        offsetAnimation.InsertKeyFrame(0.0f, new Vector3((float)to.X, (float)to.Y, 0), linear);
        offsetAnimation.InsertKeyFrame(1.0f, new Vector3(0), linear);
        targetVisual.StartAnimation("Offset", offsetAnimation);
    }

    /// <summary>
    /// Scale animation using <see cref="Microsoft.UI.Composition.Vector3KeyFrameAnimation"/>
    /// </summary>
    void AnimateUIElementScale(double to, TimeSpan duration, UIElement target)
    {
        var targetVisual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = targetVisual.Compositor;
        var linear = compositor.CreateLinearEasingFunction();
        var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = duration;
        scaleAnimation.Target = "Scale";
        scaleAnimation.InsertKeyFrame(0.0f, new Vector3(0), linear);
        scaleAnimation.InsertKeyFrame(1.0f, new Vector3((float)to), linear);
        targetVisual.StartAnimation("Scale", scaleAnimation);
    }
}
