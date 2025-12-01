using AppCommander.W7_11.WPF.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace AppCommander.W7_11.WPF.Core.Managers
{
    /// <summary>
    /// Spravuje načítanie, uloženie a validáciu sekvencií
    /// </summary>
    public class SequenceManager
    {
        #region Private Fields

        private readonly ObservableCollection<Command> _commands;
        private readonly ObservableCollection<UnifiedItem> _unifiedItems;
        private readonly Func<IntPtr> _getTargetWindowHandle;
        private readonly Func<string> _getCurrentSequenceName;
        private readonly Action<string> _updateStatus;
        private readonly Action _updateUI;
        private readonly Func<IntPtr, string> _getProcessNameFromWindow; 
        private readonly Func<IntPtr, string> _getWindowTitle;

        private UnifiedSequence _currentUnifiedSequence;
        private string _currentUnifiedSequenceFilePath;
        private string _currentFilePath;

        #endregion

        #region Properties

        public bool HasUnsavedChanges { get; set; }
        public bool HasUnsavedUnifiedChanges { get; set; }
        public string CurrentFilePath => _currentFilePath;
        public string CurrentUnifiedSequenceFilePath => _currentUnifiedSequenceFilePath;

        public UnifiedSequence CurrentUnifiedSequence
        {
            get => _currentUnifiedSequence;
            set => _currentUnifiedSequence = value;  // ← PRIDANÉ
        }

        #endregion

        #region Constructor

        public SequenceManager(
            ObservableCollection<Command> commands,
            ObservableCollection<UnifiedItem> unifiedItems,
            Func<IntPtr> getTargetWindowHandle,
            Func<string> getCurrentSequenceName,
            Action<string> updateStatus,
            Action updateUI,
            Func<IntPtr, string> getProcessNameFromWindow,
            Func<IntPtr, string> getWindowTitle)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _unifiedItems = unifiedItems ?? throw new ArgumentNullException(nameof(unifiedItems));
            _getTargetWindowHandle = getTargetWindowHandle ?? throw new ArgumentNullException(nameof(getTargetWindowHandle));
            _getCurrentSequenceName = getCurrentSequenceName ?? throw new ArgumentNullException(nameof(getCurrentSequenceName));
            _updateStatus = updateStatus ?? throw new ArgumentNullException(nameof(updateStatus));
            _updateUI = updateUI ?? throw new ArgumentNullException(nameof(updateUI));
            _getProcessNameFromWindow = getProcessNameFromWindow ?? throw new ArgumentNullException(nameof(getProcessNameFromWindow));
            _getWindowTitle = getWindowTitle ?? throw new ArgumentNullException(nameof(getWindowTitle));

            _currentUnifiedSequence = new UnifiedSequence();
            _currentUnifiedSequenceFilePath = string.Empty;
            _currentFilePath = string.Empty;
        }

        #endregion

        #region Public Methods - New Sequence

        /// <summary>
        /// Vytvorí novú unified sekvenciu
        /// </summary>
        public bool NewUnifiedSequence()
        {
            try
            {
                if (HasUnsavedUnifiedChanges)
                {
                    var result = MessageBox.Show(
                        "You have unsaved changes in unified sequence. Do you want to save before creating new?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Caller musí zavolať Save metódu
                        return false;
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        return false;
                    }
                }

                _unifiedItems.Clear();
                _currentUnifiedSequence = new UnifiedSequence();
                _currentUnifiedSequenceFilePath = string.Empty;
                HasUnsavedUnifiedChanges = false;

                _updateUI();
                _updateStatus("New unified sequence created");

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error creating new unified sequence", ex);
                return false;
            }
        }

        #endregion

        #region Public Methods - Load Operations

        /// <summary>
        /// Načíta súbor sekvencie podľa prípony
        /// </summary>
        public void LoadSequenceFile(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".uniseq")
                {
                    LoadUnifiedSequenceFromFile(filePath);
                }
                else if (extension == ".acset")
                {
                    // SequenceSet loading by malo byť v samostatnom manageri
                    Debug.WriteLine("SequenceSet loading not implemented in SequenceManager");
                    _updateStatus("SequenceSet loading requires additional implementation");
                }
                else
                {
                    LoadSequenceFromFile(filePath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error loading sequence file", ex);
            }
        }

        /// <summary>
        /// Načíta unified sekvenciu zo súboru
        /// </summary>
        public void LoadUnifiedSequenceFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show(
                        $"File '{filePath}' does not exist.",
                        "File Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                if (HasUnsavedUnifiedChanges)
                {
                    var result = MessageBox.Show(
                        "You have unsaved changes. Do you want to save before loading a new sequence?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Caller musí zavolať Save
                        return;
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }

                var json = File.ReadAllText(filePath);
                var unifiedSequence = JsonConvert.DeserializeObject<UnifiedSequence>(json);

                if (unifiedSequence == null)
                {
                    MessageBox.Show(
                        "Invalid unified sequence file format.",
                        "Invalid File",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                if (unifiedSequence.Items == null)
                {
                    unifiedSequence.Items = new List<UnifiedItem>();
                }

                _unifiedItems.Clear();

                foreach (var item in unifiedSequence.Items)
                {
                    if (item.Type == UnifiedItem.ItemType.SequenceReference)
                    {
                        if (!string.IsNullOrEmpty(item.FilePath) && !File.Exists(item.FilePath))
                        {
                            item.Status = "File Missing";
                            Debug.WriteLine($"Warning: Referenced sequence file not found: {item.FilePath}");
                        }
                    }

                    _unifiedItems.Add(item);
                }

                // Prepočítanie step numbers
                for (int i = 0; i < _unifiedItems.Count; i++)
                {
                    _unifiedItems[i].StepNumber = i + 1;
                }

                _currentUnifiedSequence = unifiedSequence;
                _currentUnifiedSequenceFilePath = filePath;
                HasUnsavedUnifiedChanges = false;

                _updateUI();
                _updateStatus($"Unified sequence loaded: {Path.GetFileName(filePath)} ({_unifiedItems.Count} items)");

                Debug.WriteLine($"Unified sequence loaded from: {filePath}");
                Debug.WriteLine($"  Name: {unifiedSequence.Name}");
                Debug.WriteLine($"  Items: {_unifiedItems.Count}");
                Debug.WriteLine($"  Created: {unifiedSequence.Created}");
                Debug.WriteLine($"  Last Modified: {unifiedSequence.LastModified}");
            }
            catch (JsonException ex)
            {
                MessageBox.Show(
                    $"Error parsing unified sequence file:\n\n{ex.Message}",
                    "Invalid JSON",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Debug.WriteLine($"JSON parse error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error loading unified sequence", ex);
            }
        }

        /// <summary>
        /// Načíta základnú sekvenciu (.acc/.json)
        /// </summary>
        public void LoadSequenceFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var json = File.ReadAllText(filePath);
                var sequence = JsonConvert.DeserializeObject<CommandSequence>(json);

                if (sequence != null && sequence.Commands != null)
                {
                    _commands.Clear();
                    foreach (var command in sequence.Commands)
                    {
                        _commands.Add(command);
                    }

                    _currentFilePath = filePath;
                    HasUnsavedChanges = false;
                    _updateUI();

                    var loopInfo = GetSequenceLoopInfo(sequence);
                    var statusMsg = $"Sequence loaded: {Path.GetFileName(filePath)} ({_commands.Count} commands{loopInfo})";
                    _updateStatus(statusMsg);
                }
                else
                {
                    MessageBox.Show("Invalid file format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error loading sequence", ex);
            }
        }

        #endregion

        #region Public Methods - Save Operations

        /// <summary>
        /// Uloží unified sekvenciu do súboru
        /// </summary>
        public void SaveUnifiedSequenceToFile(string filePath)
        {
            try
            {
                if (_unifiedItems == null || _unifiedItems.Count == 0)
                {
                    MessageBox.Show(
                        "Cannot save empty sequence.",
                        "Empty Sequence",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _currentUnifiedSequence.Name = string.IsNullOrEmpty(_currentUnifiedSequence.Name) ?
                                Path.GetFileNameWithoutExtension(filePath) :
                                _currentUnifiedSequence.Name;

                _currentUnifiedSequence.Description = $"Unified sequence created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                _currentUnifiedSequence.Items = _unifiedItems.ToList();
                _currentUnifiedSequence.LastModified = DateTime.Now;
                _currentUnifiedSequence.FilePath = filePath;

                if (_currentUnifiedSequence.Created == default(DateTime))
                {
                    _currentUnifiedSequence.Created = DateTime.Now;
                }

                var json = JsonConvert.SerializeObject(_currentUnifiedSequence, Formatting.Indented);
                File.WriteAllText(filePath, json);

                _currentUnifiedSequenceFilePath = filePath;
                HasUnsavedUnifiedChanges = false;

                _updateUI();
                _updateStatus($"Unified sequence saved: {Path.GetFileName(filePath)} ({_unifiedItems.Count} items)");

                Debug.WriteLine($"Unified sequence saved to: {filePath}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving unified sequence to file", ex);
            }
        }

        /// <summary>
        /// Uloží základnú sekvenciu do súboru
        /// </summary>
        public void SaveSequenceToFile(string filePath)
        {
            try
            {
                var sequence = new CommandSequence
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Commands = _commands.ToList(),
                    TargetApplication = _getProcessNameFromWindow(_getTargetWindowHandle()),
                    TargetProcessName = _getProcessNameFromWindow(_getTargetWindowHandle()),
                    TargetWindowTitle = _getWindowTitle(_getTargetWindowHandle()),
                    Created = DateTime.Now,
                    LastModified = DateTime.Now
                };

                var json = JsonConvert.SerializeObject(sequence, Formatting.Indented);
                File.WriteAllText(filePath, json);

                _currentFilePath = filePath;
                HasUnsavedChanges = false;
                _updateUI();
                _updateStatus($"Sequence saved: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence", ex);
            }
        }

        #endregion

        #region Public Methods - Validation

        /// <summary>
        /// Validuje či je súbor validná sekvencia
        /// </summary>
        public bool ValidateSequenceFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var sequence = JsonConvert.DeserializeObject<CommandSequence>(content);
                return sequence != null && sequence.Commands != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Public Methods - Callbacks

        /// <summary>
        /// Callback po úspešnom uložení sekvencie - aktualizuje UI
        /// </summary>
        public void OnSequenceSavedSuccessfully(string filePath)
        {
            try
            {
                // Odstráň warning položku
                var warningItem = _unifiedItems?.FirstOrDefault(
                    item => item.Type == UnifiedItem.ItemType.LiveRecording &&
                            item.Name == "⚠️ Unsaved Command Set");

                if (warningItem != null && _unifiedItems != null)
                {
                    _unifiedItems.Remove(warningItem);
                    Debug.WriteLine("Warning item removed after saving sequence");
                }

                // Pridaj novú položku SequenceReference
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var commandCount = _commands?.Count ?? 0;

                var sequenceItem = new UnifiedItem(UnifiedItem.ItemType.SequenceReference)
                {
                    StepNumber = _unifiedItems?.Count + 1 ?? 1,
                    Name = fileName,
                    Action = "Sequence File",
                    Value = $"{commandCount} command(s)",
                    RepeatCount = 1,
                    Status = "Ready",
                    Timestamp = DateTime.Now,
                    FilePath = filePath
                };

                _unifiedItems?.Add(sequenceItem);

                // Prepočítaj step numbers
                RecalculateStepNumbers();

                // Vyčisti _commands
                _commands?.Clear();

                // Aktualizuj UI
                _updateUI();
                UpdateUnsavedCommandsWarning();

                _updateStatus($"✅ Sequence '{fileName}' saved and added to sequence list");
                Debug.WriteLine($"Sequence saved and UI updated: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating UI after save: {ex.Message}");
                ShowErrorMessage("Error updating sequence list", ex);
            }
        }

        /// <summary>
        /// Aktualizuje warning položku v unified items
        /// </summary>
        public void UpdateUnsavedCommandsWarning()
        {
            try
            {
                bool hasUnsavedCommands = _commands != null && _commands.Count > 0;

                var existingWarning = _unifiedItems?.FirstOrDefault(
                    item => item.Type == UnifiedItem.ItemType.LiveRecording &&
                            item.Name == "⚠️ Unsaved Command Set");

                if (hasUnsavedCommands)
                {
                    if (existingWarning == null && _unifiedItems != null)
                    {
                        var warningItem = new UnifiedItem(UnifiedItem.ItemType.LiveRecording)
                        {
                            StepNumber = 1,
                            Name = "⚠️ Unsaved Command Set",
                            Action = "Click to edit or add to sequence",
                            Value = $"{_commands.Count} command(s) recorded",
                            RepeatCount = 1,
                            Status = "Unsaved",
                            Timestamp = DateTime.Now,
                            IsLiveRecording = true,
                            LiveSequenceReference = new CommandSequence
                            {
                                Name = "Unsaved Commands",
                                Commands = new List<Command>(_commands),
                                TargetProcessName = _getProcessNameFromWindow(_getTargetWindowHandle()),
                                TargetWindowTitle = _getWindowTitle(_getTargetWindowHandle())
                            }
                        };

                        _unifiedItems.Insert(0, warningItem);
                        RecalculateStepNumbers();
                        Debug.WriteLine($"Warning item added: {_commands.Count} unsaved commands");
                    }
                    else if (existingWarning != null)
                    {
                        existingWarning.Value = $"{_commands.Count} command(s) recorded";
                        existingWarning.Timestamp = DateTime.Now;
                        existingWarning.LiveSequenceReference = new CommandSequence
                        {
                            Name = "Unsaved Commands",
                            Commands = new List<Command>(_commands),
                            TargetProcessName = _getProcessNameFromWindow(_getTargetWindowHandle()),
                            TargetWindowTitle = _getWindowTitle(_getTargetWindowHandle())
                        };
                    }
                }
                else
                {
                    if (existingWarning != null && _unifiedItems != null)
                    {
                        _unifiedItems.Remove(existingWarning);
                        RecalculateStepNumbers();
                        Debug.WriteLine("Warning item removed - no unsaved commands");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating unsaved commands warning: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods - Helpers

        private void RecalculateStepNumbers()
        {
            for (int i = 0; i < _unifiedItems.Count; i++)
            {
                _unifiedItems[i].StepNumber = i + 1;
            }
        }

        private string GetSequenceLoopInfo(CommandSequence sequence)
        {
            var loopStarts = sequence.Commands.Count(c => c.Type == CommandType.LoopStart);
            if (loopStarts > 0)
            {
                return $", {loopStarts} loops";
            }
            return "";
        }

        private void ShowErrorMessage(string title, Exception ex)
        {
            var message = $"{title}\n\nError: {ex.Message}";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Debug.WriteLine($"{title}: {ex.Message}");
        }

        #endregion
    }
}
