using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using AutomationCondition = System.Windows.Automation.Condition; // Explicitný alias

namespace AppCommander.W7_11.WPF.Core
{
    public static class AdaptiveElementFinder
    {
        /// <summary>
        /// Inteligentne nájde UI element s viacerými fallback metódami
        /// </summary>
        public static ElementSearchResult SmartFindElement(IntPtr windowHandle, Command command)
        {
            var result = new ElementSearchResult();

            if (windowHandle == IntPtr.Zero)
            {
                result.ErrorMessage = "Invalid window handle";
                return result;
            }

            try
            {
                AutomationElement window = AutomationElement.FromHandle(windowHandle);
                if (window == null)
                {
                    result.ErrorMessage = "Cannot access window automation";
                    return result;
                }

                // Pokus 1: Presný match podľa AutomationId
                if (!string.IsNullOrEmpty(command.ElementId))
                {
                    var element = FindByAutomationId(window, command.ElementId);
                    if (element != null)
                    {
                        result = CreateSuccessResult(element, "AutomationId", 0.95);
                        return result;
                    }
                }

                // Pokus 2: Presný match podľa Name
                if (!string.IsNullOrEmpty(command.ElementName))
                {
                    var element = FindByName(window, command.ElementName);
                    if (element != null)
                    {
                        result = CreateSuccessResult(element, "Name", 0.90);
                        return result;
                    }
                }

                // Pokus 3: Fuzzy match podľa podobného názvu
                if (!string.IsNullOrEmpty(command.ElementName))
                {
                    var element = FindBySimilarName(window, command.ElementName);
                    if (element != null)
                    {
                        result = CreateSuccessResult(element, "Similar Name", 0.75);
                        return result;
                    }
                }

                // Pokus 4: Podľa typu control a pozície
                if (!string.IsNullOrEmpty(command.ElementControlType) &&
                    command.ElementX > 0 && command.ElementY > 0)
                {
                    var element = FindByTypeAndPosition(window, command.ElementControlType,
                        command.ElementX, command.ElementY);
                    if (element != null)
                    {
                        result = CreateSuccessResult(element, "Type + Position", 0.70);
                        return result;
                    }
                }

                // Pokus 5: Iba podľa pozície (široká tolerancia)
                if (command.ElementX > 0 && command.ElementY > 0)
                {
                    var element = FindByPosition(window, command.ElementX, command.ElementY, 50); // 50px tolerancia
                    if (element != null)
                    {
                        result = CreateSuccessResult(element, "Position with tolerance", 0.60);
                        return result;
                    }
                }

                // Pokus 6: Podľa class name a indexu
                if (!string.IsNullOrEmpty(command.ElementClass))
                {
                    var element = FindByClassNameAndIndex(window, command.ElementClass);
                    if (element != null)
                    {
                        result = CreateSuccessResult(element, "ClassName", 0.50);
                        return result;
                    }
                }

                result.ErrorMessage = "Element not found with any method";
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Search failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Nájde všetky interaktívne elementy v okne pre aktualizáciu príkazov
        /// </summary>
        public static List<UIElementInfo> GetAllInteractiveElements(IntPtr windowHandle)
        {
            var elements = new List<UIElementInfo>();

            try
            {
                AutomationElement window = AutomationElement.FromHandle(windowHandle);
                if (window == null) return elements;

                // Hľadaj tlačidlá, textboxy, combo boxy, atď.
                var conditions = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)
                );

                var foundElements = window.FindAll(TreeScope.Descendants, conditions);

                foreach (AutomationElement element in foundElements)
                {
                    if (element.Current.IsEnabled && !element.Current.IsOffscreen)
                    {
                        var rect = element.Current.BoundingRectangle;
                        if (rect.Width > 0 && rect.Height > 0)
                        {
                            elements.Add(new UIElementInfo
                            {
                                Name = element.Current.Name ?? string.Empty,
                                AutomationId = GetProperty(element, AutomationElement.AutomationIdProperty),
                                ClassName = GetProperty(element, AutomationElement.ClassNameProperty),
                                ControlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown",
                                X = (int)(rect.X + rect.Width / 2),
                                Y = (int)(rect.Y + rect.Height / 2),
                                BoundingRectangle = rect,
                                IsEnabled = element.Current.IsEnabled,
                                IsVisible = !element.Current.IsOffscreen,
                                AutomationElement = element
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting interactive elements: {ex.Message}");
            }

            return elements;
        }

        /// <summary>
        /// Aktualizuje staré príkazy na základe aktuálneho stavu okna
        /// </summary>
        public static void UpdateCommandsForCurrentWindow(IntPtr windowHandle, List<Command> commands)
        {
            var currentElements = GetAllInteractiveElements(windowHandle);

            foreach (var command in commands)
            {
                if (command.Type == CommandType.Click || command.Type == CommandType.SetText)
                {
                    var result = SmartFindElement(windowHandle, command);
                    if (result.IsSuccess && result.Element != null)
                    {
                        // Aktualizuj pozíciu a identifikátory
                        command.ElementX = result.Element.X;
                        command.ElementY = result.Element.Y;
                        command.ElementId = result.Element.AutomationId;
                        command.ElementClass = result.Element.ClassName;
                        command.ElementControlType = result.Element.ControlType;
                    }
                }
            }
        }

        private static AutomationElement FindByAutomationId(AutomationElement parent, string automationId)
        {
            try
            {
                var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
                return parent.FindFirst(TreeScope.Descendants, condition);
            }
            catch
            {
                return null;
            }
        }

        private static AutomationElement FindByName(AutomationElement parent, string name)
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

        private static AutomationElement FindBySimilarName(AutomationElement parent, string targetName)
        {
            try
            {
                var allElements = parent.FindAll(TreeScope.Descendants, AutomationCondition.TrueCondition);

                foreach (AutomationElement element in allElements)
                {
                    string elementName = element.Current.Name;
                    if (!string.IsNullOrEmpty(elementName))
                    {
                        // Fuzzy matching - obsahuje časť názvu (.NET Framework 4.8 kompatibilné)
                        if (elementName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            targetName.IndexOf(elementName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return element;
                        }

                        // Levenshtein distance pre podobnosť
                        if (CalculateSimilarity(elementName, targetName) > 0.7)
                        {
                            return element;
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj chyby
            }

            return null;
        }

        private static AutomationElement FindByTypeAndPosition(AutomationElement parent, string controlType, int x, int y)
        {
            try
            {
                var allElements = parent.FindAll(TreeScope.Descendants, AutomationCondition.TrueCondition);

                foreach (AutomationElement element in allElements)
                {
                    if (element.Current.ControlType?.LocalizedControlType == controlType)
                    {
                        var rect = element.Current.BoundingRectangle;
                        int centerX = (int)(rect.X + rect.Width / 2);
                        int centerY = (int)(rect.Y + rect.Height / 2);

                        // Tolerancia 20 pixelov
                        if (Math.Abs(centerX - x) <= 20 && Math.Abs(centerY - y) <= 20)
                        {
                            return element;
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj chyby
            }

            return null;
        }

        private static AutomationElement FindByPosition(AutomationElement parent, int x, int y, int tolerance)
        {
            try
            {
                var point = new System.Windows.Point(x, y);
                return AutomationElement.FromPoint(point);
            }
            catch
            {
                return null;
            }
        }

        private static AutomationElement FindByClassNameAndIndex(AutomationElement parent, string className)
        {
            try
            {
                var condition = new PropertyCondition(AutomationElement.ClassNameProperty, className);
                return parent.FindFirst(TreeScope.Descendants, condition);
            }
            catch
            {
                return null;
            }
        }

        private static ElementSearchResult CreateSuccessResult(AutomationElement element, string method, double confidence)
        {
            var rect = element.Current.BoundingRectangle;

            return new ElementSearchResult
            {
                IsSuccess = true,
                Element = new UIElementInfo
                {
                    Name = element.Current.Name ?? string.Empty,
                    AutomationId = GetProperty(element, AutomationElement.AutomationIdProperty),
                    ClassName = GetProperty(element, AutomationElement.ClassNameProperty),
                    ControlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown",
                    X = (int)(rect.X + rect.Width / 2),
                    Y = (int)(rect.Y + rect.Height / 2),
                    BoundingRectangle = rect,
                    IsEnabled = element.Current.IsEnabled,
                    IsVisible = !element.Current.IsOffscreen,
                    AutomationElement = element
                },
                SearchMethod = method,
                Confidence = confidence
            };
        }

        private static string GetProperty(AutomationElement element, AutomationProperty property)
        {
            try
            {
                object value = element.GetCurrentPropertyValue(property);
                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0;

            int maxLength = Math.Max(s1.Length, s2.Length);
            int distance = CalculateLevenshteinDistance(s1.ToLower(), s2.ToLower());

            return 1.0 - (double)distance / maxLength;
        }

        private static int CalculateLevenshteinDistance(string s1, string s2)
        {
            int[,] matrix = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                    matrix[i, j] = Math.Min(Math.Min(
                        matrix[i - 1, j] + 1,      // deletion
                        matrix[i, j - 1] + 1),     // insertion
                        matrix[i - 1, j - 1] + cost); // substitution
                }
            }

            return matrix[s1.Length, s2.Length];
        }
    }

    public class ElementSearchResult
    {
        public bool IsSuccess { get; set; }
        public UIElementInfo Element { get; set; }
        public string SearchMethod { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public override string ToString()
        {
            return IsSuccess
                ? $"Found via {SearchMethod} (confidence: {Confidence:P0})"
                : $"Failed: {ErrorMessage}";
        }
    }
}
