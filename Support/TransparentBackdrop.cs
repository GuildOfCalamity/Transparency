﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.UI.Xaml.Media;
using ICompositionSupportsSystemBackdrop = Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop;

using Windows.UI;
using Compositor = Windows.UI.Composition.Compositor;
using Microsoft.UI.Xaml.Hosting;

namespace Transparency.Support;

public class TransparentBackdrop : SystemBackdrop
{
    static Compositor Compositor => _Compositor.Value;
    static readonly Lazy<Compositor> _Compositor = new(() =>
    {
        WindowsSystemDispatcherQueueHelper.EnsureWindowsSystemDispatcherQueueController();
        return new Compositor();
    });

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, Microsoft.UI.Xaml.XamlRoot xamlRoot)
    {
        if (App.LocalConfig == null || string.IsNullOrEmpty(App.LocalConfig.background))
        {
            connectedTarget.SystemBackdrop = Compositor.CreateColorBrush(Color.FromArgb(0, 255, 255, 255));
        }
        else if (App.LocalConfig != null && App.LocalConfig.background.Length >= 8)
        {
            try
            {
                var a = App.LocalConfig?.background.Substring(0, 2);
                var r = App.LocalConfig?.background.Substring(2, 2);
                var g = App.LocalConfig?.background.Substring(4, 2);
                var b = App.LocalConfig?.background.Substring(6, 2);
                connectedTarget.SystemBackdrop = Compositor.CreateColorBrush(Color.FromArgb(Convert.ToByte(a, 16), Convert.ToByte(r, 16), Convert.ToByte(g, 16), Convert.ToByte(b, 16)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] OnTargetConnected: {ex.Message}");
                connectedTarget.SystemBackdrop = Compositor.CreateColorBrush(Color.FromArgb(0, 255, 255, 255));
            }
        }
        else
        {
            connectedTarget.SystemBackdrop = Compositor.CreateColorBrush(Color.FromArgb(20, 220, 20, 20));
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        disconnectedTarget.SystemBackdrop = null;
    }
}

public static class WindowsSystemDispatcherQueueHelper
{
    [StructLayout(LayoutKind.Sequential)]
    struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object? dispatcherQueueController);

    static object? m_dispatcherQueueController = null;
    public static void EnsureWindowsSystemDispatcherQueueController()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            return; // already exists, so use it

        if (m_dispatcherQueueController == null)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
            options.threadType = 2;    // DQTYPE_THREAD_CURRENT
            options.apartmentType = 2; // DQTAT_COM_STA

            _ = CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
        }
    }
}
