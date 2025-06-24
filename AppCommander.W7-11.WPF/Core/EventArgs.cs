// Dodatočné triedy potrebné pre kompatibilitu
using System;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    public class WindowTrackingData
    {
        public WindowTrackingInfo Info { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsTracked { get; set; }
    }
}
