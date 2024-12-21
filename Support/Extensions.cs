// Ignore Spelling: Nullable

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Storage;

namespace Transparency.Support;

public static class Extensions
{
    /// <summary>
    /// Multiplies the given <see cref="TimeSpan"/> by the scalar amount provided.
    /// </summary>
    public static TimeSpan Multiply(this TimeSpan timeSpan, double scalar) => new TimeSpan((long)(timeSpan.Ticks * scalar));

    /// <summary>
    /// Returns the AppData path including the <paramref name="moduleName"/>.
    /// e.g. "C:\Users\UserName\AppData\Local\MenuDemo\Settings"
    /// </summary>
    public static string LocalApplicationDataFolder(string moduleName = "Settings")
    {
        var result = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}\\{moduleName}");
        return result;
    }

    /// <summary>
    /// Use this if you only have a root resource dictionary.
    /// var rdBrush = Extensions.GetResource<SolidColorBrush>("PrimaryBrush");
    /// </summary>
    public static T? GetResource<T>(string resourceName) where T : class
    {
        try
        {
            if (Application.Current.Resources.TryGetValue($"{resourceName}", out object value))
                return (T)value;
            else
                return default(T);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Use this if you have merged theme resource dictionaries.
    /// var darkBrush = Extensions.GetThemeResource<SolidColorBrush>("PrimaryBrush", ElementTheme.Dark);
    /// var lightBrush = Extensions.GetThemeResource<SolidColorBrush>("PrimaryBrush", ElementTheme.Light);
    /// </summary>
    public static T? GetThemeResource<T>(string resourceName, ElementTheme? theme) where T : class
    {
        try
        {
            if (theme == null) { theme = ElementTheme.Default; }
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            foreach (var item in dictionaries)
            {
                // Do we have any themes in this resource dictionary?
                if (item.ThemeDictionaries.Count > 0)
                {
                    if (theme == ElementTheme.Dark)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Dark", out var drd))
                        {
                            ResourceDictionary? dark = drd as ResourceDictionary;
                            if (dark != null)
                            {
                                Debug.WriteLine($"[INFO] Found dark theme resource dictionary.");
                                if (dark.TryGetValue($"{resourceName}", out var tmp))
                                    return (T)tmp;
                            }
                        }
                        else { Debug.WriteLine($"[WARNING] {nameof(ElementTheme.Dark)} theme was not found."); }
                    }
                    else if (theme == ElementTheme.Light)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Light", out var lrd))
                        {
                            ResourceDictionary? light = lrd as ResourceDictionary;
                            if (light != null)
                            {
                                Debug.WriteLine($"[INFO] Found light theme resource dictionary.");
                                if (light.TryGetValue($"{resourceName}", out var tmp))
                                    return (T)tmp;
                            }
                        }
                        else { Debug.WriteLine($"[WARNING] {nameof(ElementTheme.Light)} theme was not found."); }
                    }
                    else if (theme == ElementTheme.Default)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Default", out var drd))
                        {
                            ResourceDictionary? dflt = drd as ResourceDictionary;
                            if (dflt != null)
                            {
                                Debug.WriteLine($"[INFO] Found default theme resource dictionary.");
                                if (dflt.TryGetValue($"{resourceName}", out var tmp))
                                    return (T)tmp;
                            }
                        }
                        else { Debug.WriteLine($"[WARNING] {nameof(ElementTheme.Default)} theme was not found."); }
                    }
                    else
                    {
                        Debug.WriteLine($"[WARNING] No theme to match.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[WARNING] No theme dictionaries found.");
                }
            }
            return default(T);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static IconElement? GetIcon(string imagePath)
    {
        IconElement? result = null;

        try
        {
            result = imagePath.ToLowerInvariant().EndsWith(".png") ?
                        (IconElement)new BitmapIcon() { UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute), ShowAsMonochrome = false } :
                        (IconElement)new FontIcon() { Glyph = imagePath };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] GetIcon: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// int cvrt = (int)FluentIcon.MapPin;
    /// string icon = IntToUTF16(cvrt);
    /// https://stackoverflow.com/questions/71546789/the-u-escape-sequence-in-c-sharp
    /// </summary>
    public static string IntToUTF16(int value)
    {
        var builder = new StringBuilder();
        builder.Append((char)value);
        return builder.ToString();
    }

    public static async Task<SoftwareBitmap> LoadFromFile(StorageFile file)
    {
        SoftwareBitmap softwareBitmap;
        using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);
        }
        return softwareBitmap;
    }

    public static async Task<string> LoadText(string relativeFilePath)
    {
#if IS_UNPACKAGED
        var sourcePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location ?? System.IO.Directory.GetCurrentDirectory()), relativeFilePath));
        var file = await StorageFile.GetFileFromPathAsync(sourcePath);
