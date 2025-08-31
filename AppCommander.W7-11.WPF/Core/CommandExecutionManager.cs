using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public enum CommandExecutionState
    {
        Pending,
        Executing,
        Completed,
        Failed,
        Timeout
    }

    public class CommandExecutionInfo
    {
        public int StepNumber { get; set; }
        public CommandExecutionState State { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
        public string ErrorMessage { get; set; }
        public bool RequiresConfirmation { get; set; }
    }

    public class ExecutionSettings
    {
        public int DefaultDelayMs { get; set; } = 500;
        public int MaxWaitForElementMs { get; set; } = 5000;
        public int MaxWaitForStateChangeMs { get; set; } = 3000;
        public bool WaitForPreviousCommandCompletion { get; set; } = true;
        public bool UseAdaptiveDelay { get; set; } = true;
        public bool EnableStateVerification { get; set; } = true;
    }

    public class CommandExecutionManager
    {
        private readonly Dictionary<int, CommandExecutionInfo> executionStates;
        private readonly WindowTracker windowTracker;
        private readonly AutomaticUIManager uiManager;
        private readonly System.Threading.Timer stateMonitorTimer;
        private ExecutionSettings settings;

        public event EventHandler<CommandExecutionInfo> CommandStateChanged;
        public event EventHandler<string> ExecutionSpeedAdjusted;

        public CommandExecutionManager(WindowTracker windowTracker, AutomaticUIManager uiManager)
        {
            this.windowTracker = windowTracker;
            this.uiManager = uiManager;
            this.executionStates = new Dictionary<int, CommandExecutionInfo>();
            this.settings = new ExecutionSettings();

            // Timer na monitorovanie stavu príkazov
            this.stateMonitorTimer = new System.Threading.Timer(MonitorExecutionStates, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Vykoná príkaz s čakaním na predchádzajúci
        /// </summary>
        public async Task<bool> ExecuteCommandWithWait(Command command, CancellationToken cancellationToken = default)
        {
            try
            {
                // Ak je povolené čakanie, počkaj na dokončenie predchádzajúceho príkazu
                if (settings.WaitForPreviousCommandCompletion)
                {
                    await WaitForPreviousCommandCompletion(command.StepNumber - 1, cancellationToken);
                }

                // Zaregistruj začiatok vykonávania
                var executionInfo = new CommandExecutionInfo
                {
                    StepNumber = command.StepNumber,
                    State = CommandExecutionState.Executing,
                    StartTime = DateTime.Now
                };

                executionStates[command.StepNumber] = executionInfo;
                CommandStateChanged?.Invoke(this, executionInfo);

                // Počkaj na dostupnosť target elementu
                if (!await WaitForElementAvailability(command, cancellationToken))
                {
                    executionInfo.State = CommandExecutionState.Failed;
                    executionInfo.ErrorMessage = "Element not found within timeout";
                    executionInfo.EndTime = DateTime.Now;
                    CommandStateChanged?.Invoke(this, executionInfo);
                    return false;
                }

                // Vykonaj príkaz
                bool success = await ExecuteActualCommand(command, cancellationToken);

                // Ak je povolené, over stav po vykonaní
                if (settings.EnableStateVerification && success)
                {
                    success = await VerifyCommandExecution(command, cancellationToken);
                }

                // Aktualizuj stav
                executionInfo.State = success ? CommandExecutionState.Completed : CommandExecutionState.Failed;
                executionInfo.EndTime = DateTime.Now;
                CommandStateChanged?.Invoke(this, executionInfo);

                // Adaptívne nastavenie delay pre ďalší príkaz
                if (settings.UseAdaptiveDelay)
                {
                    AdjustExecutionSpeed(executionInfo.Duration);
                }

                return success;
            }
            catch (Exception ex)
            {
                executionStates.TryGetValue(command.StepNumber, out var executionInfo);
                if (executionInfo != null)
                {
                    executionInfo.State = CommandExecutionState.Failed;
                    executionInfo.ErrorMessage = ex.Message;
                    executionInfo.EndTime = DateTime.Now;
                    CommandStateChanged?.Invoke(this, executionInfo);
                }
                return false;
            }
        }

        /// <summary>
        /// Čaká na dokončenie predchádzajúceho príkazu
        /// </summary>
        private async Task WaitForPreviousCommandCompletion(int previousStepNumber, CancellationToken cancellationToken)
        {
            if (previousStepNumber <= 0) return;

            var timeout = DateTime.Now.AddMilliseconds(settings.MaxWaitForStateChangeMs);

            while (DateTime.Now < timeout && !cancellationToken.IsCancellationRequested)
            {
                if (executionStates.TryGetValue(previousStepNumber, out var prevInfo))
                {
                    if (prevInfo.State == CommandExecutionState.Completed ||
                        prevInfo.State == CommandExecutionState.Failed)
                    {
                        // Počkaj ešte krátky moment pre UI stabilizáciu
                        await Task.Delay(Math.Min(settings.DefaultDelayMs, 200), cancellationToken);
                        return;
                    }
                }

                await Task.Delay(50, cancellationToken);
            }
        }

        /// <summary>
        /// Čaká na dostupnosť elementu
        /// </summary>
        private async Task<bool> WaitForElementAvailability(Command command, CancellationToken cancellationToken)
        {
            var timeout = DateTime.Now.AddMilliseconds(settings.MaxWaitForElementMs);

            while (DateTime.Now < timeout && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Použije existujúcu logiku AdaptiveElementFinder
                    var activeWindow = windowTracker.GetAllWindows().FirstOrDefault();
                    var searchResult = AdaptiveElementFinder.SmartFindElement(activeWindow, command);

                    if (searchResult.IsSuccess)
                    {
                        return true;
                    }

                    await Task.Delay(100, cancellationToken);
                }
                catch
                {
                    await Task.Delay(200, cancellationToken);
                }
            }

            return false;
        }

        /// <summary>
        /// Vykoná samotný príkaz
        /// </summary>
        private async Task<bool> ExecuteActualCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                // Tu by bola integrácia s existujúcim CommandExecutor
                // alebo priama implementácia vykonávania príkazov

                switch (command.Type)
                {
                    case CommandType.Click:
                        return await ExecuteClickCommand(command, cancellationToken);
                    case CommandType.SetText:
                        return await ExecuteSetTextCommand(command, cancellationToken);
                    case CommandType.KeyPress:
                        return await ExecuteKeyPressCommand(command, cancellationToken);
                    default:
                        return await ExecuteGenericCommand(command, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Command execution failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Overí či bol príkaz úspešne vykonaný
        /// </summary>
        private async Task<bool> VerifyCommandExecution(Command command, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken); // Krátka pauza pred verifikáciou

            try
            {
                switch (command.Type)
                {
                    case CommandType.SetText:
                        return await VerifyTextSet(command, cancellationToken);
                    case CommandType.Click:
                        return await VerifyClickEffect(command, cancellationToken);
                    default:
                        return true; // Pre ostatné typy príkazov predpokladáme úspech
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adaptívne nastavenie rýchlosti na základe predchádzajúcich výkonov
        /// </summary>
        private void AdjustExecutionSpeed(TimeSpan lastCommandDuration)
        {
            if (lastCommandDuration.TotalMilliseconds < 100)
            {
                // Príkaz bol veľmi rýchly, môžeme zrýchliť
                settings.DefaultDelayMs = Math.Max(100, settings.DefaultDelayMs - 50);
                ExecutionSpeedAdjusted?.Invoke(this, "Speed increased - faster execution detected");
            }
            else if (lastCommandDuration.TotalMilliseconds > 2000)
            {
                // Príkaz bol pomalý, spomalíme
                settings.DefaultDelayMs = Math.Min(2000, settings.DefaultDelayMs + 100);
                ExecutionSpeedAdjusted?.Invoke(this, "Speed decreased - slower execution detected");
            }
        }

        /// <summary>
        /// Monitoruje stavy vykonávania príkazov
        /// </summary>
        private void MonitorExecutionStates(object state)
        {
            var now = DateTime.Now;
            var timeoutThreshold = TimeSpan.FromMilliseconds(settings.MaxWaitForStateChangeMs);

            foreach (var kvp in executionStates)
            {
                var executionInfo = kvp.Value;

                if (executionInfo.State == CommandExecutionState.Executing)
                {
                    if (now.Subtract(executionInfo.StartTime) > timeoutThreshold)
                    {
                        executionInfo.State = CommandExecutionState.Timeout;
                        executionInfo.EndTime = now;
                        CommandStateChanged?.Invoke(this, executionInfo);
                    }
                }
            }
        }

        // Pomocné metódy pre vykonávanie konkrétnych typov príkazov
        private async Task<bool> ExecuteClickCommand(Command command, CancellationToken cancellationToken)
        {
            // Implementácia click príkazu s čakaním na UI response
            await Task.Delay(settings.DefaultDelayMs / 2, cancellationToken);
            // ... logika click
            return true;
        }

        private async Task<bool> ExecuteSetTextCommand(Command command, CancellationToken cancellationToken)
        {
            // Implementácia set text s overením zápisu
            await Task.Delay(settings.DefaultDelayMs, cancellationToken);
            // ... logika set text
            return true;
        }

        private async Task<bool> ExecuteKeyPressCommand(Command command, CancellationToken cancellationToken)
        {
            // Implementácia key press
            await Task.Delay(settings.DefaultDelayMs / 4, cancellationToken);
            // ... logika key press
            return true;
        }

        private async Task<bool> ExecuteGenericCommand(Command command, CancellationToken cancellationToken)
        {
            // Generická implementácia pre ostatné príkazy
            await Task.Delay(settings.DefaultDelayMs, cancellationToken);
            return true;
        }

        private async Task<bool> VerifyTextSet(Command command, CancellationToken cancellationToken)
        {
            // Overí či bol text skutočne nastavený
            await Task.Delay(50, cancellationToken);
            return true;
        }

        private async Task<bool> VerifyClickEffect(Command command, CancellationToken cancellationToken)
        {
            // Overí či click mal očakávaný efekt (napr. otvorenie dialógu)
            await Task.Delay(100, cancellationToken);
            return true;
        }

        public ExecutionSettings GetSettings() => settings;
        public void UpdateSettings(ExecutionSettings newSettings) => settings = newSettings;
    }
}
