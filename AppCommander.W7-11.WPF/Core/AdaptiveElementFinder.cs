using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using AutomationCondition = System.Windows.Automation.Condition;

namespace AppCommander.W7_11.WPF.Core
{
    public static class AdaptiveElementFinder
    {
        /// <summary>
        /// Inteligentne nájde UI element s viacerými fallback metódami a lepším error handlingom
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

                System.Diagnostics.Debug.WriteLine($"Searching for element: '{command.ElementName}' (ID: '{command.ElementId}', Class: '{command.ElementClass}')");

                // **Špeciálne spracovanie pre WinUI3 elementy**
                if (command.ElementClass == "Microsoft.UI.Content.DesktopChildSiteBridge")
                {
                    var winui3Result = FindWinUI3Element(window, command);
                    if (winui3Result.IsSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found via WinUI3 method: {winui3Result}");
                        return winui3Result;
                    }
                }

                // Pokus 1: Presný match podľa AutomationId (najspoľahlivejší)
                if (!string.IsNullOrEmpty(command.ElementId))
                {
                    System.Diagnostics.Debug.WriteLine($"Trying AutomationId: '{command.ElementId}'");
                    var element = FindByAutomationId(window, command.ElementId);
                    if (element != null && ValidateElement(element))
                    {
                        result = CreateSuccessResult(element, "AutomationId", 0.95);
                        System.Diagnostics.Debug.WriteLine($"Found via AutomationId: {result.Element}");
                        return result;
                    }
                }

                // Pokus 2: Presný match podľa Name
                if (!string.IsNullOrEmpty(command.ElementName) &&
                    !command.ElementName.StartsWith("Key_") &&
                    !command.ElementName.StartsWith("Click_at_") &&
                    !command.ElementName.StartsWith("Element_at_"))
                {
                    System.Diagnostics.Debug.WriteLine($"Trying Name: '{command.ElementName}'");
                    var element = FindByName(window, command.ElementName);
                    if (element != null && ValidateElement(element))
                    {
                        result = CreateSuccessResult(element, "Name", 0.90);
                        System.Diagnostics.Debug.WriteLine($"Found via Name: {result.Element}");
                        return result;
                    }
                }

                // Pokus 3: Kombinácia typu control a pozície (veľmi presné)
                if (!string.IsNullOrEmpty(command.ElementControlType) &&
                    command.ElementX > 0 && command.ElementY > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Trying ControlType + Position: '{command.ElementControlType}' at ({command.ElementX}, {command.ElementY})");
                    var element = FindByTypeAndPosition(window, command.ElementControlType,
                        command.ElementX, command.ElementY, 30); // 30px tolerance
                    if (element != null && ValidateElement(element))
                    {
                        result = CreateSuccessResult(element, "ControlType + Position", 0.85);
                        System.Diagnostics.Debug.WriteLine($"Found via ControlType + Position: {result.Element}");
                        return result;
                    }
                }

                // Pokus 4: Fuzzy match podľa podobného názvu
                if (!string.IsNullOrEmpty(command.ElementName) &&
                    !command.ElementName.StartsWith("Key_") &&
                    !command.ElementName.StartsWith("Click_at_") &&
                    !command.ElementName.StartsWith("Element_at_"))
                {
                    System.Diagnostics.Debug.WriteLine($"Trying Similar Name: '{command.ElementName}'");
                    var element = FindBySimilarName(window, command.ElementName);
                    if (element != null && ValidateElement(element))
                    {
                        result = CreateSuccessResult(element, "Similar Name", 0.75);
                        System.Diagnostics.Debug.WriteLine($"Found via Similar Name: {result.Element}");
                        return result;
                    }
                }

                // Pokus 5: Presná pozícia (malá tolerancia)
                if (command.ElementX > 0 && command.ElementY > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Trying Exact Position: ({command.ElementX}, {command.ElementY})");
                    var element = FindByPosition(window, command.ElementX, command.ElementY, 10); // 10px tolerance
                    if (element != null && ValidateElement(element))
                    {
                        result = CreateSuccessResult(element, "Exact Position", 0.70);
                        System.Diagnostics.Debug.WriteLine($"Found via Exact Position: {result.Element}");
                        return result;
                    }
                }

