// Dodatočné triedy potrebné pre kompatibilitu
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    // <summary>
    /// Event args pre aktivované okno
    /// </summary>
    public class WindowActivatedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        //public WindowTrackingInfo Window { get; set; }
        //public WindowTrackingInfo WindowInfo { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public DateTime ActivatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Event args pre zatvorené okno
    /// </summary>
    public class WindowClosedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo Window { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public DateTime ClosedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Event args pre automaticky detekované okno
    /// </summary>
    public class AutoWindowDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo Window { get; set; }
        public string Description { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public WindowType WindowType { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Event args pre vlastné okno ktoré sa objavilo
    /// </summary>
    public class CustomWindowAppearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public WindowType WindowType { get; set; }
        public DateTime AppearedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Event args pre vlastné okno ktoré zmizlo
    /// </summary>
    public class CustomWindowDisappearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public DateTime DisappearedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Event args pre pridaný element
    /// </summary>
    public class ElementAddedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public UIElementSnapshot Element { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }


    public class WindowTrackingData
    {
        //public WindowTrackingInfo Info { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsTracked { get; set; }
    }
}
