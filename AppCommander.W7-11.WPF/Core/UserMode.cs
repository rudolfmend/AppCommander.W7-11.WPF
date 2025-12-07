using System;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Definuje používateľské režimy aplikácie
    /// </summary>
    public enum UserMode
    {
        /// <summary>
        /// 🧾 Ekonóm - spracovanie dokumentov, drag & drop, minimal UI
        /// </summary>
        Accountant,

        /// <summary>
        /// 🧪 Tester - UI inspector, element stats, debug, recording
        /// </summary>
        Tester,

        /// <summary>
        /// 📝 Administratívny pracovník - prehrávanie sekvencií, základné ovládanie
        /// </summary>
        Worker,

        /// <summary>
        /// 🛠️ Developer - všetko odomknuté, debug panely, logy
        /// </summary>
        Developer
    }

    /// <summary>
    /// Rozšírenia pre UserMode enum
    /// </summary>
    public static class UserModeExtensions
    {
        /// <summary>
        /// Vráti emoji ikonu pre daný režim
        /// </summary>
        public static string GetIcon(this UserMode mode)
        {
            switch (mode)
            {
                case UserMode.Accountant:
                    return "🧾";
                case UserMode.Tester:
                    return "🧪";
                case UserMode.Worker:
                    return "📝";
                case UserMode.Developer:
                    return "🛠️";
                default:
                    return "❓";
            }
        }

        /// <summary>
        /// Vráti lokalizovaný názov režimu
        /// </summary>
        public static string GetDisplayName(this UserMode mode)
        {
            switch (mode)
            {
                case UserMode.Accountant:
                    return "Accountant";
                case UserMode.Tester:
                    return "Tester";
                case UserMode.Worker:
                    return "Office Worker";
                case UserMode.Developer:
                    return "Developer";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Vráti popis režimu
        /// </summary>
        public static string GetDescription(this UserMode mode)
        {
            switch (mode)
            {
                case UserMode.Accountant:
                    return "Processing documents and invoices. Minimal interface without advanced features.";
                case UserMode.Tester:
                    return "Testing UI elements, recording actions, debug tools.";
                case UserMode.Worker:
                    return "Playing sequences and basic controls. No recording features.";
                case UserMode.Developer:
                    return "All features unlocked. Debug panels, logs, editor.";
                default:
                    return "";
            }
        }
    }
}
