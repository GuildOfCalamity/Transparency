﻿using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity; // NuGet => Microsoft.Xaml.Behaviors.WinUI.Managed

namespace Transparency.Behaviors;

/// <summary>
/// Parts of this code borrowed from https://xamlbrewer.wordpress.com/2023/01/16/xaml-behaviors-and-winui-3/
/// Fixed nullability and added checks.
/// </summary>
/// <remarks>
/// The <b>AssociatedObject</b> is the inherited object from the
/// <see cref="Microsoft.Xaml.Interactivity.Behavior{T}"/> class 
/// which can represent any <see cref="DependencyObject"/>.
/// </remarks>
public class TypingPauseBehavior : Behavior<AutoSuggestBox>
{
    private DispatcherTimer? timer;

    /// <summary>
    /// Gets or sets the waiting time threshold in milliseconds.
    /// </summary>
    public int MinimumDelay { get; set; } = 500;

    /// <summary>
    /// Gets or sets the minimum search string threshold.
    /// </summary>
    public int MinimumCharacters { get; set; } = 2;

    #region [Use this if you prefer strongly typed events]
    //public delegate void EventHandler<in TSender, in TArgs>(TSender sender, TArgs args);
    //public event EventHandler<AutoSuggestBox, EventArgs>? TypingPaused;
    #endregion
    public event EventHandler? TypingPaused;

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.TextChanged += AssociatedObject_TextChanged;
        timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(MinimumDelay) };
        timer.Tick += OnTimerTick;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
        if (timer != null)
        {
            timer.Stop();
            timer.Tick -= OnTimerTick;
            timer = null;
        }
    }

    private void AssociatedObject_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if ((args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) && (sender.Text.Length >= MinimumCharacters))
            timer?.Start();
        else
            timer?.Stop();
    }

    private void OnTimerTick(object? sender, object e)
    {
        TypingPaused?.Invoke(AssociatedObject, new EventArgs());
        timer?.Stop();
    }
}
