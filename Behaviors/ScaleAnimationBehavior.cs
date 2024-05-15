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
public class ScaleAnimationBehavior : Behavior<FrameworkElement>
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

    /// <summary>
    /// <see cref="FrameworkElement"/> event.
    /// </summary>
    void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] {sender.GetType().Name} loaded at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");

        var obj = sender as FrameworkElement;
        if (obj is null) { return; }

        Debug.WriteLine($"[INFO] Scale animation will run for {Seconds} seconds.");
        AnimateUIElementScale(Final, TimeSpan.FromSeconds(Seconds), (UIElement)sender);
    }

    /// <summary>
    /// <see cref="FrameworkElement"/> event.
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
