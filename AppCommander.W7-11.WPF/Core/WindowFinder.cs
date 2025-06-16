using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AppCommander.W7_11.WPF.Core
{
    public static class WindowFinder
    {
        /// <summary>
        /// Nájde okno podľa názvu procesu a časti titulku
        /// </summary>
        public static IntPtr FindWindowByProcessAndTitle(string processName, string partialTitle = null)
        {
            var processes = Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        string windowTitle = GetWindowTitle(process.MainWindowHandle);

                        if (string.IsNullOrEmpty(partialTitle) ||
                            windowTitle.IndexOf(partialTitle, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return process.MainWindowHandle;
                        }
                    }
                }
                catch
                {
                    // Proces možno už neexistuje
                    continue;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Nájde okno podľa class name a titulku
        /// </summary>
        public static IntPtr FindWindowByClassAndTitle(string className, string windowTitle = null)
        {
            return FindWindow(className, windowTitle);
        }

        /// <summary>
        /// Nájde všetky okná patriace procesu
        /// </summary>
        public static List<WindowInfo> GetProcessWindows(string processName)
        {
            var windows = new List<WindowInfo>();
            var processes = Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                try
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        GetWindowThreadProcessId(hWnd, out uint processId);

                        if (processId == process.Id && IsWindowVisible(hWnd))
                        {
                            string title = GetWindowTitle(hWnd);
                            string className = GetClassName(hWnd);

                            if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(className))
                            {
                                windows.Add(new WindowInfo
                                {
                                    Handle = hWnd,
                                    Title = title,
                                    ClassName = className,
                                    ProcessId = (int)processId,
                                    ProcessName = processName
                                });
                            }
                        }

                        return true; // Pokračuj v enumerácii
                    }, IntPtr.Zero);
                }
                catch
                {
                    continue;
                }
            }

            return windows;
        }

        /// <summary>
        /// Inteligentné hľadanie okna s fallback možnosťami
        /// </summary>
        public static WindowSearchResult SmartFindWindow(string processName, string windowTitle = null, string className = null)
        {
            var result = new WindowSearchResult();

            // Pokus 1: Presný match podľa procesu a titulku
            if (!string.IsNullOrEmpty(processName) && !string.IsNullOrEmpty(windowTitle))
            {
                result.Handle = FindWindowByProcessAndTitle(processName, windowTitle);
                if (result.Handle != IntPtr.Zero)
                {
                    result.MatchMethod = "ProcessName + WindowTitle";
                    result.Confidence = 0.95;
                    return result;
                }
            }

            // Pokus 2: Iba názov procesu (prvé hlavné okno)
            if (!string.IsNullOrEmpty(processName))
            {
                result.Handle = FindWindowByProcessAndTitle(processName);
                if (result.Handle != IntPtr.Zero)
                {
                    result.MatchMethod = "ProcessName only";
                    result.Confidence = 0.80;
                    return result;
                }
            }

            // Pokus 3: Class name a titulok
            if (!string.IsNullOrEmpty(className))
            {
                result.Handle = FindWindowByClassAndTitle(className, windowTitle);
                if (result.Handle != IntPtr.Zero)
                {
                    result.MatchMethod = "ClassName + WindowTitle";
                    result.Confidence = 0.70;
                    return result;
                }
            }

            // Pokus 4: Fuzzy search podľa časti titulku
            if (!string.IsNullOrEmpty(windowTitle))
            {
                result.Handle = FindWindowByPartialTitle(windowTitle);
                if (result.Handle != IntPtr.Zero)
                {
                    result.MatchMethod = "Partial title match";
                    result.Confidence = 0.60;
                    return result;
                }
            }

            result.MatchMethod = "Not found";
            result.Confidence = 0.0;
            return result;
        }

        /// <summary>
        /// Sleduje či je aplikácia stále aktívna
        /// </summary>
        public static bool IsApplicationRunning(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }

        /// <summary>
        /// Počká na spustenie aplikácie (timeout v sekundách)
        /// </summary>
        public static IntPtr WaitForApplication(string processName, int timeoutSeconds = 30)
        {
            var endTime = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < endTime)
            {
                var handle = FindWindowByProcessAndTitle(processName);
                if (handle != IntPtr.Zero)
                {
                    return handle;
                }

                System.Threading.Thread.Sleep(500); // Čakaj 500ms
            }

            return IntPtr.Zero;
        }

        private static IntPtr FindWindowByPartialTitle(string partialTitle)
        {
            IntPtr foundWindow = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    string title = GetWindowTitle(hWnd);
                    if (!string.IsNullOrEmpty(title) &&
                        title.IndexOf(partialTitle, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foundWindow = hWnd;
                        return false; // Zastav enumeráciu
                    }
                }
                return true;
            }, IntPtr.Zero);

            return foundWindow;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            var buffer = new StringBuilder(nChars);

            if (GetWindowText(hWnd, buffer, nChars) > 0)
                return buffer.ToString();

            return string.Empty;
        }

        private static string GetClassName(IntPtr hWnd)
        {
            var buffer = new StringBuilder(256);
            GetClassName(hWnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        // Windows API
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);


        /// <summary>
        /// Získa všetky viditeľné okná v systéme
        /// </summary>
        /// <returns>Zoznam všetkých okien</returns>
        public static List<WindowInfo> GetAllWindows()
        {
            var windows = new List<WindowInfo>();

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        // Kontroluj či je okno viditeľné
                        if (!IsWindowVisible(hWnd))
                            return true;

                        // Získaj informácie o okne
                        string title = GetWindowTitle(hWnd);
                        string className = GetClassName(hWnd);

                        // Skip okná bez titulku a určité systémové okná
                        if (string.IsNullOrWhiteSpace(title))
                            return true;

                        // Skip určité systémové triedy
                        if (IsSystemWindow(className))
                            return true;

                        // Získaj informácie o procese
                        GetWindowThreadProcessId(hWnd, out uint processId);
                        string processName = "";

                        try
                        {
                            using (var process = Process.GetProcessById((int)processId))
                            {
                                processName = process.ProcessName;
                            }
                        }
                        catch
                        {
                            // Proces už neexistuje alebo nemáme prístup
                            return true;
                        }

                        // Skip prázdne process names
                        if (string.IsNullOrEmpty(processName))
                            return true;

                        // Vytvor WindowInfo objekt
                        var windowInfo = new WindowInfo
                        {
                            Handle = hWnd,
                            WindowTitle = title,
                            Title = title, // Alias pre kompatibilitu
                            WindowClass = className,
                            ClassName = className, // Alias pre kompatibilitu
                            ProcessName = processName,
                            ProcessId = (int)processId
                        };

                        windows.Add(windowInfo);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing window {hWnd}: {ex.Message}");
                    }

                    return true; // Pokračuj v enumerácii
                }, IntPtr.Zero);

                // Zoraď windows podľa procesu a titulku
                return windows
                    .OrderBy(w => w.ProcessName)
                    .ThenBy(w => w.WindowTitle)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating windows: {ex.Message}");
                return new List<WindowInfo>();
            }
        }

        /// <summary>
        /// Kontroluje či je okno systémové (ktoré chceme preskočiť)
        /// </summary>
        /// <param name="className">Class name okna</param>
        /// <returns>True ak je systémové okno</returns>
        private static bool IsSystemWindow(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;

            // Zoznam systémových tried ktoré chceme ignorovať
            var systemClasses = new[]
            {
                "Shell_TrayWnd",           // Taskbar
                "DV2ControlHost",          // Start menu
                "MsgrIMEWindowClass",      // IME
                "SysShadow",               // Window shadows
                "Button",                  // Systémové buttony
                "tooltips_class32",        // Tooltips
                "Shell_SecondaryTrayWnd",  // Secondary taskbar
                "WorkerW",                 // Desktop worker
                "Progman",                 // Program manager
                "DWMThumbnailHostWnd",     // DWM thumbnails
                "Windows.UI.Core.CoreWindow", // UWP system windows
                "#32769"                   // Dialog box class
            };

            return systemClasses.Any(sc => 
                className.Equals(sc, StringComparison.OrdinalIgnoreCase) ||
                className.StartsWith(sc, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class WindowInfo
    {
        //public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        //public int ProcessId { get; set; }
        //public string ProcessName { get; set; } = string.Empty;

        public string WindowTitle { get; set; } = "";
        public string WindowClass { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public int ProcessId { get; set; } = 0;
        public IntPtr Handle { get; set; } = IntPtr.Zero;
        public string ErrorMessage { get; set; } = "";

        public override string ToString()
        {
            return $"{ProcessName}: {Title} ({ClassName})";
        }
    }

    public class WindowSearchResult
    {
        public IntPtr Handle { get; set; }
        public string MatchMethod { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public bool IsValid => Handle != IntPtr.Zero;

        public override string ToString()
        {
            return IsValid
                ? $"Found via {MatchMethod} (confidence: {Confidence:P0})"
                : "Window not found";
        }
    }
}
