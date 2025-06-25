// AutomaticUIHelpers.cs - OPRAVENÉ duplikátne event args
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
//using WindowTrackerEventArgs = AppCommander.W7_11.WPF.Core.WindowActivatedEventArgs;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Hlavný manažér pre automatickú detekciu UI zmien
    /// </summary>
    public class AutomaticUIManager
    {
        #region Properties and Events

        public bool IsMonitoringActive { get; private set; }
        public int MonitoringInterval { get; set; } = 500; // ms
        public bool EnableSmartDetection { get; set; } = true;
        public bool EnableWinUI3Support { get; set; } = true;

        public event EventHandler<UIChangeDetectedEventArgs> UIChangeDetected;
        public event EventHandler<NewWindowAppearedEventArgs> NewWindowAppeared;
        // OPRAVENÉ: Používa existujúce WindowClosedEventArgs z WindowTracker.cs
        public event EventHandler<WindowClosedEventArgs> WindowClosed;
        public event EventHandler<ElementInteractionEventArgs> ElementInteractionDetected;

        private readonly System.Threading.Timer monitoringTimer;
        private readonly WindowMonitor windowMonitor;
        private readonly ElementChangeDetector elementDetector;
        private readonly SmartUIAnalyzer smartAnalyzer;
        private readonly Dictionary<IntPtr, WindowState> trackedWindows;

        #endregion

        #region Constructor and Initialization

        public AutomaticUIManager()
        {
            trackedWindows = new Dictionary<IntPtr, WindowState>();
            windowMonitor = new WindowMonitor();
            elementDetector = new ElementChangeDetector();
            smartAnalyzer = new SmartUIAnalyzer();

            // Setup timer for monitoring
            monitoringTimer = new System.Threading.Timer(MonitoringTick, null, Timeout.Infinite, Timeout.Infinite);

            // Wire up events
            SetupEventHandlers();

            System.Diagnostics.Debug.WriteLine("🤖 AutomaticUIManager initialized");
        }

        private void SetupEventHandlers()
        {
            //windowMonitor.WindowAppeared += OnWindowAppeared;
            //windowMonitor.WindowDisappeared += OnWindowDisappeared;

            //windowMonitor.WindowActivated += (sender, e) => OnWindowActivated(sender, e);            // OPRAVENÉ: Cast na generic EventHandler

            elementDetector.ElementAdded += OnElementAdded;
            elementDetector.ElementRemoved += OnElementRemoved;
            elementDetector.ElementModified += OnElementModified;

            smartAnalyzer.PatternDetected += OnPatternDetected;
            smartAnalyzer.AnomalyDetected += OnAnomalyDetected;
        }

        #endregion

        #region Public Methods



        /// <summary>
        /// Spustí automatické monitorovanie
        /// </summary>
        public void StartMonitoring(IntPtr primaryWindow = default, string targetProcess = "")
        {
            try
            {
                if (IsMonitoringActive)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Monitoring already active");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("🚀 Starting automatic UI monitoring");

                // Setup primary targets
                if (primaryWindow != IntPtr.Zero)
                {
                    AddWindowToTracking(primaryWindow, WindowTrackingPriority.Primary);
                }

                if (!string.IsNullOrEmpty(targetProcess))
                {
                    AddProcessToTracking(targetProcess);
                }

                // Start monitoring components
                windowMonitor.StartMonitoring(targetProcess);
                elementDetector.StartDetection();

                if (EnableSmartDetection)
                {
                    smartAnalyzer.StartAnalysis();
                }

                // Start timer
                monitoringTimer.Change(MonitoringInterval, MonitoringInterval);
                IsMonitoringActive = true;

                System.Diagnostics.Debug.WriteLine("✅ Automatic UI monitoring started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error starting monitoring: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Zastaví automatické monitorovanie
        /// </summary>
        public void StopMonitoring()
        {
            try
            {
                if (!IsMonitoringActive)
                    return;

                System.Diagnostics.Debug.WriteLine("🛑 Stopping automatic UI monitoring");

                monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
                windowMonitor.StopMonitoring();
                elementDetector.StopDetection();
                smartAnalyzer.StopAnalysis();

                IsMonitoringActive = false;

                System.Diagnostics.Debug.WriteLine("✅ Automatic UI monitoring stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error stopping monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá okno do sledovania
        /// </summary>
        public void AddWindowToTracking(IntPtr windowHandle, WindowTrackingPriority priority = WindowTrackingPriority.Normal)
        {
            try
            {
                if (trackedWindows.ContainsKey(windowHandle))
                {
                    trackedWindows[windowHandle].Priority = priority;
                    return;
                }

                var windowState = new WindowState
                {
                    WindowHandle = windowHandle,
                    Title = GetWindowTitle(windowHandle),
                    ProcessName = GetProcessName(windowHandle),
                    Priority = priority,
                    AddedAt = DateTime.Now,
                    LastUISnapshot = CaptureUISnapshot(windowHandle)
                };

                trackedWindows[windowHandle] = windowState;

                System.Diagnostics.Debug.WriteLine($"➕ Added window to tracking: {windowState.Title} ({priority})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding window to tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá proces do sledovania
        /// </summary>
        public void AddProcessToTracking(string processName)
        {
            try
            {
                windowMonitor.AddTargetProcess(processName);
                System.Diagnostics.Debug.WriteLine($"📝 Added process to tracking: {processName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding process to tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Získa aktuálny stav sledovaných okien
        /// </summary>
        public List<WindowState> GetTrackedWindows()
        {
            return trackedWindows.Values.Where(w => w.IsActive).ToList();
        }

        /// <summary>
        /// Vynúti okamžité skenovanie zmien
        /// </summary>
        public void ForceUIRefresh()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 Forcing UI refresh scan");
                PerformUIChangeDetection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in forced UI refresh: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods - Core Logic

        /// <summary>
        /// Hlavný tick pre monitorovanie
        /// </summary>
        private void MonitoringTick(object state)
        {
            if (!IsMonitoringActive)
                return;

            try
            {
                PerformUIChangeDetection();
                CleanupInactiveWindows();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in monitoring tick: {ex.Message}");
            }
        }

        /// <summary>
        /// Vykonáva detekciu zmien UI
        /// </summary>
        private void PerformUIChangeDetection()
        {
            var activeWindows = trackedWindows.Values.Where(w => w.IsActive).ToList();

            Parallel.ForEach(activeWindows, windowState =>
            {
                try
                {
                    DetectChangesInWindow(windowState);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error detecting changes in window {windowState.Title}: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Detekuje zmeny v konkrétnom okne
        /// </summary>
        private void DetectChangesInWindow(WindowState windowState)
        {
            // Skontroluj či okno ešte existuje
            if (!IsWindow(windowState.WindowHandle))
            {
                MarkWindowAsInactive(windowState);
                return;
            }

            // Zachytí nový UI snapshot
            var currentSnapshot = CaptureUISnapshot(windowState.WindowHandle);

            // Porovnaj s predošlým snapshot
            var changes = CompareUISnapshots(windowState.LastUISnapshot, currentSnapshot);

            if (changes.HasChanges)
            {
                ProcessUIChanges(windowState, changes);
                windowState.LastUISnapshot = currentSnapshot;
                windowState.LastChangeDetected = DateTime.Now;
            }
        }

        /// <summary>
        /// Zachytí snapshot UI elementov
        /// </summary>
        private UISnapshot CaptureUISnapshot(IntPtr windowHandle)
        {
            try
            {
                var snapshot = new UISnapshot
                {
                    WindowHandle = windowHandle,
                    CapturedAt = DateTime.Now,
                    Elements = new List<UIElementSnapshot>()
                };

                // Získaj všetky UI elementy
                var elements = AdaptiveElementFinder.GetAllInteractiveElements(windowHandle);

                foreach (var element in elements)
                {
                    snapshot.Elements.Add(new UIElementSnapshot
                    {
                        Name = element.Name,
                        AutomationId = element.AutomationId,
                        ControlType = element.ControlType,
                        ClassName = element.ClassName,
                        X = element.X,
                        Y = element.Y,
                        Width = (int)element.BoundingRectangle.Width,
                        Height = (int)element.BoundingRectangle.Height,
                        IsEnabled = element.IsEnabled,
                        IsVisible = element.IsVisible,
                        Text = element.ElementText,
                        Hash = CalculateElementHash(element)
                    });
                }

                // WinUI3 špeciálne spracovanie
                if (EnableWinUI3Support)
                {
                    var winui3Elements = GetWinUI3ElementsSnapshot(windowHandle);
                    snapshot.Elements.AddRange(winui3Elements);
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error capturing UI snapshot: {ex.Message}");
                return new UISnapshot { WindowHandle = windowHandle, CapturedAt = DateTime.Now };
            }
        }

        /// <summary>
        /// Konvertuje WindowTrackingInfo na WindowDetectionInfo
        /// </summary>
        private WindowDetectionInfo ConvertToWindowDetectionInfo(WindowTrackingInfo trackingInfo)
        {
            return new WindowDetectionInfo
            {
                WindowHandle = trackingInfo.WindowHandle,
                Title = trackingInfo.Title,
                ProcessName = trackingInfo.ProcessName,
                WindowType = trackingInfo.WindowType,
                IsModal = trackingInfo.IsModal,
                DetectedAt = trackingInfo.DetectedAt,
                ClassName = trackingInfo.ClassName ?? ""
            };
        }

        /// <summary>
        /// Porovná dva UI snapshots
        /// </summary>
        private UIChangeSet CompareUISnapshots(UISnapshot previous, UISnapshot current)
        {
            var changeSet = new UIChangeSet
            {
                PreviousSnapshot = previous,
                CurrentSnapshot = current,
                DetectedAt = DateTime.Now
            };

            if (previous == null || previous.Elements == null)
            {
                // Prvý snapshot - všetky elementy sú nové
                changeSet.AddedElements = current.Elements.ToList();
                changeSet.HasChanges = changeSet.AddedElements.Any();
                return changeSet;
            }

            var previousElements = previous.Elements.ToDictionary(e => e.Hash, e => e);
            var currentElements = current.Elements.ToDictionary(e => e.Hash, e => e);

            // Nájdi pridané elementy
            changeSet.AddedElements = currentElements.Values
                .Where(e => !previousElements.ContainsKey(e.Hash))
                .ToList();

            // Nájdi odstránené elementy
            changeSet.RemovedElements = previousElements.Values
                .Where(e => !currentElements.ContainsKey(e.Hash))
                .ToList();

            // Nájdi modifikované elementy (rovnaký ID ale iný hash)
            changeSet.ModifiedElements = new List<(UIElementSnapshot Previous, UIElementSnapshot Current)>();

            foreach (var currentElement in currentElements.Values)
            {
                var previousElement = previousElements.Values
                    .FirstOrDefault(e => e.AutomationId == currentElement.AutomationId &&
                                        e.Name == currentElement.Name &&
                                        e.Hash != currentElement.Hash);

                if (previousElement != null)
                {
                    changeSet.ModifiedElements.Add((previousElement, currentElement));
                }
            }

            changeSet.HasChanges = changeSet.AddedElements.Any() ||
                                   changeSet.RemovedElements.Any() ||
                                   changeSet.ModifiedElements.Any();

            return changeSet;
        }

        /// <summary>
        /// Spracuje detekované UI zmeny
        /// </summary>
        private void ProcessUIChanges(WindowState windowState, UIChangeSet changes)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 UI changes detected in: {windowState.Title}");
                System.Diagnostics.Debug.WriteLine($"   Added: {changes.AddedElements.Count}");
                System.Diagnostics.Debug.WriteLine($"   Removed: {changes.RemovedElements.Count}");
                System.Diagnostics.Debug.WriteLine($"   Modified: {changes.ModifiedElements.Count}");

                // Aktualizuj window state
                windowState.ChangeHistory.Add(changes);

                // Limituj históriu na posledných 10 zmien
                if (windowState.ChangeHistory.Count > 10)
                {
                    windowState.ChangeHistory.RemoveAt(0);
                }

                // Trigger event
                UIChangeDetected?.Invoke(this, new UIChangeDetectedEventArgs
                {
                    WindowHandle = windowState.WindowHandle,
                    WindowState = windowState,
                    Changes = changes
                });

                // Analyzuj zmeny ak je smart detection zapnuté
                if (EnableSmartDetection)
                {
                    smartAnalyzer.AnalyzeChanges(windowState, changes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error processing UI changes: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        //// OPRAVENÉ: Používa CustomWindowAppearedEventArgs namiesto duplikátnych
        //private void OnWindowAppeared(object sender, CustomWindowAppearedEventArgs e)
        //{
        //    try
        //    {
        //        System.Diagnostics.Debug.WriteLine($"🪟 New window appeared: {e.WindowTitle}");

        //        // Automaticky pridaj do tracking ak spĺňa kritériá
        //        if (ShouldAutoTrackWindow(e))
        //        {
        //            AddWindowToTracking(e.WindowHandle, DetermineTrackingPriority(e));

        //            NewWindowAppeared?.Invoke(this, new NewWindowAppearedEventArgs
        //            {
        //                WindowHandle = e.WindowHandle,
        //                WindowTitle = e.WindowTitle,
        //                ProcessName = e.ProcessName,
        //                WindowType = e.WindowType,
        //                AutoAdded = true
        //            });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"❌ Error handling window appeared: {ex.Message}");
        //    }
        //}

        //// OPRAVENÉ: Používa CustomWindowDisappearedEventArgs
        //private void OnWindowDisappeared(object sender, CustomWindowDisappearedEventArgs e)
        //{
        //    try
        //    {
        //        if (trackedWindows.ContainsKey(e.WindowHandle))
        //        {
        //            var windowState = trackedWindows[e.WindowHandle];
        //            MarkWindowAsInactive(windowState);

        //            System.Diagnostics.Debug.WriteLine($"🗑️ Tracked window disappeared: {windowState.Title}");

        //            // OPRAVENÉ: Používa existujúci WindowClosedEventArgs z WindowTracker.cs
        //            WindowClosed?.Invoke(this, new WindowClosedEventArgs
        //            {
        //                WindowHandle = e.WindowHandle,
        //                WindowInfo = CreateWindowTrackingInfo(windowState)
        //            });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"❌ Error handling window disappeared: {ex.Message}");
        //    }
        //}

        // Používa existujúci WindowActivatedEventArgs z WindowTracker.cs
        /// <summary>
        /// Handler pre aktivované okno
        /// </summary>
        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🎯 Window activated: {e.WindowInfo?.Title ?? "Unknown"}");

                // Ak je to tracked window, aktualizuj jeho stav
                //if (trackedWindows.ContainsKey(e.WindowHandle))
                //{
                //    // Aktualizuj kontext ak existuje
                //    if (windowContexts.ContainsKey(e.WindowHandle))
                //    {
                //        var context = windowContexts[e.WindowHandle];
                //        context.IsActive = true;
                //        context.LastActivated = DateTime.Now;

                //        // Trigger event
                //        WindowContextChanged?.Invoke(this, new WindowContextChangedEventArgs
                //        {
                //            WindowHandle = e.WindowHandle,
                //            Context = context,
                //            ChangeType = ContextChangeType.Activated
                //        });
                //    }
                //}
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling window activation: {ex.Message}");
            }
        }

        private void OnElementAdded(object sender, ElementAddedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"➕ UI element added: {e.Element.Name} in {GetWindowTitle(e.WindowHandle)}");

                // Trigger interaction detection ak je element interaktívny
                if (IsInteractiveElement(e.Element))
                {
                    ElementInteractionDetected?.Invoke(this, new ElementInteractionEventArgs
                    {
                        WindowHandle = e.WindowHandle,
                        Element = e.Element,
                        InteractionType = InteractionType.ElementAppeared
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling element added: {ex.Message}");
            }
        }

        private void OnElementRemoved(object sender, ElementRemovedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"➖ UI element removed: {e.ElementIdentifier} from {GetWindowTitle(e.WindowHandle)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling element removed: {ex.Message}");
            }
        }

        private void OnElementModified(object sender, ElementModifiedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 UI element modified: {e.Element.Name} in {GetWindowTitle(e.WindowHandle)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling element modified: {ex.Message}");
            }
        }

        private void OnPatternDetected(object sender, PatternDetectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 UI pattern detected: {e.PatternType} (confidence: {e.Confidence:P})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling pattern detection: {ex.Message}");
            }
        }

        private void OnAnomalyDetected(object sender, AnomalyDetectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ UI anomaly detected: {e.AnomalyType} in {GetWindowTitle(e.WindowHandle)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling anomaly detection: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// PRIDANÉ: Vytvorí WindowTrackingInfo z WindowState
        /// </summary>
        private WindowTrackingInfo CreateWindowTrackingInfo(WindowState windowState)
        {
            return new WindowTrackingInfo
            {
                WindowHandle = windowState.WindowHandle,
                Title = windowState.Title,
                ProcessName = windowState.ProcessName,
                IsActive = windowState.IsActive,
                LastActivated = windowState.LastActivated,
                DetectedAt = windowState.AddedAt
            };
        }

        /// <summary>
        /// Získa WinUI3 elementy snapshot
        /// </summary>
        private List<UIElementSnapshot> GetWinUI3ElementsSnapshot(IntPtr windowHandle)
        {
            var elements = new List<UIElementSnapshot>();

            try
            {
                // OPRAVENÉ: Skontrolovať či existuje WinUI3ApplicationAnalysis
                var winui3Analysis = DebugTestHelper.AnalyzeWinUI3Application(windowHandle);

                // OPRAVENÉ: Predpokladáme že má property IsWinUI3Application
                if (winui3Analysis != null && HasProperty(winui3Analysis, "IsWinUI3Application"))
                {
                    var isWinUI3 = GetPropertyValue<bool>(winui3Analysis, "IsWinUI3Application");
                    if (isWinUI3)
                    {
                        var interactiveElements = GetPropertyValue<List<WinUI3ElementInfo>>(winui3Analysis, "InteractiveElements");
                        if (interactiveElements != null)
                        {
                            foreach (var element in interactiveElements)
                            {
                                elements.Add(new UIElementSnapshot
                                {
                                    Name = element.Name ?? "",
                                    AutomationId = element.AutomationId ?? "",
                                    ControlType = element.ControlType ?? "",
                                    ClassName = GetPropertyValue<string>(element, "ClassName") ?? "",
                                    //X = element.Position?.X ?? 0,
                                    //Y = element.Position?.Y ?? 0,
                                    IsEnabled = true,
                                    IsVisible = true,
                                    Text = element.Text ?? "",
                                    Hash = CalculateElementHash(element),
                                    IsWinUI3Element = true
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error getting WinUI3 elements: {ex.Message}");
            }

            return elements;
        }

        /// <summary>
        /// PRIDANÉ: Helper pre kontrolu existencie property
        /// </summary>
        private bool HasProperty(object obj, string propertyName)
        {
            return obj.GetType().GetProperty(propertyName) != null;
        }

        /// <summary>
        /// PRIDANÉ: Helper pre získanie hodnoty property
        /// </summary>
        private T GetPropertyValue<T>(object obj, string propertyName)
        {
            var property = obj.GetType().GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(obj);
                if (value is T)
                    return (T)value;
            }
            return default(T);
        }

        /// <summary>
        /// Vypočítava hash pre UI element
        /// </summary>
        private string CalculateElementHash(UIElementInfo element)
        {
            var hashSource = $"{element.Name}|{element.AutomationId}|{element.ControlType}|{element.X}|{element.Y}|{element.ElementText}";
            return hashSource.GetHashCode().ToString();
        }

        /// <summary>
        /// Vypočítava hash pre WinUI3 element
        /// </summary>
        private string CalculateElementHash(WinUI3ElementInfo element)
        {
            var hashSource = $"{element.Name}|{element.AutomationId}|{element.ControlType}|{element.Position.X}|{element.Position.Y}|{element.Text}";
            return hashSource.GetHashCode().ToString();
        }

        /// <summary>
        /// Rozhodne či automaticky sledovať okno
        /// </summary>
        private bool ShouldAutoTrackWindow(CustomWindowAppearedEventArgs e)
        {
            // Vždy sleduj dialógy a message boxy
            if (e.WindowType == WindowType.Dialog || e.WindowType == WindowType.MessageBox)
                return true;

            // Sleduj okná z target procesov
            if (windowMonitor.IsTargetProcess(e.ProcessName))
                return true;

            return false;
        }

        /// <summary>
        /// Určí prioritu sledovania okna
        /// </summary>
        private WindowTrackingPriority DetermineTrackingPriority(CustomWindowAppearedEventArgs e)
        {
            if (e.WindowType == WindowType.MessageBox)
                return WindowTrackingPriority.Critical;
            if (e.WindowType == WindowType.Dialog)
                return WindowTrackingPriority.High;
            if (windowMonitor.IsTargetProcess(e.ProcessName))
                return WindowTrackingPriority.Normal;

            return WindowTrackingPriority.Low;
        }

        /// <summary>
        /// Kontroluje či je element interaktívny
        /// </summary>
        private bool IsInteractiveElement(UIElementSnapshot element)
        {
            var interactiveTypes = new[] { "Button", "Edit", "ComboBox", "CheckBox", "RadioButton", "ListItem", "MenuItem" };
            return interactiveTypes.Contains(element.ControlType);
        }

        /// <summary>
        /// Označí okno ako neaktívne
        /// </summary>
        private void MarkWindowAsInactive(WindowState windowState)
        {
            windowState.IsActive = false;
            windowState.ClosedAt = DateTime.Now;
        }

        /// <summary>
        /// Vyčistí neaktívne okná
        /// </summary>
        private void CleanupInactiveWindows()
        {
            var cutoffTime = DateTime.Now.AddMinutes(-5);
            var toRemove = trackedWindows.Where(kvp =>
                !kvp.Value.IsActive &&
                kvp.Value.ClosedAt.HasValue &&
                kvp.Value.ClosedAt.Value < cutoffTime).ToList();

            foreach (var item in toRemove)
            {
                trackedWindows.Remove(item.Key);
                System.Diagnostics.Debug.WriteLine($"🧹 Cleaned up inactive window: {item.Value.Title}");
            }
        }

        /// <summary>
        /// Získa title okna
        /// </summary>
        private string GetWindowTitle(IntPtr windowHandle)
        {
            try
            {
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(windowHandle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "Unknown Window";
            }
        }

        /// <summary>
        /// Získa meno procesu
        /// </summary>
        private string GetProcessName(IntPtr windowHandle)
        {
            try
            {
                GetWindowThreadProcessId(windowHandle, out uint processId);
                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "Unknown Process";
            }
        }

        #endregion

        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                StopMonitoring();
                monitoringTimer?.Dispose();
                windowMonitor?.Dispose();
                elementDetector?.Dispose();
                smartAnalyzer?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error disposing AutomaticUIManager: {ex.Message}");
            }
        }

        internal IEnumerable<object> GetNewWindows()
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    #region Supporting Classes



    /// <summary>
    /// Priorita sledovania okna
    /// </summary>
    public enum WindowTrackingPriority
    {
        Low,
        Normal,
        High,
        Critical,
        Primary
    }
    
    #endregion
}
