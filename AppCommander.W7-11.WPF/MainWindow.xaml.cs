using AppCommander.W7_11.WPF.Core;
using Microsoft.Win32; // WPF dialógy
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommandType = AppCommander.W7_11.WPF.Core.CommandType;
using WindowState = AppCommander.W7_11.WPF.Core.WindowState;

namespace AppCommander.W7_11.WPF
{
    public partial class MainWindow : Window
    {
        #region Private Fields

        private readonly CommandRecorder recorder;
        private readonly CommandPlayer player;
        private readonly ObservableCollection<Command> commands;
        private readonly ObservableCollection<ElementUsageStats> elementStatsList;
        private readonly ActionSimulator actionSimulator;

        private IntPtr targetWindowHandle = IntPtr.Zero;
        private string currentFilePath = string.Empty;
        private bool hasUnsavedChanges = false;

        // **WinUI3 support properties**
        private WinUI3ApplicationAnalysis currentWinUI3Analysis;
        private bool isWinUI3Application = false;

        // **Automatická detekcia - zjednodušené vlastnosti**
        private readonly AutomaticUIManager automaticUIManager;
        private readonly DispatcherTimer windowScanTimer;
        private readonly Dictionary<IntPtr, WindowTrackingData> activeWindows;
        private bool isAutoDetectionEnabled = true;
        private bool isRecordingUIElements = false;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            // Inicializácia základných komponentov
            InitializeComponents();

            // Inicializácia automatického systému
            InitializeAutomaticSystem();

            UpdateUI();
            System.Diagnostics.Debug.WriteLine("AppCommander initialized with Automatic Window Detection");
        }

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Inicializuje základné komponenty
        /// </summary>
        private void InitializeComponents()
        {
            recorder = new CommandRecorder();
            recorder.EnableWinUI3Analysis = true;
            recorder.EnableDetailedLogging = true;
            recorder.AutoDetectNewWindows = true;
            recorder.AutoSwitchToNewWindows = true;

            player = new CommandPlayer();
            player.PreferElementIdentifiers = true;
            player.EnableAdaptiveFinding = true;
            player.MaxElementSearchAttempts = 3;

            commands = new ObservableCollection<Command>();
            elementStatsList = new ObservableCollection<ElementUsageStats>();
            actionSimulator = new ActionSimulator();

            // Setup data bindings
            dgCommands.ItemsSource = commands;
            lstElementStats.ItemsSource = elementStatsList;

            // Subscribe to events
            SubscribeToEvents();
        }

        /// <summary>
        /// Inicializuje automatický UI systém
        /// </summary>
        private void InitializeAutomaticSystem()
        {
            automaticUIManager = new AutomaticUIManager();
            activeWindows = new Dictionary<IntPtr, WindowTrackingData>();

            // Timer pre pravidelné skenovanie okien
            windowScanTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // Skenuj každých 500ms
            };
            windowScanTimer.Tick += WindowScanTimer_Tick;

            // Nastavenie eventov pre automatickú detekciu
            SetupAutomaticDetectionEvents();

