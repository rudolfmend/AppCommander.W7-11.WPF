using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using System.Text.RegularExpressions;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Špecializovaný detektor pre tabuľkové bunky a grid elementy
    /// </summary>
    public static class TableCellDetector
    {
        /// <summary>
        /// Detekuje tabuľkovú bunku na danej pozícii a vytvorí inteligentný identifikátor
        /// </summary>
        public static TableCellInfo DetectTableCell(int x, int y)
        {
            try
            {
                // Získaj element na pozícii
                AutomationElement element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
                if (element == null) return null;

                // Pokus sa nájsť tabuľkovú štruktúru
                var tableInfo = FindTableStructure(element);
                if (tableInfo != null)
                {
                    // Vytvor identifikátor bunky
                    return CreateCellIdentifier(tableInfo, element, x, y);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting table cell: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nájde tabuľkovú štruktúru (table, grid, list) okolo elementu
        /// </summary>
        private static TableStructureInfo FindTableStructure(AutomationElement element)
        {
            AutomationElement current = element;

            // Prechádzaj hierarchiou nahor a hľadaj tabuľkovú štruktúru
            for (int depth = 0; depth < 10 && current != null; depth++)
            {
                try
                {
                    var controlType = current.Current.ControlType;
                    var className = GetProperty(current, AutomationElement.ClassNameProperty);

                    // Kontrola pre rôzne typy tabuliek
                    if (IsTableElement(controlType, className))
                    {
                        var tableInfo = AnalyzeTableStructure(current);
                        if (tableInfo.IsValid)
                        {
                            return tableInfo;
                        }
                    }

                    // Prejdi na parent element
                    current = TreeWalker.ControlViewWalker.GetParent(current);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error traversing element hierarchy: {ex.Message}");
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Kontroluje či je element tabuľkový
        /// </summary>
        private static bool IsTableElement(ControlType controlType, string className)
        {
            // Štandardné automation typy tabuliek
            var tableTypes = new[]
            {
                ControlType.Table,
                ControlType.DataGrid,
                ControlType.List
            };

            if (tableTypes.Contains(controlType))
                return true;

            // Kontrola class names pre rôzne typy tabuliek
            var tableClasses = new[]
            {
                "DataGrid", "ListView", "TableView", "GridView",
                "SysListView32", "Grid", "Table"
            };

            return tableClasses.Any(tc =>
                className.IndexOf(tc, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Analyzuje štruktúru tabuľky a identifikuje stĺpce/riadky
        /// </summary>
        private static TableStructureInfo AnalyzeTableStructure(AutomationElement tableElement)
        {
            var info = new TableStructureInfo
            {
                TableElement = tableElement,
                TableName = GetMeaningfulTableName(tableElement),
                TableClass = GetProperty(tableElement, AutomationElement.ClassNameProperty)
            };

            try
            {
                // Pokus sa použiť TablePattern
                if (tableElement.TryGetCurrentPattern(TablePattern.Pattern, out object tablePattern))
                {
                    var table = (TablePattern)tablePattern;
                    info.RowCount = table.Current.RowCount;
                    info.ColumnCount = table.Current.ColumnCount;
                    info.Headers = GetTableHeaders(table);
                    info.UseTablePattern = true;
                }
                else
                {
                    // Fallback - analyzuj štruktúru manuálne
                    AnalyzeTableManually(tableElement, info);
                }

                System.Diagnostics.Debug.WriteLine($"Table analysis: {info.TableName} - {info.RowCount}x{info.ColumnCount} cells");
                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing table structure: {ex.Message}");
                return info;
            }
        }

        /// <summary>
        /// Manuálna analýza tabuľky pre prípady kde TablePattern nefunguje
        /// </summary>
        private static void AnalyzeTableManually(AutomationElement tableElement, TableStructureInfo info)
        {
            try
            {
                // Nájdi všetky riadky
                var rowConditions = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)
                );

                var rows = tableElement.FindAll(TreeScope.Children, rowConditions);
                info.RowCount = rows.Count;

                // Analyzuj prvý riadok pre zistenie počtu stĺpcov
                if (rows.Count > 0)
                {
                    var firstRow = rows[0] as AutomationElement;
                    var cellConditions = new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom)
                    );

                    var cells = firstRow.FindAll(TreeScope.Children, cellConditions);
                    info.ColumnCount = cells.Count;

                    // Pokus sa identifikovať headers
                    info.Headers = TryIdentifyHeaders(tableElement, info.ColumnCount);
                }

                // Skús nájsť column headers
                var headerCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Header);
                var headerElements = tableElement.FindAll(TreeScope.Descendants, headerCondition);

                if (headerElements.Count > 0)
                {
                    var headerTexts = new List<string>();
                    foreach (AutomationElement header in headerElements)
                    {
                        string headerText = GetProperty(header, AutomationElement.NameProperty);
                        if (!string.IsNullOrEmpty(headerText))
                            headerTexts.Add(headerText);
                    }

                    if (headerTexts.Count > 0)
                    {
                        info.Headers = headerTexts;
                        info.ColumnCount = Math.Max(info.ColumnCount, headerTexts.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Manual table analysis error: {ex.Message}");
            }
        }

        /// <summary>
        /// Získa headers z TablePattern
        /// </summary>
        private static List<string> GetTableHeaders(TablePattern tablePattern)
        {
            var headers = new List<string>();

            try
            {
                var columnHeaders = tablePattern.Current.GetColumnHeaders();
                foreach (AutomationElement header in columnHeaders)
                {
                    string headerText = GetProperty(header, AutomationElement.NameProperty);
                    headers.Add(string.IsNullOrEmpty(headerText) ? $"Column{headers.Count + 1}" : headerText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting table headers: {ex.Message}");
            }

            return headers;
        }

        /// <summary>
        /// Pokus sa identifikovať headers pri manuálnej analýze
        /// </summary>
        private static List<string> TryIdentifyHeaders(AutomationElement tableElement, int columnCount)
        {
            var headers = new List<string>();

            try
            {
                // Hľadaj header row ako prvý riadok
                var possibleHeaderRow = tableElement.FindFirst(TreeScope.Children, Condition.TrueCondition);
                if (possibleHeaderRow != null)
                {
                    var headerCells = possibleHeaderRow.FindAll(TreeScope.Children, Condition.TrueCondition);

                    foreach (AutomationElement cell in headerCells)
                    {
                        string cellText = GetProperty(cell, AutomationElement.NameProperty);
                        if (string.IsNullOrEmpty(cellText))
                        {
                            cellText = GetElementText(cell);
                        }

                        headers.Add(string.IsNullOrEmpty(cellText) ? $"Column{headers.Count + 1}" : cellText);

                        if (headers.Count >= columnCount)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error identifying headers: {ex.Message}");
            }

            // Fallback - vytvor generické názvy stĺpcov
            while (headers.Count < columnCount)
            {
                headers.Add($"Column{headers.Count + 1}");
            }

            return headers;
        }

        /// <summary>
        /// Vytvorí identifikátor bunky na základe jej pozície v tabuľke
        /// </summary>
        private static TableCellInfo CreateCellIdentifier(TableStructureInfo tableInfo, AutomationElement cellElement, int x, int y)
        {
            try
            {
                var cellInfo = new TableCellInfo
                {
                    TableInfo = tableInfo,
                    CellElement = cellElement,
                    ClickX = x,
                    ClickY = y
                };

                // Pokus sa určiť pozíciu bunky v tabuľke
                var position = DetermineCellPosition(tableInfo, cellElement, x, y);
                cellInfo.Row = position.Row;
                cellInfo.Column = position.Column;

                // Vytvor inteligentný identifikátor
                cellInfo.CellIdentifier = CreateCellIdentifier(tableInfo, position.Row, position.Column);

                // Získaj obsah bunky
                cellInfo.CellContent = GetCellContent(cellElement);

                // Vytvor display name
                cellInfo.DisplayName = CreateCellDisplayName(tableInfo, position.Row, position.Column, cellInfo.CellContent);

                System.Diagnostics.Debug.WriteLine($"Table cell identified: {cellInfo.DisplayName} at position ({position.Row}, {position.Column})");

                return cellInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating cell identifier: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Určí pozíciu bunky v tabuľke (riadok, stĺpec)
        /// </summary>
        private static (int Row, int Column) DetermineCellPosition(TableStructureInfo tableInfo, AutomationElement cellElement, int x, int y)
        {
            try
            {
                // Metóda 1: Použitie GridItemPattern
                if (cellElement.TryGetCurrentPattern(GridItemPattern.Pattern, out object gridPattern))
                {
                    var grid = (GridItemPattern)gridPattern;
                    return (grid.Current.Row, grid.Current.Column);
                }

                // Metóda 2: Použitie TableItemPattern
                if (cellElement.TryGetCurrentPattern(TableItemPattern.Pattern, out object tablePattern))
                {
                    var table = (TableItemPattern)tablePattern;
                    return (table.Current.Row, table.Current.Column);
                }

                // Metóda 3: Manuálne určenie pozície na základe súradníc
                return DetermineCellPositionByCoordinates(tableInfo, x, y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error determining cell position: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// Určí pozíciu bunky na základe súradníc (fallback metóda)
        /// </summary>
        private static (int Row, int Column) DetermineCellPositionByCoordinates(TableStructureInfo tableInfo, int x, int y)
        {
            try
            {
                var tableRect = tableInfo.TableElement.Current.BoundingRectangle;

                // Získaj všetky bunky a ich pozície
                var cells = GetAllTableCells(tableInfo.TableElement);

                // Nájdi bunku ktorá obsahuje dané súradnice
                for (int row = 0; row < cells.GetLength(0); row++)
                {
                    for (int col = 0; col < cells.GetLength(1); col++)
                    {
                        var cell = cells[row, col];
                        if (cell != null)
                        {
                            var cellRect = cell.Current.BoundingRectangle;
                            if (x >= cellRect.X && x <= cellRect.X + cellRect.Width &&
                                y >= cellRect.Y && y <= cellRect.Y + cellRect.Height)
                            {
                                return (row, col);
                            }
                        }
                    }
                }

                // Fallback - odhad na základe relatívnej pozície
                double relativeX = (x - tableRect.X) / tableRect.Width;
                double relativeY = (y - tableRect.Y) / tableRect.Height;

                int estimatedColumn = (int)(relativeX * tableInfo.ColumnCount);
                int estimatedRow = (int)(relativeY * tableInfo.RowCount);

                return (Math.Max(0, estimatedRow), Math.Max(0, estimatedColumn));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error determining position by coordinates: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// Získa všetky bunky tabuľky
        /// </summary>
        private static AutomationElement[,] GetAllTableCells(AutomationElement tableElement)
        {
            var cells = new List<List<AutomationElement>>();

            try
            {
                // Nájdi všetky riadky
                var rowConditions = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)
                );

                var rows = tableElement.FindAll(TreeScope.Children, rowConditions);

                foreach (AutomationElement row in rows)
                {
                    var rowCells = new List<AutomationElement>();

                    // Nájdi bunky v riadku
                    var cellConditions = new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom)
                    );

                    var rowCellElements = row.FindAll(TreeScope.Children, cellConditions);

                    foreach (AutomationElement cell in rowCellElements)
                    {
                        rowCells.Add(cell);
                    }

                    cells.Add(rowCells);
                }

                // Konvertuj na 2D pole
                if (cells.Count > 0)
                {
                    int maxCols = cells.Max(r => r.Count);
                    var result = new AutomationElement[cells.Count, maxCols];

                    for (int row = 0; row < cells.Count; row++)
                    {
                        for (int col = 0; col < cells[row].Count; col++)
                        {
                            result[row, col] = cells[row][col];
                        }
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting table cells: {ex.Message}");
            }

            return new AutomationElement[0, 0];
        }

        /// <summary>
        /// Vytvorí identifikátor bunky
        /// </summary>
        private static string CreateCellIdentifier(TableStructureInfo tableInfo, int row, int column)
        {
            var parts = new List<string>();

            // Pridaj názov tabuľky ak je dostupný
            if (!string.IsNullOrEmpty(tableInfo.TableName))
            {
                parts.Add($"Table:{tableInfo.TableName}");
            }

            // Pridaj identifikátor stĺpca
            if (column < tableInfo.Headers.Count && !string.IsNullOrEmpty(tableInfo.Headers[column]))
            {
                parts.Add($"Col:{CleanIdentifier(tableInfo.Headers[column])}");
            }
            else
            {
                parts.Add($"Col:{column}");
            }

            // Pridaj identifikátor riadka
            parts.Add($"Row:{row}");

            return string.Join("_", parts);
        }

        /// <summary>
        /// Vytvorí display name pre bunku
        /// </summary>
        private static string CreateCellDisplayName(TableStructureInfo tableInfo, int row, int column, string content)
        {
            string columnName = column < tableInfo.Headers.Count ? tableInfo.Headers[column] : $"Col{column}";

            if (!string.IsNullOrEmpty(content) && content.Length <= 20)
            {
                return $"{columnName}_R{row}_{CleanIdentifier(content)}";
            }

            return $"{columnName}_R{row}";
        }

        /// <summary>
        /// Nájde bunku na základe identifikátora
        /// </summary>
        public static AutomationElement FindCellByIdentifier(IntPtr windowHandle, string cellIdentifier)
        {
            try
            {
                var window = AutomationElement.FromHandle(windowHandle);
                if (window == null) return null;

                // Parsuj identifikátor
                var parsed = ParseCellIdentifier(cellIdentifier);
                if (parsed == null) return null;

                // Nájdi tabuľku
                var tableElement = FindTableByName(window, parsed.TableName);
                if (tableElement == null) return null;

                // Nájdi bunku
                return FindCellInTable(tableElement, parsed.Row, parsed.Column, parsed.ColumnName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding cell by identifier: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parsuje identifikátor bunky
        /// </summary>
        private static CellIdentifierParsed ParseCellIdentifier(string identifier)
        {
            try
            {
                var parts = identifier.Split('_');
                var parsed = new CellIdentifierParsed();

                foreach (var part in parts)
                {
                    if (part.StartsWith("Table:"))
                        parsed.TableName = part.Substring(6);
                    else if (part.StartsWith("Col:"))
                    {
                        var colPart = part.Substring(4);
                        if (int.TryParse(colPart, out int colIndex))
                            parsed.Column = colIndex;
                        else
                            parsed.ColumnName = colPart;
                    }
                    else if (part.StartsWith("Row:"))
                    {
                        var rowPart = part.Substring(4);
                        if (int.TryParse(rowPart, out int rowIndex))
                            parsed.Row = rowIndex;
                    }
                }

                return parsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing cell identifier: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nájde tabuľku podľa názvu
        /// </summary>
        private static AutomationElement FindTableByName(AutomationElement window, string tableName)
        {
            try
            {
                if (string.IsNullOrEmpty(tableName))
                {
                    // Nájdi prvú tabuľku
                    var tableConditions = new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Table),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List)
                    );

                    return window.FindFirst(TreeScope.Descendants, tableConditions);
                }

                // Nájdi tabuľku podľa názvu
                var nameCondition = new PropertyCondition(AutomationElement.NameProperty, tableName);
                return window.FindFirst(TreeScope.Descendants, nameCondition);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding table: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nájde bunku v tabuľke
        /// </summary>
        private static AutomationElement FindCellInTable(AutomationElement tableElement, int row, int column, string columnName)
        {
            try
            {
                // Metóda 1: GridPattern
                if (tableElement.TryGetCurrentPattern(GridPattern.Pattern, out object gridPattern))
                {
                    var grid = (GridPattern)gridPattern;
                    return grid.GetItem(row, column);
                }

                // Metóda 2: Manuálne hľadanie
                var rows = tableElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem));

                if (row < rows.Count)
                {
                    var targetRow = rows[row] as AutomationElement;
                    var cells = targetRow.FindAll(TreeScope.Children, Condition.TrueCondition);

                    if (column < cells.Count)
                    {
                        return cells[column] as AutomationElement;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding cell in table: {ex.Message}");
                return null;
            }
        }

        // Helper methods
        private static string GetMeaningfulTableName(AutomationElement tableElement)
        {
            string name = GetProperty(tableElement, AutomationElement.NameProperty);
            if (!string.IsNullOrEmpty(name))
                return CleanIdentifier(name);

            string automationId = GetProperty(tableElement, AutomationElement.AutomationIdProperty);
            if (!string.IsNullOrEmpty(automationId))
                return CleanIdentifier(automationId);

            return "Table";
        }

        private static string CleanIdentifier(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return Regex.Replace(input, @"[^\w\d]", "_").Trim('_');
        }

        private static string GetElementText(AutomationElement element)
        {
            try
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                {
                    var value = ((ValuePattern)valuePattern).Current.Value;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }

                string name = GetProperty(element, AutomationElement.NameProperty);
                return !string.IsNullOrEmpty(name) ? name : "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Verejná verzia AnalyzeTableStructure pre volanie z UIElementDetector
        /// </summary>
        public static TableStructureInfo AnalyzeTableStructurePublic(AutomationElement tableElement)
        {
            return AnalyzeTableStructure(tableElement);
        }

        /// <summary>
        /// Získa obsah bunky - PRESUNUTÉ z AdaptiveElementFinder
        /// </summary>
        public static string GetCellContent(AutomationElement cellElement)
        {
            try
            {
                // Pokus sa získať hodnotu cez ValuePattern
                if (cellElement.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                {
                    var value = ((ValuePattern)valuePattern).Current.Value;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }

                // Pokus sa získať text cez TextPattern
                if (cellElement.TryGetCurrentPattern(TextPattern.Pattern, out object textPattern))
                {
                    var text = ((TextPattern)textPattern).DocumentRange.GetText(100);
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }

                // Fallback na Name property
                string name = GetProperty(cellElement, AutomationElement.NameProperty);
                if (!string.IsNullOrEmpty(name))
                    return name;

                return "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting cell content: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Helper metóda pre získanie vlastnosti elementu
        /// </summary>
        private static string GetProperty(AutomationElement element, AutomationProperty property)
        {
            try
            {
                object value = element.GetCurrentPropertyValue(property);
                return value?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }

    // Supporting classes
    public class TableStructureInfo
    {
        public AutomationElement TableElement { get; set; }
        public string TableName { get; set; } = "";
        public string TableClass { get; set; } = "";
        public int RowCount { get; set; } = 0;
        public int ColumnCount { get; set; } = 0;
        public List<string> Headers { get; set; } = new List<string>();
        public bool UseTablePattern { get; set; } = false;

        public bool IsValid => TableElement != null && RowCount > 0 && ColumnCount > 0;
    }

    public class TableCellInfo
    {
        public TableStructureInfo TableInfo { get; set; }
        public AutomationElement CellElement { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public string CellIdentifier { get; set; } = "";
        public string CellContent { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int ClickX { get; set; }
        public int ClickY { get; set; }
    }

    public class CellIdentifierParsed
    {
        public string TableName { get; set; } = "";
        public int Row { get; set; } = 0;
        public int Column { get; set; } = 0;
        public string ColumnName { get; set; } = "";
    }
}