                // Pokus 6: Širšia pozičná tolerancia
                if (command.ElementX > 0 && command.ElementY > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Trying Position with tolerance: ({command.ElementX}, {command.ElementY})");
                    var element = FindByPosition(window, command.ElementX, command.ElementY, 50); // 50px tolerance
                    if (element != null && ValidateElement(element))
                    {
                        result = CreateSuccessResult(element, "Position with tolerance", 0.60);
                        System.Diagnostics.Debug.WriteLine($"Found via Position with tolerance: {result.Element}");
                        return result;
                    }
                }

                // Pokus 7: Podľa class name
                if (!string.IsNullOrEmpty(command.ElementClass))
                {
                    System.Diagnostics.Debug.WriteLine($"Trying ClassName: '{command.ElementClass}'");
                    var element = FindByClassName(window, command.ElementClass, command.ElementX, command.ElementY);
                    if (element != null && ValidateElement(element))
                    {
                        result = CreateSuccessResult(element, "ClassName", 0.50);
                        System.Diagnostics.Debug.WriteLine($"Found via ClassName: {result.Element}");
                        return result;
                    }
                }

                result.ErrorMessage = "Element not found with any method";
                System.Diagnostics.Debug.WriteLine($"Element not found: {result.ErrorMessage}");
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Search failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Search exception: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Špeciálna metóda pre vyhľadávanie WinUI3 elementov
        /// </summary>
        private static ElementSearchResult FindWinUI3Element(AutomationElement window, Command command)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== WinUI3 SEARCH ===");
                System.Diagnostics.Debug.WriteLine($"Looking for: '{command.ElementName}' at ({command.OriginalX}, {command.OriginalY})");

                // 1. Nájdi všetky DesktopChildSiteBridge elementy
                var bridgeCondition = new PropertyCondition(AutomationElement.ClassNameProperty,
                    "Microsoft.UI.Content.DesktopChildSiteBridge");
                var bridges = window.FindAll(TreeScope.Descendants, bridgeCondition);

                System.Diagnostics.Debug.WriteLine($"Found {bridges.Count} WinUI3 bridges");

                AutomationElement bestElement = null;
                double bestScore = 0;
                string bestMethod = "";

                foreach (AutomationElement bridge in bridges)
                {
                    var result = AnalyzeWinUI3Bridge(bridge, command);
                    if (result.Score > bestScore)
                    {
                        bestScore = result.Score;
                        bestElement = result.Element;
                        bestMethod = result.Method;
                    }
                }

                if (bestElement != null && bestScore > 0.5)
                {
                    return CreateSuccessResult(bestElement, $"WinUI3-{bestMethod}", bestScore);
                }

                // 2. Fallback - pozičný mapping
                return FindWinUI3ByPositionalMapping(window, command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WinUI3 search error: {ex.Message}");
                return new ElementSearchResult { ErrorMessage = $"WinUI3 search failed: {ex.Message}" };
            }
        }

