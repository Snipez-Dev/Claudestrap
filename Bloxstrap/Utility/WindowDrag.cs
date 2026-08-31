using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Claudestrap.Utility
{
    /// <summary>
    /// Moves a borderless window by handing the drag to Windows, instead of
    /// <see cref="Window.DragMove"/>.
    ///
    /// DragMove pumps a nested message loop inside the WPF dispatcher for the whole
    /// duration of the drag. Input that arrives during it is processed against a window
    /// whose input state is mid-move, and WPF answers that by throwing
    /// NullReferenceExceptions out of its own input plumbing -- one per mouse message,
    /// so a single drag produces dozens. Telling Windows to run its standard caption
    /// drag skips WPF's loop entirely, which is what the window manager does for every
    /// ordinary title bar anyway.
    /// </summary>
    internal static class WindowDrag
    {
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 0x0002;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Starts dragging <paramref name="window"/> by its caption. Safe to call
        /// when the window has no handle yet or the button has already been released --
        /// it simply does nothing.</summary>
        public static void Begin(Window window)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;

                if (handle == IntPtr.Zero)
                    return;

                // Windows only takes over the drag if nothing else holds the mouse.
                ReleaseCapture();
                SendMessage(handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("WindowDrag::Begin", $"Could not start window drag: {ex.Message}");
            }
        }
    }
}
