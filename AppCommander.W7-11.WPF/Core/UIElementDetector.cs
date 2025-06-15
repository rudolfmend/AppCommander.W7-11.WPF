using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Collections.Generic;

namespace AppCommander.W7_11.WPF.Core
{
    public static class UIElementDetector
    {
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
                System.Diagnostics.Debug.WriteLine($"Error detecting UI element: {ex.Message}");
            }

            // Fallback - get basic window info
            return GetBasicWindowInfo(x, y);
        }

        private static UIElementInfo ExtractElementInfo(AutomationElement element, int x, int y)
        {
            try
            {
                var info = new UIElementInfo
                {
                    X = x,
                    Y = y,
                    BoundingRectangle = element.Current.BoundingRectangle,
                    IsEnabled = element.Current.IsEnabled,
                    IsVisible = !element.Current.IsOffscreen,
                    ProcessId = element.Current.ProcessId,
                    WindowHandle = new IntPtr(element.Current.NativeWindowHandle),
                    AutomationElement = element
                };

                // Získaj všetky možné identifikátory
                string name = GetElementProperty(element, AutomationElement.NameProperty);
                string automationId = GetElementProperty(element, AutomationElement.AutomationIdProperty);
                string className = GetElementProperty(element, AutomationElement.ClassNameProperty);
                string controlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown";
                string helpText = GetElementProperty(element, AutomationElement.HelpTextProperty);
                string accessKey = GetElementProperty(element, AutomationElement.AccessKeyProperty);

                // Skús získať text z elementu
                string elementText = GetElementText(element);

                // Skús získať placeholder text
                string placeholderText = GetPlaceholderText(element);

                // **Špeciálne spracovanie pre WinUI3**
                if (className == "Microsoft.UI.Content.DesktopChildSiteBridge")
                {
                    var winui3Info = ProcessWinUI3Element(element, x, y);
                    if (winui3Info != null)
                    {
                        // Použij lepšie informácie z WinUI3 analýzy
                        name = winui3Info.Name;
                        automationId = winui3Info.AutomationId;
                        controlType = winui3Info.ControlType;
                        elementText = winui3Info.ElementText;
                        placeholderText = winui3Info.PlaceholderText;
                        helpText = winui3Info.HelpText;
                    }
                }

                // Vytvor zmysluplný názov elementu
                info.Name = CreateMeaningfulName(name, automationId, elementText, placeholderText, controlType, helpText);
                info.AutomationId = automationId;
                info.ClassName = className;
                info.ControlType = controlType;

                // Pridaj extra informácie pre lepšiu identifikáciu
                info.ElementText = elementText;
                info.PlaceholderText = placeholderText;
                info.HelpText = helpText;
                info.AccessKey = accessKey;

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting element info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Špeciálne spracovanie pre WinUI3 DesktopChildSiteBridge elementy
        /// </summary>
        private static UIElementInfo ProcessWinUI3Element(AutomationElement bridgeElement, int clickX, int clickY)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== WinUI3 BRIDGE ANALYSIS ===");

                // 1. Skús nájsť child elementy v bridge
                var children = bridgeElement.FindAll(TreeScope.Children, Condition.TrueCondition);
                System.Diagnostics.Debug.WriteLine($"Bridge has {children.Count} children");

                foreach (AutomationElement child in children)
                {
                    var childInfo = AnalyzeWinUI3Child(child, clickX, clickY);
                    if (childInfo != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found meaningful child: {childInfo.Name}");
                        return childInfo;
                    }
                }

                // 2. Skús nájsť descendants (hlbšie vnorenné elementy)
                var descendants = bridgeElement.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                System.Diagnostics.Debug.WriteLine($"Bridge has {descendants.Count} descendants");

                AutomationElement bestMatch = null;
                double bestDistance = double.MaxValue;
                UIElementInfo bestInfo = null;

                foreach (AutomationElement descendant in descendants)
                {
                    try
                    {
                        var rect = descendant.Current.BoundingRectangle;
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
                                            System.Diagnostics.Debug.WriteLine($"Found exact match in bounds: {childInfo.Name}");
                                            break; // Exact match v bounds
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error analyzing descendant: {ex.Message}");
                    }
                }

                if (bestInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Best WinUI3 match: {bestInfo.Name} (distance: {bestDistance:F1})");
                    return bestInfo;
                }

                // 3. Ak nič nenašiel, skús pattern-based detection
                return DetectByPatternsWinUI3(bridgeElement, clickX, clickY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing WinUI3 element: {ex.Message}");
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
                string controlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown";
                string helpText = GetElementProperty(element, AutomationElement.HelpTextProperty);

                // Získaj text obsah
                string elementText = GetElementText(element);
                string placeholderText = GetPlaceholderText(element);

                System.Diagnostics.Debug.WriteLine($"  Child: Name='{name}', Id='{automationId}', Class='{className}', Type='{controlType}', Text='{elementText}'");

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
                        BoundingRectangle = element.Current.BoundingRectangle,
                        IsEnabled = element.Current.IsEnabled,
                        IsVisible = !element.Current.IsOffscreen,
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

        private static UIElementInfo DetectByPatternsWinUI3(AutomationElement element, int clickX, int clickY)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Trying pattern-based detection for WinUI3...");

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
                        System.Diagnostics.Debug.WriteLine($"Error checking pattern {pattern.ProgrammaticName}: {ex.Message}");
                    }
                }

                if (detectedType != "unknown")
                {
                    string name = !string.IsNullOrEmpty(detectedText) ?
                        $"{detectedType}_{CleanName(detectedText)}" :
                        $"{detectedType}_at_{clickX}_{clickY}";

                    System.Diagnostics.Debug.WriteLine($"Pattern-based detection found: {name}");

                    return new UIElementInfo
                    {
                        Name = name,
                        ControlType = detectedType,
                        ElementText = detectedText,
                        X = clickX,
                        Y = clickY,
                        BoundingRectangle = element.Current.BoundingRectangle,
                        IsEnabled = element.Current.IsEnabled,
                        IsVisible = !element.Current.IsOffscreen,
                        AutomationElement = element,
                        ClassName = "Microsoft.UI.Content.DesktopChildSiteBridge"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in pattern-based detection: {ex.Message}");
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

            // Opravené pre .NET Framework 4.8
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

        private static string GetElementText(AutomationElement element)
        {
            try
            {
                // Attempt to retrieve text using ValuePattern
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                {
                    var value = valuePattern as ValuePattern;
                    if (value != null && !string.IsNullOrWhiteSpace(value.Current.Value))
                        return value.Current.Value;
                }

                // Attempt to retrieve text using TextPattern
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out object textPattern))
                {
                    var text = textPattern as TextPattern;
                    var documentRange = text?.DocumentRange;
                    if (documentRange != null)
                    {
                        string textValue = documentRange.GetText(-1);
                        if (!string.IsNullOrWhiteSpace(textValue))
                            return textValue.Trim();
                    }
                }

                // Fallback to Name property if it resembles text
                string name = element.Current.Name;
                if (!string.IsNullOrWhiteSpace(name) && name.Length < 50)
                    return name;

                return "";
            }
            catch
            {
                return "";
            }
        }

        private static string GetPlaceholderText(AutomationElement element)
        {
            try
            {
                // Skús rôzne property pre placeholder
                var helpText = GetElementProperty(element, AutomationElement.HelpTextProperty);
                if (!string.IsNullOrWhiteSpace(helpText) && helpText.Length < 50)
                    return helpText;

                // Pre .NET Framework 4.8 - použij ItemStatusProperty namiesto LocalizedDescriptionProperty
                var itemStatus = GetElementProperty(element, AutomationElement.ItemStatusProperty);
                if (!string.IsNullOrWhiteSpace(itemStatus) && itemStatus.Length < 50)
                    return itemStatus;

                return "";
            }
            catch
            {
                return "";
            }
        }

        private static void LogElementDetails(UIElementInfo element)
        {
            System.Diagnostics.Debug.WriteLine("=== ELEMENT DETECTION DETAILS ===");
            System.Diagnostics.Debug.WriteLine($"Name: '{element.Name}'");
            System.Diagnostics.Debug.WriteLine($"AutomationId: '{element.AutomationId}'");
            System.Diagnostics.Debug.WriteLine($"ClassName: '{element.ClassName}'");
            System.Diagnostics.Debug.WriteLine($"ControlType: '{element.ControlType}'");
            System.Diagnostics.Debug.WriteLine($"ElementText: '{element.ElementText}'");
            System.Diagnostics.Debug.WriteLine($"PlaceholderText: '{element.PlaceholderText}'");
            System.Diagnostics.Debug.WriteLine($"HelpText: '{element.HelpText}'");
            System.Diagnostics.Debug.WriteLine($"Position: ({element.X}, {element.Y})");
            System.Diagnostics.Debug.WriteLine($"Enabled: {element.IsEnabled}, Visible: {element.IsVisible}");
            System.Diagnostics.Debug.WriteLine("=====================================");
        }

        private static UIElementInfo GetBasicWindowInfo(int x, int y)
        {
            IntPtr hwnd = WindowFromPoint(new POINT { x = x, y = y });

            if (hwnd != IntPtr.Zero)
            {
                string className = GetClassName(hwnd);
                string windowText = GetWindowText(hwnd);
                RECT rect;
                GetWindowRect(hwnd, out rect);

                return new UIElementInfo
                {
                    Name = !string.IsNullOrWhiteSpace(windowText) ? $"Window_{CleanName(windowText)}" : "Unknown_Window",
                    ClassName = className,
                    ControlType = "Window",
                    X = x,
                    Y = y,
                    BoundingRectangle = new System.Windows.Rect(rect.Left, rect.Top,
                        rect.Right - rect.Left, rect.Bottom - rect.Top),
                    WindowHandle = hwnd,
                    IsEnabled = IsWindowEnabled(hwnd),
                    IsVisible = IsWindowVisible(hwnd)
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
                System.Diagnostics.Debug.WriteLine($"Error finding element by name: {ex.Message}");
                return null;
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

        private static AutomationElement FindByPartialName(AutomationElement parent, string name)
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

        private static string GetElementProperty(AutomationElement element, AutomationProperty property)
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

        /// <summary>
        /// Získa vlastnosť automation elementu
        /// </summary>
        /// <param name="element">Automation element</param>
        /// <param name="property">Vlastnosť na získanie</param>
        /// <returns>Hodnota vlastnosti alebo prázdny string</returns>
        private static string GetProperty(AutomationElement element, AutomationProperty property)
        {
            try
            {
                if (element == null || property == null)
                    return "";

                object value = element.GetCurrentPropertyValue(property);
                return value?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting property {property.ProgrammaticName}: {ex.Message}");
                return "";
            }
        }

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
                System.Diagnostics.Debug.WriteLine($"Error converting AutomationElement: {ex.Message}");
                return null;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
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

    } // KONIEC UIElementDetector triedy

    public class UIElementInfo
    {
        public string Name { get; set; } = "";
        public string AutomationId { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string ControlType { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public System.Windows.Rect BoundingRectangle { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
        public int ProcessId { get; set; }
        public IntPtr WindowHandle { get; set; }
        public AutomationElement AutomationElement { get; set; }

        // Properties pre lepšiu identifikáciu
        public string ElementText { get; set; } = "";
        public string PlaceholderText { get; set; } = "";
        public string HelpText { get; set; } = "";
        public string AccessKey { get; set; } = "";

        public string GetUniqueIdentifier()
        {
            // Priorita identifikátorov
            if (!string.IsNullOrEmpty(AutomationId))
                return $"AutoId_{AutomationId}";

            if (!string.IsNullOrEmpty(Name))
                return $"Name_{Name}";

            if (!string.IsNullOrEmpty(ElementText))
                return $"Text_{ElementText}";

            return $"Class_{ClassName}_Pos_{X}_{Y}";
        }

        public override string ToString()
        {
            return $"{ControlType}: {Name} ({ClassName}) at ({X}, {Y})";
        }
    }
}
