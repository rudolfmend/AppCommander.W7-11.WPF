using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Collections.Generic;
using AppCommander.W7_11.WPF.Core;
using System.Diagnostics;
using System.Drawing;

namespace AppCommander.W7_11.WPF.Core
{
    public static partial class UIElementDetector
    {
        public enum UiFramework
        {
            Unknown,
            Win32,
            WinForms,
            Wpf,
            WinUI3,
            WebView2,
            XYFallback
        }

        public static UiFramework DetectFramework(IntPtr hwnd)
        {
            var className = GetClassName(hwnd);

            if (className == "#32770") return UiFramework.Win32; // MessageBox/Dialog
            if (className.StartsWith("WindowsForms")) return UiFramework.WinForms;
            if (className.Contains("HwndWrapper")) return UiFramework.Wpf;
            if (className.Contains("Chrome") || className.Contains("WebView2")) return UiFramework.WebView2;
            if (className.Contains("WinUIDesktopWin32WindowClass")) return UiFramework.WinUI3;

            return UiFramework.Unknown;
        }

        private static UIElementInfo GetElementSkippingAboutBlank(IntPtr hwnd, int x, int y)
        {
            var root = AutomationElement.FromHandle(hwnd);

            // Skip about:blank root
            if (GetProperty(root, AutomationElement.NameProperty) == "about:blank")
            {
                var walker = TreeWalker.RawViewWalker;
                var child = walker.GetFirstChild(root);

                while (child != null)
                {
                    if (GetProperty(child, AutomationElement.NameProperty) != "about:blank")
                    {
                        // OPRAVENÉ: AutomationElement.FromPoint namiesto child.FromPoint
                        var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
                        if (element != null && element != root)
                            return ExtractElementInfo(element, x, y);
                    }
                    child = walker.GetNextSibling(child);
                }
            }

            return GetElementAtPoint(x, y);
        }

