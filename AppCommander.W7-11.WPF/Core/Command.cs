using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public enum CommandType
    {
        KeyPress,
        MouseClick,
        SetText,
        Wait,
        Loop,
        LoopEnd,
        Click,
        DoubleClick,
        RightClick
    }

    public class Command
    {
        public int StepNumber { get; set; }
        public string ElementName { get; set; } = string.Empty;
        public CommandType Type { get; set; }
        public int RepeatCount { get; set; } = 1;
        public string Value { get; set; } = string.Empty;
        public string TargetWindow { get; set; } = string.Empty;
        public string TargetProcess { get; set; } = string.Empty;
        public bool IsLoopStart { get; set; }
        public bool IsLoopCommand { get; set; } // Commands inside loop (starts with -)
        public DateTime Timestamp { get; set; }

        // UI Element identification
        public string ElementId { get; set; } = string.Empty;
        public string ElementClass { get; set; } = string.Empty;
        public string ElementControlType { get; set; } = string.Empty;
        public int ElementX { get; set; }
        public int ElementY { get; set; }

        /// <summary>
        /// Označuje či je command pre tabuľkovú bunku
        /// </summary>
        public bool IsTableCommand { get; set; } = false;

        /// <summary>
        /// Identifikátor tabuľkovej bunky
        /// </summary>
        public string TableCellIdentifier { get; set; } = "";

        /// <summary>
        /// Názov tabuľky
        /// </summary>
        public string TableName { get; set; } = "";

        /// <summary>
        /// Číslo riadka (0-based)
        /// </summary>
        public int TableRow { get; set; } = -1;

        /// <summary>
        /// Číslo stĺpca (0-based)
        /// </summary>
        public int TableColumn { get; set; } = -1;

        /// <summary>
        /// Názov stĺpca
        /// </summary>
        public string TableColumnName { get; set; } = "";

        /// <summary>
        /// Obsah bunky
        /// </summary>
        public string TableCellContent { get; set; } = "";

        // Key-specific data - OPRAVENÉ: pridaná serializácia
        private Keys _key = Keys.None;
        public Keys Key
        {
            get => _key;
            set
            {
                _key = value;
                KeyCode = (int)value; // Synchronizuj s KeyCode
            }
        }

        // Pridané pre lepšiu serializáciu
        public int KeyCode { get; set; } = 0;

        // Mouse-specific data  
        public MouseButtons MouseButton { get; set; }

        // **WinUI3 support properties**
        public int OriginalX { get; set; }  // Originálne súradnice z nahrávky
        public int OriginalY { get; set; }  // Originálne súradnice z nahrávky
        public string ElementText { get; set; } = string.Empty;  // Text obsah elementu
        public string ElementHelpText { get; set; } = string.Empty;  // Help text
        public string ElementAccessKey { get; set; } = string.Empty;  // Access key
        public bool IsWinUI3Element { get; set; } = false;  // Označenie WinUI3 elementu
        public double ElementConfidence { get; set; } = 0.0;  // Confidence score pre finding
        public string LastFoundMethod { get; set; } = string.Empty;  // Metóda ktorou bol element nájdený

        public Command()
        {
            Timestamp = DateTime.Now;
        }

        public Command(int stepNumber, string elementName, CommandType type)
        {
            StepNumber = stepNumber;
            ElementName = elementName;
            Type = type;
            Timestamp = DateTime.Now;
        }

        // Konštruktor s WinUI3 podporou
        public Command(int stepNumber, string elementName, CommandType type, int originalX = 0, int originalY = 0)
            : this(stepNumber, elementName, type)
        {
            OriginalX = originalX;
            OriginalY = originalY;
            // Ak nie sú nastavené ElementX/Y, použi originálne
            if (ElementX == 0 && ElementY == 0)
            {
                ElementX = originalX;
                ElementY = originalY;
            }
        }

        /// <summary>
        /// Aktualizuje element informácie z UIElementInfo (hlavne pre WinUI3)
        /// </summary>
        public void UpdateFromElementInfo(UIElementInfo elementInfo)
        {
            if (elementInfo == null) return;

            // Aktualizuj identifikátory ak sú lepšie
            if (!string.IsNullOrEmpty(elementInfo.AutomationId) && string.IsNullOrEmpty(ElementId))
                ElementId = elementInfo.AutomationId;

            if (!string.IsNullOrEmpty(elementInfo.ClassName) && string.IsNullOrEmpty(ElementClass))
                ElementClass = elementInfo.ClassName;

            if (!string.IsNullOrEmpty(elementInfo.ControlType) && string.IsNullOrEmpty(ElementControlType))
                ElementControlType = elementInfo.ControlType;

            // Aktualizuj text informácie
            if (!string.IsNullOrEmpty(elementInfo.ElementText))
                ElementText = elementInfo.ElementText;

            if (!string.IsNullOrEmpty(elementInfo.HelpText))
                ElementHelpText = elementInfo.HelpText;

            if (!string.IsNullOrEmpty(elementInfo.AccessKey))
                ElementAccessKey = elementInfo.AccessKey;

            // Aktualizuj pozíciu (ale zachovaj originálne súradnice)
            ElementX = elementInfo.X;
            ElementY = elementInfo.Y;

            // Označiť WinUI3 element
            IsWinUI3Element = elementInfo.ClassName == "Microsoft.UI.Content.DesktopChildSiteBridge";

            // Vypočítaj confidence
            CalculateElementConfidence();
        }

        /// <summary>
        /// Vypočíta confidence score elementu
        /// </summary>
        private void CalculateElementConfidence()
        {
            double confidence = 0.0;

            // AutomationId má najvyššiu váhu
            if (!string.IsNullOrEmpty(ElementId) && !IsGenericId(ElementId))
                confidence += 0.4;

            // Text má strednú váhu
            if (!string.IsNullOrEmpty(ElementText) && ElementText.Length > 1)
                confidence += 0.3;

            // Name má nižšiu váhu
            if (!string.IsNullOrEmpty(ElementName) && !IsGenericName(ElementName))
                confidence += 0.2;

            // Valid coordinates
            if (ElementX > 0 && ElementY > 0)
                confidence += 0.1;

            ElementConfidence = Math.Min(confidence, 1.0);
        }

        /// <summary>
        /// Vráti najlepší dostupný identifikátor elementu
        /// </summary>
        public string GetBestElementIdentifier()
        {
            // Priorita: AutomationId > meaningful ElementName > ElementText > ClassName + ControlType
            if (!string.IsNullOrEmpty(ElementId) && !IsGenericId(ElementId))
                return $"AutomationId:{ElementId}";

            if (!string.IsNullOrEmpty(ElementName) && !IsGenericName(ElementName))
                return $"Name:{ElementName}";

            if (!string.IsNullOrEmpty(ElementText) && ElementText.Length <= 50)
                return $"Text:{ElementText}";

            if (!string.IsNullOrEmpty(ElementClass) && !string.IsNullOrEmpty(ElementControlType))
                return $"Class:{ElementClass}:Type:{ElementControlType}";

            return $"Position:{OriginalX},{OriginalY}";
        }

        private bool IsGenericId(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            if (id.Length > 20) return true;
            if (id.All(char.IsDigit)) return true;
            if (id.Contains("-") || id.Contains("{")) return true;
            return false;
        }

        private bool IsGenericName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            var genericNames = new[] { "Unknown", "pane_Unknown", "Element_at_", "Click_at_", "Microsoft.UI.Content" };
            return genericNames.Any(g => name.Contains(g));
        }

        /// <summary>
        /// Converts command to file format string - UNIFIED VERSION s WinUI3 podporou
        /// </summary>
        public string ToFileFormat()
        {
            string prefix = IsLoopCommand ? "-" : "";
            string suffix = IsLoopStart ? ":" : "";

            // Kompletný formát s všetkými WinUI3 údajmi
            var parts = new List<string>
            {
                $"{prefix}{StepNumber}",
                $"\"{ElementName}\"",
                Type.ToString(),
                RepeatCount.ToString(),
                $"\"{Value}\"",
                $"\"{ElementId}\"",
                $"\"{ElementClass}\"",
                $"\"{ElementControlType}\"",
                ElementX.ToString(),
                ElementY.ToString(),
                OriginalX.ToString(),          // WinUI3: Originálne súradnice
                OriginalY.ToString(),          // WinUI3: Originálne súradnice
                $"\"{ElementText}\"",          // WinUI3: Text obsah
                $"\"{ElementHelpText}\"",      // WinUI3: Help text
                $"\"{ElementAccessKey}\"",     // WinUI3: Access key
                IsWinUI3Element.ToString(),    // WinUI3: Flag
                ElementConfidence.ToString("F2"), // WinUI3: Confidence score
                $"\"{LastFoundMethod}\"",      // WinUI3: Posledná metóda hľadania
                KeyCode.ToString(),
                MouseButton.ToString(),
                $"\"{TargetWindow}\"",
                $"\"{TargetProcess}\""
            };

            return string.Join(",", parts) + suffix;
        }

        /// <summary>
        /// Creates command from file format string - UNIFIED VERSION s WinUI3 podporou
        /// </summary>
        public static Command FromFileFormat(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            try
            {
                var command = new Command();

                // Parse loop indicators
                if (line.TrimStart().StartsWith("-"))
                {
                    command.IsLoopCommand = true;
                    line = line.TrimStart().Substring(1);
                }

                if (line.TrimEnd().EndsWith(":"))
                {
                    command.IsLoopStart = true;
                    line = line.TrimEnd().TrimEnd(':');
                }

                var parts = SplitCsvLine(line);
                if (parts.Count < 5) return null;

                // Parse základné údaje
                if (!int.TryParse(parts[0].Trim(), out int stepNumber)) return null;
                command.StepNumber = stepNumber;

                command.ElementName = Unquote(parts[1]);

                if (!Enum.TryParse<CommandType>(parts[2].Trim(), out CommandType type)) return null;
                command.Type = type;

                if (parts.Count > 3 && int.TryParse(parts[3].Trim(), out int repeatCount))
                    command.RepeatCount = repeatCount;

                if (parts.Count > 4)
                    command.Value = Unquote(parts[4]);

                // Parse UI Element údaje
                if (parts.Count > 5) command.ElementId = Unquote(parts[5]);
                if (parts.Count > 6) command.ElementClass = Unquote(parts[6]);
                if (parts.Count > 7) command.ElementControlType = Unquote(parts[7]);

                if (parts.Count > 8 && int.TryParse(parts[8].Trim(), out int x))
                    command.ElementX = x;
                if (parts.Count > 9 && int.TryParse(parts[9].Trim(), out int y))
                    command.ElementY = y;

                // Parse WinUI3 špecifické údaje
                if (parts.Count > 10 && int.TryParse(parts[10].Trim(), out int origX))
                    command.OriginalX = origX;
                if (parts.Count > 11 && int.TryParse(parts[11].Trim(), out int origY))
                    command.OriginalY = origY;
                if (parts.Count > 12) command.ElementText = Unquote(parts[12]);
                if (parts.Count > 13) command.ElementHelpText = Unquote(parts[13]);
                if (parts.Count > 14) command.ElementAccessKey = Unquote(parts[14]);
                if (parts.Count > 15 && bool.TryParse(parts[15].Trim(), out bool isWinUI3))
                    command.IsWinUI3Element = isWinUI3;
                if (parts.Count > 16 && double.TryParse(parts[16].Trim(), out double confidence))
                    command.ElementConfidence = confidence;
                if (parts.Count > 17) command.LastFoundMethod = Unquote(parts[17]);

                // Parse ostatné údaje
                if (parts.Count > 18 && int.TryParse(parts[18].Trim(), out int keyCode))
                {
                    command.KeyCode = keyCode;
                    command._key = (Keys)keyCode;
                }

                if (parts.Count > 19 && Enum.TryParse<MouseButtons>(parts[19].Trim(), out var mouseButton))
                    command.MouseButton = mouseButton;

                if (parts.Count > 20) command.TargetWindow = Unquote(parts[20]);
                if (parts.Count > 21) command.TargetProcess = Unquote(parts[21]);

                return command;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing command: {ex.Message}");
                return null;
            }
        }

        private static List<string> SplitCsvLine(string line)
        {
            var parts = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            parts.Add(current.ToString());
            return parts;
        }

        private static string Unquote(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.Trim();
            if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length >= 2)
                return text.Substring(1, text.Length - 2);

            return text;
        }

        //public override string ToString()
        //{
        //    if (Type == CommandType.KeyPress)
        //        return $"Step {StepNumber}: {Type} {Key} (x{RepeatCount})";
        //    else
        //        return $"Step {StepNumber}: {Type} on {ElementName} (x{RepeatCount})";
        //}

        //u*u*u*u*u*u*u*u*u*
        // === ROZŠÍRENÉ METÓDY ===

        /// <summary>
        /// **Rozšírená metóda UpdateFromElementInfo s podporou tabuliek**
        /// </summary>
        public void UpdateFromElementInfoEnhanced(UIElementInfo elementInfo)
        {
            if (elementInfo == null) return;

            // Štandardná aktualizácia
            UpdateFromElementInfo(elementInfo);

            // TABUĽKOVÁ ŠPECIFICKÁ AKTUALIZÁCIA
            if (elementInfo.IsTableCell)
            {
                IsTableCommand = true;
                TableCellIdentifier = elementInfo.TableCellIdentifier;
                TableName = elementInfo.TableName;
                TableRow = elementInfo.TableRow;
                TableColumn = elementInfo.TableColumn;
                TableColumnName = elementInfo.TableColumnName;
                TableCellContent = elementInfo.TableCellContent;

                // Pre tabuľkové bunky preferuj TableCellIdentifier ako ElementId
                if (string.IsNullOrEmpty(ElementId) && !string.IsNullOrEmpty(TableCellIdentifier))
                {
                    ElementId = TableCellIdentifier;
                }

                // Aktualizuj element name na table-specific
                if (string.IsNullOrEmpty(ElementName) || IsGenericName(ElementName))
                {
                    ElementName = elementInfo.GetTableCellDisplayName();
                }

                System.Diagnostics.Debug.WriteLine($"Updated command as table cell: {ElementName} -> {TableCellIdentifier}");
            }

            // Prepočítaj confidence
            CalculateElementConfidence();
        }

        /// <summary>
        /// **Rozšírená metóda GetBestElementIdentifier s podporou tabuliek**
        /// </summary>
        public string GetBestElementIdentifierEnhanced()
        {
            // Pre tabuľkové príkazy má TableCellIdentifier najvyššiu prioritu
            if (IsTableCommand && !string.IsNullOrEmpty(TableCellIdentifier))
            {
                return $"TableCell:{TableCellIdentifier}";
            }

            // Fallback na štandardnú metódu
            return GetBestElementIdentifier();
        }

        /// <summary>
        /// **Rozšírená metóda ToFileFormat s podporou tabuliek**
        /// </summary>
        public string ToFileFormatEnhanced()
        {
            string prefix = IsLoopCommand ? "-" : "";
            string suffix = IsLoopStart ? ":" : "";

            // Kompletný formát s tabuľkovými údajmi
            var parts = new List<string>
    {
        $"{prefix}{StepNumber}",
        $"\"{ElementName}\"",
        Type.ToString(),
        RepeatCount.ToString(),
        $"\"{Value}\"",
        $"\"{ElementId}\"",
        $"\"{ElementClass}\"",
        $"\"{ElementControlType}\"",
        ElementX.ToString(),
        ElementY.ToString(),
        OriginalX.ToString(),
        OriginalY.ToString(),
        $"\"{ElementText}\"",
        $"\"{ElementHelpText}\"",
        $"\"{ElementAccessKey}\"",
        IsWinUI3Element.ToString(),
        ElementConfidence.ToString("F2"),
        $"\"{LastFoundMethod}\"",
        KeyCode.ToString(),
        MouseButton.ToString(),
        $"\"{TargetWindow}\"",
        $"\"{TargetProcess}\"",
        
        // **NOVÉ TABUĽKOVÉ FIELDS**
        IsTableCommand.ToString(),
        $"\"{TableCellIdentifier}\"",
        $"\"{TableName}\"",
        TableRow.ToString(),
        TableColumn.ToString(),
        $"\"{TableColumnName}\"",
        $"\"{TableCellContent}\""
    };

            return string.Join(",", parts) + suffix;
        }

        /// <summary>
        /// **Rozšírená metóda FromFileFormat s podporou tabuliek**
        /// </summary>
        public static Command FromFileFormatEnhanced(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            try
            {
                var command = new Command();

                // Parse loop indicators
                if (line.TrimStart().StartsWith("-"))
                {
                    command.IsLoopCommand = true;
                    line = line.TrimStart().Substring(1);
                }

                if (line.TrimEnd().EndsWith(":"))
                {
                    command.IsLoopStart = true;
                    line = line.TrimEnd().TrimEnd(':');
                }

                var parts = SplitCsvLine(line);
                if (parts.Count < 5) return null;

                // Parse základné údaje (rovnako ako predtým)
                if (!int.TryParse(parts[0].Trim(), out int stepNumber)) return null;
                command.StepNumber = stepNumber;

                command.ElementName = Unquote(parts[1]);

                if (!Enum.TryParse<CommandType>(parts[2].Trim(), out CommandType type)) return null;
                command.Type = type;

                if (parts.Count > 3 && int.TryParse(parts[3].Trim(), out int repeatCount))
                    command.RepeatCount = repeatCount;

                if (parts.Count > 4)
                    command.Value = Unquote(parts[4]);

                // Parse UI Element údaje (rovnako ako predtým)
                if (parts.Count > 5) command.ElementId = Unquote(parts[5]);
                if (parts.Count > 6) command.ElementClass = Unquote(parts[6]);
                if (parts.Count > 7) command.ElementControlType = Unquote(parts[7]);

                if (parts.Count > 8 && int.TryParse(parts[8].Trim(), out int x))
                    command.ElementX = x;
                if (parts.Count > 9 && int.TryParse(parts[9].Trim(), out int y))
                    command.ElementY = y;

                // Parse WinUI3 špecifické údaje (rovnako ako predtým)
                if (parts.Count > 10 && int.TryParse(parts[10].Trim(), out int origX))
                    command.OriginalX = origX;
                if (parts.Count > 11 && int.TryParse(parts[11].Trim(), out int origY))
                    command.OriginalY = origY;
                if (parts.Count > 12) command.ElementText = Unquote(parts[12]);
                if (parts.Count > 13) command.ElementHelpText = Unquote(parts[13]);
                if (parts.Count > 14) command.ElementAccessKey = Unquote(parts[14]);
                if (parts.Count > 15 && bool.TryParse(parts[15].Trim(), out bool isWinUI3))
                    command.IsWinUI3Element = isWinUI3;
                if (parts.Count > 16 && double.TryParse(parts[16].Trim(), out double confidence))
                    command.ElementConfidence = confidence;
                if (parts.Count > 17) command.LastFoundMethod = Unquote(parts[17]);

                // Parse ostatné údaje (rovnako ako predtým)
                if (parts.Count > 18 && int.TryParse(parts[18].Trim(), out int keyCode))
                {
                    command.KeyCode = keyCode;
                    command._key = (Keys)keyCode;
                }

                if (parts.Count > 19 && Enum.TryParse<MouseButtons>(parts[19].Trim(), out var mouseButton))
                    command.MouseButton = mouseButton;

                if (parts.Count > 20) command.TargetWindow = Unquote(parts[20]);
                if (parts.Count > 21) command.TargetProcess = Unquote(parts[21]);

                // **PARSE NOVÝCH TABUĽKOVÝCH FIELDS**
                if (parts.Count > 22 && bool.TryParse(parts[22].Trim(), out bool isTableCommand))
                    command.IsTableCommand = isTableCommand;
                if (parts.Count > 23) command.TableCellIdentifier = Unquote(parts[23]);
                if (parts.Count > 24) command.TableName = Unquote(parts[24]);
                if (parts.Count > 25 && int.TryParse(parts[25].Trim(), out int tableRow))
                    command.TableRow = tableRow;
                if (parts.Count > 26 && int.TryParse(parts[26].Trim(), out int tableColumn))
                    command.TableColumn = tableColumn;
                if (parts.Count > 27) command.TableColumnName = Unquote(parts[27]);
                if (parts.Count > 28) command.TableCellContent = Unquote(parts[28]);

                return command;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing enhanced command: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// **Rozšírený ToString s podporou tabuliek**
        /// </summary>
        public override string ToString()
        {
            if (IsTableCommand)
            {
                string tableInfo = $"Table:{TableName}, Row:{TableRow}, Col:{TableColumn}";
                if (Type == CommandType.KeyPress)
                    return $"Step {StepNumber}: {Type} {Key} in {tableInfo} (x{RepeatCount})";
                else
                    return $"Step {StepNumber}: {Type} on {ElementName} in {tableInfo} (x{RepeatCount})";
            }

            // Štandardný toString
            if (Type == CommandType.KeyPress)
                return $"Step {StepNumber}: {Type} {Key} (x{RepeatCount})";
            else
                return $"Step {StepNumber}: {Type} on {ElementName} (x{RepeatCount})";
        }
        //u*u*u*u*u*u*u*u*u*

    }

    public class CommandSequence
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Command> Commands { get; set; } = new List<Command>();
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public string TargetApplication { get; set; } = string.Empty;

        // vlastnosti pre adaptívne spustenie
        public string TargetProcessName { get; set; } = string.Empty;
        public string TargetWindowTitle { get; set; } = string.Empty;
        public string TargetWindowClass { get; set; } = string.Empty;
        public bool AutoFindTarget { get; set; } = true;
        public int MaxWaitTimeSeconds { get; set; } = 30;

        public CommandSequence()
        {
            Created = DateTime.Now;
            LastModified = DateTime.Now;
        }

        public CommandSequence(string name) : this()
        {
            Name = name;
        }

        public void AddCommand(Command command)
        {
            Commands.Add(command);
            LastModified = DateTime.Now;
        }

        public void RemoveCommand(int stepNumber)
        {
            Commands.RemoveAll(c => c.StepNumber == stepNumber);
            LastModified = DateTime.Now;
        }

        public Command GetCommand(int stepNumber)
        {
            return Commands.Find(c => c.StepNumber == stepNumber);
        }

        /// <summary>
        /// Saves command sequence to file in the defined format
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var lines = new List<string>();

            // Add header comment with metadata
            lines.Add($"# AppCommander Command File v2.1 (WinUI3 Enhanced)");
            lines.Add($"# Name: {Name}");
            lines.Add($"# Description: {Description}");
            lines.Add($"# Created: {Created:yyyy-MM-dd HH:mm:ss}");
            lines.Add($"# Last Modified: {LastModified:yyyy-MM-dd HH:mm:ss}");
            lines.Add($"# Target Application: {TargetApplication}");
            lines.Add($"# Target Process: {TargetProcessName}");
            lines.Add($"# Target Window Title: {TargetWindowTitle}");
            lines.Add($"# Target Window Class: {TargetWindowClass}");
            lines.Add($"# Auto Find Target: {AutoFindTarget}");
            lines.Add($"# Max Wait Time: {MaxWaitTimeSeconds}");
            lines.Add($"# Commands Count: {Commands.Count}");

            // Štatistiky WinUI3
            var winui3Count = Commands.Count(c => c.IsWinUI3Element);
            if (winui3Count > 0)
            {
                lines.Add($"# WinUI3 Commands: {winui3Count}");
                var avgConfidence = Commands.Where(c => c.IsWinUI3Element && c.ElementConfidence > 0)
                                          .Average(c => c.ElementConfidence);
                if (avgConfidence > 0)
                    lines.Add($"# WinUI3 Avg Confidence: {avgConfidence:F2}");
            }

            lines.Add($"# Format: StepNumber,ElementName,Type,RepeatCount,Value,ElementId,ElementClass,ElementControlType,ElementX,ElementY,OriginalX,OriginalY,ElementText,ElementHelpText,ElementAccessKey,IsWinUI3Element,ElementConfidence,LastFoundMethod,KeyCode,MouseButton,TargetWindow,TargetProcess");
            lines.Add("");

            // Add commands
            foreach (var command in Commands.OrderBy(c => c.StepNumber))
            {
                lines.Add(command.ToFileFormat());
            }

            System.IO.File.WriteAllLines(filePath, lines);
        }

        /// <summary>
        /// Loads command sequence from file
        /// </summary>
        public static CommandSequence LoadFromFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                return null;

            var sequence = new CommandSequence();
            sequence.Name = System.IO.Path.GetFileNameWithoutExtension(filePath);

            var lines = System.IO.File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                // Skip comments and empty lines
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                {
                    // Try to extract metadata from comments
                    if (line.StartsWith("# Name:"))
                        sequence.Name = line.Substring(7).Trim();
                    else if (line.StartsWith("# Description:"))
                        sequence.Description = line.Substring(14).Trim();
                    else if (line.StartsWith("# Target Application:"))
                        sequence.TargetApplication = line.Substring(21).Trim();
                    else if (line.StartsWith("# Target Process:"))
                        sequence.TargetProcessName = line.Substring(17).Trim();
                    else if (line.StartsWith("# Target Window Title:"))
                        sequence.TargetWindowTitle = line.Substring(22).Trim();
                    else if (line.StartsWith("# Target Window Class:"))
                        sequence.TargetWindowClass = line.Substring(22).Trim();
                    else if (line.StartsWith("# Auto Find Target:"))
                    {
                        if (bool.TryParse(line.Substring(19).Trim(), out bool autoFind))
                            sequence.AutoFindTarget = autoFind;
                    }
                    else if (line.StartsWith("# Max Wait Time:"))
                    {
                        if (int.TryParse(line.Substring(16).Trim(), out int maxWait))
                            sequence.MaxWaitTimeSeconds = maxWait;
                    }

                    continue;
                }

                var command = Command.FromFileFormat(line);
                if (command != null)
                {
                    sequence.Commands.Add(command);
                }
            }

            return sequence;
        }

        /// <summary>
        /// Gets next step number for new command
        /// </summary>
        public int GetNextStepNumber()
        {
            if (Commands.Count == 0)
                return 1;

            int maxStep = 0;
            foreach (var cmd in Commands)
            {
                if (cmd.StepNumber > maxStep)
                    maxStep = cmd.StepNumber;
            }

            return maxStep + 1;
        }

        /// <summary>
        /// Validates command sequence for common issues
        /// </summary>
        public List<string> Validate()
        {
            var issues = new List<string>();

            // Check for duplicate step numbers
            var stepNumbers = new HashSet<int>();
            foreach (var cmd in Commands)
            {
                if (stepNumbers.Contains(cmd.StepNumber))
                    issues.Add($"Duplicate step number: {cmd.StepNumber}");
                else
                    stepNumbers.Add(cmd.StepNumber);
            }

            // Check for loop consistency
            int loopStartCount = 0;
            int loopEndCount = 0;
            foreach (var cmd in Commands)
            {
                if (cmd.IsLoopStart)
                    loopStartCount++;
                if (cmd.Type == CommandType.LoopEnd)
                    loopEndCount++;
            }

            if (loopStartCount != loopEndCount)
                issues.Add($"Unmatched loops: {loopStartCount} starts, {loopEndCount} ends");

            return issues;
        }

        public override string ToString()
        {
            return $"{Name} ({Commands.Count} commands)";
        }
    }

    /// <summary>
    /// Statistics for recorded UI elements usage
    /// </summary>
    public class ElementUsageStats
    {
        public string ElementName { get; set; } = string.Empty;
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
}