        private static WinUI3AnalysisResult AnalyzeWinUI3Bridge(AutomationElement bridge, Command command)
        {
            var result = new WinUI3AnalysisResult();

            try
            {
                var bridgeRect = bridge.Current.BoundingRectangle;

                // Kontroluj, či je originálna pozícia v rámci tohto bridge
                bool isInBridge = command.OriginalX >= bridgeRect.X &&
                                 command.OriginalX <= bridgeRect.X + bridgeRect.Width &&
                                 command.OriginalY >= bridgeRect.Y &&
                                 command.OriginalY <= bridgeRect.Y + bridgeRect.Height;

                if (!isInBridge)
                {
                    // Vypočítaj vzdialenosť od bridge
                    double centerX = bridgeRect.X + bridgeRect.Width / 2;
                    double centerY = bridgeRect.Y + bridgeRect.Height / 2;
                    double distance = Math.Sqrt(Math.Pow(centerX - command.OriginalX, 2) +
                                              Math.Pow(centerY - command.OriginalY, 2));

                    if (distance > 100) // Príliš ďaleko
                        return result;
                }

                // Analyzuj obsah bridge
                var descendants = bridge.FindAll(TreeScope.Descendants, Condition.TrueCondition);

                foreach (AutomationElement descendant in descendants)
                {
                    var score = ScoreWinUI3Element(descendant, command);
                    if (score > result.Score)
                    {
                        result.Score = score;
                        result.Element = descendant;
                        result.Method = GetScoringMethod(descendant, command);
                    }
                }

                // Ak nenašiel zmysluplný element, skús pattern-based prístup
                if (result.Score < 0.3)
                {
                    var patternResult = AnalyzeWinUI3Patterns(bridge, command);
                    if (patternResult.Score > result.Score)
                    {
                        result = patternResult;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bridge analysis error: {ex.Message}");
                return result;
            }
        }

        private static double ScoreWinUI3Element(AutomationElement element, Command command)
        {
            try
            {
                double score = 0;

                // Základná validácia
                if (!ValidateElement(element))
                    return 0;

                string name = element.Current.Name ?? "";
                string automationId = GetProperty(element, AutomationElement.AutomationIdProperty);
                string controlType = element.Current.ControlType?.LocalizedControlType ?? "";

                // Score za name match
                if (!string.IsNullOrEmpty(command.ElementName) && !string.IsNullOrEmpty(name))
                {
                    if (name.Equals(command.ElementName, StringComparison.OrdinalIgnoreCase))
                        score += 0.8;
                    else if (name.IndexOf(command.ElementName, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 0.5;
                    else if (command.ElementName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 0.4;
                }

                // Score za AutomationId match
                if (!string.IsNullOrEmpty(command.ElementId) && !string.IsNullOrEmpty(automationId))
                {
                    if (automationId.Equals(command.ElementId, StringComparison.OrdinalIgnoreCase))
                        score += 0.9;
                }

                // Score za control type match
                if (!string.IsNullOrEmpty(command.ElementControlType) && !string.IsNullOrEmpty(controlType))
                {
                    if (controlType.Equals(command.ElementControlType, StringComparison.OrdinalIgnoreCase))
                        score += 0.3;
                }

                // Score za pozíciu
                var rect = element.Current.BoundingRectangle;
                if (rect.Width > 0 && rect.Height > 0)
                {
                    double centerX = rect.X + rect.Width / 2;
                    double centerY = rect.Y + rect.Height / 2;
                    double distance = Math.Sqrt(Math.Pow(centerX - command.OriginalX, 2) +
                                              Math.Pow(centerY - command.OriginalY, 2));

                    // Čím bližšie, tým vyšší score
                    if (distance <= 10) score += 0.4;
                    else if (distance <= 30) score += 0.3;
                    else if (distance <= 50) score += 0.2;
                    else if (distance <= 100) score += 0.1;

                    // Bonus ak je klik v rámci elementu
                    if (command.OriginalX >= rect.X && command.OriginalX <= rect.X + rect.Width &&
                        command.OriginalY >= rect.Y && command.OriginalY <= rect.Y + rect.Height)
                    {
                        score += 0.3;
                    }
                }

                // Bonus za interaktívne elementy
                var patterns = element.GetSupportedPatterns();
                if (patterns.Contains(InvokePattern.Pattern) ||
                    patterns.Contains(ValuePattern.Pattern) ||
                    patterns.Contains(TogglePattern.Pattern))
                {
                    score += 0.2;
                }

                return Math.Min(score, 1.0); // Cap na 1.0
            }
            catch
            {
                return 0;
            }
        }

        private static string GetScoringMethod(AutomationElement element, Command command)
        {
            string name = element.Current.Name ?? "";
            string automationId = GetProperty(element, AutomationElement.AutomationIdProperty);

            if (!string.IsNullOrEmpty(automationId) &&
                automationId.Equals(command.ElementId, StringComparison.OrdinalIgnoreCase))
                return "AutomationId";

            if (!string.IsNullOrEmpty(name) &&
                name.Equals(command.ElementName, StringComparison.OrdinalIgnoreCase))
                return "ExactName";

            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf(command.ElementName, StringComparison.OrdinalIgnoreCase) >= 0)
                return "PartialName";

            return "Position";
        }

        private static WinUI3AnalysisResult AnalyzeWinUI3Patterns(AutomationElement bridge, Command command)
        {
            var result = new WinUI3AnalysisResult();

            try
            {
                // Analyzuj patterns na bridge elementu
                var patterns = bridge.GetSupportedPatterns();

                foreach (var pattern in patterns)
                {
                    if (pattern == ValuePattern.Pattern && command.Type == CommandType.KeyPress)
                    {
                        result.Score = 0.6;
                        result.Element = bridge;
                        result.Method = "ValuePattern";
                        break;
                    }
                    else if (pattern == InvokePattern.Pattern && command.Type == CommandType.Click)
                    {
                        result.Score = 0.5;
                        result.Element = bridge;
                        result.Method = "InvokePattern";
                        break;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pattern analysis error: {ex.Message}");
                return result;
            }
        }

        private static ElementSearchResult FindWinUI3ByPositionalMapping(AutomationElement window, Command command)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Trying WinUI3 positional mapping...");

                // Získaj všetky WinUI3 bridge elementy
                var bridgeCondition = new PropertyCondition(AutomationElement.ClassNameProperty,
                    "Microsoft.UI.Content.DesktopChildSiteBridge");
                var bridges = window.FindAll(TreeScope.Descendants, bridgeCondition);

                // Mapovacie tabuľky pre rôzne typy UI elementov
                var elementMapping = CreateWinUI3ElementMapping(bridges, command);

                AutomationElement bestMatch = null;
                double bestDistance = double.MaxValue;

                foreach (var mapping in elementMapping)
                {
                    var rect = mapping.Current.BoundingRectangle;
                    double centerX = rect.X + rect.Width / 2;
                    double centerY = rect.Y + rect.Height / 2;
                    double distance = Math.Sqrt(Math.Pow(centerX - command.OriginalX, 2) +
                                              Math.Pow(centerY - command.OriginalY, 2));

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestMatch = mapping;
                    }
                }

                if (bestMatch != null && bestDistance <= 100) // 100px tolerancia
                {
                    System.Diagnostics.Debug.WriteLine($"Found WinUI3 element via positional mapping at distance {bestDistance:F1}");
                    return CreateSuccessResult(bestMatch, "WinUI3-PositionalMapping",
                        Math.Max(0.3, 1.0 - (bestDistance / 200.0)));
                }

                return new ElementSearchResult { ErrorMessage = "No suitable WinUI3 element found" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Positional mapping error: {ex.Message}");
                return new ElementSearchResult { ErrorMessage = $"Positional mapping failed: {ex.Message}" };
            }
        }

        private static List<AutomationElement> CreateWinUI3ElementMapping(AutomationElementCollection bridges, Command command)
        {
            var mapping = new List<AutomationElement>();

            foreach (AutomationElement bridge in bridges)
            {
                try
                {
                    // Pridaj bridge ak je interaktívny
                    var patterns = bridge.GetSupportedPatterns();
                    if (patterns.Length > 0)
                    {
                        mapping.Add(bridge);
                    }

                    // Pridaj všetky descendant elementy s užitočnými properties
                    var descendants = bridge.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    foreach (AutomationElement descendant in descendants)
                    {
                        if (IsUsefulWinUI3Element(descendant))
                        {
                            mapping.Add(descendant);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error mapping bridge: {ex.Message}");
                }
            }

            return mapping;
        }

        private static bool IsUsefulWinUI3Element(AutomationElement element)
        {
            try
            {
                if (!ValidateElement(element))
                    return false;

                // Element s neprázdnym name alebo automationId
                string name = element.Current.Name ?? "";
                string automationId = GetProperty(element, AutomationElement.AutomationIdProperty);

                if (!string.IsNullOrEmpty(name) && name.Length > 2)
                    return true;

                if (!string.IsNullOrEmpty(automationId) && automationId.Length > 2)
                    return true;

                // Element s užitočnými patterns
                var patterns = element.GetSupportedPatterns();
                if (patterns.Contains(ValuePattern.Pattern) ||
                    patterns.Contains(InvokePattern.Pattern) ||
                    patterns.Contains(TogglePattern.Pattern) ||
                    patterns.Contains(SelectionItemPattern.Pattern))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validuje, či je element použiteľný
        /// </summary>
        private static bool ValidateElement(AutomationElement element)
        {
            try
            {
                if (element == null) return false;

                // Základné kontroly
                var current = element.Current;
                if (current.IsOffscreen) return false;

                // Kontrola veľkosti (element musí mať rozumné rozmery)
                var rect = current.BoundingRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return false;
                if (rect.Width > 2000 || rect.Height > 2000) return false; // Príliš veľký

                return true;
            }
            catch
            {
                return false;
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

                // **Špeciálne spracovanie pre WinUI3**
                var winui3Elements = GetWinUI3InteractiveElements(window);
                elements.AddRange(winui3Elements);

                // Hľadaj štandardné interaktívne elementy
                var conditions = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
                );

                var foundElements = window.FindAll(TreeScope.Descendants, conditions);

                foreach (AutomationElement element in foundElements)
                {
                    if (ValidateElement(element) &&
                        element.Current.ClassName != "Microsoft.UI.Content.DesktopChildSiteBridge")
                    {
                        var rect = element.Current.BoundingRectangle;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting interactive elements: {ex.Message}");
            }

            return elements;
        }

        private static List<UIElementInfo> GetWinUI3InteractiveElements(AutomationElement window)
        {
            var elements = new List<UIElementInfo>();

            try
            {
                // Nájdi všetky WinUI3 bridge elementy
                var bridgeCondition = new PropertyCondition(AutomationElement.ClassNameProperty,
                    "Microsoft.UI.Content.DesktopChildSiteBridge");
                var bridges = window.FindAll(TreeScope.Descendants, bridgeCondition);

                foreach (AutomationElement bridge in bridges)
                {
                    var bridgeElements = AnalyzeBridgeForInteractiveElements(bridge);
                    elements.AddRange(bridgeElements);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting WinUI3 elements: {ex.Message}");
            }

            return elements;
        }

        private static List<UIElementInfo> AnalyzeBridgeForInteractiveElements(AutomationElement bridge)
        {
            var elements = new List<UIElementInfo>();

            try
            {
                var rect = bridge.Current.BoundingRectangle;

                // Ak má bridge užitočné patterns, pridaj ho
                var bridgePatterns = bridge.GetSupportedPatterns();
                if (bridgePatterns.Contains(ValuePattern.Pattern) ||
                    bridgePatterns.Contains(InvokePattern.Pattern))
                {
                    var elementInfo = UIElementDetector.GetElementAtPoint((int)(rect.X + rect.Width / 2),
                                                                         (int)(rect.Y + rect.Height / 2));
                    if (elementInfo != null && elementInfo.Name != "pane_Unknown")
                    {
                        elements.Add(elementInfo);
                    }
                }

                // Analyzuj descendants
                var descendants = bridge.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                foreach (AutomationElement descendant in descendants)
                {
                    if (IsUsefulWinUI3Element(descendant))
                    {
                        var descendantRect = descendant.Current.BoundingRectangle;
                        var elementInfo = new UIElementInfo
                        {
                            Name = GetMeaningfulElementName(descendant),
                            AutomationId = GetProperty(descendant, AutomationElement.AutomationIdProperty),
                            ClassName = GetProperty(descendant, AutomationElement.ClassNameProperty),
                            ControlType = descendant.Current.ControlType?.LocalizedControlType ?? "Unknown",
                            X = (int)(descendantRect.X + descendantRect.Width / 2),
                            Y = (int)(descendantRect.Y + descendantRect.Height / 2),
                            BoundingRectangle = descendantRect,
                            IsEnabled = descendant.Current.IsEnabled,
                            IsVisible = !descendant.Current.IsOffscreen,
                            AutomationElement = descendant
                        };

                        if (!string.IsNullOrEmpty(elementInfo.Name))
                        {
                            elements.Add(elementInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing bridge: {ex.Message}");
            }

            return elements;
        }

        private static string GetMeaningfulElementName(AutomationElement element)
        {
            string name = element.Current.Name ?? "";
            string automationId = GetProperty(element, AutomationElement.AutomationIdProperty);
            string controlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown";

            if (!string.IsNullOrEmpty(name) && name.Length > 2)
                return name;

            if (!string.IsNullOrEmpty(automationId) && automationId.Length > 2)
                return $"AutoId_{automationId}";

            // Skús získať text z pattern
            try
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                {
                    var value = ((ValuePattern)valuePattern).Current.Value;
                    if (!string.IsNullOrEmpty(value) && value.Length <= 20)
                        return $"{controlType}_{value}";
                }
            }
            catch { }

            return $"{controlType}_Interactive";
        }

        /// <summary>
        /// Aktualizuje staré príkazy na základe aktuálneho stavu okna
        /// </summary>
        public static void UpdateCommandsForCurrentWindow(IntPtr windowHandle, List<Command> commands)
        {
            try
            {
                var currentElements = GetAllInteractiveElements(windowHandle);
                System.Diagnostics.Debug.WriteLine($"Found {currentElements.Count} interactive elements for update");

                foreach (var command in commands)
                {
                    if (command.Type == CommandType.Click || command.Type == CommandType.SetText ||
                        command.Type == CommandType.DoubleClick || command.Type == CommandType.RightClick)
                    {
                        var result = SmartFindElement(windowHandle, command);
                        if (result.IsSuccess && result.Element != null)
                        {
                            // Aktualizuj pozíciu a identifikátory
                            command.ElementX = result.Element.X;
                            command.ElementY = result.Element.Y;

                            // Aktualizuj len ak sú nové hodnoty lepšie
                            if (string.IsNullOrEmpty(command.ElementId) && !string.IsNullOrEmpty(result.Element.AutomationId))
                                command.ElementId = result.Element.AutomationId;

                            if (string.IsNullOrEmpty(command.ElementClass) && !string.IsNullOrEmpty(result.Element.ClassName))
                                command.ElementClass = result.Element.ClassName;

                            if (string.IsNullOrEmpty(command.ElementControlType) && !string.IsNullOrEmpty(result.Element.ControlType))
                                command.ElementControlType = result.Element.ControlType;

                            System.Diagnostics.Debug.WriteLine($"Updated command {command.StepNumber}: {command.ElementName} -> ({command.ElementX}, {command.ElementY})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating commands: {ex.Message}");
            }
        }

        private static AutomationElement FindByAutomationId(AutomationElement parent, string automationId)
        {
            try
            {
                var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
                return parent.FindFirst(TreeScope.Descendants, condition);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindByAutomationId error: {ex.Message}");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindByName error: {ex.Message}");
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
                    if (!ValidateElement(element)) continue;

                    string elementName = element.Current.Name;
                    if (!string.IsNullOrEmpty(elementName))
                    {
                        // Presná zhoda (case insensitive)
                        if (string.Equals(elementName, targetName, StringComparison.OrdinalIgnoreCase))
                            return element;

                        // Obsahuje text
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindBySimilarName error: {ex.Message}");
            }

            return null;
        }

        private static AutomationElement FindByTypeAndPosition(AutomationElement parent, string controlType, int x, int y, int tolerance = 30)
        {
            try
            {
                var allElements = parent.FindAll(TreeScope.Descendants, AutomationCondition.TrueCondition);

                AutomationElement bestMatch = null;
                double bestDistance = double.MaxValue;

                foreach (AutomationElement element in allElements)
                {
                    if (!ValidateElement(element)) continue;

                    if (element.Current.ControlType?.LocalizedControlType == controlType)
                    {
                        var rect = element.Current.BoundingRectangle;
                        int centerX = (int)(rect.X + rect.Width / 2);
                        int centerY = (int)(rect.Y + rect.Height / 2);

                        double distance = Math.Sqrt(Math.Pow(centerX - x, 2) + Math.Pow(centerY - y, 2));

                        if (distance <= tolerance && distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestMatch = element;
                        }
                    }
                }

                return bestMatch;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindByTypeAndPosition error: {ex.Message}");
                return null;
            }
        }

        private static AutomationElement FindByPosition(AutomationElement parent, int x, int y, int tolerance)
        {
            try
            {
                // Najprv skús presný point
                var point = new System.Windows.Point(x, y);
                var element = AutomationElement.FromPoint(point);

                if (element != null && ValidateElement(element))
                {
                    // Skontroluj, či patrí do tohto okna
                    var windowElement = parent;
                    var currentElement = element;

                    while (currentElement != null)
                    {
                        if (currentElement.Equals(windowElement))
                            return element;

                        try
                        {
                            currentElement = TreeWalker.ControlViewWalker.GetParent(currentElement);
                        }
                        catch
                        {
                            break;
                        }
                    }
                }

                // Ak presný point nebol úspešný, skús v tolerancii
                return FindNearestElementInTolerance(parent, x, y, tolerance);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindByPosition error: {ex.Message}");
                return null;
            }
        }

        private static AutomationElement FindNearestElementInTolerance(AutomationElement parent, int x, int y, int tolerance)
        {
            try
            {
                var allElements = parent.FindAll(TreeScope.Descendants, AutomationCondition.TrueCondition);

                AutomationElement bestMatch = null;
                double bestDistance = double.MaxValue;

                foreach (AutomationElement element in allElements)
                {
                    if (!ValidateElement(element)) continue;

                    var rect = element.Current.BoundingRectangle;
                    int centerX = (int)(rect.X + rect.Width / 2);
                    int centerY = (int)(rect.Y + rect.Height / 2);

                    double distance = Math.Sqrt(Math.Pow(centerX - x, 2) + Math.Pow(centerY - y, 2));

                    if (distance <= tolerance && distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestMatch = element;
                    }
                }

                return bestMatch;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindNearestElementInTolerance error: {ex.Message}");
                return null;
            }
        }

        private static AutomationElement FindByClassName(AutomationElement parent, string className, int preferredX = 0, int preferredY = 0)
        {
            try
            {
                var condition = new PropertyCondition(AutomationElement.ClassNameProperty, className);
                var elements = parent.FindAll(TreeScope.Descendants, condition);

                if (elements.Count == 0) return null;
                if (elements.Count == 1)
                {
                    var element = elements[0] as AutomationElement;
                    return ValidateElement(element) ? element : null;
                }

                // Ak je viac elementov s rovnakou triedou, vyber najbližší k preferovanej pozícii
                if (preferredX > 0 && preferredY > 0)
                {
                    AutomationElement bestMatch = null;
                    double bestDistance = double.MaxValue;

                    foreach (AutomationElement element in elements)
                    {
                        if (!ValidateElement(element)) continue;

                        var rect = element.Current.BoundingRectangle;
                        int centerX = (int)(rect.X + rect.Width / 2);
                        int centerY = (int)(rect.Y + rect.Height / 2);

                        double distance = Math.Sqrt(Math.Pow(centerX - preferredX, 2) + Math.Pow(centerY - preferredY, 2));

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestMatch = element;
                        }
                    }

                    return bestMatch;
                }

                // Inak vráť prvý validný element
                foreach (AutomationElement element in elements)
                {
                    if (ValidateElement(element))
                        return element;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindByClassName error: {ex.Message}");
                return null;
            }
        }

        private static ElementSearchResult CreateSuccessResult(AutomationElement element, string method, double confidence)
        {
            try
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateSuccessResult error: {ex.Message}");
                return new ElementSearchResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Error creating result: {ex.Message}"
                };
            }
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

    // **Pomocné triedy pre WinUI3 analýzu**
    public class WinUI3AnalysisResult
    {
        public AutomationElement Element { get; set; }
        public double Score { get; set; } = 0;
        public string Method { get; set; } = "";
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
