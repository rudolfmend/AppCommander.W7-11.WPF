using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppCommander.W7_11.WPF.Core
{
    // ===== SPOLOČNÉ DEFINÍCIE EVENT ARGS =====

    /// <summary>
    /// Event args pre aktiváciu okna - jednotná verzia
    /// </summary>
    public class WindowActivatedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    /// <summary>
    /// Event args pre zatvorenie okna - jednotná verzia
    /// </summary>
    public class WindowClosedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
        public object WindowTrackingInfo { get; internal set; }
    }

    /// <summary>
    /// Event args pre objavenie sa okna - jednotná verzia
    /// </summary>
    public class WindowAppearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public WindowType WindowType { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    /// <summary>
    /// Event args pre zmiznutie okna - jednotná verzia
    /// </summary>
    public class WindowDisappearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }    
}
