using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    #region Enums

    /// <summary>
    /// Unified CommandType enum -
    /// Obsahuje VŠETKY potrebné typy príkazov pre celý projekt
    /// </summary>
    public enum CommandType
    {
        // Základné akcie  
        Click,
        SetText,
        KeyPress,
        Wait,
        Comment,

        // Mouse akcie 
        MouseClick,
        DoubleClick,
        RightClick,

        // Text akcie (z Command.cs)
        TypeText,

        Loop,           // Všeobecný loop 
        LoopStart,      // Začiatok loop-u 
        LoopEnd,        // Koniec loop-u  

        // Window a Focus operácie  
        WindowSwitch,
        ElementFocus,

        Other
    }

    /// <summary>
    /// Typ okna pre klasifikáciu
    /// </summary>
    public enum WindowType
    {
        MainWindow,
        Dialog,
        MessageBox,
        ChildWindow,
        PopupWindow,
        ToolWindow,
        Unknown
    }

    /// <summary>
    /// Priorita sledovania okna
    /// </summary>
    public enum WindowTrackingPrioritySharedClasses
    {
        Low,
        Medium,
        High,
        Critical,
        Primary
    }

    /// <summary>
    /// Typ interakcie s elementom
    /// </summary>
    public enum InteractionType
    {
        ElementAppeared,
        ElementDisappeared,
        ElementClicked,
        ElementModified,
        ElementFocused,
        ElementValueChanged
    }

    /// <summary>
    /// Citlivosť detekcie
    /// </summary>
    public enum DetectionSensitivity
    {
        Low,
        Medium,
        High,
        VeryHigh
    }

    #endregion

    #region Core Data Classes

    /// <summary>
    /// Informácie o sledovanom okne - .NET Framework 4.8 compatible
    /// </summary>
    public class WindowTrackingInfo
    {
        public WindowTrackingInfo()
        {
            Title = string.Empty;
            ProcessName = string.Empty;
            ClassName = string.Empty;
            IsVisible = true;
            DetectedAt = DateTime.Now;
            WindowType = WindowType.MainWindow;
            ProcessId = 0;
            IsActive = true;
            IsModal = false;
            LastActivated = DateTime.Now;
            Priority = WindowTrackingPrioritySharedClasses.Medium;
        }

        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
        public string ClassName { get; set; }
        public bool IsVisible { get; set; }
        public DateTime DetectedAt { get; set; }
        public WindowType WindowType { get; set; }
        public int ProcessId { get; set; }
        public bool IsActive { get; set; }
        public bool IsModal { get; set; }
        public DateTime LastActivated { get; set; }
        public WindowTrackingPrioritySharedClasses Priority { get; set; }
        public bool IsEnabled { get; set; } = true; 

        public override string ToString()
        {
            return string.Format("{0} - {1}", ProcessName, Title);
        }
    }

    /// <summary>
    /// WinUI3 Bridge informácie - .NET Framework 4.8 compatible
    /// </summary>
    public class WinUI3BridgeInfo
    {
        public WinUI3BridgeInfo()
        {
            BridgeName = string.Empty;
            BridgeHandle = IntPtr.Zero;
            IsAccessible = false;
            Version = string.Empty;
        }

        public string BridgeName { get; set; }
        public IntPtr BridgeHandle { get; set; }
        public bool IsAccessible { get; set; }
        public string Version { get; set; }


        // tieto property treba ešte skontrolovať
        public System.Drawing.Point Position { get; set; }
        public System.Drawing.Size Size { get; set; }
        public bool IsVisible { get; set; }
        public bool IsEnabled { get; set; }
        public int ChildCount { get; set; }
        public List<string> SupportedPatterns { get; set; } = new List<string>();
        public List<WinUI3ElementInfo> MeaningfulElements { get; set; } = new List<WinUI3ElementInfo>();
        public string ErrorMessage { get; set; } = "";
    }

    // Pridajte tieto definície do SharedClasses.cs alebo do samostatného súboru:

    namespace AppCommander.W7_11.WPF.Core
    {
        /// <summary>
        /// Reprezentuje pár modifikovaných elementov (predošlý a aktuálny)
        /// </summary>
        public class ModifiedElementPair
        {
            public UIElementSnapshot Previous { get; set; }
            public UIElementSnapshot Current { get; set; }

            public ModifiedElementPair(UIElementSnapshot previous, UIElementSnapshot current)
            {
                Previous = previous;
                Current = current;
            }

            // Konštrukcia z tuple
            public static implicit operator ModifiedElementPair((UIElementSnapshot Previous, UIElementSnapshot Current) tuple)
            {
                return new ModifiedElementPair(tuple.Previous, tuple.Current);
            }
        }

        /// <summary>
        /// Sada zmien v UI
        /// </summary>
        public class UIChangeSet
        {
            public UISnapshot PreviousSnapshot { get; set; }
            public UISnapshot CurrentSnapshot { get; set; }
            public DateTime DetectedAt { get; set; }
            public bool HasChanges { get; set; }

            public List<UIElementSnapshot> AddedElements { get; set; } = new List<UIElementSnapshot>();
            public List<UIElementSnapshot> RemovedElements { get; set; } = new List<UIElementSnapshot>();
            public List<ModifiedElementPair> ModifiedElements { get; set; } = new List<ModifiedElementPair>();
        }

        /// <summary>
        /// UI Snapshot pre zachytenie stavu UI v danom čase
        /// </summary>
        public class UISnapshot
        {
            public IntPtr WindowHandle { get; set; }
            public DateTime CapturedAt { get; set; }
            public List<UIElementSnapshot> Elements { get; set; } = new List<UIElementSnapshot>();
        }

        /// <summary>
        /// Stav okna pre automatické monitorovanie
        /// </summary>
        public class WindowState
        {
            public IntPtr WindowHandle { get; set; }
            public string Title { get; set; } = "";
            public string ProcessName { get; set; } = "";
            public WindowTrackingPrioritySharedClasses Priority { get; set; }
            public DateTime AddedAt { get; set; }
            public DateTime LastActivated { get; set; }
            public DateTime? ClosedAt { get; set; }
            public DateTime LastChangeDetected { get; set; }
            public bool IsActive { get; set; } = true;
            public UISnapshot LastUISnapshot { get; set; }
            public List<UIChangeSet> ChangeHistory { get; set; } = new List<UIChangeSet>();
        }

        /// <summary>
        /// Priorita sledovania okna pre SharedClasses
        /// </summary>
        public enum WindowTrackingPrioritySharedClasses
        {
            Low,
            Medium,
            High,
            Critical,
            Primary
        }
    }

    /// <summary>
    /// WinUI3 Element informácie s pozíciou - .NET Framework 4.8 compatible
    /// </summary>
    public class WinUI3ElementInfo
    {
        public WinUI3ElementInfo()
        {
            Name = string.Empty;
            AutomationId = string.Empty;
            ElementType = string.Empty;
            ControlType = string.Empty;
            IsInteractive = false;
            AccessMethod = string.Empty;
            Text = string.Empty;
            Position = null;
            IsEnabled = false;
            IsVisible = false;
        }

        public string Name { get; set; }
        public string AutomationId { get; set; }
        public string ElementType { get; set; }
        public string ControlType { get; set; }
        public bool IsInteractive { get; set; }
        public string AccessMethod { get; set; }
        public string Text { get; set; }
        public Point? Position { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
    }

    /// <summary>
    /// WinUI3 Application Analysis - .NET Framework 4.8 compatible
    /// </summary>
    public class WinUI3ApplicationAnalysis
    {
        public WinUI3ApplicationAnalysis()
        {
            IsSuccessful = false;
            ErrorMessage = string.Empty;
            WindowTitle = string.Empty;
            WindowClass = string.Empty;
            BridgeCount = 0;
            Bridges = new List<WinUI3BridgeInfo>();
            InteractiveElements = new List<WinUI3ElementInfo>();
            Recommendations = new List<string>();
            ApplicationName = string.Empty;
            Version = string.Empty;
            IsWinUI3 = false;
        }

        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
        public string WindowTitle { get; set; }
        public string WindowClass { get; set; }
        public int BridgeCount { get; set; }
        public List<WinUI3BridgeInfo> Bridges { get; set; }
        public List<WinUI3ElementInfo> InteractiveElements { get; set; }
        public List<string> Recommendations { get; set; }
        public string ApplicationName { get; set; }
        public string Version { get; set; }
        public bool IsWinUI3 { get; set; }

        // Alias pre kompatibilitu
        public bool IsWinUI3Application
        {
            get { return IsWinUI3; }
        }
    }

    #endregion

    #region Base Event Args Classes

    /// <summary>
    /// Základná trieda pre window-related event args - .NET Framework 4.8 compatible
    /// </summary>
    public abstract class WindowEventArgsBase : EventArgs
    {
        private readonly IntPtr _windowHandle;
        private readonly string _windowTitle;
        private readonly string _processName;
        private readonly DateTime _timestamp;

        protected WindowEventArgsBase(IntPtr windowHandle, string windowTitle, string processName)
        {
            _windowHandle = windowHandle;
            _windowTitle = windowTitle ?? string.Empty;
            _processName = processName ?? string.Empty;
            _timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get { return _windowHandle; } }
        public string WindowTitle { get { return _windowTitle; } }
        public string ProcessName { get { return _processName; } }
        public DateTime Timestamp { get { return _timestamp; } }
    }

    /// <summary>
    /// Základná trieda pre UI element-related event args - .NET Framework 4.8 compatible
    /// </summary>
    public abstract class UIElementEventArgsBase : EventArgs
    {
        private readonly IntPtr _windowHandle;
        private readonly UIElementSnapshot _element;
        private readonly DateTime _timestamp;

        protected UIElementEventArgsBase(IntPtr windowHandle, UIElementSnapshot element)
        {
            _windowHandle = windowHandle;
            _element = element;
            _timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get { return _windowHandle; } }
        public UIElementSnapshot Element { get { return _element; } }
        public DateTime Timestamp { get { return _timestamp; } }
    }

    #endregion

    #region Window Event Args

    /// <summary>
    /// Event args pre aktivované okno - .NET Framework 4.8 compatible
    /// </summary>
    public class WindowActivatedEventArgs : WindowEventArgsBase
    {
        private readonly WindowTrackingInfo _windowInfo;

        public WindowActivatedEventArgs(IntPtr windowHandle, string windowTitle, string processName)
            : base(windowHandle, windowTitle, processName)
        {
            _windowInfo = null;
        }

        public WindowActivatedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo != null ? windowInfo.WindowHandle : IntPtr.Zero,
                   windowInfo != null ? windowInfo.Title : null,
                   windowInfo != null ? windowInfo.ProcessName : null)
        {
            _windowInfo = windowInfo;
        }

        public WindowTrackingInfo WindowInfo { get { return _windowInfo; } }
        public DateTime ActivatedAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre zatvorené okno - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class WindowClosedEventArgs : WindowEventArgsBase
    {
        private readonly WindowTrackingInfo _windowInfo;
        private readonly WindowTrackingInfo _windowTrackingInfo;

        public WindowClosedEventArgs(IntPtr windowHandle, string windowTitle, string processName)
            : base(windowHandle, windowTitle, processName)
        {
            _windowInfo = null;
            _windowTrackingInfo = null;
        }

        public WindowClosedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo != null ? windowInfo.WindowHandle : IntPtr.Zero,
                   windowInfo != null ? windowInfo.Title : null,
                   windowInfo != null ? windowInfo.ProcessName : null)
        {
            _windowInfo = windowInfo;
            _windowTrackingInfo = windowInfo;
        }

        public WindowTrackingInfo WindowInfo { get { return _windowInfo; } }
        public WindowTrackingInfo WindowTrackingInfo { get { return _windowTrackingInfo; } }
        public WindowTrackingInfo Window { get { return WindowInfo; } }
        public DateTime ClosedAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre nové detekované okno - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class NewWindowDetectedEventArgs : WindowEventArgsBase
    {
        private readonly WindowTrackingInfo _windowInfo;
        private readonly WindowType _windowType;

        public NewWindowDetectedEventArgs(IntPtr windowHandle, string windowTitle, string processName, WindowType windowType)
            : base(windowHandle, windowTitle, processName)
        {
            _windowType = windowType;
            _windowInfo = null;
        }

        public NewWindowDetectedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo != null ? windowInfo.WindowHandle : IntPtr.Zero,
                   windowInfo != null ? windowInfo.Title : null,
                   windowInfo != null ? windowInfo.ProcessName : null)
        {
            _windowInfo = windowInfo;
            _windowType = windowInfo != null ? windowInfo.WindowType : WindowType.MainWindow;
        }

        public WindowTrackingInfo WindowInfo { get { return _windowInfo; } }
        public WindowType WindowType { get { return _windowType; } }
        public WindowTrackingInfo Window { get { return WindowInfo; } }
        public DateTime DetectedAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre automaticky detekované okno - .NET Framework 4.8 compatible
    /// </summary>
    public class WindowAutoDetectedEventArgs : WindowEventArgsBase
    {
        private readonly WindowTrackingInfo _windowInfo;
        private readonly string _description;
        private readonly WindowType _windowType;
        private readonly bool _autoSwitched;

        public WindowAutoDetectedEventArgs(IntPtr windowHandle, string description, string windowTitle, string processName, WindowType windowType)
            : base(windowHandle, windowTitle, processName)
        {
            _description = description ?? string.Empty;
            _windowType = windowType;
            _windowInfo = null;
            _autoSwitched = false;
        }

        public WindowAutoDetectedEventArgs(WindowTrackingInfo windowInfo, string description)
            : base(windowInfo != null ? windowInfo.WindowHandle : IntPtr.Zero,
                   windowInfo != null ? windowInfo.Title : null,
                   windowInfo != null ? windowInfo.ProcessName : null)
        {
            _windowInfo = windowInfo;
            _description = description ?? string.Empty;
            _windowType = windowInfo != null ? windowInfo.WindowType : WindowType.MainWindow;
            _autoSwitched = false;
        }

        public WindowTrackingInfo WindowInfo { get { return _windowInfo; } }
        public string Description { get { return _description; } }
        public WindowType WindowType { get { return _windowType; } }
        public WindowTrackingInfo Window { get { return WindowInfo; } }
        public DateTime DetectedAt { get { return Timestamp; } }
        public bool AutoSwitched { get { return _autoSwitched; } set { /* ReadOnly in base, but settable here for compatibility */ } }
    }

    /// <summary>
    /// Event args pre nové okno ktoré sa objavilo - .NET Framework 4.8 compatible
    /// </summary>
    public class NewWindowAppearedEventArgs : WindowEventArgsBase
    {
        private readonly WindowTrackingInfo _windowInfo;
        private readonly WindowType _windowType;
        private readonly bool _autoAdded;

        public NewWindowAppearedEventArgs(IntPtr windowHandle, string windowTitle, string processName, WindowType windowType)
            : base(windowHandle, windowTitle, processName)
        {
            _windowType = windowType;
            _windowInfo = null;
            _autoAdded = false;
        }

        public NewWindowAppearedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo != null ? windowInfo.WindowHandle : IntPtr.Zero,
                   windowInfo != null ? windowInfo.Title : null,
                   windowInfo != null ? windowInfo.ProcessName : null)
        {
            _windowInfo = windowInfo;
            _windowType = windowInfo != null ? windowInfo.WindowType : WindowType.MainWindow;
            _autoAdded = false;
        }

        public WindowTrackingInfo WindowInfo { get { return _windowInfo; } }
        public WindowType WindowType { get { return _windowType; } }
        public DateTime AppearedAt { get { return Timestamp; } }
        public bool AutoAdded { get { return _autoAdded; } set { /* ReadOnly in base, but settable here for compatibility */ } }
    }

    /// <summary>
    /// Event args pre okno ktoré zmizlo - .NET Framework 4.8 compatible
    /// </summary>
    public class WindowDisappearedEventArgs : WindowEventArgsBase
    {
        private readonly WindowTrackingInfo _windowInfo;

        public WindowDisappearedEventArgs(IntPtr windowHandle, string windowTitle, string processName)
            : base(windowHandle, windowTitle, processName)
        {
            _windowInfo = null;
        }

        public WindowDisappearedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo != null ? windowInfo.WindowHandle : IntPtr.Zero,
                   windowInfo != null ? windowInfo.Title : null,
                   windowInfo != null ? windowInfo.ProcessName : null)
        {
            _windowInfo = windowInfo;
        }

        public WindowTrackingInfo WindowInfo { get { return _windowInfo; } }
        public DateTime DisappearedAt { get { return Timestamp; } }
    }

    #endregion

    #region UI Element Event Args

    /// <summary>
    /// Event args pre pridaný UI element - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class ElementAddedEventArgs : UIElementEventArgsBase
    {
        public ElementAddedEventArgs(IntPtr windowHandle, UIElementSnapshot element)
            : base(windowHandle, element)
        {
        }

        public DateTime AddedAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre odstránený UI element - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class ElementRemovedEventArgs : UIElementEventArgsBase
    {
        private readonly string _elementIdentifier;

        public ElementRemovedEventArgs(IntPtr windowHandle, UIElementSnapshot element)
            : base(windowHandle, element)
        {
            _elementIdentifier = string.Empty;
        }

        public ElementRemovedEventArgs(IntPtr windowHandle, string elementIdentifier)
            : base(windowHandle, null)
        {
            _elementIdentifier = elementIdentifier ?? string.Empty;
        }

        public string ElementIdentifier { get { return _elementIdentifier; } }
        public DateTime RemovedAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre zmenený UI element - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class ElementModifiedEventArgs : UIElementEventArgsBase
    {
        private readonly UIElementSnapshot _previousElement;

        public ElementModifiedEventArgs(IntPtr windowHandle, UIElementSnapshot element, UIElementSnapshot previousElement)
            : base(windowHandle, element)
        {
            _previousElement = previousElement;
        }

        public UIElementSnapshot PreviousElement { get { return _previousElement; } }
        public DateTime ModifiedAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre interakciu s elementom - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class ElementInteractionEventArgs : EventArgs
    {
        private readonly IntPtr _windowHandle;
        private readonly UIElementSnapshot _element;
        private readonly InteractionType _interactionType;
        private readonly DateTime _timestamp;

        public ElementInteractionEventArgs(IntPtr windowHandle, UIElementSnapshot element, InteractionType interactionType)
        {
            _windowHandle = windowHandle;
            _element = element;
            _interactionType = interactionType;
            _timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get { return _windowHandle; } }
        public UIElementSnapshot Element { get { return _element; } }
        public InteractionType InteractionType { get { return _interactionType; } }
        public DateTime Timestamp { get { return _timestamp; } }
        public DateTime InteractionAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre UI zmeny detekované automaticky - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class UIChangeDetectedEventArgs : EventArgs
    {
        private readonly IntPtr _windowHandle;
        private readonly WindowState _windowState;
        private readonly UIChangeSet _changes;
        private readonly DateTime _timestamp;

        public UIChangeDetectedEventArgs(IntPtr windowHandle, WindowState windowState, UIChangeSet changes)
        {
            if (windowState == null) throw new ArgumentNullException("windowState");
            if (changes == null) throw new ArgumentNullException("changes");

            _windowHandle = windowHandle;
            _windowState = windowState;
            _changes = changes;
            _timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get { return _windowHandle; } }
        public WindowState WindowState { get { return _windowState; } }
        public UIChangeSet Changes { get { return _changes; } }
        public DateTime Timestamp { get { return _timestamp; } }
        public DateTime DetectedAt { get { return Timestamp; } }
    }

    #endregion

    #region Command & Recording Event Args

    /// <summary>
    /// Event args pre nahraný príkaz - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class CommandRecordedEventArgs : EventArgs
    {
        private readonly Command _command;
        private readonly DateTime _timestamp;

        public CommandRecordedEventArgs(Command command)
        {
            if (command == null) throw new ArgumentNullException("command");
            _command = command;
            _timestamp = DateTime.Now;
        }

        public Command Command { get { return _command; } }
        public DateTime Timestamp { get { return _timestamp; } }
        public DateTime RecordedAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre zmenu stavu nahrávania - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class RecordingStateChangedEventArgs : EventArgs
    {
        private readonly bool _isRecording;
        private readonly bool _isPaused;
        private readonly string _sequenceName;
        private readonly DateTime _timestamp;

        public RecordingStateChangedEventArgs(bool isRecording, bool isPaused, string sequenceName)
        {
            _isRecording = isRecording;
            _isPaused = isPaused;
            _sequenceName = sequenceName ?? string.Empty;
            _timestamp = DateTime.Now;
        }

        public bool IsRecording { get { return _isRecording; } }
        public bool IsPaused { get { return _isPaused; } }
        public string SequenceName { get { return _sequenceName; } }
        public DateTime Timestamp { get { return _timestamp; } }
    }

    /// <summary>
    /// Event args pre začatie live recording - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class LiveRecordingStartedEventArgs : EventArgs
    {
        private readonly CommandSequence _sequence;
        private readonly string _sequenceName;
        private readonly DateTime _timestamp;

        public LiveRecordingStartedEventArgs(CommandSequence sequence, string sequenceName)
        {
            if (sequence == null) throw new ArgumentNullException("sequence");
            _sequence = sequence;
            _sequenceName = sequenceName ?? string.Empty;
            _timestamp = DateTime.Now;
        }

        public CommandSequence Sequence { get { return _sequence; } }
        public string SequenceName { get { return _sequenceName; } }
        public DateTime Timestamp { get { return _timestamp; } }
        public DateTime StartedAt { get { return Timestamp; } }
    }

    #endregion

    #region Pattern & Analysis Event Args

    /// <summary>
    /// Event args pre detekovaný pattern - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class PatternDetectedEventArgs : EventArgs
    {
        private readonly string _patternType;
        private readonly float _confidence;
        private readonly object _patternData;
        private readonly DateTime _timestamp;

        public PatternDetectedEventArgs(string patternType, float confidence, object patternData)
        {
            _patternType = patternType ?? string.Empty;
            _confidence = confidence;
            _patternData = patternData;
            _timestamp = DateTime.Now;
        }

        public string PatternType { get { return _patternType; } }
        public float Confidence { get { return _confidence; } }
        public object PatternData { get { return _patternData; } }
        public DateTime Timestamp { get { return _timestamp; } }
    }

    /// <summary>
    /// Event args pre detekovanú anomáliu - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class AnomalyDetectedEventArgs : EventArgs
    {
        private readonly IntPtr _windowHandle;
        private readonly string _anomalyType;
        private readonly string _description;
        private readonly DateTime _timestamp;

        public AnomalyDetectedEventArgs(IntPtr windowHandle, string anomalyType, string description)
        {
            _windowHandle = windowHandle;
            _anomalyType = anomalyType ?? string.Empty;
            _description = description ?? string.Empty;
            _timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get { return _windowHandle; } }
        public string AnomalyType { get { return _anomalyType; } }
        public string Description { get { return _description; } }
        public DateTime Timestamp { get { return _timestamp; } }
    }

    #endregion

    #region UI Data Classes

    /// <summary>
    /// Stav sledovaného okna - .NET Framework 4.8 compatible
    /// </summary>
    public class WindowState
    {
        public WindowState()
        {
            WindowHandle = IntPtr.Zero;
            Title = string.Empty;
            ProcessName = string.Empty;
            Priority = WindowTrackingPrioritySharedClasses.Medium;
            AddedAt = DateTime.Now;
            LastActivated = DateTime.Now;
            ClosedAt = null;
            LastChangeDetected = DateTime.Now;
            LastSeen = DateTime.Now;
            IsActive = true;
            ActivationCount = 0;
            LastUISnapshot = null;
            ChangeHistory = new List<UIChangeSet>();
            Metadata = new Dictionary<string, object>();
        }

        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
        public WindowTrackingPrioritySharedClasses Priority { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime LastActivated { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime LastChangeDetected { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsActive { get; set; }
        public int ActivationCount { get; set; }
        public UISnapshot LastUISnapshot { get; set; }
        public List<UIChangeSet> ChangeHistory { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// Snapshot UI stavu okna - .NET Framework 4.8 compatible
    /// </summary>
    public class UISnapshot
    {
        public UISnapshot()
        {
            WindowHandle = IntPtr.Zero;
            CapturedAt = DateTime.Now;
            Elements = new List<UIElementSnapshot>();
            WindowTitle = string.Empty;
            Metadata = new Dictionary<string, object>();
        }

        public IntPtr WindowHandle { get; set; }
        public DateTime CapturedAt { get; set; }
        public List<UIElementSnapshot> Elements { get; set; }
        public string WindowTitle { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// Snapshot jednotlivého UI elementu - .NET Framework 4.8 compatible
    /// </summary>
    public class UIElementSnapshot
    {
        public UIElementSnapshot()
        {
            Name = string.Empty;
            AutomationId = string.Empty;
            ControlType = string.Empty;
            ClassName = string.Empty;
            X = 0;
            Y = 0;
            Width = 0;
            Height = 0;
            IsEnabled = false;
            IsVisible = false;
            Text = string.Empty;
            Hash = string.Empty;
            IsWinUI3Element = false;
            Properties = new Dictionary<string, object>();
        }

        public string Name { get; set; }
        public string AutomationId { get; set; }
        public string ControlType { get; set; }
        public string ClassName { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
        public string Text { get; set; }
        public string Hash { get; set; }
        public bool IsWinUI3Element { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public override string ToString()
        {
            return string.Format("{0}: {1} ({2}) at ({3}, {4})", ControlType, Name, ClassName, X, Y);
        }
    }

    /// <summary>
    /// Sada zmien v UI - .NET Framework 4.8 compatible
    /// </summary>
    public class UIChangeSet
    {
        public UIChangeSet()
        {
            PreviousSnapshot = null;
            CurrentSnapshot = null;
            DetectedAt = DateTime.Now;
            HasChanges = false;
            AddedElements = new List<UIElementSnapshot>();
            RemovedElements = new List<UIElementSnapshot>();
            ModifiedElements = new List<ModifiedElementPair>();
            ChangeDescription = string.Empty;
        }

        public UISnapshot PreviousSnapshot { get; set; }
        public UISnapshot CurrentSnapshot { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool HasChanges { get; set; }
        public List<UIElementSnapshot> AddedElements { get; set; }
        public List<UIElementSnapshot> RemovedElements { get; set; }
        // Používame vlastnú triedu namiesto tuple pre .NET Framework 4.8 compatibility
        public List<ModifiedElementPair> ModifiedElements { get; set; }
        public string ChangeDescription { get; set; }
    }

    /// <summary>
    /// Pair pre ModifiedElements - .NET Framework 4.8 compatible replacement pre tuple
    /// </summary>
    public class ModifiedElementPair
    {
        public ModifiedElementPair(UIElementSnapshot previous, UIElementSnapshot current)
        {
            Previous = previous;
            Current = current;
        }

        public UIElementSnapshot Previous { get; set; }
        public UIElementSnapshot Current { get; set; }
    }

    /// <summary>
    /// Rozšírené informácie o UI elemente - .NET Framework 4.8 compatible
    /// </summary>
    public class UIElementInfo
    {
        public UIElementInfo()
        {
            Name = string.Empty;
            AutomationId = string.Empty;
            ClassName = string.Empty;
            ControlType = string.Empty;
            X = 0;
            Y = 0;
            BoundingRectangle = Rect.Empty;
            IsEnabled = false;
            IsVisible = false;
            ProcessId = 0;
            WindowHandle = IntPtr.Zero;
            AutomationElement = null;
            ElementText = string.Empty;
            PlaceholderText = string.Empty;
            HelpText = string.Empty;
            AccessKey = string.Empty;
            IsTableCell = false;
            TableCellIdentifier = string.Empty;
            TableName = string.Empty;
            TableRow = -1;
            TableColumn = -1;
            TableColumnName = string.Empty;
            TableCellContent = string.Empty;
            TableType = string.Empty;
            TableInfo = string.Empty;
        }

        public string Name { get; set; }
        public string AutomationId { get; set; }
        public string ClassName { get; set; }
        public string ControlType { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public Rect BoundingRectangle { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
        public int ProcessId { get; set; }
        public IntPtr WindowHandle { get; set; }
        public AutomationElement AutomationElement { get; set; }

        // Extended properties
        public string ElementText { get; set; }
        public string PlaceholderText { get; set; }
        public string HelpText { get; set; }
        public string AccessKey { get; set; }

        // Table support
        public bool IsTableCell { get; set; }
        public string TableCellIdentifier { get; set; }
        public string TableName { get; set; }
        public int TableRow { get; set; }
        public int TableColumn { get; set; }
        public string TableColumnName { get; set; }
        public string TableCellContent { get; set; }
        public string TableType { get; set; }
        public string TableInfo { get; set; }

        public string GetUniqueIdentifier()
        {
            if (!string.IsNullOrEmpty(AutomationId))
                return string.Format("AutoId_{0}", AutomationId);

            if (!string.IsNullOrEmpty(Name))
                return string.Format("Name_{0}", Name);

            if (!string.IsNullOrEmpty(ElementText))
                return string.Format("Text_{0}", ElementText);

            return string.Format("Class_{0}_Pos_{1}_{2}", ClassName, X, Y);
        }

        public string GetTableCellBestIdentifier()
        {
            if (!IsTableCell)
                return GetUniqueIdentifier();

            if (!string.IsNullOrEmpty(TableCellIdentifier))
                return TableCellIdentifier;

            var parts = new List<string>();

            if (!string.IsNullOrEmpty(TableName))
                parts.Add(string.Format("Table:{0}", CleanIdentifierText(TableName)));

            if (!string.IsNullOrEmpty(TableColumnName))
                parts.Add(string.Format("Col:{0}", CleanIdentifierText(TableColumnName)));
            else if (TableColumn >= 0)
                parts.Add(string.Format("Col:{0}", TableColumn));

            if (TableRow >= 0)
                parts.Add(string.Format("Row:{0}", TableRow));

            return parts.Count > 0 ? string.Join("_", parts.ToArray()) : GetUniqueIdentifier();
        }

        public string GetTableCellDisplayName()
        {
            if (!IsTableCell)
                return Name;

            var parts = new List<string>();

            if (!string.IsNullOrEmpty(TableName))
                parts.Add(TableName);

            string columnPart = !string.IsNullOrEmpty(TableColumnName) ? TableColumnName : string.Format("Col{0}", TableColumn);
            parts.Add(columnPart);

            if (TableRow >= 0)
                parts.Add(string.Format("R{0}", TableRow));

            if (!string.IsNullOrEmpty(TableCellContent) && TableCellContent.Length <= 15)
                parts.Add(CleanIdentifierText(TableCellContent));

            return string.Join("_", parts.ToArray());
        }

        public bool IsValidTableCell()
        {
            return IsTableCell &&
                   TableRow >= 0 &&
                   TableColumn >= 0 &&
                   !string.IsNullOrEmpty(TableCellIdentifier);
        }

        private string CleanIdentifierText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // .NET Framework 4.8 compatible regex replacement
            return System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\d]", "_").Trim('_');
        }

        public override string ToString()
        {
            if (IsTableCell)
            {
                return string.Format("TableCell: {0} at ({1}, {2}) in {3}", GetTableCellDisplayName(), X, Y, TableName);
            }

            return string.Format("{0}: {1} ({2}) at ({3}, {4})", ControlType, Name, ClassName, X, Y);
        }
    }

    /// <summary>
    /// Štatistiky použitia UI elementov - .NET Framework 4.8 compatible
    /// </summary>
    public class ElementUsageStats
    {
        public ElementUsageStats()
        {
            ElementName = string.Empty;
            UsageCount = 0;
            Reliability = 1.0f;
            ElementType = string.Empty;
            ControlType = string.Empty;
            ClickCount = 0;
            KeyPressCount = 0;
            TotalUsage = 0;
            FirstUsed = DateTime.MinValue;
            LastUsed = DateTime.MinValue;
            ActionsPerformed = new List<string>();
        }

        public string ElementName { get; set; }
        public int UsageCount { get; set; }
        public float Reliability { get; set; }
        public string ElementType { get; set; }
        public string ControlType { get; set; }
        public int ClickCount { get; set; }
        public int KeyPressCount { get; set; }
        public int TotalUsage { get; set; }
        public DateTime FirstUsed { get; set; }
        public DateTime LastUsed { get; set; }
        public List<string> ActionsPerformed { get; set; }

        public void IncrementUsage(CommandType actionType)
        {
            TotalUsage++;
            LastUsed = DateTime.Now;

            if (FirstUsed == DateTime.MinValue)
                FirstUsed = DateTime.Now;

            switch (actionType)
            {
                case CommandType.Click:
                case CommandType.DoubleClick:
                case CommandType.RightClick:
                case CommandType.MouseClick:
                    ClickCount++;
                    break;
                case CommandType.KeyPress:
                    KeyPressCount++;
                    break;
            }

            if (!ActionsPerformed.Contains(actionType.ToString()))
                ActionsPerformed.Add(actionType.ToString());
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(ElementName))
                return string.Format("Unknown ({0}): {1} uses", ControlType, TotalUsage);

            return string.Format("{0} ({1}): {2} uses", ElementName, ControlType, TotalUsage);
        }
    }

    /// <summary>
    /// Vylepšené tracking data pre okná - .NET Framework 4.8 compatible
    /// </summary>
    public class WindowTrackingData
    {
        public WindowTrackingData()
        {
            WindowHandle = IntPtr.Zero;
            Title = string.Empty;
            ProcessName = string.Empty;
            ClassName = string.Empty;
            WindowType = WindowType.MainWindow;
            IsModal = false;
            IsVisible = false;
            DetectedAt = DateTime.Now;
            LastUpdate = DateTime.Now;
            IsTracked = true;
            IsActive = true;
            UIElements = new List<UIElementInfo>();
        }

        public WindowTrackingData(WindowTrackingInfo windowInfo)
        {
            if (windowInfo != null)
            {
                WindowHandle = windowInfo.WindowHandle;
                Title = windowInfo.Title ?? string.Empty;
                ProcessName = windowInfo.ProcessName ?? string.Empty;
                ClassName = windowInfo.ClassName ?? string.Empty;
                IsVisible = windowInfo.IsVisible;
                DetectedAt = windowInfo.DetectedAt;
                WindowType = windowInfo.WindowType;
                IsModal = windowInfo.IsModal;
                IsActive = windowInfo.IsActive;
            }
            else
            {
                WindowHandle = IntPtr.Zero;
                Title = string.Empty;
                ProcessName = string.Empty;
                ClassName = string.Empty;
                IsVisible = false;
                DetectedAt = DateTime.Now;
                WindowType = WindowType.MainWindow;
                IsModal = false;
                IsActive = true;
            }

            UIElements = new List<UIElementInfo>();
            LastUpdate = DateTime.Now;
            IsTracked = true;
        }

        // Window properties
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
        public string ClassName { get; set; }
        public WindowType WindowType { get; set; }
        public bool IsModal { get; set; }
        public bool IsVisible { get; set; }

        // Tracking properties
        public DateTime DetectedAt { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsTracked { get; set; }
        public bool IsActive { get; set; }

        // UI Elements
        public List<UIElementInfo> UIElements { get; set; }

        public WindowTrackingInfo ToWindowTrackingInfo()
        {
            return new WindowTrackingInfo
            {
                WindowHandle = WindowHandle,
                Title = Title,
                ProcessName = ProcessName,
                ClassName = ClassName,
                IsVisible = IsVisible,
                DetectedAt = DetectedAt,
                WindowType = WindowType,
                IsModal = IsModal,
                IsActive = IsActive
            };
        }

        public void UpdateTracking()
        {
            LastUpdate = DateTime.Now;
        }

        public void MarkAsInactive()
        {
            IsActive = false;
            IsTracked = false;
            UpdateTracking();
        }
    }

    #endregion

    #region Compatibility & Legacy Support

    /// <summary>
    /// Aliasy pre backward compatibility - označené ako obsolete
    /// </summary>
    [Obsolete("Use WindowAutoDetectedEventArgs instead")]
    public class AutoWindowDetectedEventArgs : WindowAutoDetectedEventArgs
    {
        public AutoWindowDetectedEventArgs(IntPtr windowHandle, string description, string windowTitle, string processName, WindowType windowType)
            : base(windowHandle, description, windowTitle, processName, windowType)
        {
        }
    }

    [Obsolete("Use NewWindowAppearedEventArgs instead")]
    public class CustomWindowAppearedEventArgs : NewWindowAppearedEventArgs
    {
        public CustomWindowAppearedEventArgs(IntPtr windowHandle, string windowTitle, string processName, WindowType windowType)
            : base(windowHandle, windowTitle, processName, windowType)
        {
        }
    }

    [Obsolete("Use WindowDisappearedEventArgs instead")]
    public class CustomWindowDisappearedEventArgs : WindowDisappearedEventArgs
    {
        public CustomWindowDisappearedEventArgs(IntPtr windowHandle, string windowTitle, string processName)
            : base(windowHandle, windowTitle, processName)
        {
        }
    }

    /// <summary>
    /// Event args pre WindowTracker - rozširuje base functionality
    /// </summary>
    public class WindowTrackerEventArgs : WindowActivatedEventArgs
    {
        public WindowTrackerEventArgs(IntPtr windowHandle, string windowTitle, string processName)
            : base(windowHandle, windowTitle, processName)
        {
        }

        public WindowTrackerEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo)
        {
        }
    }

    #endregion

    #region Service Classes - Placeholder Implementations

    /// <summary>
    /// Automatický detektor okien - .NET Framework 4.8 compatible
    /// </summary>
    public class AutoWindowDetector : IDisposable
    {
        public AutoWindowDetector()
        {
            EnableDialogDetection = true;
            EnableMessageBoxDetection = true;
            EnableChildWindowDetection = true;
            EnableWinUI3Detection = true;
            DetectionSensitivity = DetectionSensitivity.Medium;
            _isDetecting = false;
        }

        public bool EnableDialogDetection { get; set; }
        public bool EnableMessageBoxDetection { get; set; }
        public bool EnableChildWindowDetection { get; set; }
        public bool EnableWinUI3Detection { get; set; }
        public DetectionSensitivity DetectionSensitivity { get; set; }

        public event EventHandler<WindowAutoDetectedEventArgs> NewWindowDetected;
        public event EventHandler<WindowActivatedEventArgs> WindowActivated;
        public event EventHandler<WindowClosedEventArgs> WindowClosed;

        private bool _isDetecting;

        public void StartDetection(IntPtr primaryWindow, string targetProcess)
        {
            _isDetecting = true;
            System.Diagnostics.Debug.WriteLine("🔍 AutoWindowDetector started");
        }

        public void StopDetection()
        {
            _isDetecting = false;
            System.Diagnostics.Debug.WriteLine("🛑 AutoWindowDetector stopped");
        }

        public void Dispose()
        {
            StopDetection();
        }
    }

    /// <summary>
    /// Skaner UI elementov - .NET Framework 4.8 compatible
    /// </summary>
    public class UIElementScanner : IDisposable
    {
        public UIElementScanner()
        {
            ScanInterval = 750;
            EnableDeepScanning = true;
            EnableWinUI3ElementDetection = true;
            MaxElementsPerScan = 100;
            _isScanning = false;
        }

        public int ScanInterval { get; set; }
        public bool EnableDeepScanning { get; set; }
        public bool EnableWinUI3ElementDetection { get; set; }
        public int MaxElementsPerScan { get; set; }

        public event EventHandler<UIElementsChangedEventArgs> ElementsChanged;
        public event EventHandler<NewElementDetectedEventArgs> NewElementDetected;
        public event EventHandler<ElementDisappearedEventArgs> ElementDisappeared;

        private bool _isScanning;

        public void StartScanning(IntPtr primaryWindow)
        {
            _isScanning = true;
            System.Diagnostics.Debug.WriteLine("🔍 UIElementScanner started");
        }

        public void StopScanning()
        {
            _isScanning = false;
            System.Diagnostics.Debug.WriteLine("🛑 UIElementScanner stopped");
        }

        public void AddWindowToScan(IntPtr windowHandle)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("➕ Added window to scan: {0}", windowHandle));
        }

        public void SwitchPrimaryWindow(IntPtr newPrimaryWindow)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("🔄 Switched primary scan window: {0}", newPrimaryWindow));
        }

        public void Dispose()
        {
            StopScanning();
        }
    }

    /// <summary>
    /// Monitor okien - .NET Framework 4.8 compatible
    /// </summary>
    public class WindowMonitor : IDisposable
    {
        public WindowMonitor()
        {
            _targetProcesses = new List<string>();
            _knownWindows = new HashSet<IntPtr>();
            _isMonitoring = false;
        }

        public event EventHandler<NewWindowAppearedEventArgs> WindowAppeared;
        public event EventHandler<WindowDisappearedEventArgs> WindowDisappeared;
        public event EventHandler<WindowTrackerEventArgs> WindowActivated;

        private readonly List<string> _targetProcesses;
        private readonly HashSet<IntPtr> _knownWindows;
        private bool _isMonitoring;

        public void StartMonitoring(string targetProcess)
        {
            _isMonitoring = true;
            if (!string.IsNullOrEmpty(targetProcess))
            {
                AddTargetProcess(targetProcess);
            }
            System.Diagnostics.Debug.WriteLine("🔍 WindowMonitor started");
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            System.Diagnostics.Debug.WriteLine("🛑 WindowMonitor stopped");
        }

        public void AddTargetProcess(string processName)
        {
            if (!_targetProcesses.Contains(processName))
            {
                _targetProcesses.Add(processName);
                System.Diagnostics.Debug.WriteLine(string.Format("📝 Added target process: {0}", processName));
            }
        }

        public bool IsTargetProcess(string processName)
        {
            return _targetProcesses.Contains(processName);
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }

    /// <summary>
    /// Detektor zmien elementov - .NET Framework 4.8 compatible
    /// </summary>
    public class ElementChangeDetector : IDisposable
    {
        public ElementChangeDetector()
        {
            _isDetecting = false;
        }

        public event EventHandler<ElementAddedEventArgs> ElementAdded;
        public event EventHandler<ElementRemovedEventArgs> ElementRemoved;
        public event EventHandler<ElementModifiedEventArgs> ElementModified;

        private bool _isDetecting;

        public void StartDetection()
        {
            _isDetecting = true;
            System.Diagnostics.Debug.WriteLine("🔍 ElementChangeDetector started");
        }

        public void StopDetection()
        {
            _isDetecting = false;
            System.Diagnostics.Debug.WriteLine("🛑 ElementChangeDetector stopped");
        }

        public void Dispose()
        {
            StopDetection();
        }
    }

    /// <summary>
    /// Inteligentný analyzátor UI - .NET Framework 4.8 compatible
    /// </summary>
    public class SmartUIAnalyzer : IDisposable
    {
        public SmartUIAnalyzer()
        {
            _isAnalyzing = false;
        }

        public event EventHandler<PatternDetectedEventArgs> PatternDetected;
        public event EventHandler<AnomalyDetectedEventArgs> AnomalyDetected;

        private bool _isAnalyzing;

        public void StartAnalysis()
        {
            _isAnalyzing = true;
            System.Diagnostics.Debug.WriteLine("🧠 SmartUIAnalyzer started");
        }

        public void StopAnalysis()
        {
            _isAnalyzing = false;
            System.Diagnostics.Debug.WriteLine("🛑 SmartUIAnalyzer stopped");
        }

        public void AnalyzeChanges(WindowState windowState, UIChangeSet changes)
        {
            if (!_isAnalyzing) return;

            // Implementácia analýzy patterns a anomálií
            // Napríklad: detekcia opakujúcich sa patterns, neočakávaných zmien, atď.
        }

        public void Dispose()
        {
            StopAnalysis();
        }
    }

    #endregion

    #region Additional Event Args for Scanner

    /// <summary>
    /// Event args pre zmeny UI elementov - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class UIElementsChangedEventArgs : EventArgs
    {
        private readonly List<UIElementSnapshot> _newElements;
        private readonly List<UIElementSnapshot> _previousElements;
        private readonly IntPtr _windowHandle;
        private readonly List<UIElementSnapshot> _addedElements;
        private readonly List<UIElementSnapshot> _removedElements;
        private readonly List<UIElementSnapshot> _modifiedElements;
        private readonly DateTime _timestamp;

        public UIElementsChangedEventArgs(IntPtr windowHandle, List<UIElementSnapshot> addedElements, List<UIElementSnapshot> removedElements, List<UIElementSnapshot> modifiedElements)
        {
            _windowHandle = windowHandle;
            _addedElements = addedElements ?? new List<UIElementSnapshot>();
            _removedElements = removedElements ?? new List<UIElementSnapshot>();
            _modifiedElements = modifiedElements ?? new List<UIElementSnapshot>();
            _timestamp = DateTime.Now;
            _newElements = new List<UIElementSnapshot>();
            _previousElements = new List<UIElementSnapshot>();
        }

        public List<UIElementSnapshot> NewElements { get { return _newElements; } }
        public List<UIElementSnapshot> PreviousElements { get { return _previousElements; } }
        public IntPtr WindowHandle { get { return _windowHandle; } }
        public List<UIElementSnapshot> AddedElements { get { return _addedElements; } }
        public List<UIElementSnapshot> RemovedElements { get { return _removedElements; } }
        public List<UIElementSnapshot> ModifiedElements { get { return _modifiedElements; } }
        public DateTime Timestamp { get { return _timestamp; } }

        public bool HasChanges
        {
            get
            {
                return AddedElements.Count > 0 ||
                       RemovedElements.Count > 0 ||
                       ModifiedElements.Count > 0;
            }
        }
    }

    /// <summary>
    /// Event args pre nový detekovaný element - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class NewElementDetectedEventArgs : UIElementEventArgsBase
    {
        public NewElementDetectedEventArgs(IntPtr windowHandle, UIElementSnapshot element)
            : base(windowHandle, element)
        {
        }

        public DateTime DetectedAt { get { return Timestamp; } }
    }

    /// <summary>
    /// Event args pre zmiznutý element - .NET Framework 4.8 compatible
    /// </summary>
    public sealed class ElementDisappearedEventArgs : EventArgs
    {
        private readonly IntPtr _windowHandle;
        private readonly string _elementIdentifier;
        private readonly UIElementSnapshot _lastKnownState;
        private readonly DateTime _timestamp;

        public ElementDisappearedEventArgs(IntPtr windowHandle, string elementIdentifier, UIElementSnapshot lastKnownState)
        {
            _windowHandle = windowHandle;
            _elementIdentifier = elementIdentifier ?? string.Empty;
            _lastKnownState = lastKnownState;
            _timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get { return _windowHandle; } }
        public string ElementIdentifier { get { return _elementIdentifier; } }
        public UIElementSnapshot LastKnownState { get { return _lastKnownState; } }
        public DateTime Timestamp { get { return _timestamp; } }
        public DateTime DisappearedAt { get { return Timestamp; } }
    }

    #endregion
}
