using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace AppCommander.W7_11.WPF.Core.Managers
{
    /// <summary>
    /// Spravuje file operácie, Drag & Drop a document processing workflow
    /// </summary>
    public class FileManager
    {
        #region Private Fields

        private readonly Action<string> _updateStatus;
        private readonly Action _updateUI;

        // Target file pre document processing (Excel/CSV kam sa zapisujú dáta)
        private string _targetOutputFile;
        private string _targetOutputFileType;

        // Queue source súborov na spracovanie (PDF faktúry, atď)
        private readonly ObservableCollection<SourceFileInfo> _sourceFilesQueue;

        // Recent files history
        private readonly List<string> _recentFiles;
        private const int MAX_RECENT_FILES = 10;

        #endregion

        #region Properties

        /// <summary>
        /// Cesta k target output súboru (Excel/CSV)
        /// </summary>
        public string TargetOutputFile
        {
            get => _targetOutputFile;
            private set
            {
                _targetOutputFile = value;
                _targetOutputFileType = !string.IsNullOrEmpty(value) ? Path.GetExtension(value).ToLower() : null;
            }
        }

        /// <summary>
        /// Či je nastavený target output súbor
        /// </summary>
        public bool HasTargetFile => !string.IsNullOrEmpty(_targetOutputFile);

        /// <summary>
        /// Zoznam source súborov na spracovanie
        /// </summary>
        public ObservableCollection<SourceFileInfo> SourceFilesQueue => _sourceFilesQueue;

        /// <summary>
        /// Počet súborov v queue
        /// </summary>
        public int SourceFileCount => _sourceFilesQueue.Count;

        /// <summary>
        /// Recent files history
        /// </summary>
        public IReadOnlyList<string> RecentFiles => _recentFiles.AsReadOnly();

        #endregion

        #region Supported File Types

        /// <summary>
        /// Podporované TARGET output formáty (kam sa zapisujú dáta)
        /// </summary>
        public static readonly string[] SupportedOutputFormats = { ".xlsx", ".csv", ".xls" };

        /// <summary>
        /// Podporované SOURCE input formáty (odkiaľ sa čítajú dáta)
        /// </summary>
        public static readonly string[] SupportedInputFormats = { ".pdf", ".jpg", ".jpeg", ".png", ".txt", ".xml", ".json" };

        /// <summary>
        /// Podporované SEQUENCE formáty (Senaro príkazy)
        /// </summary>
        public static readonly string[] SupportedSequenceFormats = { ".acc", ".uniseq", ".acset", ".json" };

        #endregion

        #region Constructor

        public FileManager(
            Action<string> updateStatus,
            Action updateUI)
        {
            _updateStatus = updateStatus ?? throw new ArgumentNullException(nameof(updateStatus));
            _updateUI = updateUI ?? throw new ArgumentNullException(nameof(updateUI));

            _sourceFilesQueue = new ObservableCollection<SourceFileInfo>();
            _recentFiles = new List<string>();
            _targetOutputFile = null;
            _targetOutputFileType = null;
        }

        #endregion

        #region Drag & Drop - File Detection

        /// <summary>
        /// Spracuje Drag & Drop operáciu
        /// </summary>
        public DragDropResult ProcessDroppedFiles(string[] filePaths)
        {
            try
            {
                if (filePaths == null || filePaths.Length == 0)
                {
                    return new DragDropResult { Success = false, Message = "No files provided" };
                }

                var result = new DragDropResult { Success = true };

                // Roztriedenie súborov podľa typu
                var sequenceFiles = new List<string>();
                var outputFiles = new List<string>();
                var inputFiles = new List<string>();
                var unsupportedFiles = new List<string>();

                foreach (var file in filePaths)
                {
                    if (!File.Exists(file))
                    {
                        unsupportedFiles.Add(file);
                        continue;
                    }

                    var extension = Path.GetExtension(file).ToLower();

                    if (SupportedSequenceFormats.Contains(extension))
                    {
                        sequenceFiles.Add(file);
                    }
                    else if (SupportedOutputFormats.Contains(extension))
                    {
                        outputFiles.Add(file);
                    }
                    else if (SupportedInputFormats.Contains(extension))
                    {
                        inputFiles.Add(file);
                    }
                    else
                    {
                        unsupportedFiles.Add(file);
                    }
                }

                // Výsledky
                result.SequenceFiles = sequenceFiles;
                result.OutputFiles = outputFiles;
                result.InputFiles = inputFiles;
                result.UnsupportedFiles = unsupportedFiles;

                // Zostavenie správy
                var messages = new List<string>();

                if (sequenceFiles.Any())
                    messages.Add($"{sequenceFiles.Count} sequence file(s)");
                if (outputFiles.Any())
                    messages.Add($"{outputFiles.Count} output file(s)");
                if (inputFiles.Any())
                    messages.Add($"{inputFiles.Count} input file(s)");
                if (unsupportedFiles.Any())
                    messages.Add($"{unsupportedFiles.Count} unsupported file(s)");

                result.Message = $"Detected: {string.Join(", ", messages)}";

                Debug.WriteLine($"Drag & Drop processed: {result.Message}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing dropped files: {ex.Message}");
                return new DragDropResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        #endregion

        #region Target Output File Management

        /// <summary>
        /// Nastaví target output súbor (Excel/CSV)
        /// </summary>
        public bool SetTargetOutputFile(string filePath, bool confirmOverwrite = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    MessageBox.Show("Invalid file path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                var extension = Path.GetExtension(filePath).ToLower();

                if (!SupportedOutputFormats.Contains(extension))
                {
                    MessageBox.Show(
                        $"Unsupported output format: {extension}\n\n" +
                        $"Supported formats: {string.Join(", ", SupportedOutputFormats)}",
                        "Unsupported Format",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                // Ak už je nastavený target, opýtaj sa na potvrdenie
                if (HasTargetFile && confirmOverwrite)
                {
                    var result = MessageBox.Show(
                        $"Replace current target file?\n\n" +
                        $"Current: {Path.GetFileName(_targetOutputFile)}\n" +
                        $"New: {Path.GetFileName(filePath)}",
                        "Replace Target File",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return false;
                }

                TargetOutputFile = filePath;
                AddToRecentFiles(filePath);

                _updateUI();
                _updateStatus($"Target output file set: {Path.GetFileName(filePath)}");

                Debug.WriteLine($"Target output file set: {filePath}");

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error setting target output file", ex);
                return false;
            }
        }

        /// <summary>
        /// Vymaže target output súbor
        /// </summary>
        public void ClearTargetOutputFile()
        {
            TargetOutputFile = null;
            _updateUI();
            _updateStatus("Target output file cleared");
            Debug.WriteLine("Target output file cleared");
        }

        /// <summary>
        /// Otvorí dialog na výber target output súboru
        /// </summary>
        public bool SelectTargetOutputFileDialog()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select Target Output File (Excel/CSV)",
                    Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|Excel 97-2003 (*.xls)|*.xls|All Files (*.*)|*.*",
                    DefaultExt = ".xlsx",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    return SetTargetOutputFile(dialog.FileName, confirmOverwrite: false);
                }

                return false;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error selecting target output file", ex);
                return false;
            }
        }

        #endregion

        #region Source Files Queue Management

        /// <summary>
        /// Pridá source súbor do queue
        /// </summary>
        public bool AddSourceFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File not found: {filePath}", "File Not Found",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                var extension = Path.GetExtension(filePath).ToLower();

                if (!SupportedInputFormats.Contains(extension))
                {
                    MessageBox.Show(
                        $"Unsupported input format: {extension}\n\n" +
                        $"Supported formats: {string.Join(", ", SupportedInputFormats)}",
                        "Unsupported Format",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                // Kontrola duplicity
                if (_sourceFilesQueue.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    _updateStatus($"File already in queue: {Path.GetFileName(filePath)}");
                    return false;
                }

                var sourceFile = new SourceFileInfo
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileType = extension,
                    FileSize = new FileInfo(filePath).Length,
                    AddedTime = DateTime.Now,
                    Status = "Pending",
                    Priority = SourceFilePriority.Normal
                };

                _sourceFilesQueue.Add(sourceFile);
                AddToRecentFiles(filePath);

                _updateUI();
                _updateStatus($"Added to queue: {sourceFile.FileName}");

                Debug.WriteLine($"Source file added to queue: {filePath}");

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error adding source file", ex);
                return false;
            }
        }

        /// <summary>
        /// Pridá viacero source súborov naraz
        /// </summary>
        public int AddSourceFiles(IEnumerable<string> filePaths)
        {
            int addedCount = 0;

            foreach (var filePath in filePaths)
            {
                if (AddSourceFile(filePath))
                    addedCount++;
            }

            if (addedCount > 0)
            {
                _updateStatus($"Added {addedCount} file(s) to processing queue");
            }

            return addedCount;
        }

        /// <summary>
        /// Odstráni source súbor z queue
        /// </summary>
        public bool RemoveSourceFile(SourceFileInfo file)
        {
            if (file == null) return false;

            var removed = _sourceFilesQueue.Remove(file);

            if (removed)
            {
                _updateUI();
                _updateStatus($"Removed from queue: {file.FileName}");
                Debug.WriteLine($"Source file removed from queue: {file.FilePath}");
            }

            return removed;
        }

        /// <summary>
        /// Vymaže všetky source súbory z queue
        /// </summary>
        public void ClearSourceFilesQueue()
        {
            var count = _sourceFilesQueue.Count;
            _sourceFilesQueue.Clear();

            _updateUI();
            _updateStatus($"Queue cleared ({count} file(s) removed)");
            Debug.WriteLine($"Source files queue cleared: {count} files");
        }

        /// <summary>
        /// Aktualizuje status source súboru
        /// </summary>
        public void UpdateSourceFileStatus(string filePath, string status, string errorMessage = null)
        {
            var file = _sourceFilesQueue.FirstOrDefault(f =>
                f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (file != null)
            {
                file.Status = status;
                file.ErrorMessage = errorMessage;

                if (status == "Completed")
                    file.ProcessedTime = DateTime.Now;

                _updateUI();
            }
        }

        #endregion

        #region File Dialogs - Sequence Operations

        /// <summary>
        /// Otvorí dialog pre Open Sequence
        /// </summary>
        public string OpenSequenceDialog()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "All Supported Files|*.acc;*.json;*.uniseq|" +
                            "Senaro Files (*.acc)|*.acc|" +
                            "Unified Sequence Files (*.uniseq)|*.uniseq|" +
                            "JSON Files (*.json)|*.json|" +
                            "All Files (*.*)|*.*",
                    DefaultExt = ".acc",
                    Title = "Open Sequence"
                };

                if (dialog.ShowDialog() == true)
                {
                    AddToRecentFiles(dialog.FileName);
                    return dialog.FileName;
                }

                return null;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening sequence dialog", ex);
                return null;
            }
        }

        /// <summary>
        /// Otvorí dialog pre Save Sequence As
        /// </summary>
        public string SaveSequenceAsDialog(string defaultFileName = null)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Senaro Files (*.acc)|*.acc|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".acc",
                    Title = "Save Sequence As",
                    FileName = defaultFileName ?? $"Sequence_{DateTime.Now:yyyyMMdd_HHmmss}.acc"
                };

                if (dialog.ShowDialog() == true)
                {
                    AddToRecentFiles(dialog.FileName);
                    return dialog.FileName;
                }

                return null;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence dialog", ex);
                return null;
            }
        }

        /// <summary>
        /// Otvorí dialog pre Save Unified Sequence As
        /// </summary>
        public string SaveUnifiedSequenceAsDialog(string defaultFileName = null)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Unified Sequence Files (*.uniseq)|*.uniseq|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".uniseq",
                    Title = "Save Unified Sequence As",
                    FileName = defaultFileName ?? $"UnifiedSequence_{DateTime.Now:yyyyMMdd_HHmmss}.uniseq"
                };

                if (dialog.ShowDialog() == true)
                {
                    AddToRecentFiles(dialog.FileName);
                    return dialog.FileName;
                }

                return null;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving unified sequence dialog", ex);
                return null;
            }
        }

        #endregion

        #region File Validation

        /// <summary>
        /// Validuje či súbor existuje
        /// </summary>
        public bool ValidateFileExists(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            return File.Exists(filePath);
        }

        /// <summary>
        /// Validuje extension súboru
        /// </summary>
        public bool ValidateFileExtension(string filePath, string[] allowedExtensions)
        {
            if (string.IsNullOrWhiteSpace(filePath) || allowedExtensions == null)
                return false;

            var extension = Path.GetExtension(filePath).ToLower();
            return allowedExtensions.Contains(extension);
        }

        /// <summary>
        /// Získa user-friendly popis typu súboru
        /// </summary>
        public string GetFileTypeDescription(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            if (extension == ".acc")
                return "Senaro Command Sequence";
            else if (extension == ".uniseq")
                return "Unified Sequence";
            else if (extension == ".acset")
                return "Sequence Set";
            else if (extension == ".xlsx")
                return "Excel Workbook";
            else if (extension == ".xls")
                return "Excel 97-2003";
            else if (extension == ".csv")
                return "CSV File";
            else if (extension == ".pdf")
                return "PDF Document";
            else if (extension == ".json")
                return "JSON File";
            else if (extension == ".txt")
                return "Text File";
            else if (extension == ".xml")
                return "XML File";
            else if (extension == ".jpg" || extension == ".jpeg")
                return "JPEG Image";
            else if (extension == ".png")
                return "PNG Image";
            else
                return "Unknown File Type";
        }

        #endregion

        #region Recent Files Management

        /// <summary>
        /// Pridá súbor do recent files
        /// </summary>
        private void AddToRecentFiles(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            // Odstráň ak už existuje
            _recentFiles.Remove(filePath);

            // Pridaj na začiatok
            _recentFiles.Insert(0, filePath);

            // Limit na max počet
            while (_recentFiles.Count > MAX_RECENT_FILES)
            {
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            }

            Debug.WriteLine($"Added to recent files: {filePath}");
        }

        /// <summary>
        /// Vymaže recent files
        /// </summary>
        public void ClearRecentFiles()
        {
            _recentFiles.Clear();
            _updateUI();
            Debug.WriteLine("Recent files cleared");
        }

        #endregion

        #region Helper Methods

        private void ShowErrorMessage(string title, Exception ex)
        {
            var message = $"{title}\n\nError: {ex.Message}";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Debug.WriteLine($"{title}: {ex.Message}");
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Informácie o source súbore v queue
    /// </summary>
    public class SourceFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public DateTime AddedTime { get; set; }
        public DateTime? ProcessedTime { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public SourceFilePriority Priority { get; set; }

        public string FileSizeFormatted => FormatFileSize(FileSize);

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int order = 0;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Priorita source súboru
    /// </summary>
    public enum SourceFilePriority
    {
        Low,
        Normal,
        High
    }

    /// <summary>
    /// Výsledok Drag & Drop operácie
    /// </summary>
    public class DragDropResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> SequenceFiles { get; set; } = new List<string>();
        public List<string> OutputFiles { get; set; } = new List<string>();
        public List<string> InputFiles { get; set; } = new List<string>();
        public List<string> UnsupportedFiles { get; set; } = new List<string>();
    }

    #endregion
}
