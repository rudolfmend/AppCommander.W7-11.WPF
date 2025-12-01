using System;
using System.Diagnostics;
using System.Windows;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF.Core.Managers
{
    /// <summary>
    /// Manažér pre recording funkcionalitu - wrapper pre CommandRecorder, WindowTracker a AutomaticUIManager
    /// </summary>
    public class RecordingManager
    {
        #region Fields

        private readonly CommandRecorder _recorder;
        private readonly WindowTracker _windowTracker;
        private readonly AutomaticUIManager _automaticUIManager;
        private IntPtr _targetWindowHandle;
        private string _currentSequenceName;

        #endregion

        #region Properties

        public bool IsRecording => _recorder?.IsRecording ?? false;
        public bool IsPaused => _recorder?.IsPaused ?? false;
        public IntPtr TargetWindowHandle => _targetWindowHandle;
        public string CurrentSequenceName => _currentSequenceName;
        public CommandSequence CurrentSequence => _recorder?.CurrentSequence;

        // Forward recorder properties
        public bool EnableRealTimeElementScanning
        {
            get => _recorder.EnableRealTimeElementScanning;
            set => _recorder.EnableRealTimeElementScanning = value;
        }

        public bool AutoUpdateExistingCommands
        {
            get => _recorder.AutoUpdateExistingCommands;
            set => _recorder.AutoUpdateExistingCommands = value;
        }

        public bool EnablePredictiveDetection
        {
            get => _recorder.EnablePredictiveDetection;
            set => _recorder.EnablePredictiveDetection = value;
        }

        #endregion

        #region Events

        public event EventHandler<CommandRecordedEventArgs> CommandRecorded
        {
            add => _recorder.CommandRecorded += value;
            remove => _recorder.CommandRecorded -= value;
        }

        public event EventHandler<RecordingStateChangedEventArgs> RecordingStateChanged
        {
            add => _recorder.RecordingStateChanged += value;
            remove => _recorder.RecordingStateChanged -= value;
        }

        #endregion

        #region Constructor

        public RecordingManager(CommandRecorder recorder, WindowTracker windowTracker, AutomaticUIManager automaticUIManager)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _automaticUIManager = automaticUIManager ?? throw new ArgumentNullException(nameof(automaticUIManager));

            Debug.WriteLine("✅ RecordingManager initialized");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Nastaví target window pre nahrávanie
        /// </summary>
        public void SetTargetWindow(IntPtr windowHandle, string processName)
        {
            _targetWindowHandle = windowHandle;
            _recorder.SetTargetWindow(windowHandle);
            Debug.WriteLine($"Target window set: {processName} (Handle: 0x{windowHandle:X})");
        }

        /// <summary>
        /// Spustí nahrávanie s validáciou
        /// </summary>
        public bool StartRecording(IntPtr targetWindowHandle, string targetProcessName)
        {
            try
            {
                Debug.WriteLine("════════════════════════════════════════");
                Debug.WriteLine("📍 RecordingManager.StartRecording() CALLED");
                Debug.WriteLine("════════════════════════════════════════");

                // Validácia - target window musí byť nastavený
                if (targetWindowHandle == IntPtr.Zero)
                {
                    Debug.WriteLine("❌ Cannot start - no target window");
                    MessageBox.Show(
                        "Please select a target window first.",
                        "No Target Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                // Validácia - target nesmie byť AppCommander/Senaro
                if (IsAppCommanderProcess(targetProcessName))
                {
                    Debug.WriteLine("❌ Cannot start - target is AppCommander/Senaro itself");
                    MessageBox.Show(
                        "You cannot record actions on AppCommander itself.\n" +
                        "Please select a different target application.",
                        "Invalid Target",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                Debug.WriteLine("✅ All checks passed, configuring recorder...");

                // Konfigurácia recordera
                _targetWindowHandle = targetWindowHandle;
                _recorder.EnableRealTimeElementScanning = true;
                _recorder.AutoUpdateExistingCommands = true;
                _recorder.EnablePredictiveDetection = true;

                // Spusti WindowTracker
                Debug.WriteLine($"📍 Starting WindowTracker for: {targetProcessName}");
                _windowTracker.StartTracking(targetProcessName);

                // Spusti AutomaticUIManager
                Debug.WriteLine("📍 Starting AutomaticUIManager");
                _automaticUIManager.StartMonitoring(targetWindowHandle, targetProcessName);

                // Spusti nahrávanie
                _currentSequenceName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}";
                Debug.WriteLine($"📍 Calling _recorder.StartRecording({_currentSequenceName})...");

                _recorder.StartRecording(_currentSequenceName, targetWindowHandle);

                Debug.WriteLine($"📍 After StartRecording: IsRecording = {IsRecording}");
                Debug.WriteLine("✅ RecordingManager.StartRecording() COMPLETED");
                Debug.WriteLine("════════════════════════════════════════");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ EXCEPTION in RecordingManager.StartRecording: {ex.Message}");
                Debug.WriteLine($"   Stack trace: {ex.StackTrace}");
                Debug.WriteLine("════════════════════════════════════════");

                MessageBox.Show(
                    $"Error starting recording:\n{ex.Message}",
                    "Recording Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        /// <summary>
        /// Zastaví nahrávanie
        /// </summary>
        public void StopRecording()
        {
            try
            {
                Debug.WriteLine("📍 RecordingManager.StopRecording() CALLED");

                _recorder.StopRecording();
                _windowTracker.StopTracking();
                _automaticUIManager.StopMonitoring();

                Debug.WriteLine("✅ Recording stopped successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in RecordingManager.StopRecording: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Pridá Wait command do nahrávky
        /// </summary>
        public void AddWaitCommand(int waitTimeMs)
        {
            if (!IsRecording)
            {
                Debug.WriteLine("⚠️ Cannot add Wait command - recording not active");
                return;
            }

            _recorder.AddWaitCommand(waitTimeMs);
            Debug.WriteLine($"✅ Wait command added: {waitTimeMs}ms");
        }

        /// <summary>
        /// Dispose metóda pre cleanup
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (IsRecording)
                {
                    StopRecording();
                }

                _recorder?.Dispose();
                _windowTracker?.Dispose();

                Debug.WriteLine("🧹 RecordingManager disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error disposing RecordingManager: {ex.Message}");
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Kontrola, či process patrí AppCommander/Senaro
        /// </summary>
        private bool IsAppCommanderProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            return processName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("Senaro", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
