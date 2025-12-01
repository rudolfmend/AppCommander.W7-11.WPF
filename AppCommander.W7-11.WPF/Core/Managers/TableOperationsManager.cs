using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AppCommander.W7_11.WPF.Core.Managers
{
    /// <summary>
    /// Manažér všetkých operácií s tabuľkou položiek (UnifiedItems)
    /// - Move Up/Down
    /// - Add/Edit/Delete
    /// - Duplicate
    /// - Batch operations
    /// - Step number recalculation
    /// </summary>
    public class TableOperationsManager
    {
        #region Fields

        private readonly ObservableCollection<UnifiedItem> _items;
        private readonly DataGrid _dataGrid;
        private readonly Action<string> _updateStatus;
        private readonly Action _updateUI;

        // Callback pre nastavenie HasUnsavedUnifiedChanges
        private readonly Action<bool> _setUnsavedChanges;

        // Callback pre UpdateUnsavedCommandsWarning (ak je k dispozícii)
        private readonly Action _updateWarning;

        #endregion

        #region Constructor

        /// <summary>
        /// Konštruktor
        /// </summary>
        /// <param name="items">Referencia na UnifiedItems collection</param>
        /// <param name="dataGrid">Referencia na DataGrid</param>
        /// <param name="updateStatus">Callback pre update statusu</param>
        /// <param name="updateUI">Callback pre update UI</param>
        /// <param name="setUnsavedChanges">Callback pre nastavenie HasUnsavedUnifiedChanges (optional)</param>
        /// <param name="updateWarning">Callback pre UpdateUnsavedCommandsWarning (optional)</param>
        public TableOperationsManager(
            ObservableCollection<UnifiedItem> items,
            DataGrid dataGrid,
            Action<string> updateStatus,
            Action updateUI,
            Action<bool> setUnsavedChanges = null,
            Action updateWarning = null)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _dataGrid = dataGrid ?? throw new ArgumentNullException(nameof(dataGrid));
            _updateStatus = updateStatus ?? throw new ArgumentNullException(nameof(updateStatus));
            _updateUI = updateUI ?? throw new ArgumentNullException(nameof(updateUI));
            _setUnsavedChanges = setUnsavedChanges;
            _updateWarning = updateWarning;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Označí zmeny ako neuložené
        /// </summary>
        private void MarkAsUnsaved()
        {
            _setUnsavedChanges?.Invoke(true);
        }

        /// <summary>
        /// Aktualizuje warning o neuložených príkazoch
        /// </summary>
        private void UpdateWarning()
        {
            _updateWarning?.Invoke();
        }

        #endregion

        #region Move Operations

        /// <summary>
        /// Presunie označenú položku nahor
        /// </summary>
        public void MoveUp()
        {
            var selectedItem = GetSelectedItem();
            if (selectedItem == null)
            {
                _updateStatus("⚠️ No item selected");
                return;
            }

            int currentIndex = _items.IndexOf(selectedItem);
            if (currentIndex <= 0)
            {
                _updateStatus("⚠️ Cannot move up - already at top");
                return;
            }

            // Presun
            _items.Move(currentIndex, currentIndex - 1);

            // Prepočítaj step numbers
            RecalculateStepNumbers();
            MarkAsUnsaved();

            // Zachovaj selection
            _dataGrid.SelectedItem = selectedItem;
            _dataGrid.ScrollIntoView(selectedItem);

            _updateStatus($"✅ Moved '{selectedItem.Name}' up");
            _updateUI();
        }

        /// <summary>
        /// Presunie označenú položku nadol
        /// </summary>
        public void MoveDown()
        {
            var selectedItem = GetSelectedItem();
            if (selectedItem == null)
            {
                _updateStatus("⚠️ No item selected");
                return;
            }

            int currentIndex = _items.IndexOf(selectedItem);
            if (currentIndex >= _items.Count - 1)
            {
                _updateStatus("⚠️ Cannot move down - already at bottom");
                return;
            }

            // Presun
            _items.Move(currentIndex, currentIndex + 1);

            // Prepočítaj step numbers
            RecalculateStepNumbers();
            MarkAsUnsaved();

            // Zachovaj selection
            _dataGrid.SelectedItem = selectedItem;
            _dataGrid.ScrollIntoView(selectedItem);

            _updateStatus($"✅ Moved '{selectedItem.Name}' down");
            _updateUI();
        }

        /// <summary>
        /// Presunie viacero označených položiek nahor
        /// </summary>
        public void MoveSelectedItemsUp()
        {
            var selectedItems = GetSelectedItems();
            if (!selectedItems.Any())
            {
                _updateStatus("⚠️ No items selected");
                return;
            }

            // Zoraď podľa indexu (od najnižšieho)
            var sortedItems = selectedItems
                .Select(item => new { Item = item, Index = _items.IndexOf(item) })
                .OrderBy(x => x.Index)
                .ToList();

            // Ak prvá položka je už na začiatku, nemôžeme posunúť
            if (sortedItems[0].Index == 0)
            {
                _updateStatus("⚠️ Cannot move up - selection contains top item");
                return;
            }

            // Presuň všetky položky o jeden nahor
            foreach (var item in sortedItems)
            {
                int currentIndex = _items.IndexOf(item.Item);
                _items.Move(currentIndex, currentIndex - 1);
            }

            RecalculateStepNumbers();
            MarkAsUnsaved();

            // Zachovaj selection
            _dataGrid.SelectedItems.Clear();
            foreach (var item in selectedItems)
            {
                _dataGrid.SelectedItems.Add(item);
            }

            _updateStatus($"✅ Moved {selectedItems.Count} items up");
            _updateUI();
        }

        /// <summary>
        /// Presunie viacero označených položiek nadol
        /// </summary>
        public void MoveSelectedItemsDown()
        {
            var selectedItems = GetSelectedItems();
            if (!selectedItems.Any())
            {
                _updateStatus("⚠️ No items selected");
                return;
            }

            // Zoraď podľa indexu (od najvyššieho)
            var sortedItems = selectedItems
                .Select(item => new { Item = item, Index = _items.IndexOf(item) })
                .OrderByDescending(x => x.Index)
                .ToList();

            // Ak posledná položka je už na konci, nemôžeme posunúť
            if (sortedItems[0].Index == _items.Count - 1)
            {
                _updateStatus("⚠️ Cannot move down - selection contains bottom item");
                return;
            }

            // Presuň všetky položky o jeden nadol
            foreach (var item in sortedItems)
            {
                int currentIndex = _items.IndexOf(item.Item);
                _items.Move(currentIndex, currentIndex + 1);
            }

            RecalculateStepNumbers();
            MarkAsUnsaved();

            // Zachovaj selection
            _dataGrid.SelectedItems.Clear();
            foreach (var item in selectedItems)
            {
                _dataGrid.SelectedItems.Add(item);
            }

            _updateStatus($"✅ Moved {selectedItems.Count} items down");
            _updateUI();
        }

        #endregion

        #region Delete Operations

        /// <summary>
        /// Vymaže označenú položku (s potvrdením)
        /// </summary>
        public void DeleteSelected()
        {
            var selectedItem = GetSelectedItem();
            if (selectedItem == null)
            {
                _updateStatus("⚠️ No item selected");
                return;
            }

            var message = $"Are you sure you want to delete this item?\n\n" +
                         $"Type: {selectedItem.TypeDisplay}\n" +
                         $"Name: {selectedItem.Name}";

            var result = MessageBox.Show(
                message,
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DeleteItemInternal(selectedItem);
                UpdateWarning();
            }
        }

        /// <summary>
        /// Vymaže viacero označených položiek (s potvrdením)
        /// </summary>
        public void DeleteSelectedItems()
        {
            var selectedItems = GetSelectedItems();
            if (!selectedItems.Any())
            {
                _updateStatus("⚠️ No items selected");
                return;
            }

            var result = MessageBox.Show(
                $"Delete {selectedItems.Count} selected items?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int count = selectedItems.Count;
                foreach (var item in selectedItems.ToList())
                {
                    _items.Remove(item);
                }

                RecalculateStepNumbers();
                MarkAsUnsaved();
                UpdateWarning();

                _updateStatus($"✅ Deleted {count} items");
                _updateUI();
            }
        }

        /// <summary>
        /// Vymaže všetky položky po potvrdení
        /// </summary>
        public void DeleteAll()
        {
            if (_items.Count == 0)
            {
                _updateStatus("⚠️ No items to delete");
                return;
            }

            var result = MessageBox.Show(
                $"Delete all {_items.Count} items?",
                "Confirm Delete All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                int count = _items.Count;
                _items.Clear();
                MarkAsUnsaved();
                UpdateWarning();

                _updateStatus($"✅ Deleted all {count} items");
                _updateUI();
            }
        }

        /// <summary>
        /// Interná metóda pre vymazanie položky (bez potvrdenia)
        /// </summary>
        private void DeleteItemInternal(UnifiedItem item)
        {
            if (item == null) return;

            string itemName = item.Name;
            _items.Remove(item);
            RecalculateStepNumbers();
            MarkAsUnsaved();

            _updateStatus($"✅ Deleted '{itemName}'");
            _updateUI();
        }

        #endregion

        #region Duplicate Operations

        /// <summary>
        /// Duplikuje označenú položku
        /// </summary>
        public void DuplicateSelected()
        {
            var selectedItem = GetSelectedItem();
            if (selectedItem == null)
            {
                _updateStatus("⚠️ No item selected");
                return;
            }

            DuplicateItemInternal(selectedItem, showMessage: true);
        }

        /// <summary>
        /// Duplikuje viacero označených položiek
        /// </summary>
        public void DuplicateSelectedItems()
        {
            var selectedItems = GetSelectedItems();
            if (!selectedItems.Any())
            {
                _updateStatus("⚠️ No items selected");
                return;
            }

            foreach (var item in selectedItems.ToList())
            {
                DuplicateItemInternal(item, showMessage: false);
            }

            _updateStatus($"✅ Duplicated {selectedItems.Count} items");
        }

        /// <summary>
        /// Interná metóda pre duplikáciu položky
        /// </summary>
        private void DuplicateItemInternal(UnifiedItem item, bool showMessage = false)
        {
            if (item == null) return;

            // Vytvor kópiu - použitím len existujúcich vlastností UnifiedItem
            var duplicate = new UnifiedItem
            {
                Type = item.Type,
                Name = item.Name + " (Copy)",
                Action = item.Action,
                Value = item.Value,
                RepeatCount = item.RepeatCount,
                Status = "Ready",
                Timestamp = DateTime.Now,
                FilePath = item.FilePath,
                ElementX = item.ElementX,
                ElementY = item.ElementY,
                ElementId = item.ElementId,
                ClassName = item.ClassName,
                IsLiveRecording = item.IsLiveRecording,
                LiveSequenceReference = item.LiveSequenceReference
            };

            // Vlož hneď za originál
            int originalIndex = _items.IndexOf(item);
            _items.Insert(originalIndex + 1, duplicate);

            RecalculateStepNumbers();
            MarkAsUnsaved();

            // Označ duplikát
            _dataGrid.SelectedItem = duplicate;
            _dataGrid.ScrollIntoView(duplicate);

            if (showMessage)
            {
                _updateStatus($"✅ Duplicated '{item.Name}'");
            }

            _updateUI();
        }

        #endregion

        #region Copy Operations

        /// <summary>
        /// Skopíruje detaily označenej položky do schránky
        /// </summary>
        public void CopyToClipboard()
        {
            var selectedItem = GetSelectedItem();
            if (selectedItem == null)
            {
                _updateStatus("⚠️ No item selected");
                return;
            }

            var details = $"Step: {selectedItem.StepNumber}\n" +
                         $"Type: {selectedItem.TypeDisplay}\n" +
                         $"Name: {selectedItem.Name}\n" +
                         $"Action: {selectedItem.Action}\n" +
                         $"Value: {selectedItem.Value}";

            Clipboard.SetText(details);
            _updateStatus("✅ Command details copied to clipboard");
        }

        #endregion

        #region Selection Operations

        /// <summary>
        /// Označí všetky položky
        /// </summary>
        public void SelectAll()
        {
            if (_items.Count == 0)
            {
                _updateStatus("⚠️ No items to select");
                return;
            }

            _dataGrid.SelectAll();
            _updateStatus($"✅ Selected all {_items.Count} items");
        }

        /// <summary>
        /// Zruší označenie
        /// </summary>
        public void ClearSelection()
        {
            _dataGrid.UnselectAll();
            _updateStatus("✅ Selection cleared");
        }

        /// <summary>
        /// Invertuje označenie
        /// </summary>
        public void InvertSelection()
        {
            if (_items.Count == 0)
            {
                _updateStatus("⚠️ No items available");
                return;
            }

            var currentlySelected = GetSelectedItems();
            var allItems = _items.ToList();

            _dataGrid.SelectedItems.Clear();

            foreach (var item in allItems)
            {
                if (!currentlySelected.Contains(item))
                {
                    _dataGrid.SelectedItems.Add(item);
                }
            }

            _updateStatus($"✅ Inverted selection ({_dataGrid.SelectedItems.Count} items selected)");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Prepočíta step numbers pre všetky položky
        /// </summary>
        public void RecalculateStepNumbers()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].StepNumber = i + 1;
                _items[i].CanMoveDown = i < _items.Count - 1;
            }
        }

        /// <summary>
        /// Vráti označenú položku
        /// </summary>
        public UnifiedItem GetSelectedItem()
        {
            return _dataGrid.SelectedItem as UnifiedItem;
        }

        /// <summary>
        /// Vráti všetky označené položky
        /// </summary>
        public List<UnifiedItem> GetSelectedItems()
        {
            return _dataGrid.SelectedItems.Cast<UnifiedItem>().ToList();
        }

        /// <summary>
        /// Skontroluje či je niečo označené
        /// </summary>
        public bool HasSelection()
        {
            return _dataGrid.SelectedItems.Count > 0;
        }

        /// <summary>
        /// Vráti počet označených položiek
        /// </summary>
        public int GetSelectedCount()
        {
            return _dataGrid.SelectedItems.Count;
        }

        /// <summary>
        /// Vráti index označenej položky
        /// </summary>
        public int GetSelectedIndex()
        {
            return _dataGrid.SelectedIndex;
        }

        /// <summary>
        /// Nastaví označenú položku podľa indexu
        /// </summary>
        public void SetSelectedIndex(int index)
        {
            if (index >= 0 && index < _items.Count)
            {
                _dataGrid.SelectedIndex = index;
            }
        }

        #endregion

        #region Context Menu Handlers

        /// <summary>
        /// Handler pre context menu - Move Up
        /// </summary>
        public void ContextMenu_MoveUp(object sender, RoutedEventArgs e)
        {
            if (GetSelectedCount() > 1)
                MoveSelectedItemsUp();
            else
                MoveUp();
        }

        /// <summary>
        /// Handler pre context menu - Move Down
        /// </summary>
        public void ContextMenu_MoveDown(object sender, RoutedEventArgs e)
        {
            if (GetSelectedCount() > 1)
                MoveSelectedItemsDown();
            else
                MoveDown();
        }

        /// <summary>
        /// Handler pre context menu - Duplicate
        /// </summary>
        public void ContextMenu_Duplicate(object sender, RoutedEventArgs e)
        {
            if (GetSelectedCount() > 1)
                DuplicateSelectedItems();
            else
                DuplicateSelected();
        }

        /// <summary>
        /// Handler pre context menu - Delete
        /// </summary>
        public void ContextMenu_Delete(object sender, RoutedEventArgs e)
        {
            if (GetSelectedCount() > 1)
                DeleteSelectedItems();
            else
                DeleteSelected();
        }

        /// <summary>
        /// Handler pre context menu - Copy
        /// </summary>
        public void ContextMenu_Copy(object sender, RoutedEventArgs e)
        {
            CopyToClipboard();
        }

        /// <summary>
        /// Handler pre context menu - Select All
        /// </summary>
        public void ContextMenu_SelectAll(object sender, RoutedEventArgs e)
        {
            SelectAll();
        }

        /// <summary>
        /// Handler pre context menu - Clear Selection
        /// </summary>
        public void ContextMenu_ClearSelection(object sender, RoutedEventArgs e)
        {
            ClearSelection();
        }

        /// <summary>
        /// Handler pre context menu - Invert Selection
        /// </summary>
        public void ContextMenu_InvertSelection(object sender, RoutedEventArgs e)
        {
            InvertSelection();
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Vyexportuje označené položky
        /// </summary>
        public List<UnifiedItem> ExportSelected()
        {
            return GetSelectedItems();
        }

        /// <summary>
        /// Importuje položky na aktuálnu pozíciu
        /// </summary>
        public void ImportItems(List<UnifiedItem> items, int? insertIndex = null)
        {
            if (items == null || !items.Any())
            {
                _updateStatus("⚠️ No items to import");
                return;
            }

            int targetIndex = insertIndex ?? _items.Count;

            foreach (var item in items)
            {
                // Vytvor kópiu aby sme nemali shared references
                var newItem = new UnifiedItem
                {
                    Type = item.Type,
                    Name = item.Name,
                    Action = item.Action,
                    Value = item.Value,
                    RepeatCount = item.RepeatCount,
                    Status = "Ready",
                    FilePath = item.FilePath,
                    ElementX = item.ElementX,
                    ElementY = item.ElementY,
                    ElementId = item.ElementId,
                    ClassName = item.ClassName,
                    IsLiveRecording = item.IsLiveRecording,
                    LiveSequenceReference = item.LiveSequenceReference,
                    Timestamp = DateTime.Now
                };

                _items.Insert(targetIndex++, newItem);
            }

            RecalculateStepNumbers();
            MarkAsUnsaved();

            _updateStatus($"✅ Imported {items.Count} items");
            _updateUI();
        }

        #endregion
    }
}
