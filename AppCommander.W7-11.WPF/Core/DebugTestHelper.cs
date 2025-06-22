using System;
using System.Collections.Generic;
using System.Linq;
using AppCommander.W7_11.WPF.Core;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    public static class DebugTestHelper
    {

        /// <summary>
        /// Export sequence ako text report
        /// </summary>
        public static string ExportSequenceAsText(CommandSequence sequence)
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine($"Sequence: {sequence.Name}");
            report.AppendLine($"Commands: {sequence.Commands.Count}");
            report.AppendLine($"Target: {sequence.TargetProcessName}");
            report.AppendLine("");

            for (int i = 0; i < sequence.Commands.Count; i++)
            {
                var cmd = sequence.Commands[i];
                report.AppendLine($"{i + 1:D3}. {cmd.Type} - {cmd.ElementName}");
                report.AppendLine($"     Position: ({cmd.ElementX}, {cmd.ElementY})");

                if (!string.IsNullOrEmpty(cmd.ElementId))
                    report.AppendLine($"     ID: {cmd.ElementId}");

                if (!string.IsNullOrEmpty(cmd.ElementText))
                    report.AppendLine($"     Text: {cmd.ElementText}");

                if (cmd.IsWinUI3Element)
                    report.AppendLine($"     WinUI3: Yes (confidence: {cmd.ElementConfidence:F2})");

                if (!string.IsNullOrEmpty(cmd.Value))
                    report.AppendLine($"     Value: {cmd.Value}");

                report.AppendLine("");
            }

            return report.ToString();
        }

        /// <summary>
        /// **Komplexná validácia sequence s WinUI3 podporou**
        /// </summary>
        public static ValidationResult ValidateSequenceWithWinUI3(CommandSequence sequence, IntPtr targetWindow = default)
        {
            var result = new ValidationResult();

            if (sequence == null)
            {
                result.AddError("Sequence is null");
                return result;
            }

            if (sequence.Commands.Count == 0)
            {
                result.AddError("No commands in sequence");
                return result;
            }

            // Základná validácia príkazov
            foreach (var cmd in sequence.Commands)
            {
                ValidateCommandEnhanced(cmd, result);
            }

            // WinUI3 špecifická validácia
            var winui3Commands = sequence.Commands.Where(c => c.IsWinUI3Element).ToList();
            if (winui3Commands.Any())
            {
                result.WinUI3CommandCount = winui3Commands.Count;
                ValidateWinUI3Commands(winui3Commands, result, targetWindow);
            }

            // Validácia dostupnosti elementov ak je poskytnuté target window
            if (targetWindow != IntPtr.Zero)
            {
                ValidateElementAvailability(sequence.Commands, targetWindow, result);
            }

            return result;
        }

        private static void ValidateCommandEnhanced(Command cmd, ValidationResult result)
        {
            string prefix = $"Command {cmd.StepNumber}";

            // Základná validácia podľa typu
            switch (cmd.Type)
            {
                case CommandType.Click:
                case CommandType.DoubleClick:
                case CommandType.RightClick:
                case CommandType.MouseClick:
                    ValidateMouseCommand(cmd, prefix, result);
                    break;

                case CommandType.KeyPress:
                    ValidateKeyCommand(cmd, prefix, result);
                    break;

                case CommandType.SetText:
                    ValidateTextCommand(cmd, prefix, result);
                    break;

                case CommandType.Wait:
                    ValidateWaitCommand(cmd, prefix, result);
                    break;

                case CommandType.Loop:
                    ValidateLoopCommand(cmd, prefix, result);
                    break;
            }

            // **WinUI3 špecifická validácia**
            if (cmd.IsWinUI3Element)
            {
                ValidateWinUI3Command(cmd, prefix, result);
            }
        }

        private static void ValidateMouseCommand(Command cmd, string prefix, ValidationResult result)
        {
            // Kontrola súradníc
            bool hasValidCurrentCoords = cmd.ElementX > 0 && cmd.ElementY > 0;
            bool hasValidOriginalCoords = cmd.OriginalX > 0 && cmd.OriginalY > 0;

            if (!hasValidCurrentCoords && !hasValidOriginalCoords)
            {
                result.AddError($"{prefix}: No valid coordinates available");
            }

            // Kontrola identifikátorov
            bool hasElementId = !string.IsNullOrEmpty(cmd.ElementId) && !IsGenericId(cmd.ElementId);
            bool hasMeaningfulName = !string.IsNullOrEmpty(cmd.ElementName) && !IsGenericName(cmd.ElementName);
            bool hasText = !string.IsNullOrEmpty(cmd.ElementText);

            if (!hasElementId && !hasMeaningfulName && !hasText)
            {
                result.AddWarning($"{prefix}: Element has weak identifiers - may be unreliable");
            }

            // Kontrola screen bounds
            if (hasValidCurrentCoords && !IsPointOnScreen(cmd.ElementX, cmd.ElementY))
            {
                result.AddWarning($"{prefix}: Current coordinates ({cmd.ElementX}, {cmd.ElementY}) outside screen bounds");
            }
        }

        private static void ValidateKeyCommand(Command cmd, string prefix, ValidationResult result)
        {
            if (cmd.Key == System.Windows.Forms.Keys.None && cmd.KeyCode == 0)
            {
                result.AddError($"{prefix}: No key specified");
            }

            // Varovánie pre problematické klávesy
            var problematicKeys = new[] {
                System.Windows.Forms.Keys.LWin, System.Windows.Forms.Keys.RWin,
                System.Windows.Forms.Keys.Alt, System.Windows.Forms.Keys.F4
            };

            if (problematicKeys.Contains(cmd.Key))
            {
                result.AddWarning($"{prefix}: Key '{cmd.Key}' may cause issues during playback");
            }
        }

        private static void ValidateTextCommand(Command cmd, string prefix, ValidationResult result)
        {
            if (string.IsNullOrEmpty(cmd.Value))
            {
                result.AddError($"{prefix}: No text value specified");
            }

            bool hasValidCoords = (cmd.ElementX > 0 && cmd.ElementY > 0) || (cmd.OriginalX > 0 && cmd.OriginalY > 0);
            bool hasElementId = !string.IsNullOrEmpty(cmd.ElementId);

            if (!hasValidCoords && !hasElementId)
            {
                result.AddError($"{prefix}: Text command needs either coordinates or element ID");
            }
        }

        private static void ValidateWaitCommand(Command cmd, string prefix, ValidationResult result)
        {
            if (!int.TryParse(cmd.Value, out int waitTime) || waitTime <= 0)
            {
                result.AddError($"{prefix}: Invalid wait time '{cmd.Value}'");
            }
            else if (waitTime > 30000) // 30 sekúnd
            {
                result.AddWarning($"{prefix}: Very long wait time ({waitTime}ms)");
            }
        }

        private static void ValidateLoopCommand(Command cmd, string prefix, ValidationResult result)
        {
            if (cmd.RepeatCount <= 0)
            {
                result.AddError($"{prefix}: Invalid loop repeat count {cmd.RepeatCount}");
            }
            else if (cmd.RepeatCount > 100)
            {
                result.AddWarning($"{prefix}: High loop repeat count ({cmd.RepeatCount}) - may take long time");
            }
        }

        private static void ValidateWinUI3Command(Command cmd, string prefix, ValidationResult result)
        {
            if (cmd.ElementClass != "Microsoft.UI.Content.DesktopChildSiteBridge")
            {
                result.AddWarning($"{prefix}: Marked as WinUI3 but class is '{cmd.ElementClass}'");
            }

            // WinUI3 elementy by mali mať lepšie identifikátory
            bool hasStrongId = !string.IsNullOrEmpty(cmd.ElementId) && !IsGenericId(cmd.ElementId);
            bool hasText = !string.IsNullOrEmpty(cmd.ElementText);
            bool hasHelpText = !string.IsNullOrEmpty(cmd.ElementHelpText);

            if (!hasStrongId && !hasText && !hasHelpText)
            {
                result.AddWarning($"{prefix}: WinUI3 element lacks strong identifiers");
            }

            if (cmd.ElementConfidence > 0 && cmd.ElementConfidence < 0.5)
            {
                result.AddWarning($"{prefix}: Low element confidence ({cmd.ElementConfidence:F2})");
            }
        }

        private static void ValidateWinUI3Commands(List<Command> winui3Commands, ValidationResult result, IntPtr targetWindow)
        {
            result.AddInfo($"Found {winui3Commands.Count} WinUI3 commands");

            // Analyzuj kvalitu WinUI3 identifikátorov
            var weakCommands = winui3Commands.Where(c =>
                string.IsNullOrEmpty(c.ElementId) &&
                string.IsNullOrEmpty(c.ElementText) &&
                IsGenericName(c.ElementName)).ToList();

            if (weakCommands.Any())
            {
                result.AddWarning($"{weakCommands.Count} WinUI3 commands have weak identifiers");
            }

            // Skontroluj duplicitné elementy
            var duplicateGroups = winui3Commands
                .GroupBy(c => c.GetBestElementIdentifier())
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in duplicateGroups)
            {
                result.AddWarning($"Duplicate WinUI3 elements: {group.Key} ({group.Count()} commands)");
            }
        }

        private static void ValidateElementAvailability(List<Command> commands, IntPtr targetWindow, ValidationResult result)
        {
            try
            {
                var clickCommands = commands.Where(c =>
                    c.Type == CommandType.Click || c.Type == CommandType.DoubleClick ||
                    c.Type == CommandType.RightClick || c.Type == CommandType.SetText).ToList();

                if (!clickCommands.Any()) return;

                int foundCount = 0;
                int totalCount = clickCommands.Count;

                foreach (var cmd in clickCommands)
                {
                    var searchResult = AdaptiveElementFinder.SmartFindElement(targetWindow, cmd);
                    if (searchResult.IsSuccess)
                    {
                        foundCount++;
                    }
                }

                result.ElementsFound = foundCount;
                result.ElementsTotal = totalCount;

                if (foundCount == 0)
                {
                    result.AddError("No elements found in target window - sequence may fail completely");
                }
                else if (foundCount < totalCount)
                {
                    result.AddWarning($"Only {foundCount}/{totalCount} elements found - some commands may fail");
                }
                else
                {
                    result.AddInfo($"All {foundCount} elements found successfully");
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Element availability check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// **Detailná analýza WinUI3 aplikácie**
        /// </summary>
        public static WinUI3ApplicationAnalysis AnalyzeWinUI3Application(IntPtr windowHandle)
        {
            var analysis = new WinUI3ApplicationAnalysis();

            try
            {
                if (windowHandle == IntPtr.Zero)
                {
                    analysis.ErrorMessage = "Invalid window handle";
                    return analysis;
                }

                AutomationElement window = AutomationElement.FromHandle(windowHandle);
                if (window == null)
                {
                    analysis.ErrorMessage = "Cannot access window automation";
                    return analysis;
                }

                analysis.WindowTitle = window.Current.Name ?? "Unknown";
                analysis.WindowClass = GetProperty(window, AutomationElement.ClassNameProperty);

                // Nájdi všetky WinUI3 bridge elementy
                var bridgeCondition = new PropertyCondition(AutomationElement.ClassNameProperty,
                    "Microsoft.UI.Content.DesktopChildSiteBridge");
                var bridges = window.FindAll(TreeScope.Descendants, bridgeCondition);

                analysis.BridgeCount = bridges.Count;

                foreach (AutomationElement bridge in bridges)
                {
                    var bridgeInfo = AnalyzeBridge(bridge);
                    analysis.Bridges.Add(bridgeInfo);
                }

                // Analyzuj interaktívne elementy
                analysis.InteractiveElements = GetInteractiveWinUI3Elements(window);
                analysis.IsSuccessful = true;

                // Vytvor odporúčania
                analysis.Recommendations = GenerateRecommendations(analysis);
            }
            catch (Exception ex)
            {
                analysis.ErrorMessage = ex.Message;
                analysis.IsSuccessful = false;
            }

            return analysis;
        }

        private static WinUI3BridgeInfo AnalyzeBridge(AutomationElement bridge)
        {
            var info = new WinUI3BridgeInfo();

            try
            {
                var rect = bridge.Current.BoundingRectangle;
                info.Position = new System.Drawing.Point((int)rect.X, (int)rect.Y);
                info.Size = new System.Drawing.Size((int)rect.Width, (int)rect.Height);
                info.IsVisible = !bridge.Current.IsOffscreen;
                info.IsEnabled = bridge.Current.IsEnabled;

                // Analyzuj patterns
                var patterns = bridge.GetSupportedPatterns();
                info.SupportedPatterns = patterns.Select(p => p.ProgrammaticName.Replace("Pattern", "")).ToList();

                // Analyzuj child elementy
                var children = bridge.FindAll(TreeScope.Children, Condition.TrueCondition);
                info.ChildCount = children.Count;

                // Analyzuj descendants s užitočným obsahom
                var descendants = bridge.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                foreach (AutomationElement descendant in descendants)
                {
                    if (HasMeaningfulContent(descendant))
                    {
                        var elementInfo = new WinUI3ElementInfo
                        {
                            Name = descendant.Current.Name ?? "",
                            AutomationId = GetProperty(descendant, AutomationElement.AutomationIdProperty),
                            ControlType = descendant.Current.ControlType?.LocalizedControlType ?? "Unknown",
                            Text = GetElementText(descendant),
                            Position = new System.Drawing.Point(
                                (int)(descendant.Current.BoundingRectangle.X + descendant.Current.BoundingRectangle.Width / 2),
                                (int)(descendant.Current.BoundingRectangle.Y + descendant.Current.BoundingRectangle.Height / 2)
                            )
                        };
                        info.MeaningfulElements.Add(elementInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
            }

            return info;
        }

        private static List<WinUI3ElementInfo> GetInteractiveWinUI3Elements(AutomationElement window)
        {
            var elements = new List<WinUI3ElementInfo>();

            try
            {
                var bridgeCondition = new PropertyCondition(AutomationElement.ClassNameProperty,
                    "Microsoft.UI.Content.DesktopChildSiteBridge");
                var bridges = window.FindAll(TreeScope.Descendants, bridgeCondition);

                foreach (AutomationElement bridge in bridges)
                {
                    var bridgeElements = GetElementsFromBridge(bridge);
                    elements.AddRange(bridgeElements);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting interactive elements: {ex.Message}");
            }

            return elements;
        }

        private static List<WinUI3ElementInfo> GetElementsFromBridge(AutomationElement bridge)
        {
            var elements = new List<WinUI3ElementInfo>();

            try
            {
                // Skontroluj bridge samotný
                if (HasInteractivePatterns(bridge.GetSupportedPatterns()))
                {
                    elements.Add(CreateElementInfo(bridge));
                }

                // Skontroluj descendants
                var descendants = bridge.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                foreach (AutomationElement descendant in descendants)
                {
                    if (HasInteractivePatterns(descendant.GetSupportedPatterns()) ||
                        HasMeaningfulContent(descendant))
                    {
                        elements.Add(CreateElementInfo(descendant));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing bridge: {ex.Message}");
            }

            return elements;
        }

        private static WinUI3ElementInfo CreateElementInfo(AutomationElement element)
        {
            var rect = element.Current.BoundingRectangle;
            return new WinUI3ElementInfo
            {
                Name = element.Current.Name ?? "",
                AutomationId = GetProperty(element, AutomationElement.AutomationIdProperty),
                ControlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown",
                Text = GetElementText(element),
                Position = new System.Drawing.Point(
                    (int)(rect.X + rect.Width / 2),
                    (int)(rect.Y + rect.Height / 2)
                ),
                IsEnabled = element.Current.IsEnabled,
                IsVisible = !element.Current.IsOffscreen
            };
        }

        private static List<string> GenerateRecommendations(WinUI3ApplicationAnalysis analysis)
        {
            var recommendations = new List<string>();

            if (analysis.BridgeCount == 0)
            {
                recommendations.Add("No WinUI3 bridges found - this may not be a WinUI3 application");
            }
            else if (analysis.BridgeCount > 10)
            {
                recommendations.Add("Many WinUI3 bridges detected - consider using more specific element selectors");
            }

            var weakElements = analysis.InteractiveElements.Where(e =>
                string.IsNullOrEmpty(e.AutomationId) &&
                string.IsNullOrEmpty(e.Text) &&
                string.IsNullOrEmpty(e.Name)).Count();

            if (weakElements > 0)
            {
                recommendations.Add($"{weakElements} elements lack strong identifiers - may be unreliable for automation");
            }

            var duplicateElements = analysis.InteractiveElements
                .GroupBy(e => $"{e.Name}_{e.AutomationId}_{e.ControlType}")
                .Where(g => g.Count() > 1)
                .Count();

            if (duplicateElements > 0)
            {
                recommendations.Add($"{duplicateElements} duplicate element signatures found - use position-based selectors");
            }

            if (analysis.InteractiveElements.Count == 0)
            {
                recommendations.Add("No interactive elements found - check if application is fully loaded");
            }

            return recommendations;
        }

        // Helper methods
        private static bool HasInteractivePatterns(AutomationPattern[] patterns)
        {
            var interactivePatterns = new[]
            {
                InvokePattern.Pattern, ValuePattern.Pattern, TogglePattern.Pattern,
                SelectionItemPattern.Pattern, ExpandCollapsePattern.Pattern, TextPattern.Pattern
            };

            return patterns.Any(p => interactivePatterns.Contains(p));
        }

        private static bool HasMeaningfulContent(AutomationElement element)
        {
            try
            {
                string name = element.Current.Name ?? "";
                string automationId = GetProperty(element, AutomationElement.AutomationIdProperty);
                string text = GetElementText(element);

                return (!string.IsNullOrEmpty(name) && name.Length > 2 && !IsGenericName(name)) ||
                       (!string.IsNullOrEmpty(automationId) && automationId.Length > 2 && !IsGenericId(automationId)) ||
                       (!string.IsNullOrEmpty(text) && text.Length > 1);
            }
            catch
            {
                return false;
            }
        }

        private static string GetElementText(AutomationElement element)
        {
            try
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                {
                    var value = ((ValuePattern)valuePattern).Current.Value;
                    if (!string.IsNullOrEmpty(value)) return value;
                }

                if (element.TryGetCurrentPattern(TextPattern.Pattern, out object textPattern))
                {
                    var text = ((TextPattern)textPattern).DocumentRange.GetText(100);
                    if (!string.IsNullOrEmpty(text)) return text.Trim();
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        public static string GetProperty(AutomationElement element, AutomationProperty property)
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

        private static bool IsGenericName(string name)
        {
            var genericNames = new[] { "Unknown", "pane_Unknown", "Element_at_", "Click_at_", "Microsoft.UI.Content" };
            return genericNames.Any(g => name.Contains(g));
        }

        private static bool IsGenericId(string id)
        {
            return string.IsNullOrEmpty(id) || id.Length > 20 || id.All(char.IsDigit) || id.Contains("-") || id.Contains("{");
        }

        private static bool IsPointOnScreen(int x, int y)
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                return screens.Any(screen =>
                    x >= screen.Bounds.X && x <= screen.Bounds.X + screen.Bounds.Width &&
                    y >= screen.Bounds.Y && y <= screen.Bounds.Y + screen.Bounds.Height);
            }
            catch
            {
                return true; // Fallback
            }
        }

        /// <summary>
        /// **Export WinUI3 analýzy do reportu**
        /// </summary>
        public static string ExportWinUI3AnalysisReport(WinUI3ApplicationAnalysis analysis)
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("=== WINUI3 APPLICATION ANALYSIS REPORT ===");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Window: {analysis.WindowTitle}");
            report.AppendLine($"Class: {analysis.WindowClass}");
            report.AppendLine($"Success: {analysis.IsSuccessful}");
            report.AppendLine("");

            if (!analysis.IsSuccessful)
            {
                report.AppendLine($"ERROR: {analysis.ErrorMessage}");
                return report.ToString();
            }

            report.AppendLine($"WinUI3 Bridges Found: {analysis.BridgeCount}");
            report.AppendLine($"Interactive Elements: {analysis.InteractiveElements.Count}");
            report.AppendLine("");

            // Bridge details
            report.AppendLine("=== BRIDGE DETAILS ===");
            for (int i = 0; i < analysis.Bridges.Count; i++)
            {
                var bridge = analysis.Bridges[i];
                report.AppendLine($"Bridge {i + 1}:");
                report.AppendLine($"  Position: {bridge.Position}");
                report.AppendLine($"  Size: {bridge.Size}");
                report.AppendLine($"  Visible: {bridge.IsVisible}, Enabled: {bridge.IsEnabled}");
                report.AppendLine($"  Children: {bridge.ChildCount}");
                report.AppendLine($"  Meaningful Elements: {bridge.MeaningfulElements.Count}");

                if (bridge.SupportedPatterns.Any())
                {
                    report.AppendLine($"  Patterns: {string.Join(", ", bridge.SupportedPatterns)}");
                }

                if (!string.IsNullOrEmpty(bridge.ErrorMessage))
                {
                    report.AppendLine($"  ERROR: {bridge.ErrorMessage}");
                }

                report.AppendLine("");
            }

            // Interactive elements
            report.AppendLine("=== INTERACTIVE ELEMENTS ===");
            foreach (var element in analysis.InteractiveElements)
            {
                report.AppendLine($"Element: {element.Name}");
                report.AppendLine($"  Type: {element.ControlType}");
                report.AppendLine($"  ID: {element.AutomationId}");
                report.AppendLine($"  Text: {element.Text}");
                report.AppendLine($"  Position: {element.Position}");
                report.AppendLine($"  Enabled: {element.IsEnabled}, Visible: {element.IsVisible}");
                report.AppendLine("");
            }

            // Recommendations
            if (analysis.Recommendations.Any())
            {
                report.AppendLine("=== RECOMMENDATIONS ===");
                foreach (var recommendation in analysis.Recommendations)
                {
                    report.AppendLine($"• {recommendation}");
                }
                report.AppendLine("");
            }

            return report.ToString();
        }

        // Existujúce metódy ostávajú rovnaké...
    }

    // **Support classes pre WinUI3 analýzu**

    public class ValidationResult
    {
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Info { get; set; } = new List<string>();
        public int WinUI3CommandCount { get; set; } = 0;
        public int ElementsFound { get; set; } = 0;
        public int ElementsTotal { get; set; } = 0;

        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;

        public void AddError(string message) => Errors.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);
        public void AddInfo(string message) => Info.Add(message);

        public override string ToString()
        {
            var result = new System.Text.StringBuilder();
            result.AppendLine($"Validation Result: {(IsValid ? "PASSED" : "FAILED")}");

            if (Errors.Any())
            {
                result.AppendLine("ERRORS:");
                Errors.ForEach(e => result.AppendLine($"  ✗ {e}"));
            }

            if (Warnings.Any())
            {
                result.AppendLine("WARNINGS:");
                Warnings.ForEach(w => result.AppendLine($"  ⚠ {w}"));
            }

            if (Info.Any())
            {
                result.AppendLine("INFO:");
                Info.ForEach(i => result.AppendLine($"  ℹ {i}"));
            }

            return result.ToString();
        }

        /// <summary>
        /// Extrahuje informácie o okne
        /// </summary>
        public static WindowTrackingInfo ExtractWindowInfo(IntPtr windowHandle)
        {
            try
            {
                var info = new WindowTrackingInfo();

                // Get window title
                int titleLength = GetWindowTextLength(windowHandle);
                if (titleLength > 0)
                {
                    System.Text.StringBuilder title = new System.Text.StringBuilder(titleLength + 1);
                    GetWindowText(windowHandle, title, title.Capacity);
                    info.Title = title.ToString();
                }

                // Get window class
                System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                GetClassName(windowHandle, className, className.Capacity);
                info.ClassName = className.ToString();

                // Get process info
                GetWindowThreadProcessId(windowHandle, out uint processId);
                try
                {
                    var process = System.Diagnostics.Process.GetProcessById((int)processId);
                    info.ProcessName = process.ProcessName;
                    info.ProcessId = (int)processId;
                }
                catch
                {
                    info.ProcessName = "Unknown";
                    info.ProcessId = (int)processId;
                }

                info.WindowHandle = windowHandle;
                return info;
            }
            catch (Exception ex)
            {
                return new WindowTrackingInfo
                {
                    Title = "Error",
                    ProcessName = "Unknown",
                    ClassName = "Unknown",
                    WindowHandle = windowHandle,
                    //ErrorMessage = ex.Message
                };
            }
        }

        // Windows API imports
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }

    public class WinUI3ApplicationAnalysis
    {
        public bool IsSuccessful { get; set; } = false;
        public string ErrorMessage { get; set; } = "";
        public string WindowTitle { get; set; } = "";
        public string WindowClass { get; set; } = "";
        public int BridgeCount { get; set; } = 0;
        public List<WinUI3BridgeInfo> Bridges { get; set; } = new List<WinUI3BridgeInfo>();
        public List<WinUI3ElementInfo> InteractiveElements { get; set; } = new List<WinUI3ElementInfo>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    public class WinUI3BridgeInfo
    {
        public System.Drawing.Point Position { get; set; }
        public System.Drawing.Size Size { get; set; }
        public bool IsVisible { get; set; }
        public bool IsEnabled { get; set; }
        public int ChildCount { get; set; }
        public List<string> SupportedPatterns { get; set; } = new List<string>();
        public List<WinUI3ElementInfo> MeaningfulElements { get; set; } = new List<WinUI3ElementInfo>();
        public string ErrorMessage { get; set; } = "";
    }

    public class WinUI3ElementInfo
    {
        public string Name { get; set; } = "";
        public string AutomationId { get; set; } = "";
        public string ControlType { get; set; } = "";
        public string Text { get; set; } = "";
        public System.Drawing.Point Position { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
    }

}