#else
        Uri sourceUri = new Uri("ms-appx:///" + relativeFilePath);
        var file = await StorageFile.GetFileFromApplicationUriAsync(sourceUri);
#endif
        return await FileIO.ReadTextAsync(file);
    }

    public static async Task<IList<string>> LoadLines(string relativeFilePath)
    {
        string fileContents = await LoadText(relativeFilePath);
        return fileContents.Split(Environment.NewLine).ToList();
    }

    /// <summary>
    /// Creates a Windows Runtime asynchronous operation that returns the last element of the observable sequence.
    /// Upon cancellation of the asynchronous operation, the subscription to the source sequence will be disposed.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">Source sequence to expose as an asynchronous operation.</param>
    /// <returns>Windows Runtime asynchronous operation object that returns the last element of the observable sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static IAsyncOperation<TSource> ToAsyncOperation<TSource>(this IObservable<TSource> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        return AsyncInfo.Run(ct => source.ToTask(ct));
    }

    /// <summary>
    /// Returns a task that will receive the last value or the exception produced by the observable sequence.
    /// </summary>
    /// <typeparam name="TResult">The type of the elements in the source sequence.</typeparam>
    /// <param name="observable">Observable sequence to convert to a task.</param>
    /// <returns>A task that will receive the last element or the exception produced by the observable sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="observable"/> is <c>null</c>.</exception>
    public static Task<TResult> ToTask<TResult>(this IObservable<TResult> observable)
    {
        if (observable == null)
            throw new ArgumentNullException(nameof(observable));

        return observable.ToTask(new CancellationToken());
    }

    /// <summary>
    /// Returns a task that will receive the last value or the exception produced by the observable sequence.
    /// </summary>
    /// <typeparam name="TResult">The type of the elements in the source sequence.</typeparam>
    /// <param name="observable">Observable sequence to convert to a task.</param>
    /// <param name="state">The state to use as the underlying task's AsyncState.</param>
    /// <returns>A task that will receive the last element or the exception produced by the observable sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="observable"/> is <c>null</c>.</exception>
    public static Task<TResult> ToTask<TResult>(this IObservable<TResult> observable, object? state)
    {
        if (observable == null)
        {
            throw new ArgumentNullException(nameof(observable));
        }

        return observable.ToTask(new CancellationToken());
    }

    /// <summary>
    /// Returns a task that will receive the last value or the exception produced by the observable sequence.
    /// </summary>
    /// <typeparam name="TResult">The type of the elements in the source sequence.</typeparam>
    /// <param name="observable">Observable sequence to convert to a task.</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel the task, causing unsubscription from the observable sequence.</param>
    /// <returns>A task that will receive the last element or the exception produced by the observable sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="observable"/> is <c>null</c>.</exception>
    public static Task<TResult> ToTask<TResult>(this IObservable<TResult> observable, CancellationToken cancellationToken)
    {
        if (observable == null)
        {
            throw new ArgumentNullException(nameof(observable));
        }

        return observable.ToTask(cancellationToken);
    }

    /// <summary>
    /// ToTask Helper Extension
    /// ((Func<double, double, double>)Math.Pow).ToTask(2d, 2d).ContinueWith(x => ((Action<string, object[]>) Console.WriteLine).ToTask("Power value: {0}", new object[] { x.Result })).Wait();
    /// </summary>
    public static Task<TResult> ToTask<TResult>(this Func<TResult> function, AsyncCallback? callback = default(AsyncCallback), object? @object = default(object), TaskCreationOptions creationOptions = default(TaskCreationOptions), TaskScheduler? scheduler = default(TaskScheduler))
    {
        return Task<TResult>.Factory.FromAsync(function.BeginInvoke(callback, @object), function.EndInvoke, creationOptions, (scheduler ?? TaskScheduler.Current) ?? TaskScheduler.Default);
    }
    public static Task<TResult> ToTask<T, TResult>(this Func<T, TResult> function, T arg, AsyncCallback? callback = default(AsyncCallback), object @object = default(object), TaskCreationOptions creationOptions = default(TaskCreationOptions), TaskScheduler? scheduler = default(TaskScheduler))
    {
        return Task<TResult>.Factory.FromAsync(function.BeginInvoke(arg, callback, @object), function.EndInvoke, creationOptions, (scheduler ?? TaskScheduler.Current) ?? TaskScheduler.Default);
    }

    /// <summary>
    /// Task extension to add a timeout.
    /// </summary>
    /// <returns>The task with timeout.</returns>
    /// <param name="task">Task.</param>
    /// <param name="timeoutInMilliseconds">Timeout duration in Milliseconds.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public async static Task<T> WithTimeout<T>(this Task<T> task, int timeoutInMilliseconds)
    {
        var retTask = await Task.WhenAny(task, Task.Delay(timeoutInMilliseconds))
            .ConfigureAwait(false);

        #pragma warning disable CS8603 // Possible null reference return.
        return retTask is Task<T> ? task.Result : default;
        #pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>
    /// Task extension to add a timeout.
    /// </summary>
    /// <returns>The task with timeout.</returns>
    /// <param name="task">Task.</param>
    /// <param name="timeout">Timeout Duration.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public static Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout) => WithTimeout(task, (int)timeout.TotalMilliseconds);

    #pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
    /// <summary>
    /// Attempts to await on the task and catches exception
    /// </summary>
    /// <param name="task">Task to execute</param>
    /// <param name="onException">What to do when method has an exception</param>
    /// <param name="continueOnCapturedContext">If the context should be captured.</param>
    public static async void SafeFireAndForget(this Task task, Action<Exception>? onException = null, bool continueOnCapturedContext = false)
    #pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (Exception ex) when (onException != null)
        {
            onException.Invoke(ex);
        }
    }

    /// <summary>
    /// Chainable task helper.
    /// var result = await SomeLongAsyncFunction().WithCancellation(cts.Token);
    /// </summary>
    /// <typeparam name="TResult">the type of task result</typeparam>
    /// <returns><see cref="Task"/>TResult</returns>
    public static Task<TResult> WithCancellation<TResult>(this Task<TResult> task, CancellationToken cancelToken)
    {
        TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
        CancellationTokenRegistration reg = cancelToken.Register(() => tcs.TrySetCanceled());
        task.ContinueWith(ant =>
        {
            reg.Dispose(); // NOTE: it's important to dispose of CancellationTokenRegistrations or they will hand around in memory until the application closes
            if (ant.IsCanceled)
                tcs.TrySetCanceled();
            else if (ant.IsFaulted)
                tcs.TrySetException(ant.Exception.InnerException);
            else
                tcs.TrySetResult(ant.Result);
        });
        return tcs.Task;  // Return the TaskCompletionSource result
    }

    public static Task<T> WithAllExceptions<T>(this Task<T> task)
    {
        TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

        task.ContinueWith(ignored =>
        {
            switch (task.Status)
            {
                case TaskStatus.Canceled:
                    Debug.WriteLine($"[TaskStatus.Canceled]");
                    tcs.SetCanceled();
                    break;
                case TaskStatus.RanToCompletion:
                    tcs.SetResult(task.Result);
                    Debug.WriteLine($"[TaskStatus.RanToCompletion]: {task.Result}");
                    break;
                case TaskStatus.Faulted:
                    // SetException will automatically wrap the original AggregateException in another
                    // one. The new wrapper will be removed in TaskAwaiter, leaving the original intact.
                    Debug.WriteLine($"[TaskStatus.Faulted]: {task.Exception?.Message}");
                    tcs.SetException(task.Exception ?? new Exception("Exception object was null"));
                    break;
                default:
                    Debug.WriteLine($"[TaskStatus.Invalid]: Continuation called illegally.");
                    tcs.SetException(new InvalidOperationException("Continuation called illegally."));
                    break;
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Task.Factory.StartNew (() => { throw null; }).IgnoreExceptions();
    /// </summary>
    public static void IgnoreExceptions(this Task task, bool logEx = false)
    {
        task.ContinueWith(t =>
        {
            AggregateException ignore = t.Exception;

            ignore?.Flatten().Handle(ex =>
            {
                if (logEx)
                    Debug.WriteLine("Exception type: {0}\r\nException Message: {1}", ex.GetType(), ex.Message);
                return true; // don't re-throw
            });

        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public static Task ContinueWithState<TState>(this Task task, Action<Task, TState> continuationAction, TState state, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions)
    {
        return task.ContinueWith(
            (t, tupleObject) =>
            {
                var (closureAction, closureState) = ((Action<Task, TState>, TState))tupleObject!;

                closureAction(t, closureState);
            },
            (continuationAction, state),
            cancellationToken,
            continuationOptions,
            TaskScheduler.Default);
    }

    public static Task ContinueWithState<TResult, TState>(this Task<TResult> task, Action<Task<TResult>, TState> continuationAction, TState state, CancellationToken cancellationToken)
    {
        return task.ContinueWith(
            (t, tupleObject) =>
            {
                var (closureAction, closureState) = ((Action<Task<TResult>, TState>, TState))tupleObject!;

                closureAction(t, closureState);
            },
            (continuationAction, state),
            cancellationToken);
    }

    public static Task ContinueWithState<TResult, TState>(this Task<TResult> task, Action<Task<TResult>, TState> continuationAction, TState state, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions)
    {
        return task.ContinueWith(
            (t, tupleObject) =>
            {
                var (closureAction, closureState) = ((Action<Task<TResult>, TState>, TState))tupleObject!;

                closureAction(t, closureState);
            },
            (continuationAction, state),
            cancellationToken,
            continuationOptions,
            TaskScheduler.Default);
    }

    public static bool ImplementsInterface(this Type baseType, Type interfaceType) => baseType.GetInterfaces().Any(interfaceType.Equals);

    public static void PostWithComplete<T>(this SynchronizationContext context, Action<T> action, T state)
    {
        context.OperationStarted();
        context.Post(o => {
                try { action((T)o!); }
                finally { context.OperationCompleted(); }
            },
            state
        );
    }

    public static void PostWithComplete(this SynchronizationContext context, Action action)
    {
        context.OperationStarted();
        context.Post(_ => {
                try { action(); }
                finally { context.OperationCompleted(); }
            },
            null
        );
    }

    /// <summary>
    /// Helper function to calculate an element's rectangle in root-relative coordinates.
    /// </summary>
    public static Windows.Foundation.Rect GetElementRect(this Microsoft.UI.Xaml.FrameworkElement element)
    {
        try
        {
            Microsoft.UI.Xaml.Media.GeneralTransform transform = element.TransformToVisual(null);
            Windows.Foundation.Point point = transform.TransformPoint(new Windows.Foundation.Point());
            return new Windows.Foundation.Rect(point, new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight));
        }
        catch (Exception)
        {
            return new Windows.Foundation.Rect(0, 0, 0, 0);
        }
    }

    public static string SeparateCamelCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        StringBuilder result = new StringBuilder();
        result.Append(input[0]);

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
                result.Append(' ');

            result.Append(input[i]);
        }

        return result.ToString();
    }

    public static int CompareName(this Object obj1, Object obj2)
    {
        if (obj1 is null && obj2 is null)
            return 0;

        PropertyInfo? pi1 = obj1 as PropertyInfo;
        if (pi1 is null)
            return -1;

        PropertyInfo? pi2 = obj2 as PropertyInfo;
        if (pi2 is null)
            return 1;

        return String.Compare(pi1.Name, pi2.Name);
    }

    /// <summary>
    /// Finds the contrast ratio.
    /// This is helpful for determining if one control's foreground and another control's background will be hard to distinguish.
    /// https://www.w3.org/WAI/GL/wiki/Contrast_ratio
    /// (L1 + 0.05) / (L2 + 0.05), where
    /// L1 is the relative luminance of the lighter of the colors, and
    /// L2 is the relative luminance of the darker of the colors.
    /// </summary>
    /// <param name="first"><see cref="Windows.UI.Color"/></param>
    /// <param name="second"><see cref="Windows.UI.Color"/></param>
    /// <returns>ratio between relative luminance</returns>
    public static double CalculateContrastRatio(Windows.UI.Color first, Windows.UI.Color second)
    {
        double relLuminanceOne = GetRelativeLuminance(first);
        double relLuminanceTwo = GetRelativeLuminance(second);
        return (Math.Max(relLuminanceOne, relLuminanceTwo) + 0.05) / (Math.Min(relLuminanceOne, relLuminanceTwo) + 0.05);
    }

    /// <summary>
    /// Gets the relative luminance.
    /// https://www.w3.org/WAI/GL/wiki/Relative_luminance
    /// For the sRGB colorspace, the relative luminance of a color is defined as L = 0.2126 * R + 0.7152 * G + 0.0722 * B
    /// </summary>
    /// <param name="c"><see cref="Windows.UI.Color"/></param>
    /// <remarks>This is mainly used by <see cref="Helpers.CalculateContrastRatio(Color, Color)"/></remarks>
    public static double GetRelativeLuminance(Windows.UI.Color c)
    {
        double rSRGB = c.R / 255.0;
        double gSRGB = c.G / 255.0;
        double bSRGB = c.B / 255.0;

        // WebContentAccessibilityGuideline 2.x definition was 0.03928 (incorrect)
        // WebContentAccessibilityGuideline 3.x definition is 0.04045 (correct)
        double r = rSRGB <= 0.04045 ? rSRGB / 12.92 : Math.Pow(((rSRGB + 0.055) / 1.055), 2.4);
        double g = gSRGB <= 0.04045 ? gSRGB / 12.92 : Math.Pow(((gSRGB + 0.055) / 1.055), 2.4);
        double b = bSRGB <= 0.04045 ? bSRGB / 12.92 : Math.Pow(((bSRGB + 0.055) / 1.055), 2.4);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// Calculates the linear interpolated Color based on the given Color values.
    /// </summary>
    /// <param name="colorFrom">Source Color.</param>
    /// <param name="colorTo">Target Color.</param>
    /// <param name="amount">Weight given to the target color.</param>
    /// <returns>Linear Interpolated Color.</returns>
    public static Windows.UI.Color Lerp(this Windows.UI.Color colorFrom, Windows.UI.Color colorTo, float amount)
    {
        // Convert colorFrom components to lerp-able floats
        float sa = colorFrom.A, sr = colorFrom.R, sg = colorFrom.G, sb = colorFrom.B;

        // Convert colorTo components to lerp-able floats
        float ea = colorTo.A, er = colorTo.R, eg = colorTo.G, eb = colorTo.B;

        // lerp the colors to get the difference
        byte a = (byte)Math.Max(0, Math.Min(255, sa.Lerp(ea, amount))),
             r = (byte)Math.Max(0, Math.Min(255, sr.Lerp(er, amount))),
             g = (byte)Math.Max(0, Math.Min(255, sg.Lerp(eg, amount))),
             b = (byte)Math.Max(0, Math.Min(255, sb.Lerp(eb, amount)));

        // return the new color
        return Windows.UI.Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// Darkens the color by the given percentage using lerp.
    /// </summary>
    /// <param name="color">Source color.</param>
    /// <param name="amount">Percentage to darken. Value should be between 0 and 1.</param>
    /// <returns>Color</returns>
    public static Windows.UI.Color DarkerBy(this Windows.UI.Color color, float amount)
    {
        return color.Lerp(Colors.Black, amount);
    }

    /// <summary>
    /// Lightens the color by the given percentage using lerp.
    /// </summary>
    /// <param name="color">Source color.</param>
    /// <param name="amount">Percentage to lighten. Value should be between 0 and 1.</param>
    /// <returns>Color</returns>
    public static Windows.UI.Color LighterBy(this Windows.UI.Color color, float amount)
    {
        return color.Lerp(Colors.White, amount);
    }

    /// <summary>
    /// Clamping function for any value of type <see cref="IComparable{T}"/>.
    /// </summary>
    /// <param name="val">initial value</param>
    /// <param name="min">lowest range</param>
    /// <param name="max">highest range</param>
    /// <returns>clamped value</returns>
    public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
    {
        return val.CompareTo(min) < 0 ? min : (val.CompareTo(max) > 0 ? max : val);
    }

    /// <summary>
    /// Linear interpolation for a range of floats.
    /// </summary>
    public static float Lerp(this float start, float end, float amount = 0.5F) => start + (end - start) * amount;
    public static float LogLerp(this float start, float end, float percent, float logBase = 1.2F) => start + (end - start) * (float)Math.Log(percent, logBase);

    /// <summary>
    /// Similar to <see cref="GetReadableTime(TimeSpan)"/>.
    /// </summary>
    /// <param name="timeSpan"><see cref="TimeSpan"/></param>
    /// <returns>formatted text</returns>
    public static string ToReadableString(this TimeSpan span)
    {
        var parts = new StringBuilder();
        if (span.Days > 0)
            parts.Append($"{span.Days} day{(span.Days == 1 ? string.Empty : "s")} ");
        if (span.Hours > 0)
            parts.Append($"{span.Hours} hour{(span.Hours == 1 ? string.Empty : "s")} ");
        if (span.Minutes > 0)
            parts.Append($"{span.Minutes} minute{(span.Minutes == 1 ? string.Empty : "s")} ");
        if (span.Seconds > 0)
            parts.Append($"{span.Seconds} second{(span.Seconds == 1 ? string.Empty : "s")} ");
        if (span.Milliseconds > 0)
            parts.Append($"{span.Milliseconds} millisecond{(span.Milliseconds == 1 ? string.Empty : "s")} ");

        if (parts.Length == 0) // result was less than 1 millisecond
            return $"{span.TotalMilliseconds:N4} milliseconds"; // similar to span.Ticks
        else
            return parts.ToString().Trim();
    }

    /// <summary>
    /// This should only be used on instantiated objects, not static objects.
    /// </summary>
    public static string ToStringDump<T>(this T obj)
    {
        const string Seperator = "\r\n";
        const System.Reflection.BindingFlags BindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

        if (obj is null)
            return string.Empty;

        try
        {
            var objProperties =
                from property in obj?.GetType().GetProperties(BindingFlags)
                where property.CanRead
                select string.Format("{0} : {1}", property.Name, property.GetValue(obj, null));

            return string.Join(Seperator, objProperties);
        }
        catch (Exception ex)
        {
            return $"⇒ Probably a non-instanced object: {ex.Message}";
        }
    }

    /// <summary>
    /// var stack = GeneralExtensions.GetStackTrace(new StackTrace());
    /// </summary>
    public static string GetStackTrace(StackTrace st)
    {
        string result = string.Empty;
        for (int i = 0; i < st.FrameCount; i++)
        {
            StackFrame? sf = st.GetFrame(i);
            result += sf?.GetMethod() + " <== ";
        }
        return result;
    }

    public static string Flatten(this Exception? exception)
    {
        var sb = new StringBuilder();
        while (exception != null)
        {
            sb.AppendLine(exception.Message);
            sb.AppendLine(exception.StackTrace);
            exception = exception.InnerException;
        }
        return sb.ToString();
    }

    public static string DumpFrames(this Exception exception)
    {
        var sb = new StringBuilder();
        var st = new StackTrace(exception, true);
        var frames = st.GetFrames();
        foreach (var frame in frames)
        {
            if (frame != null)
            {
                if (frame.GetFileLineNumber() < 1)
                    continue;

                sb.Append($"File: {frame.GetFileName()}")
                  .Append($", Method: {frame.GetMethod()?.Name}")
                  .Append($", LineNumber: {frame.GetFileLineNumber()}")
                  .Append($"{Environment.NewLine}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// var collection = new[] { 10, 20, 30 };
    /// collection.ForEach(Debug.WriteLine);
    /// </summary>
    public static void ForEach<T>(this IEnumerable<T> ie, Action<T> action)
    {
        foreach (var i in ie)
        {
            try
            {
                action(i);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] ForEach: {ex.Message}");
            }
        }
    }

    public static IEnumerable<T> JoinLists<T>(this IEnumerable<T> list1, IEnumerable<T> list2)
    {
        var joined = new[] { list1, list2 }.Where(x => x != null).SelectMany(x => x);
        return joined ?? Enumerable.Empty<T>();
    }

    public static IEnumerable<T> JoinLists<T>(this IEnumerable<T> list1, IEnumerable<T> list2, IEnumerable<T> list3)
    {
        var joined = new[] { list1, list2, list3 }.Where(x => x != null).SelectMany(x => x);
        return joined ?? Enumerable.Empty<T>();
    }

    public static IEnumerable<T> JoinMany<T>(params IEnumerable<T>[] array)
    {
        var final = array.Where(x => x != null).SelectMany(x => x);
        return final ?? Enumerable.Empty<T>();
    }

    public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
    {
        if (target == null) { throw new ArgumentNullException(nameof(target)); }
        if (source == null) { throw new ArgumentNullException(nameof(source)); }
        foreach (var element in source) { target.Add(element); }
    }

    public static string NameOf(this object obj) => $"{obj.GetType().Name} => {obj.GetType().BaseType?.Name}";
    public static int MapValue(this int val, int inMin, int inMax, int outMin, int outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
    public static float MapValue(this float val, float inMin, float inMax, float outMin, float outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
    public static double MapValue(this double val, double inMin, double inMax, double outMin, double outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;

    public static T? DeserializeFromFile<T>(string filePath, ref string error)
    {
        try
        {
            string jsonString = System.IO.File.ReadAllText(filePath);
            T? result = System.Text.Json.JsonSerializer.Deserialize<T>(jsonString);
            error = string.Empty;
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] {nameof(DeserializeFromFile)}: {ex.Message}");
            error = ex.Message;
            return default;
        }
    }

    public static bool SerializeToFile<T>(T obj, string filePath, ref string error)
    {
        if (obj == null || string.IsNullOrEmpty(filePath))
            return false;

        try
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(obj);
            System.IO.File.WriteAllText(filePath, jsonString);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] {nameof(SerializeToFile)}: {ex.Message}");
            error = ex.Message;
            return false;
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue CastTo<TValue>(TValue value) where TValue : unmanaged
    {
        return (TValue)(object)value;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue? CastToNullable<TValue>(TValue? value) where TValue : unmanaged
    {
        if (value is null)
            return null;

        TValue validValue = value.GetValueOrDefault();
        return (TValue)(object)validValue;
    }
}
