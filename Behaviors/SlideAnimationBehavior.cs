using System;
using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Xaml.Interactivity;

using Windows.Foundation;
using Windows.System;
using Windows.UI.Core.AnimationMetrics;

namespace Transparency.Behaviors;

/// <summary>
/// When the <see cref="FrameworkElement"/> is loaded the translation animation will be performed.
/// We'll consider sliding up to be transitioning into a usable state, while sliding down means to put away.
/// </summary>
public class SlideAnimationBehavior : Behavior<FrameworkElement>
{
    #region [Props]
    DispatcherTimer? _timer;

    /// <summary>
    /// Identifies the <see cref="Time"/> property for the animation.
    /// </summary>
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time),
        typeof(double),
        typeof(SlideAnimationBehavior),
        new PropertyMetadata(1.5d));

    /// <summary>
    /// Gets or sets the <see cref="TimeSpan"/> to run the animation for.
    /// </summary>
    public double Time
    {
        get => (double)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <summary>
    /// Identifies the direction property for the animation.
    /// </summary>
    public static readonly DependencyProperty DownProperty = DependencyProperty.Register(
        nameof(Down),
        typeof(bool),
        typeof(SlideAnimationBehavior),
        new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets the <see cref="TimeSpan"/> to run the animation for.
    /// </summary>
    public bool Down
    {
        get => (bool)GetValue(DownProperty);
        set => SetValue(DownProperty, value);
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

    /// <summary>
    /// <see cref="FrameworkElement"/> event.
    /// </summary>
    void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] FrameworkElement loaded at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");

        var obj = sender as FrameworkElement;
        if (obj is null) { return; }

        if (obj.ActualHeight != double.NaN && obj.ActualHeight != 0)
        {
            if (!Down)
            {
                AnimateUIElementOpacity(0, 1, TimeSpan.FromSeconds(Time), (UIElement)sender);
                AnimateUIElementOffset(new Point(0, obj.ActualHeight), TimeSpan.FromSeconds(Time), (UIElement)sender);
            }
            else
            {
                AnimateUIElementOpacity(1, 0, TimeSpan.FromSeconds(Time), (UIElement)sender);
                AnimateUIElementOffset(new Point(0, obj.ActualHeight), TimeSpan.FromSeconds(Time), (UIElement)sender, AnimationDirection.Reverse);
                // If the control happens to slide down over another control then the pointer's
                // hit test may become a problem, so we'll set the visibility property.
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(Time);
                _timer.Tick += (_, _) =>
                {
                    _timer.Stop();
                    ((UIElement)sender).Visibility = Visibility.Collapsed;
                };
                _timer.Start();
            }
        }
        else
        {
            if (!Down)
            {
                AnimateUIElementOpacity(0, 1, TimeSpan.FromSeconds(Time), (UIElement)sender);
                AnimateUIElementOffset(new Point(0, 250), TimeSpan.FromSeconds(Time), (UIElement)sender);
            }
            else
            {
                AnimateUIElementOpacity(1, 0, TimeSpan.FromSeconds(Time), (UIElement)sender);
                AnimateUIElementOffset(new Point(0, 250), TimeSpan.FromSeconds(Time), (UIElement)sender, AnimationDirection.Reverse);
                // If the control happens to slide down over another control then the pointer's
                // hit test may become a problem, so we'll set the visibility property.
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(Time);
                _timer.Tick += (_, _) =>
                {
                    _timer.Stop();
                    ((UIElement)sender).Visibility = Visibility.Collapsed;
                };
                _timer.Start();
            }
        }
    }


    /// <summary>
    /// <see cref="FrameworkElement"/> event.
    /// </summary>
    void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] FrameworkElement unloaded at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
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
