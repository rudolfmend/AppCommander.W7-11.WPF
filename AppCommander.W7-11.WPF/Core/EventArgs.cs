using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppCommander.W7_11.WPF.Core
{
    // ===== SPOLOČNÉ DEFINÍCIE EVENT ARGS =====

    /// <summary>
    /// Event args pre aktiváciu okna - jednotná verzia
    /// </summary>
    public class WindowActivatedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    /// <summary>
    /// Event args pre zatvorenie okna - jednotná verzia
    /// </summary>
    public class WindowClosedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    /// <summary>
    /// Event args pre objavenie sa okna - jednotná verzia
    /// </summary>
    public class WindowAppearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public WindowType WindowType { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    /// <summary>
    /// Event args pre zmiznutie okna - jednotná verzia
    /// </summary>
    public class WindowDisappearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    // ===== KROKY NA OPRAVU =====

    /*

    2. UPRAVTE všetky použitia v AutomaticUIManager.cs:
       - Tam kde používate len e.WindowHandle, pridajte e.WindowInfo ak je potrebné
       - Alebo vytvorte WindowTrackingInfo objekt ak nie je dostupný

    3. PRÍKLAD opravy v AutomaticUIManager.cs metóde OnWindowActivated:

       private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
       {
           try
           {
               if (trackedWindows.ContainsKey(e.WindowHandle))
               {
                   var windowState = trackedWindows[e.WindowHandle];
                   windowState.LastActivated = DateTime.Now;

                   // OPRAVENÉ: Použite e.WindowInfo ak je dostupné, alebo vytvorte info
                   var windowInfo = e.WindowInfo ?? CreateWindowInfoFromHandle(e.WindowHandle);

                   System.Diagnostics.Debug.WriteLine($"🎯 Window activated: {windowInfo?.Title ?? "Unknown"}");

                   // Váš zvyšný kód...
               }
           }
           catch (Exception ex)
           {
               System.Diagnostics.Debug.WriteLine($"❌ Error handling window activation: {ex.Message}");
           }
       }

    4. PRIDAJTE helper metódu ak je potrebná:

       private WindowTrackingInfo CreateWindowInfoFromHandle(IntPtr windowHandle)
       {
           if (trackedWindows.ContainsKey(windowHandle))
           {
               var state = trackedWindows[windowHandle];
               return new WindowTrackingInfo
               {
                   WindowHandle = windowHandle,
                   Title = state.WindowTitle,
                   ProcessName = state.WindowProcessName,
                   IsActive = state.IsActive
                   // ... ostatné vlastnosti
               };
           }
           return null;
       }
    */
}
