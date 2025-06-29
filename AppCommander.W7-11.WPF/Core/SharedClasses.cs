using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    #region Enums

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
    /// OPRAVENÉ: Informácie o sledovanom okne s pridanými properties
    /// </summary>
    public class WindowTrackingInfo
    {
        public WindowTrackingInfo()
        {
            WindowHandle = IntPtr.Zero;
            Title = string.Empty;
            ProcessName = string.Empty;
            ClassName = string.Empty;
            DetectedAt = DateTime.Now;
            LastActivated = DateTime.Now;
        }

        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public DateTime DetectedAt { get; set; } = DateTime.Now;
        public WindowType WindowType { get; set; } = WindowType.MainWindow;

        public int ProcessId { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsModal { get; set; } = false;
        public DateTime LastActivated { get; set; } = DateTime.Now;
        public WindowTrackingPrioritySharedClasses Priority { get; set; } = WindowTrackingPrioritySharedClasses.Medium;

        // PRIDANÉ: Properties pre kompatibilitu
        public bool IsEnabled { get; set; } = true;
        public int Width { get; set; } = 0;
        public int Height { get; set; } = 0;

        public override string ToString()
        {
            return $"{ProcessName} - {Title}";
        }
    }

    /// <summary>
    /// WinUI3 Element informácie s pozíciou
    /// </summary>
    public class WinUI3ElementInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AutomationId { get; set; } = string.Empty;
        public string ElementType { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public bool IsInteractive { get; set; }
        public string AccessMethod { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        // PRIDANÉ: Position property pre kompatibilitu
        public Point? Position { get; set; }
        public bool IsEnabled { get; internal set; }
        public bool IsVisible { get; internal set; }
    }

    /// <summary>
    /// WinUI3 Application Analysis
    /// </summary>
    public class WinUI3ApplicationAnalysis
    {
        public bool IsSuccessful { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string WindowClass { get; set; } = string.Empty;
        public int BridgeCount { get; set; } = 0;
        public List<WinUI3BridgeInfo> Bridges { get; set; } = new List<WinUI3BridgeInfo>();
        public List<WinUI3ElementInfo> InteractiveElements { get; set; } = new List<WinUI3ElementInfo>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public string ApplicationName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;

        // OPRAVENÉ: Unifikované názvy properties
        public bool IsWinUI3 { get; set; } = false;
        public bool IsWinUI3Application => IsWinUI3; // Alias pre kompatibilitu
    }

    #endregion

    #region Command Player Event Args (NOVÉ)

    /// <summary>
    /// Event args pre vykonaný príkaz
    /// </summary>
    public class CommandExecutedEventArgs : EventArgs
    {
        public Command Command { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int CommandIndex { get; set; }
        public int TotalCommands { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.Now;

        public CommandExecutedEventArgs()
        {
        }

        public CommandExecutedEventArgs(Command command, bool success, int commandIndex, int totalCommands)
        {
            Command = command;
            Success = success;
            CommandIndex = commandIndex;
            TotalCommands = totalCommands;
        }
    }

    /// <summary>
    /// Event args pre zmenu stavu playback
    /// </summary>
    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public PlaybackState State { get; set; }
        public int CurrentIndex { get; set; }
        public int TotalCommands { get; set; }
        public string SequenceName { get; set; } = string.Empty;
        public string AdditionalInfo { get; set; } = string.Empty;
        public DateTime StateChangedAt { get; set; } = DateTime.Now;

        public PlaybackStateChangedEventArgs()
        {
        }

        public PlaybackStateChangedEventArgs(PlaybackState state, string sequenceName)
        {
            State = state;
            SequenceName = sequenceName;
        }
    }

    /// <summary>
    /// Event args pre chybu v playback
    /// </summary>
    public class PlaybackErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public int CommandIndex { get; set; }
        public Command Command { get; set; }
        public Exception Exception { get; set; }
        public DateTime ErrorOccurredAt { get; set; } = DateTime.Now;

        public PlaybackErrorEventArgs()
        {
        }

        public PlaybackErrorEventArgs(string errorMessage, int commandIndex, Command command = null)
        {
            ErrorMessage = errorMessage;
            CommandIndex = commandIndex;
            Command = command;
        }
    }

    /// <summary>
    /// Event args pre dokončenie playback
    /// </summary>
    public class PlaybackCompletedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CommandsExecuted { get; set; }
        public int TotalCommands { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.Now;

        public PlaybackCompletedEventArgs()
        {
        }

        public PlaybackCompletedEventArgs(bool success, string message, int commandsExecuted, int totalCommands)
        {
            Success = success;
            Message = message;
            CommandsExecuted = commandsExecuted;
            TotalCommands = totalCommands;
        }
    }

    /// <summary>
    /// Stav playback
    /// </summary>
    public enum PlaybackState
    {
        Stopped,
        Started,
        Paused,
        Resumed,
        Completed,
        Error
    }

    #endregion

    #region Base Event Args Classes

    /// <summary>
    /// Základná trieda pre window-related event args
    /// </summary>
    public abstract class WindowEventArgsBase : EventArgs
    {
        protected WindowEventArgsBase(IntPtr windowHandle, string windowTitle = null, string processName = null)
        {
            WindowHandle = windowHandle;
            WindowTitle = windowTitle ?? string.Empty;
            ProcessName = processName ?? string.Empty;
            Timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get; }
        public string WindowTitle { get; }
        public string ProcessName { get; }
        public DateTime Timestamp { get; }
    }

    /// <summary>
    /// Základná trieda pre UI element-related event args
    /// </summary>
    public abstract class UIElementEventArgsBase : EventArgs
    {
        protected UIElementEventArgsBase(IntPtr windowHandle, UIElementSnapshot element)
        {
            WindowHandle = windowHandle;
            Element = element;
            Timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get; }
        public UIElementSnapshot Element { get; }
        public DateTime Timestamp { get; }
    }

    #endregion

    #region Window Event Args

    /// <summary>
    /// Event args pre aktivované okno
    /// </summary>
    public class WindowActivatedEventArgs : WindowEventArgsBase
    {
        public WindowActivatedEventArgs(IntPtr windowHandle, string windowTitle = null, string processName = null)
            : base(windowHandle, windowTitle, processName)
        {
        }

        public WindowActivatedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo?.WindowHandle ?? IntPtr.Zero, windowInfo?.Title, windowInfo?.ProcessName)
        {
            WindowInfo = windowInfo;
        }

        public WindowTrackingInfo WindowInfo { get; }
        public DateTime ActivatedAt => Timestamp;
    }

    /// <summary>
    /// Event args pre zatvorené okno
    /// </summary>
    public sealed class WindowClosedEventArgs : WindowEventArgsBase
    {
        public WindowClosedEventArgs(IntPtr windowHandle, string windowTitle = null, string processName = null)
            : base(windowHandle, windowTitle, processName)
        {
        }

        public WindowClosedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo?.WindowHandle ?? IntPtr.Zero, windowInfo?.Title, windowInfo?.ProcessName)
        {
            WindowInfo = windowInfo;
            WindowTrackingInfo = windowInfo;
        }

        public WindowTrackingInfo WindowInfo { get; }
        public WindowTrackingInfo WindowTrackingInfo { get; }
        public WindowTrackingInfo Window => WindowInfo;
        public DateTime ClosedAt => Timestamp;
    }

    /// <summary>
    /// Event args pre nové detekované okno
    /// </summary>
    public sealed class NewWindowDetectedEventArgs : WindowEventArgsBase
    {
        public NewWindowDetectedEventArgs(
            IntPtr windowHandle,
            string windowTitle = null,
            string processName = null,
            WindowType windowType = WindowType.MainWindow)
            : base(windowHandle, windowTitle, processName)
        {
            WindowType = windowType;
        }

        public NewWindowDetectedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo?.WindowHandle ?? IntPtr.Zero, windowInfo?.Title, windowInfo?.ProcessName)
        {
            WindowInfo = windowInfo;
            WindowType = windowInfo?.WindowType ?? WindowType.MainWindow;
        }

        public WindowTrackingInfo WindowInfo { get; }
        public WindowType WindowType { get; }
        public WindowTrackingInfo Window => WindowInfo;
        public DateTime DetectedAt => Timestamp;
    }

    /// <summary>
    /// Event args pre nové okno ktoré sa objavilo
    /// </summary>
    public class NewWindowAppearedEventArgs : WindowEventArgsBase
    {
        public NewWindowAppearedEventArgs(
            IntPtr windowHandle,
            string windowTitle = null,
            string processName = null,
            WindowType windowType = WindowType.MainWindow)
            : base(windowHandle, windowTitle, processName)
        {
            WindowType = windowType;
        }

        public NewWindowAppearedEventArgs(WindowTrackingInfo windowInfo)
            : base(windowInfo?.WindowHandle ?? IntPtr.Zero, windowInfo?.Title, windowInfo?.ProcessName)
        {
            WindowInfo = windowInfo;
            WindowType = windowInfo?.WindowType ?? WindowType.MainWindow;
        }

        public WindowTrackingInfo WindowInfo { get; }
        public WindowType WindowType { get; }
        public DateTime AppearedAt => Timestamp;
        public bool AutoAdded { get; set; }
    }

    #endregion

    #region UI Element Event Args

    /// <summary>
    /// Event args pre pridaný UI element
    /// </summary>
    public sealed class ElementAddedEventArgs : UIElementEventArgsBase
    {
        public ElementAddedEventArgs(IntPtr windowHandle, UIElementSnapshot element)
            : base(windowHandle, element)
        {
        }

        public DateTime AddedAt => Timestamp;
    }

    /// <summary>
    /// Event args pre odstránený UI element
    /// </summary>
    public sealed class ElementRemovedEventArgs : UIElementEventArgsBase
    {
        public ElementRemovedEventArgs(IntPtr windowHandle, UIElementSnapshot element)
            : base(windowHandle, element)
        {
        }

        public ElementRemovedEventArgs(IntPtr windowHandle, string elementIdentifier)
            : base(windowHandle, null)
        {
            ElementIdentifier = elementIdentifier ?? string.Empty;
        }

        public string ElementIdentifier { get; } = string.Empty;
        public DateTime RemovedAt => Timestamp;
    }

    /// <summary>
    /// Event args pre zmenený UI element
    /// </summary>
    public sealed class ElementModifiedEventArgs : UIElementEventArgsBase
    {
        public ElementModifiedEventArgs(
            IntPtr windowHandle,
            UIElementSnapshot element,
            UIElementSnapshot previousElement = null)
            : base(windowHandle, element)
        {
            PreviousElement = previousElement;
        }

        public UIElementSnapshot PreviousElement { get; }
        public DateTime ModifiedAt => Timestamp;
    }

    /// <summary>
    /// OPRAVENÉ: Event args pre interakciu s elementom - nastaviteľné properties
    /// </summary>
    public sealed class ElementInteractionEventArgs : EventArgs
    {
        public ElementInteractionEventArgs(
            IntPtr windowHandle,
            UIElementSnapshot element,
            InteractionType interactionType)
        {
            WindowHandle = windowHandle;
            Element = element;
            InteractionType = interactionType;
            Timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get; }
        public UIElementSnapshot Element { get; }
        public InteractionType InteractionType { get; }
        public DateTime Timestamp { get; }
        public DateTime InteractionAt => Timestamp;
    }

    /// <summary>
    /// OPRAVENÉ: Event args pre UI zmeny detekované automaticky - nastaviteľné properties
    /// </summary>
    public sealed class UIChangeDetectedEventArgs : EventArgs
    {
        public UIChangeDetectedEventArgs(
            IntPtr windowHandle,
            WindowState windowState,
            UIChangeSet changes)
        {
            WindowHandle = windowHandle;
            WindowState = windowState ?? throw new ArgumentNullException(nameof(windowState));
            Changes = changes ?? throw new ArgumentNullException(nameof(changes));
            Timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get; }
        public WindowState WindowState { get; }
        public UIChangeSet Changes { get; }
        public DateTime Timestamp { get; }
        public DateTime DetectedAt => Timestamp;
    }

    #endregion

    #region Command & Recording Event Args

    /// <summary>
    /// Event args pre nahraný príkaz - používa Command z Command.cs
    /// </summary>
    public sealed class CommandRecordedEventArgs : EventArgs
    {
        public CommandRecordedEventArgs(Command command)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Timestamp = DateTime.Now;
        }

        public Command Command { get; }
        public DateTime Timestamp { get; }
        public DateTime RecordedAt => Timestamp;
    }

    /// <summary>
    /// Event args pre zmenu stavu nahrávania
    /// </summary>
    public sealed class RecordingStateChangedEventArgs : EventArgs
    {
        public RecordingStateChangedEventArgs(
            bool isRecording,
            bool isPaused = false,
            string sequenceName = "")
        {
            IsRecording = isRecording;
            IsPaused = isPaused;
            SequenceName = sequenceName ?? string.Empty;
            Timestamp = DateTime.Now;
        }

        public bool IsRecording { get; }
        public bool IsPaused { get; }
        public string SequenceName { get; }
        public DateTime Timestamp { get; }
    }

    #endregion

    #region Pattern & Analysis Event Args

    /// <summary>
    /// Event args pre detekovaný pattern
    /// </summary>
    public sealed class PatternDetectedEventArgs : EventArgs
    {
        public PatternDetectedEventArgs(
            string patternType,
            float confidence,
            object patternData = null)
        {
            PatternType = patternType ?? string.Empty;
            Confidence = confidence;
            PatternData = patternData;
            Timestamp = DateTime.Now;
        }

        public string PatternType { get; }
        public float Confidence { get; }
        public object PatternData { get; }
        public DateTime Timestamp { get; }
    }

    /// <summary>
    /// Event args pre detekovanú anomáliu
    /// </summary>
    public sealed class AnomalyDetectedEventArgs : EventArgs
    {
        public AnomalyDetectedEventArgs(
            IntPtr windowHandle,
            string anomalyType,
            string description)
        {
            WindowHandle = windowHandle;
            AnomalyType = anomalyType ?? string.Empty;
            Description = description ?? string.Empty;
            Timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get; }
        public string AnomalyType { get; }
        public string Description { get; }
        public DateTime Timestamp { get; }
    }

    #endregion

    #region UI Data Classes

    /// <summary>
    /// Stav sledovaného okna
    /// </summary>
    public class WindowState
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public WindowTrackingPrioritySharedClasses Priority { get; set; } = WindowTrackingPrioritySharedClasses.Medium;
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public DateTime LastActivated { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }
        public DateTime LastChangeDetected { get; set; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public int ActivationCount { get; set; } = 0;
        public UISnapshot LastUISnapshot { get; set; }
        public List<UIChangeSet> ChangeHistory { get; set; } = new List<UIChangeSet>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Snapshot UI stavu okna
    /// </summary>
    public class UISnapshot
    {
        public IntPtr WindowHandle { get; set; }
        public DateTime CapturedAt { get; set; } = DateTime.Now;
        public List<UIElementSnapshot> Elements { get; set; } = new List<UIElementSnapshot>();
        public string WindowTitle { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Snapshot jednotlivého UI elementu
    /// </summary>
    public class UIElementSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public string AutomationId { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool IsWinUI3Element { get; set; } = false;
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        public override string ToString()
        {
            return $"{ControlType}: {Name} ({ClassName}) at ({X}, {Y})";
        }
    }

    /// <summary>
    /// Sada zmien v UI
    /// </summary>
    public class UIChangeSet
    {
        public UISnapshot PreviousSnapshot { get; set; }
        public UISnapshot CurrentSnapshot { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
        public bool HasChanges { get; set; }
        public List<UIElementSnapshot> AddedElements { get; set; } = new List<UIElementSnapshot>();
        public List<UIElementSnapshot> RemovedElements { get; set; } = new List<UIElementSnapshot>();
        public List<(UIElementSnapshot Previous, UIElementSnapshot Current)> ModifiedElements { get; set; } = new List<(UIElementSnapshot, UIElementSnapshot)>();
        public string ChangeDescription { get; set; } = string.Empty;
    }

    /// <summary>
    /// Rozšírené informácie o UI elemente
    /// </summary>
    public class UIElementInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AutomationId { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public Rect BoundingRectangle { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
        public int ProcessId { get; set; }
        public IntPtr WindowHandle { get; set; }
        public AutomationElement AutomationElement { get; set; }

        // Extended properties
        public string ElementText { get; set; } = string.Empty;
        public string PlaceholderText { get; set; } = string.Empty;
        public string HelpText { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;

        // Table support
        public bool IsTableCell { get; set; } = false;
        public string TableCellIdentifier { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public int TableRow { get; set; } = -1;
        public int TableColumn { get; set; } = -1;
        public string TableColumnName { get; set; } = string.Empty;
        public string TableCellContent { get; set; } = string.Empty;
        public string TableType { get; set; } = string.Empty;
        public string TableInfo { get; set; } = string.Empty;

        public string GetUniqueIdentifier()
        {
            if (!string.IsNullOrEmpty(AutomationId))
                return $"AutoId_{AutomationId}";

            if (!string.IsNullOrEmpty(Name))
                return $"Name_{Name}";

            if (!string.IsNullOrEmpty(ElementText))
                return $"Text_{ElementText}";

            return $"Class_{ClassName}_Pos_{X}_{Y}";
        }

        public string GetTableCellBestIdentifier()
        {
            if (!IsTableCell)
                return GetUniqueIdentifier();

            if (!string.IsNullOrEmpty(TableCellIdentifier))
                return TableCellIdentifier;

            var parts = new List<string>();

            if (!string.IsNullOrEmpty(TableName))
                parts.Add($"Table:{CleanIdentifierText(TableName)}");

            if (!string.IsNullOrEmpty(TableColumnName))
                parts.Add($"Col:{CleanIdentifierText(TableColumnName)}");
            else if (TableColumn >= 0)
                parts.Add($"Col:{TableColumn}");

            if (TableRow >= 0)
                parts.Add($"Row:{TableRow}");

            return parts.Count > 0 ? string.Join("_", parts) : GetUniqueIdentifier();
        }

        public string GetTableCellDisplayName()
        {
            if (!IsTableCell)
                return Name;

            var parts = new List<string>();

            if (!string.IsNullOrEmpty(TableName))
                parts.Add(TableName);

            string columnPart = !string.IsNullOrEmpty(TableColumnName) ? TableColumnName : $"Col{TableColumn}";
            parts.Add(columnPart);

            if (TableRow >= 0)
                parts.Add($"R{TableRow}");

            if (!string.IsNullOrEmpty(TableCellContent) && TableCellContent.Length <= 15)
                parts.Add(CleanIdentifierText(TableCellContent));

            return string.Join("_", parts);
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

            return System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\d]", "_").Trim('_');
        }

        public override string ToString()
        {
            if (IsTableCell)
            {
                return $"TableCell: {GetTableCellDisplayName()} at ({X}, {Y}) in {TableName}";
            }

            return $"{ControlType}: {Name} ({ClassName}) at ({X}, {Y})";
        }
    }

    /// <summary>
    /// Štatistiky použitia UI elementov
    /// </summary>
    public class ElementUsageStats
    {
        public string ElementName { get; set; } = string.Empty;
        public int UsageCount { get; set; } = 0;
        public float Reliability { get; set; } = 1.0f;
        public string ElementType { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public int ClickCount { get; set; }
        public int KeyPressCount { get; set; }
        public int TotalUsage { get; set; }
        public DateTime FirstUsed { get; set; }
        public DateTime LastUsed { get; set; }
        public List<string> ActionsPerformed { get; set; } = new List<string>();

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
                return $"Unknown ({ControlType}): {TotalUsage} uses";

            return $"{ElementName} ({ControlType}): {TotalUsage} uses";
        }
    }

    #endregion

    #region Service Classes - Placeholder Implementations

    /// <summary>
    /// Automatický detektor okien
    /// </summary>
    public class AutoWindowDetector : IDisposable
    {
        public bool EnableDialogDetection { get; set; } = true;
        public bool EnableMessageBoxDetection { get; set; } = true;
        public bool EnableChildWindowDetection { get; set; } = true;
        public bool EnableWinUI3Detection { get; set; } = true;
        public DetectionSensitivity DetectionSensitivity { get; set; } = DetectionSensitivity.Medium;

        public event EventHandler<WindowActivatedEventArgs> WindowActivated;
        public event EventHandler<WindowClosedEventArgs> WindowClosed;

        private bool _isDetecting = false;

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
    /// Skaner UI elementov
    /// </summary>
    public class UIElementScanner : IDisposable
    {
        public int ScanInterval { get; set; } = 750;
        public bool EnableDeepScanning { get; set; } = true;
        public bool EnableWinUI3ElementDetection { get; set; } = true;
        public int MaxElementsPerScan { get; set; } = 100;

        public event EventHandler<UIElementsChangedEventArgs> ElementsChanged;
        public event EventHandler<NewElementDetectedEventArgs> NewElementDetected;
        public event EventHandler<ElementDisappearedEventArgs> ElementDisappeared;

        private bool _isScanning = false;

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
            System.Diagnostics.Debug.WriteLine($"➕ Added window to scan: {windowHandle}");
        }

        public void SwitchPrimaryWindow(IntPtr newPrimaryWindow)
        {
            System.Diagnostics.Debug.WriteLine($"🔄 Switched primary scan window: {newPrimaryWindow}");
        }

        public void Dispose()
        {
            StopScanning();
        }
    }

    /// <summary>
    /// Monitor okien
    /// </summary>
    public class WindowMonitor : IDisposable
    {
        public event EventHandler<NewWindowAppearedEventArgs> WindowAppeared;
        public event EventHandler<WindowClosedEventArgs> WindowClosed;

        private readonly List<string> _targetProcesses = new List<string>();
        private readonly HashSet<IntPtr> _knownWindows = new HashSet<IntPtr>();
        private bool _isMonitoring = false;

        public void StartMonitoring(string targetProcess = "")
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
                System.Diagnostics.Debug.WriteLine($"📝 Added target process: {processName}");
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
    /// Detektor zmien elementov
    /// </summary>
    public class ElementChangeDetector : IDisposable
    {
        public event EventHandler<ElementAddedEventArgs> ElementAdded;
        public event EventHandler<ElementRemovedEventArgs> ElementRemoved;
        public event EventHandler<ElementModifiedEventArgs> ElementModified;

        private bool _isDetecting = false;

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
    /// Inteligentný analyzátor UI
    /// </summary>
    public class SmartUIAnalyzer : IDisposable
    {
        public event EventHandler<PatternDetectedEventArgs> PatternDetected;
        public event EventHandler<AnomalyDetectedEventArgs> AnomalyDetected;

        private bool _isAnalyzing = false;

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
    /// Event args pre zmeny UI elementov
    /// </summary>
    public sealed class UIElementsChangedEventArgs : EventArgs
    {
        public UIElementsChangedEventArgs(
            IntPtr windowHandle,
            List<UIElementSnapshot> addedElements = null,
            List<UIElementSnapshot> removedElements = null,
            List<UIElementSnapshot> modifiedElements = null)
        {
            WindowHandle = windowHandle;
            AddedElements = addedElements ?? new List<UIElementSnapshot>();
            RemovedElements = removedElements ?? new List<UIElementSnapshot>();
            ModifiedElements = modifiedElements ?? new List<UIElementSnapshot>();
            Timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get; }
        public List<UIElementSnapshot> AddedElements { get; }
        public List<UIElementSnapshot> RemovedElements { get; }
        public List<UIElementSnapshot> ModifiedElements { get; }
        public DateTime Timestamp { get; }

        public bool HasChanges =>
            AddedElements.Count > 0 ||
            RemovedElements.Count > 0 ||
            ModifiedElements.Count > 0;
    }

    /// <summary>
    /// Event args pre nový detekovaný element
    /// </summary>
    public sealed class NewElementDetectedEventArgs : UIElementEventArgsBase
    {
        public NewElementDetectedEventArgs(IntPtr windowHandle, UIElementSnapshot element)
            : base(windowHandle, element)
        {
        }

        public DateTime DetectedAt => Timestamp;
    }

    /// <summary>
    /// Event args pre zmiznutý element
    /// </summary>
    public sealed class ElementDisappearedEventArgs : EventArgs
    {
        public ElementDisappearedEventArgs(
            IntPtr windowHandle,
            string elementIdentifier,
            UIElementSnapshot lastKnownState = null)
        {
            WindowHandle = windowHandle;
            ElementIdentifier = elementIdentifier ?? string.Empty;
            LastKnownState = lastKnownState;
            Timestamp = DateTime.Now;
        }

        public IntPtr WindowHandle { get; }
        public string ElementIdentifier { get; }
        public UIElementSnapshot LastKnownState { get; }
        public DateTime Timestamp { get; }
        public DateTime DisappearedAt => Timestamp;
    }

    #endregion


    //public class WinUI3BridgeInfo // - nachádza sa už v triede DebugTestHelper.cs
    //{
    //    public System.Drawing.Point Position { get; set; }
    //    public System.Drawing.Size Size { get; set; }
    //    public bool IsVisible { get; set; }
    //    public bool IsEnabled { get; set; }
    //    public int ChildCount { get; set; }
    //    public List<string> SupportedPatterns { get; set; } = new List<string>();
    //    public List<WinUI3ElementInfo> MeaningfulElements { get; set; } = new List<WinUI3ElementInfo>();
    //    public string ErrorMessage { get; set; } = "";
    //}

}
