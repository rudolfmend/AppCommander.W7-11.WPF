using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF
{
    public partial class SequenceEditorWindow : Window
    {
        // Verejné vlastnosti pre komunikáciu s MainWindow
        public bool WasSaved { get; private set; }
        public List<UnifiedItem> EditedItems { get; private set; }

        // Privátne polia
        private ObservableCollection<UnifiedItem> _items;
        private string _sequenceName;
        private string _originalFilePath; // Cesta k súboru sekvencie
        private bool _isEditingSequenceFile; // Flag či editujeme súbor alebo len items

        // Prázdny konštruktor (pre XAML designer)
        public SequenceEditorWindow()
        {
            InitializeComponent();
            WasSaved = false;
            EditedItems = new List<UnifiedItem>();
            _isEditingSequenceFile = false;
        }

        // Konštruktor s parametrami (používa MainWindow)
        public SequenceEditorWindow(IEnumerable<UnifiedItem> items, string sequenceName) : this()
        {
            _sequenceName = sequenceName ?? "Untitled Sequence";

            // NOVÁ LOGIKA: Skontroluj či items obsahujú SequenceReference
            var itemsList = items.ToList();
            _items = new ObservableCollection<UnifiedItem>();

            if (itemsList.Count == 1 && itemsList[0].Type == UnifiedItem.ItemType.SequenceReference)
            {
                // Načítaj príkazy zo súboru sekvencie
                LoadSequenceCommands(itemsList[0]);
            }
            else
            {
                // Pôvodná logika - skopíruj items
                foreach (var item in itemsList)
                {
                    _items.Add(CloneUnifiedItem(item));
                }
            }

            // Nastav UI
            InitializeUI();
        }

        // Alternatívny konštruktor (ak sa používa List)
        public SequenceEditorWindow(List<UnifiedItem> items, string sequenceName)
            : this((IEnumerable<UnifiedItem>)items, sequenceName)
        {
        }

        /// <summary>
        /// Načíta príkazy zo sekvencie súboru
        /// </summary>
        private void LoadSequenceCommands(UnifiedItem sequenceItem)
        {
            try
            {
                if (string.IsNullOrEmpty(sequenceItem.FilePath) || !System.IO.File.Exists(sequenceItem.FilePath))
                {
                    MessageBox.Show(
                        $"Sequence file not found: {sequenceItem.FilePath}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Ulož cestu k súboru pre neskoršie uloženie
                _originalFilePath = sequenceItem.FilePath;
                _isEditingSequenceFile = true;

                // Načítaj CommandSequence zo súboru
                var commandSequence = CommandSequence.LoadFromFile(sequenceItem.FilePath);

                if (commandSequence == null || commandSequence.Commands == null)
                {
                    MessageBox.Show(
                        "Failed to load sequence commands.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Aktualizuj názov sekvencie
                _sequenceName = commandSequence.Name ?? _sequenceName;

                // Konvertuj Command-y na UnifiedItem-y
                int stepNumber = 1;
                foreach (var command in commandSequence.Commands.OrderBy(c => c.StepNumber))
                {
                    var unifiedItem = UnifiedItem.FromCommand(command, stepNumber);
                    _items.Add(unifiedItem);
                    stepNumber++;
                }

                System.Diagnostics.Debug.WriteLine($"✅ Loaded {_items.Count} commands from sequence: {_sequenceName}");
                UpdateStatus($"Loaded {_items.Count} commands from: {System.IO.Path.GetFileName(_originalFilePath)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading sequence: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ Error loading sequence commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Uloží editované príkazy späť do súboru sekvencie
        /// </summary>
        private void SaveSequenceToFile(string filePath)
        {
            try
            {
                // Vytvor CommandSequence
                var commandSequence = new CommandSequence(_sequenceName);

                // Konvertuj UnifiedItem-y späť na Command-y
                foreach (var item in _items.OrderBy(i => i.StepNumber))
                {
                    var command = item.ToCommand();
                    if (command != null)
                    {
                        commandSequence.AddCommand(command);
                    }
                }

                // Ulož do súboru
                commandSequence.SaveToFile(filePath);

                System.Diagnostics.Debug.WriteLine($"✅ Saved {commandSequence.Commands.Count} commands to: {filePath}");
                UpdateStatus($"Saved {commandSequence.Commands.Count} commands successfully");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save sequence to file: {ex.Message}", ex);
            }
        }

        private void InitializeUI()
        {
            // Nastav názov okna a popisky
            AppCommander_TxtTitle.Text = "📋 Sequence Editor";
            AppCommander_TxtSequenceName.Text = _sequenceName;
            AppCommander_TxtCommandCount.Text = $"{_items.Count} commands";

            // Napoj DataGrid na kolekciu
            CommandTable.ItemsSource = _items;

            // Nastav status
            if (_isEditingSequenceFile)
            {
                AppCommander_TxtStatusInfo.Text = $"Editing: {System.IO.Path.GetFileName(_originalFilePath)}";
            }
            else
            {
                AppCommander_TxtStatusInfo.Text = "Ready to edit";
            }
        }

        private UnifiedItem CloneUnifiedItem(UnifiedItem original)
        {
            return new UnifiedItem
            {
                StepNumber = original.StepNumber,
                Type = original.Type,
                Name = original.Name,
                Action = original.Action,
                Value = original.Value,
                RepeatCount = original.RepeatCount,
                Status = original.Status,
                Timestamp = original.Timestamp,
                FilePath = original.FilePath,
                ElementX = original.ElementX,
                ElementY = original.ElementY,
                ElementId = original.ElementId,
                ClassName = original.ClassName,
                IsLiveRecording = original.IsLiveRecording,
                LiveSequenceReference = original.LiveSequenceReference
            };
        }

        // Event handler pre tlačidlo Save
        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Skontroluj validitu dát
                if (_items == null || _items.Count == 0)
                {
                    MessageBox.Show(
                        "No commands to save.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Prepočítaj čísla krokov
                RecalculateStepNumbers();

                // AK EDITUJEME SÚBOR SEKVENCIE, ulož priamo do súboru
                if (_isEditingSequenceFile && !string.IsNullOrEmpty(_originalFilePath))
                {
                    SaveSequenceToFile(_originalFilePath);

                    MessageBox.Show(
                        $"Sequence saved successfully!\n\nFile: {System.IO.Path.GetFileName(_originalFilePath)}\nCommands: {_items.Count}",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                // Ulož editované items (pre kompatibilitu s MainWindow)
                EditedItems = _items.ToList();
                WasSaved = true;

                // Nastav DialogResult a zavri okno
                this.DialogResult = true;
                AppCommander_TxtStatusInfo.Text = "Changes saved successfully!";
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving changes: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"❌ Error saving: {ex.Message}");
            }
        }

        // Event handler pre tlačidlo Cancel
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Opýtaj sa na potvrdenie ak boli vykonané zmeny
                var result = MessageBox.Show(
                    "Are you sure you want to discard all changes?",
                    "Confirm Cancel",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    WasSaved = false;
                    this.DialogResult = false;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error canceling: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Event handler pre double-click na riadok v DataGrid
        private void CommandTable_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (CommandTable.SelectedItem is UnifiedItem selectedItem)
                {
                    EditSelectedCommand(selectedItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error editing command: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void EditSelectedCommand(UnifiedItem item)
        {
            // TODO: Implementovať advanced edit dialog
            // Zatiaľ len zobraz info
            var message = $"Edit command: {item.Name}\n\n" +
                         $"Type: {item.TypeDisplay}\n" +
                         $"Action: {item.Action}\n" +
                         $"Value: {item.Value}\n" +
                         $"Repeat: {item.RepeatCount}x";

            MessageBox.Show(
                message,
                "Command Details",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #region Toolbar Commands

        /// <summary>
        /// Presúva položku vyššie v tabuľke
        /// </summary>
        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = CommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null || selectedItem.StepNumber <= 1)
                {
                    UpdateStatus("Cannot move item up - select an item that is not first");
                    return;
                }

                var currentIndex = _items.IndexOf(selectedItem);
                if (currentIndex > 0)
                {
                    _items.Move(currentIndex, currentIndex - 1);
                    RecalculateStepNumbers();

                    // Keep selection on moved item
                    CommandTable.SelectedItem = selectedItem;
                    UpdateStatus($"Moved '{selectedItem.Name}' up");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error moving item up: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Presúva položku nižšie v tabuľke
        /// </summary>
        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = CommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null || selectedItem.StepNumber >= _items.Count)
                {
                    UpdateStatus("Cannot move item down - select an item that is not last");
                    return;
                }

                var currentIndex = _items.IndexOf(selectedItem);
                if (currentIndex < _items.Count - 1)
                {
                    _items.Move(currentIndex, currentIndex + 1);
                    RecalculateStepNumbers();

                    CommandTable.SelectedItem = selectedItem;
                    UpdateStatus($"Moved '{selectedItem.Name}' down");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error moving item down: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Edituje vybranú položku
        /// </summary>
        private void EditCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = CommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    UpdateStatus("Please select an item to edit");
                    return;
                }

                EditSelectedCommand(selectedItem);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error editing command: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Duplikuje vybranú položku
        /// </summary>
        private void DuplicateCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = CommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    UpdateStatus("Please select an item to duplicate");
                    return;
                }

                // Vytvoriť kópiu
                var duplicate = CloneUnifiedItem(selectedItem);
                duplicate.StepNumber = _items.Count + 1;
                duplicate.Name = selectedItem.Name + " (copy)";
                duplicate.Status = "Ready";
                duplicate.Timestamp = DateTime.Now;

                _items.Add(duplicate);
                RecalculateStepNumbers();
                UpdateStatus($"Command duplicated: {duplicate.Name}");

                // Aktualizuj počet príkazov
                AppCommander_TxtCommandCount.Text = $"{_items.Count} commands";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error duplicating command: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Zmaže vybranú položku
        /// </summary>
        private void DeleteCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = CommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    UpdateStatus("Please select an item to delete");
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{selectedItem.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _items.Remove(selectedItem);
                    RecalculateStepNumbers();
                    UpdateStatus($"Deleted: {selectedItem.Name}");

                    // Aktualizuj počet príkazov
                    AppCommander_TxtCommandCount.Text = $"{_items.Count} commands";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error deleting command: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Pridá Wait príkaz
        /// </summary>
        private void AddWait_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Vytvor nový Wait príkaz
                var waitItem = new UnifiedItem(UnifiedItem.ItemType.Command)
                {
                    StepNumber = _items.Count + 1,
                    Name = "Wait",
                    Action = "Wait",
                    Value = "1000", // 1 sekunda default
                    RepeatCount = 1,
                    Status = "Ready",
                    Timestamp = DateTime.Now
                };

                _items.Add(waitItem);
                RecalculateStepNumbers();
                UpdateStatus("Added Wait command (1 second)");

                AppCommander_TxtCommandCount.Text = $"{_items.Count} commands";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error adding wait command: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Pridá Loop Start marker
        /// </summary>
        private void AddLoopStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var loopStart = UnifiedItem.CreateLoopStart(_items.Count + 1, 2); // 2x default
                _items.Add(loopStart);
                RecalculateStepNumbers();
                UpdateStatus("Added Loop Start (2 iterations)");

                AppCommander_TxtCommandCount.Text = $"{_items.Count} commands";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error adding loop start: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Pridá Loop End marker
        /// </summary>
        private void AddLoopEnd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var loopEnd = UnifiedItem.CreateLoopEnd(_items.Count + 1);
                _items.Add(loopEnd);
                RecalculateStepNumbers();
                UpdateStatus("Added Loop End");

                AppCommander_TxtCommandCount.Text = $"{_items.Count} commands";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error adding loop end: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Prepočíta StepNumber pre všetky položky v poradí
        /// </summary>
        private void RecalculateStepNumbers()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].StepNumber = i + 1;
            }
        }

        /// <summary>
        /// Aktualizuje status text
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (AppCommander_TxtStatusInfo != null)
            {
                AppCommander_TxtStatusInfo.Text = message;
            }
            System.Diagnostics.Debug.WriteLine($"Status: {message}");
        }

        #endregion
    }
}