        private static UIElementInfo GetWin32Element(IntPtr hwnd, int x, int y)
        {
            try
            {
                var className = GetClassName(hwnd);

                // Win32 MessageBox alebo Dialog
                if (className == "#32770")
                {
                    // Hľadaj buttony v dialógu
                    var children = new List<IntPtr>();
                    EnumChildWindows(hwnd, (childHwnd, lParam) =>
                    {
                        var childClass = GetClassName(childHwnd);
                        if (childClass == "Button")
                        {
                            RECT rect;
                            GetWindowRect(childHwnd, out rect);

                            if (x >= rect.Left && x <= rect.Right &&
                                y >= rect.Top && y <= rect.Bottom)
                            {
                                children.Add(childHwnd);
                            }
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (children.Count > 0)
                    {
                        var buttonHwnd = children[0];
                        var buttonText = GetWindowText(buttonHwnd);
                        RECT btnRect;
                        GetWindowRect(buttonHwnd, out btnRect);

                        return new UIElementInfo
                        {
                            Name = $"Win32Button_{buttonText}",
                            ClassName = "Button",
                            ControlType = "Button",
                            WindowHandle = buttonHwnd,
                            X = x,
                            Y = y,
                            BoundingRectangle = new System.Windows.Rect(
                                btnRect.Left, btnRect.Top,
                                btnRect.Right - btnRect.Left,
                                btnRect.Bottom - btnRect.Top),
                            IsEnabled = IsWindowEnabled(buttonHwnd),
                            IsVisible = IsWindowVisible(buttonHwnd)
                        };
                    }
                }

                // Fallback na štandardnú detekciu
                return GetElementAtPoint(x, y);
            }
            catch
            {
                return GetElementAtPoint(x, y);
            }
        }

        /// <summary>
        /// Získa element na danej pozícii
        /// </summary>
        public static UIElementInfo GetElementAtPoint(int x, int y)
        {
            try
            {
                // Use UI Automation to find element at point
                AutomationElement element = AutomationElement.FromPoint(new System.Windows.Point(x, y));

                if (element != null)
                {
                    var elementInfo = ExtractElementInfo(element, x, y);
                    if (elementInfo != null)
                    {
                        LogElementDetails(elementInfo);
                        return elementInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting UI element: {ex.Message}");
            }

            // Fallback - get basic window info
            return GetBasicWindowInfo(x, y);
        }

        /// <summary>
        /// HLAVNÁ METÓDA - rozšírená verzia GetElementAtPoint s podporou tabuliek
        /// </summary>
        public static UIElementInfo GetElementAtPointEnhanced(int x, int y)
        {
            try
            {
                // Najprv skús detekovať tabuľkovú bunku
                var tableCellInfo = TableCellDetector.DetectTableCell(x, y);
                if (tableCellInfo != null)
                {
                    Debug.WriteLine($"=== TABLE CELL DETECTED ===");
                    Debug.WriteLine($"Cell: {tableCellInfo.DisplayName}");
                    Debug.WriteLine($"Position: Row {tableCellInfo.Row}, Column {tableCellInfo.Column}");
                    Debug.WriteLine($"Table: {tableCellInfo.TableInfo.TableName} ({tableCellInfo.TableInfo.RowCount}x{tableCellInfo.TableInfo.ColumnCount})");
                    Debug.WriteLine($"Identifier: {tableCellInfo.CellIdentifier}");

                    // Vytvor UIElementInfo pre tabuľkovú bunku
                    return CreateTableCellUIElementInfo(tableCellInfo, x, y);
                }

                // Fallback na štandardnú detekciu
                return GetElementAtPoint(x, y);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in enhanced element detection: {ex.Message}");
                return GetElementAtPoint(x, y);
            }
        }

        /// <summary>
        /// Vytvorí UIElementInfo pre tabuľkovú bunku
        /// </summary>
        private static UIElementInfo CreateTableCellUIElementInfo(TableCellInfo tableCellInfo, int x, int y)
        {
            try
            {
                var cellElement = tableCellInfo.CellElement;
                var tableInfo = tableCellInfo.TableInfo;

                var uiInfo = new UIElementInfo
                {
                    // Základné informácie
                    Name = tableCellInfo.DisplayName,
                    AutomationId = GetProperty(cellElement, AutomationElement.AutomationIdProperty),
                    ClassName = GetProperty(cellElement, AutomationElement.ClassNameProperty),
                    ControlType = "TableCell",
                    X = x,
                    Y = y,
                    BoundingRectangle = cellElement.Current.BoundingRectangle,
                    IsEnabled = cellElement.Current.IsEnabled,
                    IsVisible = !cellElement.Current.IsOffscreen,
                    AutomationElement = cellElement,

                    // Tabuľkové špecifické informácie
                    ElementText = tableCellInfo.CellContent,
                    TableCellIdentifier = tableCellInfo.CellIdentifier,
                    TableRow = tableCellInfo.Row,
                    TableColumn = tableCellInfo.Column,
                    TableName = tableInfo.TableName,
                    IsTableCell = true,

                    // Dodatočné informácie pre lepšiu identifikáciu
                    HelpText = $"Table: {tableInfo.TableName}, Row: {tableCellInfo.Row}, Column: {tableCellInfo.Column}",
                    PlaceholderText = tableCellInfo.Column < tableInfo.Headers.Count ? tableInfo.Headers[tableCellInfo.Column] : $"Col{tableCellInfo.Column}"
                };

                return uiInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating table cell UI info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nájde tabuľkovú bunku na základe identifikátora
        /// </summary>
        public static UIElementInfo FindTableCellByIdentifier(IntPtr windowHandle, string cellIdentifier)
        {
            try
            {
                var cellElement = TableCellDetector.FindCellByIdentifier(windowHandle, cellIdentifier);
                if (cellElement != null)
                {
                    var rect = cellElement.Current.BoundingRectangle;
                    int centerX = (int)(rect.X + rect.Width / 2);
                    int centerY = (int)(rect.Y + rect.Height / 2);

                    // Použij enhanced detection na tej pozícii
                    return GetElementAtPointEnhanced(centerX, centerY);
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding table cell by identifier: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Skontroluje či element je tabuľková bunka
        /// </summary>
        public static bool IsTableCellElement(AutomationElement element)
        {
            try
            {
                if (element == null) return false;

                // Kontrola patterns pre table cells
                var patterns = element.GetSupportedPatterns();
                if (patterns.Contains(GridItemPattern.Pattern) || patterns.Contains(TableItemPattern.Pattern))
                    return true;

                // Kontrola parent elementov
                var parent = TreeWalker.ControlViewWalker.GetParent(element);
                while (parent != null)
                {
                    var controlType = parent.Current.ControlType;
                    if (controlType == ControlType.Table || controlType == ControlType.DataGrid || controlType == ControlType.List)
                        return true;

                    parent = TreeWalker.ControlViewWalker.GetParent(parent);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Získa všetky tabuľky v okne
        /// </summary>
        public static List<TableStructureInfo> GetAllTablesInWindow(IntPtr windowHandle)
        {
            var tables = new List<TableStructureInfo>();

            try
            {
                var window = AutomationElement.FromHandle(windowHandle);
                if (window == null) return tables;

                var tableConditions = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Table),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List)
                );

                var tableElements = window.FindAll(TreeScope.Descendants, tableConditions);

                foreach (AutomationElement tableElement in tableElements)
                {
                    try
                    {
                        // priame volanie public metódy
                        var tableInfo = TableCellDetector.AnalyzeTableStructurePublic(tableElement);
                        if (tableInfo != null && tableInfo.IsValid)
                        {
                            tables.Add(tableInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error analyzing table: {ex.Message}");
                    }
                }

                Debug.WriteLine($"Found {tables.Count} tables in window");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting tables in window: {ex.Message}");
            }

            return tables;
        }

        /// <summary>
        /// Extrahuje informácie z elementu
        /// GARANTUJE: Nikdy nevráti null, vždy vráti aspoň základné informácie
        /// Používa properties z UIElementInfo
        /// </summary>
        public static UIElementInfo ExtractElementInfo(AutomationElement element, int x, int y)
        {
            // Inicializuj základný objekt s pozíciou
            var elementInfo = new UIElementInfo
            {
                X = x,
                Y = y,
                Name = "",
                AutomationId = "",
                ClassName = "",
                ControlType = "",
                ProcessId = 0,
                WindowHandle = IntPtr.Zero,
                IsEnabled = false,
                IsVisible = false,
                ElementText = "",
                PlaceholderText = "",
                HelpText = "",
                AccessKey = "",
                BoundingRectangle = System.Windows.Rect.Empty
            };

            try
            {
                if (element == null)
                {
                    elementInfo.Name = "Element not available";
                    return elementInfo;
                }

                // === ZÍSKAVANIE VLASTNOSTÍ S VYLEPŠENÝM BEZPEČNÝM PRÍSTUPOM ===

                // String vlastnosti - používame GetElementProperty (už existuje)
                try
                {
                    elementInfo.Name = GetElementProperty(element, AutomationElement.NameProperty);
                }
                catch { }

                try
                {
                    elementInfo.AutomationId = GetElementProperty(element, AutomationElement.AutomationIdProperty);
                }
                catch { }

                try
                {
                    elementInfo.ClassName = GetElementProperty(element, AutomationElement.ClassNameProperty);
                }
                catch { }

                // ControlType - používame bezpečnú metódu
                try
                {
                    elementInfo.ControlType = GetControlTypeSafe(element);
                }
                catch { }

                // ProcessId - s fallback na string parsing
                try
                {
                    elementInfo.ProcessId = element.Current.ProcessId;
                }
                catch
                {
                    try
                    {
                        // Fallback parsing zo string
                        string processIdStr = GetElementProperty(element, AutomationElement.ProcessIdProperty);
                        if (int.TryParse(processIdStr, out int pid))
                            elementInfo.ProcessId = pid;
                    }
                    catch { }
                }

                // WindowHandle - s fallback na string parsing
                try
                {
                    int handle = element.Current.NativeWindowHandle;
                    elementInfo.WindowHandle = new IntPtr(handle);
                }
                catch
                {
                    try
                    {
                        // Fallback parsing zo string
                        string handleStr = GetElementProperty(element, AutomationElement.NativeWindowHandleProperty);
                        if (int.TryParse(handleStr, out int handle))
                            elementInfo.WindowHandle = new IntPtr(handle);
                    }
                    catch { }
                }

                // IsEnabled - používame bezpečnú metódu s fallback na string parsing
                try
                {
                    elementInfo.IsEnabled = GetIsEnabledSafe(element);
                }
                catch
                {
                    try
                    {
                        // Fallback parsing zo string
                        string enabledStr = GetElementProperty(element, AutomationElement.IsEnabledProperty);
                        if (bool.TryParse(enabledStr, out bool isEnabled))
                            elementInfo.IsEnabled = isEnabled;
                    }
                    catch { }
                }

                // BoundingRectangle - používame bezpečnú metódu A aktualizujeme X,Y
                try
                {
                    var rect = GetBoundingRectangleSafe(element);
                    if (!rect.IsEmpty)
                    {
                        elementInfo.X = (int)(rect.X + rect.Width / 2);  // Stred elementu
                        elementInfo.Y = (int)(rect.Y + rect.Height / 2); // Stred elementu
                        elementInfo.BoundingRectangle = rect;
                    }
                }
                catch { }

                // ElementText
                try
                {
                    elementInfo.ElementText = GetElementText(element);
                }
                catch { }

                // PlaceholderText
                try
                {
                    elementInfo.PlaceholderText = GetPlaceholderText(element);
                }
                catch { }

                // HelpText
                try
                {
                    elementInfo.HelpText = GetElementProperty(element, AutomationElement.HelpTextProperty);
                }
                catch { }

                // AccessKey
                try
                {
                    elementInfo.AccessKey = GetElementProperty(element, AutomationElement.AccessKeyProperty);
                }
                catch { }

                // IsVisible - používame bezpečnú metódu
                try
                {
                    elementInfo.IsVisible = GetIsVisibleSafe(element);
                }
                catch { }

                // AutomationElement
                try
                {
                    elementInfo.AutomationElement = element;
                }
                catch { }

                // TreePath - hierarchická cesta v UI strome
                try
                {
                    elementInfo.TreePath = ElementIdentifier.GenerateTreePath(element);
                    System.Diagnostics.Debug.WriteLine($"[UIDetector] TreePath generated: {elementInfo.TreePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UIDetector] TreePath generation failed: {ex.Message}");
                }

                // === FALLBACK: Win32 API ak nemáme žiadne info ===
                if (string.IsNullOrEmpty(elementInfo.Name) &&
                    string.IsNullOrEmpty(elementInfo.AutomationId) &&
                    string.IsNullOrEmpty(elementInfo.ClassName))
                {
                    try
                    {
                        if (elementInfo.WindowHandle != IntPtr.Zero)
                        {
                            int length = GetWindowTextLength(elementInfo.WindowHandle);
                            if (length > 0)
                            {
                                var text = new System.Text.StringBuilder(length + 1);
                                if (GetWindowText(elementInfo.WindowHandle, text, text.Capacity) > 0)
                                {
                                    elementInfo.Name = text.ToString();
                                }
                            }
                        }
                    }
                    catch { }
                }

                System.Diagnostics.Debug.WriteLine($"[UIDetector] Element extracted: {elementInfo.ControlType} - {elementInfo.Name}");
                return elementInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIDetector] Critical error in ExtractElementInfo: {ex.GetType().Name} - {ex.Message}");
                elementInfo.Name = $"Error detecting element at ({x}, {y})";
                return elementInfo;
            }
        }

        /// <summary>
        /// Bezpečne získa text z elementu (pre TextBox, Edit, atď.)
        /// </summary>
        internal static string GetElementText(AutomationElement element)
        {
            try
            {
                // ValuePattern - pre TextBox, ComboBox, atď.
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePatternObj))
                {
                    var value = valuePatternObj as ValuePattern;
                    if (value != null && !string.IsNullOrWhiteSpace(value.Current.Value))
                        return value.Current.Value;
                }

                // TextPattern - pre RichTextBox a podobné
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out object textPatternObj))
                {
                    var text = textPatternObj as TextPattern;
                    var documentRange = text?.DocumentRange;
                    if (documentRange != null)
                    {
                        string textValue = documentRange.GetText(100);
                        if (!string.IsNullOrWhiteSpace(textValue))
                            return textValue.Trim();
                    }
                }

                // ✅  Fallback cez bezpečnú metódu namiesto element.Current.Name - POZOR TREBA POUŽIŤ LEN GetProperty(element, AutomationElement.NameProperty)
                string name = GetProperty(element, AutomationElement.NameProperty);
                if (!string.IsNullOrWhiteSpace(name) && name.Length < 100)
                    return name;
            }
            catch
            {
                // Nevadí, pokračujeme
            }

            return "";
        }

        /// <summary>
        /// Bezpečne získa placeholder text z elementu
        /// </summary>
        private static string GetPlaceholderText(AutomationElement element)
        {
            try
            {
                // HelpText môže obsahovať placeholder
                var helpText = GetElementProperty(element, AutomationElement.HelpTextProperty);
                if (!string.IsNullOrWhiteSpace(helpText) && helpText.Length < 100)
                    return helpText;

                // ItemStatus môže obsahovať placeholder
                var itemStatus = GetElementProperty(element, AutomationElement.ItemStatusProperty);
                if (!string.IsNullOrWhiteSpace(itemStatus) && itemStatus.Length < 100)
                    return itemStatus;
            }
            catch
            {
                // Nevadí, pokračujeme
            }

            return "";
        }

        /// <summary>
        /// Špeciálne spracovanie pre WinUI3 DesktopChildSiteBridge elementy
        /// </summary>
        private static UIElementInfo ProcessWinUI3Element(AutomationElement bridgeElement, int clickX, int clickY)
        {
            try
            {
                Debug.WriteLine("=== WinUI3 BRIDGE ANALYSIS ===");

                // 1. Skús nájsť child elementy v bridge
                var children = bridgeElement.FindAll(TreeScope.Children, Condition.TrueCondition);
                Debug.WriteLine($"Bridge has {children.Count} children");

                foreach (AutomationElement child in children)
                {
                    var childInfo = AnalyzeWinUI3Child(child, clickX, clickY);
                    if (childInfo != null)
                    {
                        Debug.WriteLine($"Found meaningful child: {childInfo.Name}");
                        return childInfo;
                    }
                }

                // 2. Skús nájsť descendants (hlbšie vnorenné elementy)
                var descendants = bridgeElement.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                Debug.WriteLine($"Bridge has {descendants.Count} descendants");

                AutomationElement bestMatch = null;
                double bestDistance = double.MaxValue;
                UIElementInfo bestInfo = null;

                foreach (AutomationElement descendant in descendants)
                {
                    try
                    {
                        // používame bezpečnú metódu
                        var rect = GetBoundingRectangleSafe(descendant);

                        if (rect.Width > 0 && rect.Height > 0)
                        {
                            // Vypočítaj vzdialenosť od klik pozície
                            double centerX = rect.X + rect.Width / 2;
                            double centerY = rect.Y + rect.Height / 2;
                            double distance = Math.Sqrt(Math.Pow(centerX - clickX, 2) + Math.Pow(centerY - clickY, 2));

                            // Kontroluj, či klik je v oblasti elementu
                            bool isInBounds = clickX >= rect.X && clickX <= rect.X + rect.Width &&
                                            clickY >= rect.Y && clickY <= rect.Y + rect.Height;

                            if (isInBounds || distance < bestDistance)
                            {
                                var childInfo = AnalyzeWinUI3Child(descendant, clickX, clickY);
                                if (childInfo != null && !string.IsNullOrEmpty(childInfo.Name) &&
                                    childInfo.Name != "pane_Unknown")
                                {
                                    if (isInBounds || distance < bestDistance)
                                    {
                                        bestDistance = distance;
                                        bestMatch = descendant;
                                        bestInfo = childInfo;

                                        if (isInBounds)
                                        {
                                            Debug.WriteLine($"Found exact match in bounds: {childInfo.Name}");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error analyzing descendant: {ex.Message}");
                    }
                }



                if (bestInfo != null)
                {
                    Debug.WriteLine($"Best WinUI3 match: {bestInfo.Name} (distance: {bestDistance:F1})");
                    return bestInfo;
                }

                // 3. Ak nič nenašiel, skús pattern-based detection
                return DetectByPatternsWinUI3(bridgeElement, clickX, clickY);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing WinUI3 element: {ex.Message}");
                return null;
            }
        }

        private static UIElementInfo AnalyzeWinUI3Child(AutomationElement element, int clickX, int clickY)
        {
            try
            {
                string name = GetElementProperty(element, AutomationElement.NameProperty);
                string automationId = GetElementProperty(element, AutomationElement.AutomationIdProperty);
                string className = GetElementProperty(element, AutomationElement.ClassNameProperty);

                // používame bezpečnú metódu namiesto element.Current.ControlType
                string controlType = GetControlTypeSafe(element);

                string helpText = GetElementProperty(element, AutomationElement.HelpTextProperty);

                // Získaj text obsah
                string elementText = GetElementText(element);
                string placeholderText = GetPlaceholderText(element);

                Debug.WriteLine($"  Child: Name='{name}', Id='{automationId}', Class='{className}', Type='{controlType}', Text='{elementText}'");

                // Ignoruj generické/prázdne elementy
                if (IsGenericWinUI3Element(name, automationId, className, controlType))
                {
                    return null;
                }

                // Vytvor meaningful name
                string meaningfulName = CreateMeaningfulName(name, automationId, elementText, placeholderText, controlType, helpText);

                if (meaningfulName != $"{controlType}_Unknown")
                {
                    return new UIElementInfo
                    {
                        Name = meaningfulName,
                        AutomationId = automationId,
                        ClassName = className,
                        ControlType = controlType,
                        ElementText = elementText,
                        PlaceholderText = placeholderText,
                        HelpText = helpText,
                        X = clickX,
                        Y = clickY,
                        BoundingRectangle = GetBoundingRectangleSafe(element),
                        IsEnabled = GetIsEnabledSafe(element),
                        IsVisible = GetIsVisibleSafe(element),
                        AutomationElement = element
                    };
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsGenericWinUI3Element(string name, string automationId, string className, string controlType)
        {
            // Ignoruj prázdne alebo generické elementy
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(automationId))
                return true;

            var genericClasses = new[]
            {
                "Microsoft.UI.Content.DesktopChildSiteBridge",
                "ContentPresenter", "Border", "Grid", "StackPanel",
                "Canvas", "ScrollViewer", "UserControl"
            };

            if (genericClasses.Any(g => className.IndexOf(g, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            var genericTypes = new[] { "pane", "group" };
            if (genericTypes.Contains(controlType.ToLower()) &&
                string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(automationId))
                return true;

            return false;
        }

        private static UIElementInfo DetectWin32Element(UiDetectionContext ctx)
        {
            // MessageBox (#32770) alebo Dialog
            if (ctx.ClassName == "#32770")
            {
                var buttons = new List<(IntPtr hwnd, string text, RECT rect)>();

                EnumChildWindows(ctx.Hwnd, (childHwnd, lParam) =>
                {
                    if (GetClassName(childHwnd) == "Button")
                    {
                        var text = GetWindowText(childHwnd);
                        RECT rect;
                        GetWindowRect(childHwnd, out rect);

                        if (ctx.ClickPoint.X >= rect.Left && ctx.ClickPoint.X <= rect.Right &&
                            ctx.ClickPoint.Y >= rect.Top && ctx.ClickPoint.Y <= rect.Bottom)
                        {
                            buttons.Add((childHwnd, text, rect));
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                if (buttons.Count > 0)
                {
                    var btn = buttons[0];
                    return new UIElementInfo
                    {
                        Name = $"Win32Button_{btn.text}",
                        ClassName = "Button",
                        ControlType = "Button",
                        WindowHandle = btn.hwnd,
                        X = ctx.ClickPoint.X,
                        Y = ctx.ClickPoint.Y,
                        BoundingRectangle = new System.Windows.Rect(
                            btn.rect.Left, btn.rect.Top,
                            btn.rect.Right - btn.rect.Left,
                            btn.rect.Bottom - btn.rect.Top),
                        IsEnabled = IsWindowEnabled(btn.hwnd),
                        IsVisible = IsWindowVisible(btn.hwnd),
                        ElementText = btn.text
                    };
                }
            }

            // Fallback
            return GetBasicWindowInfo(ctx.ClickPoint.X, ctx.ClickPoint.Y);
        }

        private static UIElementInfo DetectWinFormsElement(UiDetectionContext ctx)
        {
            try
            {
                var element = AutomationElement.FromPoint(
                    new System.Windows.Point(ctx.ClickPoint.X, ctx.ClickPoint.Y));

                if (element != null)
                {
                    var info = ExtractElementInfo(element, ctx.ClickPoint.X, ctx.ClickPoint.Y);

                    // ❌ SKIP "about:blank" - rovnako ako DetectWebElement
                    if (info != null &&
                        info.Name == "about:blank" &&
                        info.AutomationId == "RootWebArea" &&
                        info.ControlType == "document")
                    {
                        Debug.WriteLine("[WinForms] Skipping 'about:blank' root, using fallback");
                        return GetBasicWindowInfo(ctx.ClickPoint.X, ctx.ClickPoint.Y);
                    }

                    // WinForms často nemá AutomationId - použi ClassName
                    if (string.IsNullOrEmpty(info.AutomationId) && !string.IsNullOrEmpty(info.ClassName))
                    {
                        info.Name = $"{info.ControlType}_{info.ClassName}";
                    }

                    return info;
                }
            }
            catch { }

            return GetBasicWindowInfo(ctx.ClickPoint.X, ctx.ClickPoint.Y);
        }

        private static UIElementInfo DetectWpfElement(UiDetectionContext ctx)
        {
            // WPF má spoľahlivý UIA
            return GetElementAtPoint(ctx.ClickPoint.X, ctx.ClickPoint.Y);
        }

        private static UIElementInfo DetectWinUI3Element(UiDetectionContext ctx)
        {
            // Už máš implementované v ProcessWinUI3Element
            try
            {
                var element = AutomationElement.FromHandle(ctx.Hwnd);
                return ProcessWinUI3Element(element, ctx.ClickPoint.X, ctx.ClickPoint.Y);
            }
            catch
            {
                return GetBasicWindowInfo(ctx.ClickPoint.X, ctx.ClickPoint.Y);
            }
        }

        private static UIElementInfo DetectWebElement(UiDetectionContext ctx)
        {
            try
            {
                var root = AutomationElement.FromHandle(ctx.Hwnd);

                // Skip about:blank root - hľadaj reálne elementy
                if (GetProperty(root, AutomationElement.NameProperty) == "about:blank")
                {
                    var walker = TreeWalker.RawViewWalker;
                    var child = walker.GetFirstChild(root);

                    while (child != null)
                    {
                        var name = GetProperty(child, AutomationElement.NameProperty);
                        if (name != "about:blank")
                        {
                            // OPRAVENÉ: ctx.ClickPoint.X, ctx.ClickPoint.Y namiesto x, y
                            var element = AutomationElement.FromPoint(
                                new System.Windows.Point(ctx.ClickPoint.X, ctx.ClickPoint.Y));

                            if (element != null && element != root)
                            {
                                var info = ExtractElementInfo(element, ctx.ClickPoint.X, ctx.ClickPoint.Y);

                                // Ak stále about:blank, použi XY fallback
                                if (info.Name == "about:blank")
                                {
                                    return new UIElementInfo
                                    {
                                        Name = $"WebClick_{ctx.ClickPoint.X}_{ctx.ClickPoint.Y}",
                                        ControlType = "WebElement",
                                        X = ctx.ClickPoint.X,
                                        Y = ctx.ClickPoint.Y,
                                        WindowHandle = ctx.Hwnd,
                                        ClassName = ctx.ClassName
                                    };
                                }

                                return info;
                            }
                        }
                        child = walker.GetNextSibling(child);
                    }
                }
            }
            catch { }

            // XY fallback
            return new UIElementInfo
            {
                Name = $"WebClick_{ctx.ClickPoint.X}_{ctx.ClickPoint.Y}",
                ControlType = "WebElement",
                X = ctx.ClickPoint.X,
                Y = ctx.ClickPoint.Y,
                WindowHandle = ctx.Hwnd
            };
        }

        private static UIElementInfo DetectGenericElement(UiDetectionContext ctx)
        {
            // Generic UIA fallback
            return GetElementAtPoint(ctx.ClickPoint.X, ctx.ClickPoint.Y);
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private static UIElementInfo DetectByPatternsWinUI3(AutomationElement element, int clickX, int clickY)
        {
            try
            {
                Debug.WriteLine("Trying pattern-based detection for WinUI3...");

                // Skús rôzne patterny na identifikáciu typu elementu
                var patterns = element.GetSupportedPatterns();

                string detectedType = "unknown";
                string detectedText = "";

                foreach (var pattern in patterns)
                {
                    try
                    {
                        if (pattern == ValuePattern.Pattern)
                        {
                            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                            {
                                var value = ((ValuePattern)valuePattern).Current.Value;
                                if (!string.IsNullOrEmpty(value))
                                {
                                    detectedType = "textbox";
                                    detectedText = value;
                                    break;
                                }
                            }
                        }
                        else if (pattern == InvokePattern.Pattern)
                        {
                            detectedType = "button";
                        }
                        else if (pattern == TogglePattern.Pattern)
                        {
                            detectedType = "checkbox";
                        }
                        else if (pattern == SelectionPattern.Pattern)
                        {
                            detectedType = "list";
                        }
                        else if (pattern == TextPattern.Pattern)
                        {
                            if (element.TryGetCurrentPattern(TextPattern.Pattern, out object textPattern))
                            {
                                var text = ((TextPattern)textPattern).DocumentRange.GetText(100);
                                if (!string.IsNullOrEmpty(text))
                                {
                                    detectedType = "text";
                                    detectedText = text;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking pattern {pattern.ProgrammaticName}: {ex.Message}");
                    }
                }

                if (detectedType != "unknown")
                {
                    string name = !string.IsNullOrEmpty(detectedText) ?
                        $"{detectedType}_{CleanName(detectedText)}" :
                        $"{detectedType}_at_{clickX}_{clickY}";

                    Debug.WriteLine($"Pattern-based detection found: {name}");

                    return new UIElementInfo
                    {
                        Name = name,
                        ControlType = detectedType,
                        ElementText = detectedText,
                        X = clickX,
                        Y = clickY,
                        BoundingRectangle = GetBoundingRectangleSafe(element),
                        IsEnabled = GetIsEnabledSafe(element),
                        IsVisible = GetIsVisibleSafe(element),
                        AutomationElement = element,
                        ClassName = "Microsoft.UI.Content.DesktopChildSiteBridge"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in pattern-based detection: {ex.Message}");
                return null;
            }
        }

        private static string CreateMeaningfulName(string name, string automationId, string elementText,
                                                  string placeholderText, string controlType, string helpText)
        {
            // Priorita názvov:
            // 1. Skutočný názov elementu
            if (!string.IsNullOrWhiteSpace(name) && !IsGenericName(name))
                return CleanName(name);

            // 2. AutomationId (ak nie je generický)
            if (!string.IsNullOrWhiteSpace(automationId) && !IsGenericId(automationId))
                return $"AutoId_{CleanName(automationId)}";

            // 3. Text v elemente (pre buttony, labels)
            if (!string.IsNullOrWhiteSpace(elementText))
                return $"{controlType}_{CleanName(elementText)}";

            // 4. Placeholder text (pre textboxy)
            if (!string.IsNullOrWhiteSpace(placeholderText))
                return $"{controlType}_{CleanName(placeholderText)}";

            // 5. Help text
            if (!string.IsNullOrWhiteSpace(helpText))
                return $"{controlType}_{CleanName(helpText)}";

            // 6. Fallback na typ + pozíciu
            return $"{controlType}_Unknown";
        }

        private static bool IsGenericName(string name)
        {
            var genericNames = new[]
            {
                "Microsoft.UI.Content.DesktopChildSiteBridge",
                "DesktopChildSiteBridge",
                "ContentPresenter",
                "Border",
                "Grid",
                "StackPanel",
                "Canvas",
                "UserControl"
            };

            return genericNames.Any(generic =>
                name.IndexOf(generic, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsGenericId(string id)
        {
            // Generické IDs sú zvyčajne UUID alebo číselné
            return id.Length > 20 || id.All(char.IsDigit) || id.Contains("-");
        }

        private static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            // Odstráň špeciálne znaky, ponechaj len alfanumerické a underscore
            return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ')
                                 .ToArray())
                                 .Replace(" ", "_")
                                 .Trim('_');
        }

        private static void LogElementDetails(UIElementInfo element)
        {
            Debug.WriteLine("=== ELEMENT DETECTION DETAILS ===");
            Debug.WriteLine($"Name: '{element.Name}'");
            Debug.WriteLine($"AutomationId: '{element.AutomationId}'");
            Debug.WriteLine($"ClassName: '{element.ClassName}'");
            Debug.WriteLine($"ControlType: '{element.ControlType}'");
            Debug.WriteLine($"ElementText: '{element.ElementText}'");
            Debug.WriteLine($"PlaceholderText: '{element.PlaceholderText}'");
            Debug.WriteLine($"HelpText: '{element.HelpText}'");
            Debug.WriteLine($"Position: ({element.X}, {element.Y})");
            Debug.WriteLine($"Enabled: {element.IsEnabled}, Visible: {element.IsVisible}");
            Debug.WriteLine("=====================================");
        }

        public static UIElementInfo GetBasicWindowInfo(int x, int y)
        {
            IntPtr hwnd = WindowFromPoint(new POINT { x = x, y = y });

            if (hwnd != IntPtr.Zero)
            {
                string className = GetClassName(hwnd);
                string windowText = GetWindowText(hwnd);
                RECT rect;
                GetWindowRect(hwnd, out rect);

                // Pokús sa použiť UIA pre lepšiu identifikáciu
                string elementName = windowText;
                string controlType = "Window";

                try
                {
                    var element = AutomationElement.FromHandle(hwnd);
                    if (element != null)
                    {
                        // Získaj lepšie info z UIA
                        var uiaName = GetProperty(element, AutomationElement.NameProperty);
                        var uiaControlType = GetControlTypeSafe(element);

                        if (!string.IsNullOrEmpty(uiaName) && uiaName != "about:blank")
                        {
                            elementName = uiaName;
                        }

                        if (!string.IsNullOrEmpty(uiaControlType) && uiaControlType != "Unknown")
                        {
                            controlType = uiaControlType;
                        }
                    }
                }
                catch
                {
                    // UIA zlyhalo, použij Win32 info
                }

                // Vytvor zmysluplný názov
                string finalName;
                if (!string.IsNullOrWhiteSpace(elementName))
                {
                    finalName = CleanName(elementName);
                }
                else if (!string.IsNullOrWhiteSpace(windowText))
                {
                    finalName = $"Window_{CleanName(windowText)}";
                }
                else
                {
                    // Použij ClassName ako základ
                    finalName = $"{controlType}_{CleanName(className)}";
                }

                return new UIElementInfo
                {
                    Name = finalName,
                    ClassName = className,
                    ControlType = controlType,
                    X = x,
                    Y = y,
                    BoundingRectangle = new System.Windows.Rect(rect.Left, rect.Top,
                        rect.Right - rect.Left, rect.Bottom - rect.Top),
                    WindowHandle = hwnd,
                    IsEnabled = IsWindowEnabled(hwnd),
                    IsVisible = IsWindowVisible(hwnd),
                    ElementText = elementName
                };
            }

            return null;
        }

        public static UIElementInfo FindElementByName(string name, IntPtr windowHandle)
        {
            try
            {
                AutomationElement window = AutomationElement.FromHandle(windowHandle);
                if (window == null) return null;

                // Najprv skús presný match
                var exactElement = FindByExactName(window, name);
                if (exactElement != null)
                    return ExtractElementInfo(exactElement, 0, 0);

                // Potom skús partial match
                var partialElement = FindByPartialName(window, name);
                if (partialElement != null)
                    return ExtractElementInfo(partialElement, 0, 0);

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding element by name: {ex.Message}");
                return null;
            }
        }

        private static AutomationElement GetElementWithCache(IntPtr windowHandle)
        {
            CacheRequest cacheRequest = new CacheRequest();
            cacheRequest.Add(AutomationElement.NameProperty);
            cacheRequest.Add(AutomationElement.AutomationIdProperty);
            cacheRequest.Add(AutomationElement.ClassNameProperty);
            cacheRequest.TreeScope = TreeScope.Element;

            using (cacheRequest.Activate())
            {
                return AutomationElement.FromHandle(windowHandle);
            }
        }

        private static AutomationElement FindByExactName(AutomationElement parent, string name)
        {
            try
            {
                var condition = new PropertyCondition(AutomationElement.NameProperty, name);
                return parent.FindFirst(TreeScope.Descendants, condition);
            }
            catch
            {
                return null;
            }
        }

        public static AutomationElement FindByPartialName(AutomationElement parent, string name)
        {
            try
            {
                var allElements = parent.FindAll(TreeScope.Descendants, Condition.TrueCondition);

                foreach (AutomationElement element in allElements)
                {
                    var elementInfo = ExtractElementInfo(element, 0, 0);
                    if (elementInfo?.Name != null &&
                        elementInfo.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return element;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// FALLBACK Úroveň 2: Získa vlastnosť priamo cez element.Current
        /// </summary>
        public static string GetPropertyViaCurrent(AutomationElement element, AutomationProperty property)
        {
            if (element == null || property == null)
                return "";

            // Extra kontrola pre problematické elementy
            if (IsProblematicComElementSafe(element))
            {
                Debug.WriteLine($"[UIDetector] Skipping GetPropertyViaCurrent for problematic element");
                return ""; // Preskočíme tento fallback
            }

            try
            {
                // String properties
                if (property == AutomationElement.NameProperty)
                    return element.Current.Name ?? "";

                if (property == AutomationElement.AutomationIdProperty)
                    return element.Current.AutomationId ?? "";

                if (property == AutomationElement.ClassNameProperty)
                    return element.Current.ClassName ?? "";

                if (property == AutomationElement.HelpTextProperty)
                    return element.Current.HelpText ?? "";

                if (property == AutomationElement.LocalizedControlTypeProperty)
                    return element.Current.LocalizedControlType ?? "";

                if (property == AutomationElement.ControlTypeProperty)
                    return element.Current.ControlType?.ProgrammaticName ?? "";

                if (property == AutomationElement.AcceleratorKeyProperty)
                    return element.Current.AcceleratorKey ?? "";

                if (property == AutomationElement.AccessKeyProperty)
                    return element.Current.AccessKey ?? "";

                if (property == AutomationElement.ItemTypeProperty)
                    return element.Current.ItemType ?? "";

                if (property == AutomationElement.ItemStatusProperty)
                    return element.Current.ItemStatus ?? "";

                if (property == AutomationElement.FrameworkIdProperty)
                    return element.Current.FrameworkId ?? "";

                // Boolean properties
                if (property == AutomationElement.IsEnabledProperty)
                    return element.Current.IsEnabled.ToString();

                if (property == AutomationElement.IsOffscreenProperty)
                    return element.Current.IsOffscreen.ToString();

                if (property == AutomationElement.IsKeyboardFocusableProperty)
                    return element.Current.IsKeyboardFocusable.ToString();

                if (property == AutomationElement.HasKeyboardFocusProperty)
                    return element.Current.HasKeyboardFocus.ToString();

                if (property == AutomationElement.IsPasswordProperty)
                    return element.Current.IsPassword.ToString();

                if (property == AutomationElement.IsContentElementProperty)
                    return element.Current.IsContentElement.ToString();

                if (property == AutomationElement.IsControlElementProperty)
                    return element.Current.IsControlElement.ToString();

                // Numeric properties
                if (property == AutomationElement.ProcessIdProperty)
                    return element.Current.ProcessId.ToString();

                if (property == AutomationElement.NativeWindowHandleProperty)
                    return element.Current.NativeWindowHandle.ToString();

                // BoundingRectangle
                if (property == AutomationElement.BoundingRectangleProperty)
                {
                    var rect = element.Current.BoundingRectangle;
                    return $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";
                }
            }
            catch
            {
                // Aj tento fallback môže zlyhať - nevadí
            }

            return "";
        }

        // <summary>
        /// FALLBACK Úroveň 3: Získa vlastnosť cez UI Automation Patterns
        /// Používa sa najmä pre získanie textu z TextBox, ComboBox, atď.
        /// </summary>
        public static string GetPropertyViaPatterns(AutomationElement element, AutomationProperty property)
        {
            if (element == null || property == null)
                return "";

            try
            {
                // Pre Name property skúsime získať text cez ValuePattern alebo TextPattern
                if (property == AutomationElement.NameProperty)
                {
                    // ValuePattern - pre TextBox, ComboBox, atď.
                    if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePatternObj))
                    {
                        var valuePattern = valuePatternObj as ValuePattern;
                        if (valuePattern != null && !string.IsNullOrEmpty(valuePattern.Current.Value))
                        {
                            return valuePattern.Current.Value;
                        }
                    }

                    // TextPattern - pre RichTextBox a podobné
                    if (element.TryGetCurrentPattern(TextPattern.Pattern, out object textPatternObj))
                    {
                        var textPattern = textPatternObj as TextPattern;
                        if (textPattern != null)
                        {
                            string text = textPattern.DocumentRange.GetText(100);
                            if (!string.IsNullOrEmpty(text))
                                return text.Trim();
                        }
                    }
                }
            }
            catch
            {
                // Pattern môže zlyhať - nevadí
            }

            return "";
        }

        /// <summary>
        /// FALLBACK Úroveň 4: Získa vlastnosť cez Win32 API
        /// Používa sa ako posledná možnosť pre kritické vlastnosti
        /// </summary>
        public static string GetPropertyViaWin32(AutomationElement element, AutomationProperty property)
        {
            if (element == null || property == null)
                return "";

            try
            {
                // Získame native window handle
                int handle = element.Current.NativeWindowHandle;
                if (handle == 0)
                    return "";

                IntPtr hwnd = new IntPtr(handle);

                // Pre ClassName property použijeme GetClassName Win32 API
                if (property == AutomationElement.ClassNameProperty)
                {
                    var className = new System.Text.StringBuilder(256);
                    if (GetClassName(hwnd, className, className.Capacity) > 0)
                    {
                        return className.ToString();
                    }
                }

                // Pre Name property použijeme GetWindowText Win32 API
                if (property == AutomationElement.NameProperty)
                {
                    int length = GetWindowTextLength(hwnd);
                    if (length > 0)
                    {
                        var text = new System.Text.StringBuilder(length + 1);
                        if (GetWindowText(hwnd, text, text.Capacity) > 0)
                        {
                            return text.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Win32 API môže zlyhať - nevadí
            }

            return "";
        }

        /// <summary>
        /// Pomocná metóda - získa vlastnosť automation elementu
        /// pre použitie v celom UIElementDetector
        /// </summary>
        public static string GetProperty(AutomationElement element, AutomationProperty property)
        {
            // Používa hlavnú metódu GetElementProperty s plným fallback mechanizmom
            return GetElementProperty(element, property);
        }

        // === Win32 API Declarations ===
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]



        private static extern int GetWindowTextLength(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);


        /// <summary>
        /// Bezpečne získa string vlastnosť
        /// </summary>
        /// <param name="element">Automation element</param>
        /// <param name="property">String vlastnosť</param>
        /// <param name="defaultValue">Predvolená hodnota</param>
        /// <returns>String hodnota vlastnosti</returns>
        private static string GetStringProperty(AutomationElement element, AutomationProperty property, string defaultValue = "")
        {
            try
            {
                var value = GetProperty(element, property);
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Bezpečne získa bool vlastnosť
        /// </summary>
        /// <param name="element">Automation element</param>
        /// <param name="property">Bool vlastnosť</param>
        /// <param name="defaultValue">Predvolená hodnota</param>
        /// <returns>Bool hodnota vlastnosti</returns>
        private static bool GetBoolProperty(AutomationElement element, AutomationProperty property, bool defaultValue = false)
        {
            try
            {
                object value = element.GetCurrentPropertyValue(property);
                if (value is bool boolValue)
                    return boolValue;

                // Pokús sa konvertovať string na bool
                if (value != null && bool.TryParse(value.ToString(), out bool parsedValue))
                    return parsedValue;

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Konvertuje AutomationElement na UIElementInfo - OPRAVENÁ VERZIA
        /// </summary>
        public static UIElementInfo ConvertToUIElementInfo(AutomationElement element)
        {
            try
            {
                if (element == null) return null;

                var rect = element.Current.BoundingRectangle;

                var info = new UIElementInfo
                {
                    Name = GetProperty(element, AutomationElement.NameProperty),
                    AutomationId = GetProperty(element, AutomationElement.AutomationIdProperty),
                    ClassName = GetProperty(element, AutomationElement.ClassNameProperty),
                    ControlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown",
                    ElementText = GetElementText(element),
                    X = (int)(rect.X + rect.Width / 2),
                    Y = (int)(rect.Y + rect.Height / 2),
                    IsEnabled = element.Current.IsEnabled,
                    IsVisible = !element.Current.IsOffscreen,
                    BoundingRectangle = rect,
                    AutomationElement = element
                };

                return info;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting AutomationElement: {ex.Message}");
                return null;
            }
        }

        #region Solving Problems with COM Exceptions, NonComVisibleBaseClass, ElementNotAvailableException

        /// <summary>
        /// Získa vlastnosť elementu s viacúrovňovým fallback mechanizmom
        /// Získa vlastnosť elementu BEZ MDA warnings
        /// Problematické elementy identifikuje PRED volaním GetCurrentPropertyValue
        /// GARANTUJE: Žiadny element nebude vynechaný, aplikácia nikdy nespadne
        /// </summary>
        private static string GetElementProperty(AutomationElement element, AutomationProperty property)
        {
            try
            {
                if (element == null || property == null)
                    return "";

                // === PREDIKČNÁ KONTROLA: Zistíme či je element problematický ===

                // Predbežný test - ak GetCurrentPropertyValue zlyhá, element je problematický
                try
                {
                    var testValue = element.GetCurrentPropertyValue(AutomationElement.ClassNameProperty, true);
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Tento element určite spôsobí problémy
                    Debug.WriteLine($"[UIDetector] Pre-test failed - using Win32 fallback only");
                    return GetPropertyViaWin32(element, property);
                }

                bool isProblematic = IsProblematicComElementSafe(element);
                if (isProblematic)
                {
                    // Pre problematické elementy PRESKOČÍME GetCurrentPropertyValue
                    // a ideme PRIAMO na fallback metódy
                    Debug.WriteLine($"[UIDetector] Skipping GetCurrentPropertyValue for problematic element, using fallback directly");

                    // Úroveň 2: element.Current fallback
                    try
                    {
                        string currentValue = GetPropertyViaCurrent(element, property);
                        if (!string.IsNullOrEmpty(currentValue))
                            return currentValue;
                    }
                    catch { }

                    // Úroveň 3: Patterns fallback
                    try
                    {
                        string patternValue = GetPropertyViaPatterns(element, property);
                        if (!string.IsNullOrEmpty(patternValue))
                            return patternValue;
                    }
                    catch { }

                    // Úroveň 4: Win32 API fallback
                    try
                    {
                        string win32Value = GetPropertyViaWin32(element, property);
                        if (!string.IsNullOrEmpty(win32Value))
                            return win32Value;
                    }
                    catch { }

                    return "";
                }

                // === Pre NEPROBLEMATICKÉ elementy použijeme štandardnú metódu ===

                // Úroveň 1: Primárny pokus - GetCurrentPropertyValue
                try
                {
                    object value = element.GetCurrentPropertyValue(property, false);
                    if (value != null)
                    {
                        string result = value.ToString();
                        if (!string.IsNullOrEmpty(result))
                            return result;
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Aj napriek kontrole môže nastať COM exception
                    // (pre  neznáme problematické typy)
                    Debug.WriteLine($"[UIDetector] Unexpected COM exception, trying fallback");
                }
                catch (ElementNotAvailableException)
                {
                    return "";
                }

                // === Fallback metódy (pre prípad, že primárna metóda vrátila null/empty) ===

                // Úroveň 2: element.Current fallback
                try
                {
                    string currentValue = GetPropertyViaCurrent(element, property);
                    if (!string.IsNullOrEmpty(currentValue))
                        return currentValue;
                }
                catch { }

                // Úroveň 3: Patterns fallback
                try
                {
                    string patternValue = GetPropertyViaPatterns(element, property);
                    if (!string.IsNullOrEmpty(patternValue))
                        return patternValue;
                }
                catch { }

                // Úroveň 4: Win32 API fallback
                try
                {
                    string win32Value = GetPropertyViaWin32(element, property);
                    if (!string.IsNullOrEmpty(win32Value))
                        return win32Value;
                }
                catch { }

                return "";
            }
            catch (Exception ex)
            {
                // Zachytíme AKÚKOĽVEK výnimku
                Debug.WriteLine($"[UIDetector] Unexpected error for {property.ProgrammaticName}: {ex.GetType().Name}");
                return "";
            }
        }

        #endregion

        #region Enhanced Safe Property Access

        /// <summary>
        /// Zistí, či je element problematický BEZ použitia element.Current
        /// SKUTOČNE bezpečná verzia - používa len GetCurrentPropertyValue s ignoreDefaultValue
        /// </summary>
        public static bool IsProblematicComElementSafe(AutomationElement element)
        {
            if (element == null)
                return false;

            try
            {
                // === ÚROVEŇ 1: Pokús o získanie ClassName BEZ element.Current ===
                string className = "";
                try
                {
                    // Použijeme GetCurrentPropertyValue s ignoreDefaultValue=true
                    // Toto je bezpečnejšie ako element.Current
                    var classNameObj = element.GetCurrentPropertyValue(
                        AutomationElement.ClassNameProperty,
                        true);  // ignoreDefaultValue=true minimalizuje COM volania

                    className = classNameObj?.ToString() ?? "";
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Ak zlyhá už tento základný pokus, element je problematický
                    Debug.WriteLine($"[UIDetector] COM exception getting ClassName - element is problematic");
                    return true;
                }
                catch (ElementNotAvailableException)
                {
                    return true;
                }
                catch
                {
                    // Akákoľvek iná výnimka = problematický
                    return true;
                }

                // === ÚROVEŇ 2: Kontrola na MS.Internal namespace ===
                if (!string.IsNullOrEmpty(className))
                {
                    // Kontrola na MS.Internal - známe problematické proxy triedy
                    if (className.IndexOf("MS.Internal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.WriteLine($"[UIDetector] MS.Internal element detected: {className}");
                        return true;
                    }

                    // Zoznam známych problematických tried
                    var problematicClasses = new[]
                    {
                "WindowsForms10.EDIT",
                "WindowsForms10.RichEdit",
                "WindowsForms10.COMBOBOX",
                "Edit",
                "RichEdit",
                "RichEdit20W",
                "RichEdit20A",
                "RichEdit50W",
                "NonClientArea",
                "TitleBar",
                "ScrollBar",
                "SysHeader32",
                "Internet Explorer_Server",
                "Shell DocObject View",
                "MSCTFIME UI"
            };

                    foreach (var problematic in problematicClasses)
                    {
                        if (className.IndexOf(problematic, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.WriteLine($"[UIDetector] Problematic element type: {className}");
                            return true;
                        }
                    }
                }

                // === ÚROVEŇ 3: Kontrola ControlType (tiež s ignoreDefaultValue) ===
                try
                {
                    var controlTypeObj = element.GetCurrentPropertyValue(
                        AutomationElement.ControlTypeProperty,
                        true);

                    if (controlTypeObj is ControlType controlType)
                    {
                        if (controlType == ControlType.TitleBar ||
                            controlType == ControlType.MenuBar ||
                            controlType == ControlType.ScrollBar)
                        {
                            Debug.WriteLine($"[UIDetector] Problematic ControlType: {controlType.ProgrammaticName}");
                            return true;
                        }
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // COM exception pri získavaní ControlType = problematický element
                    return true;
                }
                catch
                {
                    // Ignorujeme ostatné výnimky pri ControlType check
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UIDetector] Exception in IsProblematicComElementSafe: {ex.Message}");
                // Pri akejkoľvek výnimke považujeme za problematický
                return true;
            }
        }

        /// <summary>
        /// Bezpečne získa ClassName cez Win32 API
        /// </summary>
        public static string GetClassNameViaWin32(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return "";

            try
            {
                var sb = new System.Text.StringBuilder(256);
                int result = GetClassName(hwnd, sb, sb.Capacity);
                return result > 0 ? sb.ToString() : "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Bezpečne získa boolean vlastnosť elementu s fallback mechanizmom
        /// </summary>
        public static bool GetBooleanProperty(AutomationElement element, Func<AutomationElement, bool> getter, bool defaultValue = false)
        {
            if (element == null)
                return defaultValue;

            // Kontrola, či je element problematický
            bool isProblematic = IsProblematicComElementSafe(element);

            if (isProblematic)
            {
                System.Diagnostics.Debug.WriteLine($"[UIDetector] Skipping Current access for problematic element (boolean)");

                // Pre problematické elementy použijeme Win32 API fallback
                try
                {
                    IntPtr hwnd = new IntPtr(element.Current.NativeWindowHandle);
                    if (hwnd != IntPtr.Zero)
                    {
                        // Pre IsEnabled
                        if (getter.Method.Name.Contains("IsEnabled"))
                        {
                            return IsWindowEnabled(hwnd);
                        }
                        // Pre IsVisible
                        if (getter.Method.Name.Contains("IsOffscreen"))
                        {
                            return IsWindowVisible(hwnd); // Pozor: inverse logic
                        }
                    }
                }
                catch { }

                return defaultValue;
            }

            // Pre neproblematické elementy - normálny prístup
            try
            {
                return getter(element);
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIDetector] COM exception getting boolean: {ex.Message}");
                return defaultValue;
            }
            catch (ElementNotAvailableException)
            {
                return defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIDetector] Unexpected exception getting boolean: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Bezpečne získa IsEnabled s fallback na Win32 API
        /// </summary>
        public static bool GetIsEnabledSafe(AutomationElement element)
        {
            return GetBooleanProperty(element, e => e.Current.IsEnabled, defaultValue: false);
        }

        /// <summary>
        /// Bezpečne získa IsVisible (inverse of IsOffscreen) s fallback na Win32 API
        /// </summary>
        /// 
        public static bool GetIsVisibleSafe(AutomationElement element)
        {
            // Pozor: IsOffscreen je inverse IsVisible!
            return GetBooleanProperty(element, e => !e.Current.IsOffscreen, defaultValue: false);
        }

        /// <summary>
        /// Bezpečne získa ControlType string
        /// </summary>
        public static string GetControlTypeSafe(AutomationElement element)
        {
            if (element == null)
                return "Unknown";

            bool isProblematic = IsProblematicComElementSafe(element);

            if (isProblematic)
            {
                System.Diagnostics.Debug.WriteLine($"[UIDetector] Skipping Current access for problematic element (ControlType)");

                // Fallback: skús určiť typ z ClassName
                try
                {
                    IntPtr hwnd = new IntPtr(element.Current.NativeWindowHandle);
                    if (hwnd != IntPtr.Zero)
                    {
                        string className = GetClassNameViaWin32(hwnd);
                        return GuessControlTypeFromClassName(className);
                    }
                }
                catch { }

                return "Unknown";
            }

            try
            {
                return element.Current.ControlType?.LocalizedControlType ?? "Unknown";
            }
            catch (COMException)
            {
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Pokúsi sa odhadnúť ControlType z ClassName
        /// </summary>
        public static string GuessControlTypeFromClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
                return "Unknown";

            className = className.ToLowerInvariant();

            if (className.Contains("edit")) return "Edit";
            if (className.Contains("button")) return "Button";
            if (className.Contains("combobox")) return "ComboBox";
            if (className.Contains("listbox")) return "List";
            if (className.Contains("richedit")) return "Document";
            if (className.Contains("static")) return "Text";
            if (className.Contains("scrollbar")) return "ScrollBar";

            return "Unknown";
        }

        /// <summary>
        /// Bezpečne získa BoundingRectangle
        /// </summary>
        public static System.Windows.Rect GetBoundingRectangleSafe(AutomationElement element)
        {
            if (element == null)
                return System.Windows.Rect.Empty;

            bool isProblematic = IsProblematicComElementSafe(element);

            if (isProblematic)
            {
                System.Diagnostics.Debug.WriteLine($"[UIDetector] Skipping Current access for problematic element (BoundingRect)");

                // Fallback na Win32 API
                try
                {
                    IntPtr hwnd = new IntPtr(element.Current.NativeWindowHandle);
                    if (hwnd != IntPtr.Zero)
                    {
                        RECT rect;
                        if (GetWindowRect(hwnd, out rect))
                        {
                            return new System.Windows.Rect(
                                rect.Left,
                                rect.Top,
                                rect.Right - rect.Left,
                                rect.Bottom - rect.Top);
                        }
                    }
                }
                catch { }

                return System.Windows.Rect.Empty;
            }

            try
            {
                return element.Current.BoundingRectangle;
            }
            catch (COMException)
            {
                return System.Windows.Rect.Empty;
            }
            catch
            {
                return System.Windows.Rect.Empty;
            }
        }

        #endregion

        #region Win32 API Declarations

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        //private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        //[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private static string GetClassName(IntPtr hWnd)
        {
            System.Text.StringBuilder className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        private static string GetWindowText(IntPtr hWnd)
        {
            System.Text.StringBuilder windowText = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, windowText, windowText.Capacity);
            return windowText.ToString();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion

        public class UiDetectionContext
        {
            public IntPtr Hwnd { get; set; }
            public System.Drawing.Point ClickPoint { get; set; } 
            public UiFramework Framework { get; set; }
            public string ClassName { get; set; }
            public string ProcessName { get; set; }
        }

        public static UIElementInfo DetectElementWithRouting(int x, int y)
        {
            var context = BuildDetectionContext(x, y);
            Debug.WriteLine($"[Routing] Framework: {context.Framework}, Process: {context.ProcessName}, Class: {context.ClassName}");

            // ═══════════════════════════════════════════════════════════
            // ŠPECIFICKÉ PRÍPADY - kontroluj PRED switch
            // ═══════════════════════════════════════════════════════════

            // 1. Win32 MessageBox detection (#32770)
            if (context.ClassName == "#32770")
            {
                Debug.WriteLine("[Routing] Detected Win32 Dialog/MessageBox");
                return DetectWin32Element(context);
            }

            // 2. WinForms Button/Control detection
            if (context.ClassName.StartsWith("WindowsForms10."))
            {
                Debug.WriteLine("[Routing] Detected WinForms control");
                return DetectWinFormsElement(context);
            }

            // 3. Chrome/Edge WebView detection
            if (context.ClassName.Contains("Chrome_WidgetWin") ||
                context.ClassName.Contains("Chrome_RenderWidgetHostHWND"))
            {
                Debug.WriteLine("[Routing] Detected Chrome/WebView2");
                return DetectWebElement(context);
            }

            // 4. WinUI3 DesktopChildSiteBridge
            if (context.ClassName.Contains("DesktopChildSiteBridge"))
            {
                Debug.WriteLine("[Routing] Detected WinUI3 DesktopChildSiteBridge");
                return DetectWinUI3Element(context);
            }

            // 5. Office aplikácie (Word, Excel, PowerPoint)
            if (context.ProcessName.Contains("WINWORD") ||
                context.ProcessName.Contains("EXCEL") ||
                context.ProcessName.Contains("POWERPNT"))
            {
                Debug.WriteLine("[Routing] Detected Office application");
                return DetectOfficeElement(context);
            }

            // 6. Java aplikácie (Swing/AWT)
            if (context.ClassName.StartsWith("SunAwt"))
            {
                Debug.WriteLine("[Routing] Detected Java Swing/AWT");
                return DetectJavaElement(context);
            }

            // 7. Qt aplikácie
            if (context.ClassName.StartsWith("Qt"))
            {
                Debug.WriteLine("[Routing] Detected Qt application");
                return DetectQtElement(context);
            }

            // 8. Electron aplikácie (VS Code, Discord, Slack...)
            if (context.ProcessName.Contains("electron") ||
                context.ProcessName == "Code" ||
                context.ProcessName == "Discord" ||
                context.ProcessName == "Slack")
            {
                Debug.WriteLine("[Routing] Detected Electron app");
                return DetectWebElement(context); // Electron používa Chromium
            }

            // 9. Legacy Win32 controls (Button, Edit, Static...)
            if (IsLegacyWin32Control(context.ClassName))
            {
                Debug.WriteLine("[Routing] Detected legacy Win32 control");
                return DetectWin32Element(context);
            }

            // ═══════════════════════════════════════════════════════════
            // ŠTANDARDNÝ SWITCH (ak nič nevyhovelo vyššie)
            // ═══════════════════════════════════════════════════════════

            switch (context.Framework)
            {
                case UiFramework.Win32:
                    return DetectWin32Element(context);

                case UiFramework.WinForms:
                    return DetectWinFormsElement(context);

                case UiFramework.Wpf:
                    return DetectWpfElement(context);

                case UiFramework.WinUI3:
                    return DetectWinUI3Element(context);

                case UiFramework.WebView2:
                    return DetectWebElement(context);

                default:
                    return DetectGenericElement(context);
            }
        }

        /// <summary>
        /// Kontroluje či je className legacy Win32 control
        /// </summary>
        private static bool IsLegacyWin32Control(string className)
        {
            var legacyControls = new[]
            {
                "Button", "Edit", "Static", "ListBox", "ComboBox",
                "ScrollBar", "SysTreeView32", "SysListView32",
                "SysHeader32", "msctls_statusbar32", "ToolbarWindow32",
                "RichEdit", "RichEdit20W", "RichEdit20A", "RichEdit50W"
            };

            return legacyControls.Any(c => className.Equals(c, StringComparison.OrdinalIgnoreCase) ||
                                           className.StartsWith(c, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Detektor pre Office aplikácie (Word, Excel, PowerPoint)
        /// </summary>
        private static UIElementInfo DetectOfficeElement(UiDetectionContext ctx)
        {
            try
            {
                var element = AutomationElement.FromPoint(
                    new System.Windows.Point(ctx.ClickPoint.X, ctx.ClickPoint.Y));

                if (element != null)
                {
                    return ExtractElementInfo(element, ctx.ClickPoint.X, ctx.ClickPoint.Y);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Office] Detection failed: {ex.Message}");
            }

            return GetBasicWindowInfo(ctx.ClickPoint.X, ctx.ClickPoint.Y);
        }

        /// <summary>
        /// Detektor pre Java Swing/AWT aplikácie
        /// </summary>
        private static UIElementInfo DetectJavaElement(UiDetectionContext ctx)
        {
            try
            {
                // Java Swing používa AccessBridge, ale funguje aj UIA
                var element = AutomationElement.FromPoint(
                    new System.Windows.Point(ctx.ClickPoint.X, ctx.ClickPoint.Y));

                if (element != null)
                {
                    return ExtractElementInfo(element, ctx.ClickPoint.X, ctx.ClickPoint.Y);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Java] Detection failed: {ex.Message}");
            }

            return GetBasicWindowInfo(ctx.ClickPoint.X, ctx.ClickPoint.Y);
        }

        /// <summary>
        /// Detektor pre Qt aplikácie
        /// </summary>
        private static UIElementInfo DetectQtElement(UiDetectionContext ctx)
        {
            try
            {
                // Qt aplikácie - UIA podporuje Qt 5.7+
                var element = AutomationElement.FromPoint(
                    new System.Windows.Point(ctx.ClickPoint.X, ctx.ClickPoint.Y));

                if (element != null)
                {
                    return ExtractElementInfo(element, ctx.ClickPoint.X, ctx.ClickPoint.Y);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Qt] Detection failed: {ex.Message}");
            }

            return GetBasicWindowInfo(ctx.ClickPoint.X, ctx.ClickPoint.Y);
        }

        private static UiDetectionContext BuildDetectionContext(int x, int y)
        {
            var hwnd = WindowFromPoint(new POINT { x = x, y = y });
            var className = GetClassName(hwnd);

            GetWindowThreadProcessId(hwnd, out uint processId);
            var processName = "";
            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    processName = process.ProcessName;
                }
            }
            catch { }

            return new UiDetectionContext
            {
                Hwnd = hwnd,
                ClickPoint = new System.Drawing.Point(x, y),
                ClassName = className,
                ProcessName = processName,
                Framework = DetectFramework(hwnd)
            };
        }
    }
}