            // Spustenie automatického systému
            StartAutomaticDetection();
        }

        /// <summary>
        /// Nastavenie eventov pre automatickú detekciu
        /// </summary>
        private void SetupAutomaticDetectionEvents()
        {
            // Eventy z automatického UI managera
            automaticUIManager.UIChangeDetected += OnAutomaticUIChangeDetected;
            automaticUIManager.NewWindowAppeared += OnAutomaticNewWindowDetected;
            automaticUIManager.WindowClosed += OnAutomaticWindowClosed;

            // Eventy od recordera
            recorder.WindowAutoDetected += OnWindowAutoDetected;
        }

        /// <summary>
        /// Spustenie automatického systému detekcie
        /// </summary>
        private void StartAutomaticDetection()
        {
            if (isAutoDetectionEnabled)
            {
                automaticUIManager.StartMonitoring();
                windowScanTimer.Start();

                System.Diagnostics.Debug.WriteLine("🔍 Automatic window detection started");
                UpdateStatusMessage("Auto-detection: ON");
            }
        }

        #endregion

        #region Recording Methods - Zjednodušené

        /// <summary>
        /// Spustí nahrávanie s automatickou detekciou
        /// </summary>
        private void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.", "No Target Window",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string sequenceName = txtSequenceName?.Text ?? "Auto Recording";
                if (string.IsNullOrWhiteSpace(sequenceName))
                {
                    sequenceName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}";
                }

                // Skontroluj či už beží nahrávanie
                if (recorder.IsRecording)
                {
                    var result = MessageBox.Show(
                        "Recording is already in progress. Stop current recording and start new one?",
                        "Recording in Progress",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                        return;

                    recorder.StopRecording();
                }

                // Spusti nahrávanie s automatickou detekciou
                StartRecordingWithAutoDetection(sequenceName);

                System.Diagnostics.Debug.WriteLine("✅ Recording with auto-detection started");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start recording: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ Error starting recording: {ex.Message}");
            }
        }

        /// <summary>
        /// Spustí nahrávanie s automatickou detekciou
        /// </summary>
        private void StartRecordingWithAutoDetection(string sequenceName)
        {
            try
            {
                // Vyčisti existujúce príkazy ak je potrebné
                if (commands.Count > 0)
                {
                    var result = MessageBox.Show(
                        "Clear existing commands and start fresh recording?",
                        "Clear Commands",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        commands.Clear();
                        elementStatsList.Clear();
                    }
                }

                // Spusti automatické UI monitorovanie
                if (automaticUIManager != null)
                {
                    automaticUIManager.StartMonitoring(targetWindowHandle, GetProcessNameFromWindow(targetWindowHandle));
                    isRecordingUIElements = true;
                }

                // Konfigurácia recordera
                recorder.AutoDetectNewWindows = true;
                recorder.AutoSwitchToNewWindows = true;
                recorder.LogWindowChanges = true;
                recorder.StartRecording(sequenceName, targetWindowHandle);

                // Aktualizuj UI
                UpdateRecordingUI(true);

                // Pridaj počiatočné info o target okne
                LogRecordingInfo();

                System.Diagnostics.Debug.WriteLine($"🎬 Recording started: {sequenceName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in StartRecordingWithAutoDetection: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region UI Update Methods - Zjednodušené

        /// <summary>
        /// Aktualizuje UI pre nahrávanie
        /// </summary>
        private void UpdateRecordingUI(bool isRecording)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Aktualizuj status
                    if (txtStatus != null)
                    {
                        txtStatus.Text = isRecording
                            ? "Recording (Auto-detect ON)"
                            : "Ready";
                    }

                    // Aktualizuj indikátory
                    UpdateAutoDetectionIndicators(isRecording);

                    // Aktualizuj progress bar ak existuje
                    if (progressEnhancedRecording != null)
                    {
                        progressEnhancedRecording.IsIndeterminate = isRecording;
                        progressEnhancedRecording.Visibility = isRecording ? Visibility.Visible : Visibility.Collapsed;
                    }

                    UpdateUI(); // Existujúca metóda
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating recording UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizuje indikátory automatickej detekcie
        /// </summary>
        private void UpdateAutoDetectionIndicators(bool isRecording)
        {
            // Aktualizuj indikátor automatickej detekcie
            if (lblAutoDetectionStatus != null)
            {
                lblAutoDetectionStatus.Content = isRecording && isAutoDetectionEnabled
                    ? "🟢 Auto-Detection Active"
                    : "🔴 Auto-Detection Inactive";
            }

            // Aktualizuj UI element recording indikátor
            if (lblUIRecordingStatus != null)
            {
                lblUIRecordingStatus.Content = isRecordingUIElements
                    ? "🟢 UI Scanning Active"
                    : "🔴 UI Scanning Inactive";
            }
        }

        /// <summary>
        /// Loguje informácie o nahrávaní
        /// </summary>
        private void LogRecordingInfo()
        {
            try
            {
                var targetInfo = GetWindowInfo(targetWindowHandle);
                var processName = GetProcessNameFromWindow(targetWindowHandle);

                LogToUI("=== RECORDING STARTED ===");
                LogToUI($"Target: {targetInfo}");
                LogToUI($"Process: {processName}");
                LogToUI($"Auto-Detection: {(isAutoDetectionEnabled ? "Enabled" : "Disabled")}");
                LogToUI($"UI Scanning: {(isRecordingUIElements ? "Enabled" : "Disabled")}");
                LogToUI($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogToUI("==============================");

                // Ak je automatický UI manager aktívny, zobraz počet sledovaných okien
                if (automaticUIManager?.IsMonitoringActive == true)
                {
                    var trackedCount = automaticUIManager.GetTrackedWindows().Count;
                    LogToUI($"Tracking {trackedCount} windows automatically");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error logging recording info: {ex.Message}");
            }
        }

        #endregion

        #region Automatic UI Management - Zjednodušené

        /// <summary>
        /// Automaticky aktualizuje UI elementy pre všetky aktívne okná
        /// </summary>
        private void AutoRefreshAllUIElements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 Starting auto-refresh of all UI elements");

                // Ak automatický manager beží, vynúť refresh
                if (automaticUIManager?.IsMonitoringActive == true)
                {
                    automaticUIManager.ForceUIRefresh();
                    LogToUI("Auto-refreshed all tracked windows");
                }
                else
                {
                    // Fallback - refresh target window
                    if (targetWindowHandle != IntPtr.Zero)
                    {
                        RefreshTargetWindowElements();
                    }
                    else
                    {
                        MessageBox.Show("No target window selected for refresh.", "No Target",
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                System.Diagnostics.Debug.WriteLine("✅ Auto-refresh completed");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during auto-refresh: {ex.Message}", "Refresh Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ Error in auto-refresh: {ex.Message}");
            }
        }

        /// <summary>
        /// Obnoví UI elementy target okna
        /// </summary>
        private void RefreshTargetWindowElements()
        {
            try
            {
                if (targetWindowHandle == IntPtr.Zero)
                    return;

                Task.Run(() =>
                {
                    try
                    {
                        // Získaj nové UI elementy
                        var currentElements = AdaptiveElementFinder.GetAllInteractiveElements(targetWindowHandle);

                        Dispatcher.InvokeAsync(() =>
                        {
                            // Aktualizuj element statistics
                            RefreshElementStatistics(currentElements);

                            // Ak beží nahrávanie, aktualizuj existujúce príkazy
                            if (recorder?.IsRecording == true)
                            {
                                AdaptiveElementFinder.UpdateCommandsForCurrentWindow(targetWindowHandle, commands.ToList());
                            }

                            LogToUI($"Refreshed {currentElements.Count} UI elements for target window");

                            System.Diagnostics.Debug.WriteLine($"🔄 Refreshed {currentElements.Count} elements for target window");
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Error refreshing target window elements: {ex.Message}");
                        Dispatcher.InvokeAsync(() =>
                        {
                            LogToUI($"Error refreshing UI elements: {ex.Message}");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in RefreshTargetWindowElements: {ex.Message}");
            }
        }

        /// <summary>
        /// Automaticky detekuje a pridá nové okno do sledovania
        /// </summary>
        private void AutoDetectAndAddWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 Starting automatic window detection");

                // Získaj všetky aktuálne okná
                var allWindows = GetAllVisibleWindows();
                var newWindows = new List<WindowDetectionInfo>();

                foreach (var window in allWindows)
                {
                    // Skontroluj či už nie je sledované
                    if (!activeWindows.ContainsKey(window))
                    {
                        var windowInfo = AnalyzeWindow(window);
                        if (windowInfo != null && ShouldAutoTrackWindow(windowInfo))
                        {
                            newWindows.Add(windowInfo);
                        }
                    }
                }

                if (newWindows.Any())
                {
                    var result = MessageBox.Show(
                        $"Found {newWindows.Count} new windows to track:\n\n" +
                        string.Join("\n", newWindows.Select(w => $"- {w.Title} ({w.WindowType})")),
                        "New Windows Detected",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        foreach (var windowInfo in newWindows)
                        {
                            AutomaticallyAddWindow(windowInfo);
                        }

                        LogToUI($"Added {newWindows.Count} new windows to tracking");
                    }
                }
                else
                {
                    MessageBox.Show("No new windows found to track.", "Detection Complete",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Auto-detection completed - found {newWindows.Count} new windows");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during window detection: {ex.Message}", "Detection Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ Error in auto window detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Prepína medzi automatickými režimami
        /// </summary>
        private void ToggleAutomaticMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prepni automatickú detekciu
                isAutoDetectionEnabled = !isAutoDetectionEnabled;

                if (isAutoDetectionEnabled)
                {
                    // Spusti automatické služby
                    if (automaticUIManager != null && !automaticUIManager.IsMonitoringActive)
                    {
                        var targetProcess = GetProcessNameFromWindow(targetWindowHandle);
                        automaticUIManager.StartMonitoring(targetWindowHandle, targetProcess);
                    }

                    if (!windowScanTimer.IsEnabled)
                    {
                        windowScanTimer.Start();
                    }

                    LogToUI("🟢 Automatic mode: ENABLED");
                    System.Diagnostics.Debug.WriteLine("🟢 Automatic mode enabled");
                }
                else
                {
                    // Zastaví automatické služby
                    automaticUIManager?.StopMonitoring();
                    windowScanTimer.Stop();

                    LogToUI("🔴 Automatic mode: DISABLED");
                    System.Diagnostics.Debug.WriteLine("🔴 Automatic mode disabled");
                }

                // Aktualizuj UI
                UpdateRecordingUI(recorder?.IsRecording ?? false);

                // Aktualizuj button text ak existuje
                if (sender is Button button)
                {
                    button.Content = isAutoDetectionEnabled ? "Disable Auto Mode" : "Enable Auto Mode";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling automatic mode: {ex.Message}", "Toggle Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ Error toggling automatic mode: {ex.Message}");
            }
        }

        #endregion

        #region Timer and Scanning Logic

        /// <summary>
        /// Timer pre pravidelné skenovanie zmien v oknách
        /// </summary>
        private void WindowScanTimer_Tick(object sender, EventArgs e)
        {
            if (!isAutoDetectionEnabled) return;

            try
            {
                // Skenuj nové okná len ak beží nahrávanie
                if (recorder?.IsRecording == true)
                {
                    ScanForNewWindows();

                    // Ak je aktívne nahrávanie UI elementov, aktualizuj ich
                    if (isRecordingUIElements)
                    {
                        RefreshUIElementsForActiveWindows();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Window scan error: {ex.Message}");
            }
        }

        /// <summary>
        /// Skenuje nové okná a automaticky ich pridá do sledovania
        /// </summary>
        private void ScanForNewWindows()
        {
            var newWindows = automaticUIManager.GetNewWindows();

            foreach (var windowInfo in newWindows)
            {
                // Rozhodnie či je okno relevantné
                if (ShouldAutoTrackWindow(windowInfo))
                {
                    AutomaticallyAddWindow(windowInfo);
                }
            }
        }

        /// <summary>
        /// Aktualizuje UI elementy pre všetky aktívne okná
        /// </summary>
        private void RefreshUIElementsForActiveWindows()
        {
            Task.Run(() => {
                try
                {
                    foreach (var kvp in activeWindows.ToList())
                    {
                        var windowHandle = kvp.Key;
                        var windowData = kvp.Value;

                        // Skontroluj či okno ešte existuje
                        if (!IsWindow(windowHandle))
                        {
                            activeWindows.Remove(windowHandle);
                            continue;
                        }

                        // Aktualizuj UI elementy
                        var currentElements = AdaptiveElementFinder.GetAllInteractiveElements(windowHandle);

                        // Porovnaj s predošlými elementmi
                        if (HasUIElementsChanged(windowData.UIElements, currentElements))
                        {
                            windowData.UIElements = currentElements;

                            Dispatcher.InvokeAsync(() => {
                                OnUIElementsChanged(windowHandle, currentElements);
                            });

                            System.Diagnostics.Debug.WriteLine($"🔄 UI elements updated for: {windowData.Title}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error refreshing UI elements: {ex.Message}");
                }
            });
        }

        #endregion

        #region Event Handlers pre automatickú detekciu

        /// <summary>
        /// Handler pre automaticky detekované UI zmeny
        /// </summary>
        private void OnAutomaticUIChangeDetected(object sender, UIChangeDetectedEventArgs e)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 UI changes detected in: {e.WindowState.Title}");

                    // Ak je to target window, aktualizuj element statistics
                    if (e.WindowHandle == targetWindowHandle)
                    {
                        var newElements = e.Changes.CurrentSnapshot?.Elements?.Select(el => new UIElementInfo
                        {
                            Name = el.Name,
                            AutomationId = el.AutomationId,
                            ControlType = el.ControlType,
                            ClassName = el.ClassName,
                            X = el.X,
                            Y = el.Y,
                            IsEnabled = el.IsEnabled,
                            IsVisible = el.IsVisible,
                            ElementText = el.Text
                        }).ToList() ?? new List<UIElementInfo>();

                        RefreshElementStatistics(newElements);
                    }

                    // Log významné zmeny
                    if (e.Changes.AddedElements.Count > 0)
                    {
                        LogToUI($"New UI elements detected: {e.Changes.AddedElements.Count} in {e.WindowState.Title}");
                    }

                    if (e.Changes.RemovedElements.Count > 0)
                    {
                        LogToUI($"UI elements removed: {e.Changes.RemovedElements.Count} from {e.WindowState.Title}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling automatic UI change: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre nové automaticky detekované okno
        /// </summary>
        private void OnAutomaticNewWindowDetected(object sender, NewWindowAppearedEventArgs e)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🪟 New window auto-detected: {e.WindowTitle} ({e.WindowType})");

                    // Ak je to relevantné okno a beží nahrávanie, možno prepni naň
                    if (recorder?.IsRecording == true && ShouldAutoSwitchToNewWindow(e))
                    {
                        var previousTarget = targetWindowHandle;
                        targetWindowHandle = e.WindowHandle;

                        LogToUI($"Auto-switched to: {e.WindowType} - {e.WindowTitle}");

                        // Pridaj switch command
                        if (recorder.IsRecording)
                        {
                            AddAutomaticWindowSwitchCommand(e, previousTarget);
                        }

                        UpdateUI();
                    }
                    else
                    {
                        LogToUI($"Detected: {e.WindowType} - {e.WindowTitle}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling automatic new window: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre zatvorenie automaticky sledovaného okna
        /// </summary>
        private void OnAutomaticWindowClosed(object sender, WindowClosedEventArgs e)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🗑️ Tracked window closed: {e.WindowTrackingInfo.Title}");

                    // Ak sa zatvoril target window, pokús sa nájsť náhradu
                    if (e.WindowHandle == targetWindowHandle)
                    {
                        LogToUI($"Target window closed: {e.WindowTrackingInfo.Title}");

                        // Hľadaj náhradné okno
                        var replacementWindow = FindReplacementWindow(e.WindowTrackingInfo.ProcessName);
                        if (replacementWindow != IntPtr.Zero)
                        {
                            targetWindowHandle = replacementWindow;
                            LogToUI($"Switched to replacement window: {GetWindowTitle(replacementWindow)}");
                            UpdateUI();
                        }
                        else
                        {
                            LogToUI("No replacement window found - please select new target");
                        }
                    }
                    else
                    {
                        LogToUI($"Window closed: {e.WindowTrackingInfo.Title}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling automatic window closed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre automaticky detekované okno z recordera
        /// </summary>
        private void OnWindowAutoDetected(object sender, WindowAutoDetectedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Window auto-detected: {e.Description}");

                    // Pridaj do aktívnych okien
                    if (!activeWindows.ContainsKey(e.WindowHandle))
                    {
                        var windowData = new WindowTrackingData
                        {
                            WindowHandle = e.WindowHandle,
                            Title = GetWindowTitle(e.WindowHandle),
                            ProcessName = GetProcessNameFromWindow(e.WindowHandle),
                            WindowType = DetermineWindowType(e.WindowHandle),
                            IsModal = IsModalWindow(e.WindowHandle),
                            DetectedAt = DateTime.Now
                        };

                        AutomaticallyAddWindow(windowData);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error handling window auto-detection: {ex.Message}");
                }
            });
        }

        #endregion

        #region Helper Methods - Opravené a zjednodušené

        /// <summary>
        /// Rozhodne či automaticky sledovať okno
        /// </summary>
        private bool ShouldAutoTrackWindow(WindowTrackingData windowInfo)
        {
            // Vždy sleduj dialógy a message boxy
            if (windowInfo.WindowType == WindowType.Dialog ||
                windowInfo.WindowType == WindowType.MessageBox)
                return true;

            // Sleduj okná z rovnakého procesu ako target
            if (!string.IsNullOrEmpty(GetProcessNameFromWindow(targetWindowHandle)))
            {
                return windowInfo.ProcessName.Equals(
                    GetProcessNameFromWindow(targetWindowHandle),
                    StringComparison.OrdinalIgnoreCase);
            }

            // Sleduj modálne okná
            if (windowInfo.IsModal)
                return true;

            return false;
        }

        /// <summary>
        /// Automaticky pridá okno do sledovania a aktualizuje UI elementy
        /// </summary>
        private void AutomaticallyAddWindow(WindowTrackingData windowInfo)
        {
            try
            {
                // Pridaj do sledovaných okien
                activeWindows[windowInfo.WindowHandle] = windowInfo;

                // Okamžite naskenuj UI elementy pre nové okno
                var uiElements = AdaptiveElementFinder.GetAllInteractiveElements(windowInfo.WindowHandle);
                windowInfo.UIElements = uiElements;

                // Aktualizuj zoznam dostupných okien v UI
                Dispatcher.InvokeAsync(() => {
                    RefreshWindowList();

                    // Ak je to významné okno (dialog/messagebox), prepni naň
                    if (windowInfo.WindowType != WindowType.MainWindow && recorder?.IsRecording == true)
                    {
                        SwitchToNewWindow(windowInfo);
                    }
                });

                System.Diagnostics.Debug.WriteLine($"✅ Auto-added window: {windowInfo.Title} ({windowInfo.WindowType})");
                System.Diagnostics.Debug.WriteLine($"   📋 Found {uiElements.Count} UI elements");

                // Log do UI
                LogToUI($"Auto-detected: {windowInfo.WindowType} - {windowInfo.Title}");
                LogToUI($"  UI Elements: {uiElements.Count} found");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error auto-adding window: {ex.Message}");
            }
        }

        /// <summary>
        /// Prepne na nové okno ako aktívny target
        /// </summary>
        private void SwitchToNewWindow(WindowTrackingData windowInfo)
        {
            try
            {
                var previousTarget = targetWindowHandle;
                targetWindowHandle = windowInfo.WindowHandle;

                // Aktualizuj UI
                Dispatcher.InvokeAsync(() => {
                    UpdateTargetWindowDisplay(windowInfo);
                    UpdateUI();
                });

                System.Diagnostics.Debug.WriteLine($"🔄 Auto-switched to: {windowInfo.Title}");
                LogToUI($"Switched to: {windowInfo.Title}");

                // Pridaj automatický command pre switch
                AddWindowSwitchCommand(windowInfo, previousTarget);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error switching window: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá command pre prepnutie okna
        /// </summary>
        private void AddWindowSwitchCommand(WindowTrackingData newWindow, IntPtr previousWindow)
        {
            if (recorder?.IsRecording != true) return;

            try
            {
                var switchCommand = new Command
                {
                    StepNumber = commands.Count + 1,
                    ElementName = $"AutoSwitch_To_{newWindow.WindowType}",
                    Type = CommandType.Wait,
                    Value = "500", // 500ms čakanie na načítanie okna
                    TargetWindow = newWindow.Title,
                    TargetProcess = newWindow.ProcessName,
                    ElementClass = "AutoWindowSwitch",
                    ElementControlType = newWindow.WindowType.ToString(),
                    Timestamp = DateTime.Now,
                    ElementX = -1,
                    ElementY = -1
                };

                Dispatcher.InvokeAsync(() => {
                    commands.Add(switchCommand);
                    hasUnsavedChanges = true;
                });

                System.Diagnostics.Debug.WriteLine($"➕ Added window switch command");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding switch command: {ex.Message}");
            }
        }

        /// <summary>
        /// Rozhodne či automaticky prepnúť na nové okno
        /// </summary>
        private bool ShouldAutoSwitchToNewWindow(NewWindowAppearedEventArgs e)
        {
            // Vždy prepni na dialógy a message boxy
            if (e.WindowType == WindowType.Dialog || e.WindowType == WindowType.MessageBox)
                return true;

            // Prepni ak je to z rovnakého procesu ako target
            var targetProcess = GetProcessNameFromWindow(targetWindowHandle);
            if (!string.IsNullOrEmpty(targetProcess) &&
                e.ProcessName.Equals(targetProcess, StringComparison.OrdinalIgnoreCase))
            {
                return e.WindowType != WindowType.MainWindow; // Neprepínaj medzi hlavnými oknami
            }

            return false;
        }

        /// <summary>
        /// Pridá automatický command pre prepnutie okna
        /// </summary>
        private void AddAutomaticWindowSwitchCommand(NewWindowAppearedEventArgs e, IntPtr previousWindow)
        {
            try
            {
                var switchCommand = new Command
                {
                    StepNumber = commands.Count + 1,
                    ElementName = $"AutoSwitch_To_{e.WindowType}",
                    Type = CommandType.Wait,
                    Value = "500", // 500ms čakanie
                    TargetWindow = e.WindowTitle,
                    TargetProcess = e.ProcessName,
                    ElementClass = "AutoWindowSwitch",
                    ElementControlType = e.WindowType.ToString(),
                    Timestamp = DateTime.Now,
                    ElementX = -1,
                    ElementY = -1
                };

                commands.Add(switchCommand);
                hasUnsavedChanges = true;

                System.Diagnostics.Debug.WriteLine($"➕ Added automatic window switch command");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding automatic window switch command: {ex.Message}");
            }
        }

        /// <summary>
        /// Kontroluje či sa UI elementy zmenili
        /// </summary>
        private bool HasUIElementsChanged(List<UIElementInfo> oldElements, List<UIElementInfo> newElements)
        {
            if (oldElements == null && newElements != null) return true;
            if (oldElements != null && newElements == null) return true;
            if (oldElements?.Count != newElements?.Count) return true;

            return false; // Zjednodušená kontrola
        }

        /// <summary>
        /// Handler pre zmenu UI elementov
        /// </summary>
        private void OnUIElementsChanged(IntPtr windowHandle, List<UIElementInfo> newElements)
        {
            try
            {
                if (activeWindows.ContainsKey(windowHandle))
                {
                    var windowData = activeWindows[windowHandle];
                    System.Diagnostics.Debug.WriteLine($"🔄 UI elements changed in: {windowData.Title}");
                    System.Diagnostics.Debug.WriteLine($"   New count: {newElements.Count}");

                    // Aktualizuj elementy ak je to target window
                    if (windowHandle == targetWindowHandle)
                    {
                        RefreshElementStatistics(newElements);
                    }

                    LogToUI($"UI updated: {windowData.Title} ({newElements.Count} elements)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling UI elements change: {ex.Message}");
            }
        }

        /// <summary>
        /// Získa všetky viditeľné okná
        /// </summary>
        private List<IntPtr> GetAllVisibleWindows()
        {
            var windows = new List<IntPtr>();

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
                    {
                        windows.Add(hWnd);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error getting visible windows: {ex.Message}");
            }

            return windows;
        }

        /// <summary>
        /// Analyzuje okno pre automatickú detekciu
        /// </summary>
        private WindowDetectionInfo AnalyzeWindow(IntPtr windowHandle)
        {
            try
            {
                var title = GetWindowTitle(windowHandle);
                var processName = GetProcessNameFromWindow(windowHandle);
                var className = GetWindowClassName(windowHandle);

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(processName))
                    return null;

                return new WindowDetectionInfo
                {
                    WindowHandle = windowHandle,
                    Title = title,
                    ProcessName = processName,
                    ClassName = className,
                    WindowType = DetermineWindowType(windowHandle, title, className),
                    IsModal = IsModalWindow(windowHandle),
                    DetectedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error analyzing window: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nájde náhradné okno
        /// </summary>
        private IntPtr FindReplacementWindow(string processName)
        {
            try
            {
                var trackedWindows = automaticUIManager?.GetTrackedWindows() ?? new List<WindowState>();

                // Hľadaj hlavné okno z rovnakého procesu
                var replacement = trackedWindows
                    .Where(w => w.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(w => w.LastActivated)
                    .FirstOrDefault();

                return replacement?.WindowHandle ?? IntPtr.Zero;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error finding replacement window: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Určí typ okna rozšírene
        /// </summary>
        private WindowType DetermineWindowType(IntPtr windowHandle, string title, string className)
        {
            try
            {
                // MessageBox detection
                if (className.Contains("MessageBox") || className == "#32770")
                {
                    if (title.Contains("Error") || title.Contains("Warning") ||
                        title.Contains("Information") || title.Contains("Confirm") ||
                        title.Contains("Alert"))
                        return WindowType.MessageBox;
                }

                // Dialog detection
                if (className.Contains("Dialog") || className == "#32770" || title.Contains("Dialog"))
                    return WindowType.Dialog;

                // Child window detection
                IntPtr parent = GetParent(windowHandle);
                if (parent != IntPtr.Zero)
                    return WindowType.ChildWindow;

                return WindowType.MainWindow;
            }
            catch
            {
                return WindowType.MainWindow;
            }
        }

        /// <summary>
        /// Získa class name okna
        /// </summary>
        private string GetWindowClassName(IntPtr windowHandle)
        {
            try
            {
                var sb = new System.Text.StringBuilder(256);
                GetClassName(windowHandle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Aktualizuje zobrazenie target okna
        /// </summary>
        private void UpdateTargetWindowDisplay(WindowTrackingData windowData)
        {
            try
            {
                // Aktualizuj UI prvky pre target window
                if (lblTargetWindow != null)
                {
                    lblTargetWindow.Content = $"Target: {windowData.Title}";
                }

                if (txtTargetProcess != null)
                {
                    txtTargetProcess.Text = windowData.ProcessName;
                }

                // Aktualizuj status
                UpdateStatusMessage($"Target: {windowData.WindowType} - {windowData.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating target display: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizuje status správu
        /// </summary>
        private void UpdateStatusMessage(string message)
        {
            try
            {
                if (txtStatus != null)
                {
                    txtStatus.Text = message;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating status: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá správu do logu
        /// </summary>
        private void LogToUI(string message)
        {
            try
            {
                if (txtLog != null)
                {
                    Dispatcher.InvokeAsync(() => {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                        txtLog.ScrollToEnd();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error logging to UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Obnoví zoznam okien v UI
        /// </summary>
        private void RefreshWindowList()
        {
            try
            {
                // Implementácia aktualizácie zoznamu okien
                System.Diagnostics.Debug.WriteLine($"🔄 Window list refreshed - {activeWindows.Count} active windows");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error refreshing window list: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizuje štatistiky elementov
        /// </summary>
        private void RefreshElementStatistics(List<UIElementInfo> elements)
        {
            try
            {
                Dispatcher.InvokeAsync(() => {
                    // Aktualizuj element stats list
                    elementStatsList.Clear();

                    foreach (var element in elements.Take(20)) // Zobraz prvých 20
                    {
                        elementStatsList.Add(new ElementUsageStats
                        {
                            ElementName = element.Name,
                            UsageCount = 0,
                            LastUsed = DateTime.Now,
                            Reliability = 1.0f
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error refreshing element stats: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper metóda pre získanie window info
        /// </summary>
        private string GetWindowInfo(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return "No window selected";

                var windowInfo = ExtractWindowInfo(windowHandle);
                return $"{windowInfo.ProcessName} - {windowInfo.WindowTitle}";
            }
            catch
            {
                return "Unknown window";
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
        /// Získa meno procesu z okna
        /// </summary>
        private string GetProcessNameFromWindow(IntPtr windowHandle)
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

        /// <summary>
        /// Kontroluje či je okno modálne
        /// </summary>
        private bool IsModalWindow(IntPtr windowHandle)
        {
            try
            {
                long exStyle = GetWindowLong(windowHandle, -20); // GWL_EXSTYLE
                return (exStyle & 0x00000001L) != 0; // WS_EX_DLGMODALFRAME
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Určí typ okna
        /// </summary>
        private WindowType DetermineWindowType(IntPtr windowHandle)
        {
            try
            {
                string className = GetWindowClassName(windowHandle);
                string title = GetWindowTitle(windowHandle);

                // MessageBox detection
                if (className.Contains("MessageBox") || className == "#32770")
                {
                    if (title.Contains("Error") || title.Contains("Warning") ||
                        title.Contains("Information") || title.Contains("Confirm"))
                        return WindowType.MessageBox;
                }

                // Dialog detection
                if (className.Contains("Dialog") || className == "#32770")
                    return WindowType.Dialog;

                // Child window detection
                IntPtr parent = GetParent(windowHandle);
                if (parent != IntPtr.Zero)
                    return WindowType.ChildWindow;

                return WindowType.MainWindow;
            }
            catch
            {
                return WindowType.MainWindow;
            }
        }

        #endregion

        #region Status and System Info Methods

        /// <summary>
        /// Zobrazí aktuálny stav automatického systému
        /// </summary>
        private void ShowAutomaticSystemStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var status = new System.Text.StringBuilder();
                status.AppendLine("=== AUTOMATIC SYSTEM STATUS ===");
                status.AppendLine($"Auto-Detection: {(isAutoDetectionEnabled ? "✅ Enabled" : "❌ Disabled")}");
                status.AppendLine($"UI Element Recording: {(isRecordingUIElements ? "✅ Active" : "❌ Inactive")}");
                status.AppendLine($"Window Monitoring: {(automaticUIManager?.IsMonitoringActive == true ? "✅ Active" : "❌ Inactive")}");
                status.AppendLine($"Command Recording: {(recorder?.IsRecording == true ? "✅ Active" : "❌ Inactive")}");
                status.AppendLine();

                // Tracked windows
                //var trackedWindows = automaticUIManager?.GetTrackedWindows() ?? new List<System.Windows.WindowState>();
                var trackedWindows = automaticUIManager?.GetTrackedWindows() ?? new List<WindowState>(); // - AppCommander.W7-11.WPF - WindowState je zjednodušená trieda pre sledovanie okien
                status.AppendLine($"Tracked Windows: {trackedWindows.Count}");
                foreach (var window in trackedWindows.Take(5))
                {
                    status.AppendLine($"  • {window.Title} ({window.Priority})");
                }
                if (trackedWindows.Count > 5)
                {
                    status.AppendLine($"  ... and {trackedWindows.Count - 5} more");
                }

                status.AppendLine();
                status.AppendLine($"Active Commands: {commands.Count}");
                status.AppendLine($"Element Statistics: {elementStatsList.Count}");

                // Target window info
                if (targetWindowHandle != IntPtr.Zero)
                {
                    status.AppendLine();
                    status.AppendLine("Target Window:");
                    status.AppendLine($"  Title: {GetWindowTitle(targetWindowHandle)}");
                    status.AppendLine($"  Process: {GetProcessNameFromWindow(targetWindowHandle)}");
                }

                MessageBox.Show(status.ToString(), "System Status",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting system status: {ex.Message}", "Status Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ Error getting system status: {ex.Message}");
            }
        }

        /// <summary>
        /// Resetuje automatický systém
        /// </summary>
        private void ResetAutomaticSystem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will reset the automatic detection system and clear all tracked windows.\n\n" +
                    "Are you sure you want to continue?",
                    "Reset Automatic System",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Zastaví všetky automatické služby
                    automaticUIManager?.StopMonitoring();
                    windowScanTimer.Stop();

                    // Vyčisti tracked windows
                    activeWindows.Clear();

                    // Resetni flags
                    isAutoDetectionEnabled = false;
                    isRecordingUIElements = false;

                    // Aktualizuj UI
                    UpdateRecordingUI(false);

                    LogToUI("🔄 Automatic system reset completed");
                    System.Diagnostics.Debug.WriteLine("🔄 Automatic system reset");

                    MessageBox.Show("Automatic system has been reset successfully.", "Reset Complete",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting automatic system: {ex.Message}", "Reset Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ Error resetting automatic system: {ex.Message}");
            }
        }

        #endregion

        #region Win32 API Imports

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region Existujúce metódy - Placeholder na integráciu

        // Tu budú existujúce metódy z pôvodného MainWindow.xaml.cs:
        // - SubscribeToEvents()
        // - OnCommandRecorded()
        // - OnRecordingStateChanged()
        // - UpdateUI()
        // - ExtractWindowInfo()
        // - atď.

        /// <summary>
        /// Placeholder pre existujúcu metódu
        /// </summary>
        private void SubscribeToEvents()
        {
            // Implementácia existujúcich eventov
        }

        /// <summary>
        /// Placeholder pre existujúcu metódu
        /// </summary>
        private void UpdateUI()
        {
            // Implementácia existujúcej UpdateUI metódy
        }

        /// <summary>
        /// Placeholder pre existujúcu metódu
        /// </summary>
        private WindowTrackingInfo ExtractWindowInfo(IntPtr windowHandle)
        {
            // Implementácia existujúcej metódy
            return new WindowTrackingInfo { Title = "Placeholder" };
        }

        #endregion
    }

    #region Supporting Classes - Zjednodušené

    /// <summary>
    /// Dáta o sledovanom okne
    /// </summary>
    public class WindowTrackingData
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public WindowType WindowType { get; set; }
        public bool IsModal { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<UIElementInfo> UIElements { get; set; } = new List<UIElementInfo>();
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Informácie o detekovanom okne
    /// </summary>
    public class WindowDetectionInfo
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public WindowType WindowType { get; set; }
        public bool IsModal { get; set; }
        public DateTime DetectedAt { get; set; }
        public string ClassName { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
    }

    #endregion
}
