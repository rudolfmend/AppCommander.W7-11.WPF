// SharedClasses.cs - Všetky pomocné triedy a event args na jednom mieste
using System;
using System.Collections.Generic;
using System.Windows.Automation;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF.Core
{
    #region Event Args Classes

    /// <summary>
    /// Event args pre detekovanú zmenu UI
    /// </summary>
    public class UIChangeDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowState WindowState { get; set; }
        public UIChangeSet Changes { get; set; }
    }

    /// <summary>
    /// Event args pre nové okno
    /// </summary>
    public class NewWindowAppearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public WindowType WindowType { get; set; }
        public bool AutoAdded { get; set; }
    }

    /// <summary>
    /// Event args pre interakciu s elementom
    /// </summary>
    public class ElementInteractionEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public UIElementSnapshot Element { get; set; }
        public InteractionType InteractionType { get; set; }
    }

    /// <summary>
    /// Event args pre window auto detection
    /// </summary>
    public class WindowAutoDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string Description { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
        public bool AutoSwitched { get; set; }
    }

    /// <summary>
    /// Event args pre zmenu stavu nahrávania
    /// </summary>
    public class RecordingStateChangedEventArgs : EventArgs
    {
        public bool IsRecording { get; set; }
        public bool IsPaused { get; set; }
        public string SequenceName { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }


    /// <summary>
    /// VLASTNÉ Event args pre objavenie sa okna (aby sa predišlo duplikátom)
    /// </summary>
    public class CustomWindowAppearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public WindowType WindowType { get; set; }
    }

    /// <summary>
    /// VLASTNÉ Event args pre zmiznutie okna (aby sa predišlo duplikátom)
    /// </summary>
    public class CustomWindowDisappearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        //public WindowTrackingInfo WindowInfo { get; set; }
    }

    /// <summary>
    /// Event args pre pridaný element
    /// </summary>
    public class ElementAddedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public UIElementSnapshot Element { get; set; }
    }

    /// <summary>
    /// Event args pre odstránený element
    /// </summary>
    public class ElementRemovedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string ElementIdentifier { get; set; }
    }

    /// <summary>
    /// Event args pre modifikovaný element
    /// </summary>
    public class ElementModifiedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public UIElementSnapshot Element { get; set; }
    }


    /// <summary>
    /// Event args pre detekovaný pattern
    /// </summary>
    public class PatternDetectedEventArgs : EventArgs
    {
        public string PatternType { get; set; }
        public float Confidence { get; set; }
        public object PatternData { get; set; }
    }


    /// <summary>
    /// Event args pre detekovanú anomáliu
    /// </summary>
    public class AnomalyDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string AnomalyType { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region Supporting Classes

    /// <summary>
    /// Stav sledovaného okna
    /// </summary>
    public class WindowState
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public WindowTrackingPriority Priority { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime LastActivated { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime LastChangeDetected { get; set; }
        public bool IsActive { get; set; } = true;
        public int ActivationCount { get; set; } = 0;
        public UISnapshot LastUISnapshot { get; set; }
        public List<UIChangeSet> ChangeHistory { get; set; } = new List<UIChangeSet>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        //public IntPtr WindowHandle { get; set; }
        //public string Title { get; set; }
        //public bool IsActive { get; set; }
        public DateTime LastSeen { get; set; }
        //public UISnapshot LastUISnapshot { get; set; }
        //public DateTime LastChangeDetected { get; set; }
    }


    public class WinUI3ApplicationAnalysis
    {
        public bool IsSuccessful { get; set; } = false;
        public string ErrorMessage { get; set; } = "";
        public string WindowTitle { get; set; } = "";
        public string WindowClass { get; set; } = "";
        public int BridgeCount { get; set; } = 0;
        public List<WinUI3BridgeInfo> Bridges { get; set; } = new List<WinUI3BridgeInfo>();
        public List<WinUI3ElementInfo> InteractiveElements { get; set; } = new List<WinUI3ElementInfo>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public string ApplicationName { get; set; }
        public string Version { get; set; }
        public bool IsWinUI3 { get; set; }
    }

    /// <summary>
    /// Snapshot UI stavu okna
    /// </summary>
    public class UISnapshot
    {
        public IntPtr WindowHandle { get; set; }
        public DateTime CapturedAt { get; set; }
        public List<UIElementSnapshot> Elements { get; set; } = new List<UIElementSnapshot>();
        public string WindowTitle { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Snapshot jednotlivého UI elementu
    /// </summary>
    public class UIElementSnapshot
    {
        public string Name { get; set; } = "";
        public string AutomationId { get; set; } = "";
        public string ControlType { get; set; } = "";
        public string ClassName { get; set; } = "";
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
        //public string Name { get; set; }
        //public string AutomationId { get; set; }
        //public string ClassName { get; set; }
        //public int X { get; set; }
        //public int Y { get; set; }
        //public int Width { get; set; }
        //public int Height { get; set; }
        //public bool IsEnabled { get; set; }
        //public bool IsVisible { get; set; }
        //public string Text { get; set; } = "";
        //public string Hash { get; set; } = "";
        //public bool IsWinUI3Element { get; set; } = false;
        //public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
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
        public List<(UIElementSnapshot Previous, UIElementSnapshot Current)> ModifiedElements { get; set; } = new List<(UIElementSnapshot, UIElementSnapshot)>();
        public string ChangeDescription { get; set; } = "";
        //public UISnapshot PreviousSnapshot { get; set; }
        //public UISnapshot CurrentSnapshot { get; set; }
        //public DateTime DetectedAt { get; set; }
        //public bool HasChanges { get; set; }
        //public List<UIElementSnapshot> AddedElements { get; set; } = new List<UIElementSnapshot>();
        //public List<UIElementSnapshot> RemovedElements { get; set; } = new List<UIElementSnapshot>();
        //public List<(UIElementSnapshot Previous, UIElementSnapshot Current)> ModifiedElements { get; set; } = new List<(UIElementSnapshot, UIElementSnapshot)>();
        //public string ChangeDescription { get; set; } = "";
    }

    //public class UIElementInfo
    //{
    //    public string Name { get; set; }
    //    public string Type { get; set; }
    //    public string AutomationId { get; set; }
    //    public bool IsEnabled { get; set; }
    //    public bool IsVisible { get; set; }
    //}

    public class UIElementInfo
    {
        public string Name { get; set; } = "";
        public string AutomationId { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string ControlType { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public System.Windows.Rect BoundingRectangle { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
        public int ProcessId { get; set; }
        public IntPtr WindowHandle { get; set; }
        public AutomationElement AutomationElement { get; set; }

        // Properties pre lepšiu identifikáciu
        public string ElementText { get; set; } = "";
        public string PlaceholderText { get; set; } = "";
        public string HelpText { get; set; } = "";
        public string AccessKey { get; set; } = "";

        /// <summary>
        /// Označuje či je element tabuľková bunka
        /// </summary>
        public bool IsTableCell { get; set; } = false;

        /// <summary>
        /// Identifikátor tabuľkovej bunky (Table:TableName_Col:ColumnName_Row:RowNumber)
        /// </summary>
        public string TableCellIdentifier { get; set; } = "";

        /// <summary>
        /// Názov tabuľky v ktorej sa bunka nachádza
        /// </summary>
        public string TableName { get; set; } = "";

        /// <summary>
        /// Číslo riadka bunky (0-based)
        /// </summary>
        public int TableRow { get; set; } = -1;

        /// <summary>
        /// Číslo stĺpca bunky (0-based)  
        /// </summary>
        public int TableColumn { get; set; } = -1;

        /// <summary>
        /// Názov stĺpca bunky
        /// </summary>
        public string TableColumnName { get; set; } = "";

        /// <summary>
        /// Obsah bunky
        /// </summary>
        public string TableCellContent { get; set; } = "";

        /// <summary>
        /// Typ tabuľky (DataGrid, List, Table, atď.)
        /// </summary>
        public string TableType { get; set; } = "";

        /// <summary>
        /// Dodatočné informácie o tabuľke
        /// </summary>
        public string TableInfo { get; set; } = "";

        /// <summary>
        /// Získa najlepší identifikátor pre tabuľkovú bunku
        /// </summary>
        public string GetTableCellBestIdentifier()
        {
            if (!IsTableCell)
                return GetUniqueIdentifier();

            // Pre tabuľkové bunky uprednostni TableCellIdentifier
            if (!string.IsNullOrEmpty(TableCellIdentifier))
                return TableCellIdentifier;

            // Fallback - vytvor identifikátor z dostupných informácií
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

        /// <summary>
        /// Vytvorí display name pre tabuľkovú bunku
        /// </summary>
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

        /// <summary>
        /// Skontroluje či je tabuľková bunka validná
        /// </summary>
        public bool IsValidTableCell()
        {
            return IsTableCell &&
                   TableRow >= 0 &&
                   TableColumn >= 0 &&
                   !string.IsNullOrEmpty(TableCellIdentifier);
        }

        /// <summary>
        /// Čistí text pre použitie v identifikátore
        /// </summary>
        private string CleanIdentifierText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\d]", "_").Trim('_');
        }

        /// <summary>
        /// Rozšírená ToString metóda s podporou tabuliek
        /// </summary>
        public override string ToString()
        {
            if (IsTableCell)
            {
                return $"TableCell: {GetTableCellDisplayName()} at ({X}, {Y}) in {TableName}";
            }

            return $"{ControlType}: {Name} ({ClassName}) at ({X}, {Y})";
        }
        public string GetUniqueIdentifier()
        {
            // Priorita identifikátorov
            if (!string.IsNullOrEmpty(AutomationId))
                return $"AutoId_{AutomationId}";

            if (!string.IsNullOrEmpty(Name))
                return $"Name_{Name}";

            if (!string.IsNullOrEmpty(ElementText))
                return $"Text_{ElementText}";

            return $"Class_{ClassName}_Pos_{X}_{Y}";
        }

        //public override string ToString()
        //{
        //    return $"{ControlType}: {Name} ({ClassName}) at ({X}, {Y})";
        //}
    }


    //public class ElementUsageStats
    //{
    //    public string ElementName { get; set; }
    //    public int UsageCount { get; set; }
    //    public DateTime LastUsed { get; set; }
    //    public string ElementType { get; set; }
    //

    /// <summary>
    /// Statistics for recorded UI elements usage
    /// </summary>
    public class ElementUsageStats
    {
        public string ElementName { get; set; } = "";
        public int UsageCount { get; set; } = 0;    // PRIDANÉ
        public float Reliability { get; set; } = 1.0f; // PRIDANÉ

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

    #region Enums

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

    #endregion

    #region Placeholder Classes (pre kompatibilitu)

    /// <summary>
    /// Automatický detektor okien - zjednodušený
    /// </summary>
    public class AutoWindowDetector
    {
        public bool EnableDialogDetection { get; set; } = true;
        public bool EnableMessageBoxDetection { get; set; } = true;
        public bool EnableChildWindowDetection { get; set; } = true;
        public bool EnableWinUI3Detection { get; set; } = true;
        public DetectionSensitivity DetectionSensitivity { get; set; } = DetectionSensitivity.Medium;

        public event EventHandler<AutoWindowDetectedEventArgs> NewWindowDetected;
        public event EventHandler<WindowActivatedEventArgs> WindowActivated;
        public event EventHandler<WindowClosedEventArgs> WindowClosed;

        private bool isDetecting = false;

        public void StartDetection(IntPtr primaryWindow, string targetProcess)
        {
            isDetecting = true;
            System.Diagnostics.Debug.WriteLine("🔍 AutoWindowDetector started");
        }

        public void StopDetection()
        {
            isDetecting = false;
            System.Diagnostics.Debug.WriteLine("🛑 AutoWindowDetector stopped");
        }
    }

    /// <summary>
    /// Skener UI elementov - zjednodušený
    /// </summary>
    public class UIElementScanner
    {
        public int ScanInterval { get; set; } = 750;
        public bool EnableDeepScanning { get; set; } = true;
        public bool EnableWinUI3ElementDetection { get; set; } = true;
        public int MaxElementsPerScan { get; set; } = 100;

        public event EventHandler<UIElementsChangedEventArgs> ElementsChanged;
        public event EventHandler<NewElementDetectedEventArgs> NewElementDetected;
        public event EventHandler<ElementDisappearedEventArgs> ElementDisappeared;

        private bool isScanning = false;

        public void StartScanning(IntPtr primaryWindow)
        {
            isScanning = true;
            System.Diagnostics.Debug.WriteLine("🔍 UIElementScanner started");
        }

        public void StopScanning()
        {
            isScanning = false;
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
    }
    //public class AutoWindowDetector
    //{
    //    public event EventHandler<NewWindowDetectedEventArgs> NewWindowDetected;
    //    //public event EventHandler<WindowActivatedEventArgs> WindowActivated;
    //    public event EventHandler<WindowClosedEventArgs> WindowClosed;
    //    private bool isDetecting;
    //}

    //public class ElementChangeDetector
    //{
    //    public event EventHandler<ElementAddedEventArgs> ElementAdded;
    //    public event EventHandler<ElementModifiedEventArgs> ElementModified;
    //    public event EventHandler<ElementRemovedEventArgs> ElementRemoved;
    //    private bool isDetecting;

    //    public void StartDetection()
    //    {
    //        isDetecting = true;
    //    }

    //    public void StopDetection()
    //    {
    //        isDetecting = false;
    //    }

    //    public void Dispose()
    //    {
    //        StopDetection();
    //    }
    //}

    //public class SmartUIAnalyzer
    //{
    //    public event EventHandler<PatternDetectedEventArgs> PatternDetected;
    //    public event EventHandler<AnomalyDetectedEventArgs> AnomalyDetected;

    //    public void StartAnalysis() { }
    //    public void StopAnalysis() { }
    //    public void AnalyzeChanges(WindowState windowState, UIChangeSet changes) { }
    //    public void Dispose() { }
    //}

    //public class UIElementScanner
    //{
    //    public event EventHandler ElementDisappeared;
    //    public event EventHandler ElementsChanged;
    //    public event EventHandler NewElementDetected;
    //    private bool isScanning;
    //}



    /// <summary>
    /// Monitor okien - OPRAVENÉ eventy
    /// </summary>
    public class WindowMonitor : IDisposable
    {
        public event EventHandler<CustomWindowAppearedEventArgs> WindowAppeared;
        public event EventHandler<CustomWindowDisappearedEventArgs> WindowDisappeared;

        // používa WindowActivatedEventArgs z WindowTracker.cs
        public event EventHandler<WindowTrackerEventArgs> WindowActivated;

        private readonly List<string> targetProcesses = new List<string>();
        private readonly HashSet<IntPtr> knownWindows = new HashSet<IntPtr>();
        private bool isMonitoring = false;

        public void StartMonitoring(string targetProcess = "")
        {
            isMonitoring = true;
            if (!string.IsNullOrEmpty(targetProcess))
            {
                AddTargetProcess(targetProcess);
            }
            System.Diagnostics.Debug.WriteLine("🔍 WindowMonitor started");
        }

        public void StopMonitoring()
        {
            isMonitoring = false;
            System.Diagnostics.Debug.WriteLine("🛑 WindowMonitor stopped");
        }

        public void AddTargetProcess(string processName)
        {
            if (!targetProcesses.Contains(processName))
            {
                targetProcesses.Add(processName);
                System.Diagnostics.Debug.WriteLine($"📝 Added target process: {processName}");
            }
        }

        public bool IsTargetProcess(string processName)
        {
            return targetProcesses.Contains(processName);
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

        private bool isDetecting = false;

        public void StartDetection()
        {
            isDetecting = true;
            System.Diagnostics.Debug.WriteLine("🔍 ElementChangeDetector started");
        }

        public void StopDetection()
        {
            isDetecting = false;
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

        private bool isAnalyzing = false;

        public void StartAnalysis()
        {
            isAnalyzing = true;
            System.Diagnostics.Debug.WriteLine("🧠 SmartUIAnalyzer started");
        }

        public void StopAnalysis()
        {
            isAnalyzing = false;
            System.Diagnostics.Debug.WriteLine("🛑 SmartUIAnalyzer stopped");
        }

        public void AnalyzeChanges(WindowState windowState, UIChangeSet changes)
        {
            if (!isAnalyzing) return;

            // Implementácia analýzy patterns a anomálií
            // Napríklad: detekcia opakujúcich sa patterns, neočakávaných zmien, atď.
        }

        public void Dispose()
        {
            StopAnalysis();
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
}
