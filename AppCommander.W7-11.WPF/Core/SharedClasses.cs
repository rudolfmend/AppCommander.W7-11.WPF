// SharedClasses.cs - Všetky pomocné triedy a event args na jednom mieste
using System;
using System.Collections.Generic;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    #region Event Args Classes

    public class UIChangeDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowState WindowState { get; set; }
        public UIChangeSet Changes { get; set; }
        public string ChangeType { get; set; }
        public string Description { get; set; }
    }

    public class NewWindowAppearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public WindowType WindowType { get; set; }
        public bool AutoAdded { get; set; }
    }

    public class ElementInteractionEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public UIElementSnapshot Element { get; set; }
        public InteractionType InteractionType { get; set; }
        public string ElementName { get; set; }
        public string Action { get; set; }
    }

    public class WindowAutoDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string Description { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    public class RecordingStateChangedEventArgs : EventArgs
    {
        public bool IsRecording { get; set; }
        public bool IsPaused { get; set; }
        public string SequenceName { get; set; }
    }

    public class CustomWindowAppearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public WindowType WindowType { get; set; }
    }

    public class CustomWindowDisappearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    public class ElementAddedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public UIElementSnapshot Element { get; set; }
    }

    public class ElementRemovedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string ElementIdentifier { get; set; }
    }

    public class ElementModifiedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public UIElementSnapshot Element { get; set; }
    }

    public class PatternDetectedEventArgs : EventArgs
    {
        public string PatternType { get; set; }
        public float Confidence { get; set; }
        public object PatternData { get; set; }
    }

    public class AnomalyDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string AnomalyType { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region Supporting Classes

    public class WindowState
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastSeen { get; set; }
        public UISnapshot LastUISnapshot { get; set; }
        public DateTime LastChangeDetected { get; set; }
    }

    public class WinUI3ApplicationAnalysis
    {
        public string ApplicationName { get; set; }
        public string Version { get; set; }
        public bool IsWinUI3 { get; set; }
    }

    public class UISnapshot
    {
        public IntPtr WindowHandle { get; set; }
        public DateTime CapturedAt { get; set; }
        public List<UIElementSnapshot> Elements { get; set; } = new List<UIElementSnapshot>();
    }

    public class UIElementSnapshot
    {
        public string Name { get; set; }
        public string AutomationId { get; set; }
        public string ClassName { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
        public string Text { get; set; } = "";
        public string Hash { get; set; } = "";
        public bool IsWinUI3Element { get; set; } = false;
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class UIChangeSet
    {
        public UISnapshot PreviousSnapshot { get; set; }
        public UISnapshot CurrentSnapshot { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool HasChanges { get; set; }
        public List<UIElementSnapshot> AddedElements { get; set; } = new List<UIElementSnapshot>();
        public List<UIElementSnapshot> RemovedElements { get; set; } = new List<UIElementSnapshot>();
        public List<(UIElementSnapshot Previous, UIElementSnapshot Current)> ModifiedElements { get; set; } = new List<(UIElementSnapshot, UIElementSnapshot)>();
        public string ChangeDescription { get; set; } = "";
    }

    public class UIElementInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string AutomationId { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
    }

    public class ElementUsageStats
    {
        public string ElementName { get; set; }
        public int UsageCount { get; set; }
        public DateTime LastUsed { get; set; }
        public string ElementType { get; set; }
    }

    #endregion

    #region Enums

    public enum InteractionType
    {
        ElementAppeared,
        ElementDisappeared,
        ElementClicked,
        ElementModified,
        ElementFocused,
        ElementValueChanged
    }

    #endregion

    #region Placeholder Classes (pre kompatibilitu)

    public class AutoWindowDetector
    {
        public event EventHandler<NewWindowDetectedEventArgs> NewWindowDetected;
        //public event EventHandler<WindowActivatedEventArgs> WindowActivated;
        public event EventHandler<WindowClosedEventArgs> WindowClosed;
        private bool isDetecting;
    }

    public class ElementChangeDetector
    {
        public event EventHandler<ElementAddedEventArgs> ElementAdded;
        public event EventHandler<ElementModifiedEventArgs> ElementModified;
        public event EventHandler<ElementRemovedEventArgs> ElementRemoved;
        private bool isDetecting;

        public void StartDetection()
        {
            isDetecting = true;
        }

        public void StopDetection()
        {
            isDetecting = false;
        }

        public void Dispose()
        {
            StopDetection();
        }
    }

    public class SmartUIAnalyzer
    {
        public event EventHandler<PatternDetectedEventArgs> PatternDetected;
        public event EventHandler<AnomalyDetectedEventArgs> AnomalyDetected;

        public void StartAnalysis() { }
        public void StopAnalysis() { }
        public void AnalyzeChanges(WindowState windowState, UIChangeSet changes) { }
        public void Dispose() { }
    }

    public class UIElementScanner
    {
        public event EventHandler ElementDisappeared;
        public event EventHandler ElementsChanged;
        public event EventHandler NewElementDetected;
        private bool isScanning;
    }

    public class WindowMonitor
    {
        public event EventHandler<CustomWindowAppearedEventArgs> WindowAppeared;
        public event EventHandler<CustomWindowDisappearedEventArgs> WindowDisappeared;
        //public event EventHandler<WindowActivatedEventArgs> WindowActivated;
        private bool isMonitoring;
    }

    #endregion
}
