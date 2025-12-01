using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using System.Text;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Správa identifikátorov UI elementov s fallback mechanizmom
    /// </summary>
    public static class ElementIdentifier
    {
        /// <summary>
        /// Generuje TreePath pre element (hierarchická cesta v UI strome)
        /// Formát: "Window[0]/Panel[2]/Button[1]"
        /// </summary>
        public static string GenerateTreePath(AutomationElement element)
        {
            if (element == null) return string.Empty;

            try
            {
                var pathParts = new List<string>();
                var current = element;

                // Prechádzaj nahor cez rodičov
                while (current != null)
                {
                    try
                    {
                        var parent = TreeWalker.RawViewWalker.GetParent(current);

                        if (parent == null)
                        {
                            // Root element (Desktop)
                            pathParts.Add("Desktop");
                            break;
                        }

                        // Nájdi index medzi súrodencami rovnakého typu
                        string controlType = UIElementDetector.GetControlTypeSafe(current);
                        int index = GetSiblingIndexOfType(current, parent, controlType);

                        pathParts.Add($"{controlType}[{index}]");
                        current = parent;

                        // Limit depth pre výkon
                        if (pathParts.Count >= 10)
                            break;
                    }
                    catch
                    {
                        break;
                    }
                }

                // Obráť poradie (od root k element)
                pathParts.Reverse();
                return string.Join("/", pathParts);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Nájde index elementu medzi súrodencami rovnakého typu
        /// </summary>
        private static int GetSiblingIndexOfType(AutomationElement element, AutomationElement parent, string controlType)
        {
            try
            {
                var siblings = parent.FindAll(TreeScope.Children, Condition.TrueCondition);
                int index = 0;

                foreach (AutomationElement sibling in siblings)
                {
                    try
                    {
                        string siblingType = UIElementDetector.GetControlTypeSafe(sibling);

                        if (siblingType == controlType)
                        {
                            if (Automation.Compare(sibling, element))
                                return index;
                            index++;
                        }
                    }
                    catch { }
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Nájde element podľa TreePath
        /// </summary>
        public static AutomationElement FindByTreePath(AutomationElement root, string treePath)
        {
            if (root == null || string.IsNullOrEmpty(treePath)) return null;

            try
            {
                var parts = treePath.Split('/');
                var current = root;

                // Preskočíme Desktop ak je tam
                int startIndex = parts[0] == "Desktop" ? 1 : 0;

                for (int i = startIndex; i < parts.Length; i++)
                {
                    var part = parts[i];

                    // Parse "ControlType[index]"
                    var match = System.Text.RegularExpressions.Regex.Match(part, @"(.+)\[(\d+)\]");
                    if (!match.Success) return null;

                    string controlType = match.Groups[1].Value;
                    int targetIndex = int.Parse(match.Groups[2].Value);

                    // Nájdi child element podľa typu a indexu
                    current = FindChildByTypeAndIndex(current, controlType, targetIndex);

                    if (current == null)
                        return null;
                }

                return current;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Nájde child element podľa typu a indexu
        /// </summary>
        private static AutomationElement FindChildByTypeAndIndex(AutomationElement parent, string controlType, int targetIndex)
        {
            try
            {
                var children = parent.FindAll(TreeScope.Children, Condition.TrueCondition);
                int currentIndex = 0;

                foreach (AutomationElement child in children)
                {
                    try
                    {
                        string childType = UIElementDetector.GetControlTypeSafe(child);

                        if (childType == controlType)
                        {
                            if (currentIndex == targetIndex)
                                return child;
                            currentIndex++;
                        }
                    }
                    catch { }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fallback mechanizmus - vracia najlepší dostupný identifikátor
        /// Priorita: AutomationId > Name + ControlType > TreePath > ClassName + Position > Coordinates
        /// </summary>
        public static (string identifier, int priority) GetBestIdentifier(UIElementInfo elementInfo)
        {
            if (elementInfo == null)
                return (string.Empty, 0);

            // Úroveň 1: AutomationId (najspoľahlivejší)
            if (!string.IsNullOrEmpty(elementInfo.AutomationId) && !IsGenericId(elementInfo.AutomationId))
                return ($"AutomationId:{elementInfo.AutomationId}", 5);

            // Úroveň 2: Name + ControlType
            if (!string.IsNullOrEmpty(elementInfo.Name) &&
                !IsGenericName(elementInfo.Name) &&
                !string.IsNullOrEmpty(elementInfo.ControlType))
                return ($"Name:{elementInfo.Name}|Type:{elementInfo.ControlType}", 4);

            // Úroveň 3: TreePath
            if (!string.IsNullOrEmpty(elementInfo.TreePath))
                return ($"TreePath:{elementInfo.TreePath}", 3);

            // Úroveň 4: ClassName + Position v rodičovi
            if (!string.IsNullOrEmpty(elementInfo.ClassName) &&
                !IsGenericClassName(elementInfo.ClassName))
                return ($"Class:{elementInfo.ClassName}|Pos:{elementInfo.X},{elementInfo.Y}", 2);

            // Úroveň 5: Súradnice (najmenej spoľahlivé)
            if (elementInfo.X > 0 && elementInfo.Y > 0)
                return ($"Coords:{elementInfo.X},{elementInfo.Y}", 1);

            return (string.Empty, 0);
        }

        /// <summary>
        /// Skontroluje či je AutomationId generický
        /// </summary>
        private static bool IsGenericId(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            if (id.Length > 50) return true; // Príliš dlhé ID sú často generované
            if (id.All(char.IsDigit)) return true; // Len čísla
            if (id.Contains("{") && id.Contains("}")) return true; // GUID formát
            if (System.Text.RegularExpressions.Regex.IsMatch(id, @"^[0-9a-f-]{20,}$")) return true; // Hash-like
            return false;
        }

        /// <summary>
        /// Skontroluje či je Name generický
        /// </summary>
        private static bool IsGenericName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            var genericPatterns = new[]
            {
                "Unknown", "pane_Unknown", "Element_at_", "Click_at_",
                "Microsoft.UI.Content", "DesktopChildSiteBridge"
            };

            return genericPatterns.Any(p => name.Contains(p));
        }

        /// <summary>
        /// Skontroluje či je ClassName generický
        /// </summary>
        private static bool IsGenericClassName(string className)
        {
            if (string.IsNullOrEmpty(className)) return true;

            var genericClasses = new[]
            {
                "ContentPresenter", "Border", "Grid", "StackPanel",
                "Canvas", "ScrollViewer", "UserControl", "Panel"
            };

            return genericClasses.Any(g => className.Equals(g, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Rozšírenie UIElementInfo o TreePath
    /// </summary>
    public partial class UIElementInfo
    {
        public string TreePath { get; set; } = string.Empty;
    }
}
