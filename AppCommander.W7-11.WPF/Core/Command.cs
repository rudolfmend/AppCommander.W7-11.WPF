using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace AppCommander.W7_11.WPF.Core
{
    public enum CommandType
    {
        KeyPress,
        MouseClick,
        SetText,
        Wait,
        Loop,
        LoopEnd,
        Click,
        DoubleClick,
        RightClick
    }

    public class Command
    {
        public int StepNumber { get; set; }
        public string ElementName { get; set; } = string.Empty;
        public CommandType Type { get; set; }
        public int RepeatCount { get; set; } = 1;
        public string Value { get; set; } = string.Empty;
        public string TargetWindow { get; set; } = string.Empty;
        public string TargetProcess { get; set; } = string.Empty;
        public bool IsLoopStart { get; set; }
        public bool IsLoopCommand { get; set; } // Commands inside loop (starts with -)
        public DateTime Timestamp { get; set; }

        // UI Element identification
        public string ElementId { get; set; } = string.Empty;
        public string ElementClass { get; set; } = string.Empty;
        public string ElementControlType { get; set; } = string.Empty;
        public int ElementX { get; set; }
        public int ElementY { get; set; }

        // Key-specific data
        public Keys Key { get; set; }

        // Mouse-specific data  
        public MouseButtons MouseButton { get; set; }

        public Command()
        {
            Timestamp = DateTime.Now;
        }

        public Command(int stepNumber, string elementName, CommandType type)
        {
            StepNumber = stepNumber;
            ElementName = elementName;
            Type = type;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Converts command to file format string
        /// Format: StepNumber, ElementName, Type, RepeatCount, Value, [:]
        /// </summary>
        public string ToFileFormat()
        {
            string prefix = IsLoopCommand ? "-" : "";
            string suffix = IsLoopStart ? ":" : "";

            return $"{prefix}{StepNumber}, {ElementName}, {Type}, {RepeatCount}, \"{Value}\", {suffix}".TrimEnd(' ', ',');
        }

        /// <summary>
        /// Creates command from file format string
        /// </summary>
        public static Command FromFileFormat(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var parts = line.Split(',');
            if (parts.Length < 4)
                return null;

            var command = new Command();

            // Check if it's a loop command (starts with -)
            string stepPart = parts[0].Trim();
            if (stepPart.StartsWith("-"))
            {
                command.IsLoopCommand = true;
                stepPart = stepPart.Substring(1);
            }

            // Parse step number
            if (!int.TryParse(stepPart, out int stepNumber))
                return null;

            command.StepNumber = stepNumber;
            command.ElementName = parts[1].Trim();

            // Parse command type
            if (!Enum.TryParse<CommandType>(parts[2].Trim(), out CommandType type))
                return null;
            command.Type = type;

            // Parse repeat count
            if (parts.Length > 3 && int.TryParse(parts[3].Trim(), out int repeatCount))
                command.RepeatCount = repeatCount;

            // Parse value (remove quotes)
            if (parts.Length > 4)
            {
                command.Value = parts[4].Trim().Trim('"');
            }

            // Check if it's loop start (ends with :)
            if (line.TrimEnd().EndsWith(":"))
            {
                command.IsLoopStart = true;
            }

            return command;
        }

        public override string ToString()
        {
            return $"Step {StepNumber}: {Type} on {ElementName} (x{RepeatCount})";
        }
    }

    public class CommandSequence
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Command> Commands { get; set; } = new List<Command>();
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public string TargetApplication { get; set; } = string.Empty;

        // Nové vlastnosti pre adaptívne spustenie
        public string TargetProcessName { get; set; } = string.Empty;
        public string TargetWindowTitle { get; set; } = string.Empty;
        public string TargetWindowClass { get; set; } = string.Empty;
        public bool AutoFindTarget { get; set; } = true;
        public int MaxWaitTimeSeconds { get; set; } = 30;

        public CommandSequence()
        {
            Created = DateTime.Now;
            LastModified = DateTime.Now;
        }

        public CommandSequence(string name) : this()
        {
            Name = name;
        }

        public void AddCommand(Command command)
        {
            Commands.Add(command);
            LastModified = DateTime.Now;
        }

        public void RemoveCommand(int stepNumber)
        {
            Commands.RemoveAll(c => c.StepNumber == stepNumber);
            LastModified = DateTime.Now;
        }

        public Command GetCommand(int stepNumber)
        {
            return Commands.Find(c => c.StepNumber == stepNumber);
        }

        /// <summary>
        /// Saves command sequence to file in the defined format
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var lines = new List<string>();

            // Add header comment with metadata
            lines.Add($"# AppCommander Command File");
            lines.Add($"# Name: {Name}");
            lines.Add($"# Description: {Description}");
            lines.Add($"# Created: {Created:yyyy-MM-dd HH:mm:ss}");
            lines.Add($"# Target Application: {TargetApplication}");
            lines.Add($"# Target Process: {TargetProcessName}");
            lines.Add($"# Target Window Title: {TargetWindowTitle}");
            lines.Add($"# Target Window Class: {TargetWindowClass}");
            lines.Add($"# Auto Find Target: {AutoFindTarget}");
            lines.Add($"# Max Wait Time: {MaxWaitTimeSeconds}");
            lines.Add($"# Format: StepNumber, ElementName, Type, RepeatCount, Value");
            lines.Add("");

            // Add commands
            foreach (var command in Commands)
            {
                lines.Add(command.ToFileFormat());
            }

            System.IO.File.WriteAllLines(filePath, lines);
        }

        /// <summary>
        /// Loads command sequence from file
        /// </summary>
        public static CommandSequence LoadFromFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                return null;

            var sequence = new CommandSequence();
            sequence.Name = System.IO.Path.GetFileNameWithoutExtension(filePath);

            var lines = System.IO.File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                // Skip comments and empty lines
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                {
                    // Try to extract metadata from comments
                    if (line.StartsWith("# Name:"))
                        sequence.Name = line.Substring(7).Trim();
                    else if (line.StartsWith("# Description:"))
                        sequence.Description = line.Substring(14).Trim();
                    else if (line.StartsWith("# Target Application:"))
                        sequence.TargetApplication = line.Substring(21).Trim();
                    else if (line.StartsWith("# Target Process:"))
                        sequence.TargetProcessName = line.Substring(17).Trim();
                    else if (line.StartsWith("# Target Window Title:"))
                        sequence.TargetWindowTitle = line.Substring(22).Trim();
                    else if (line.StartsWith("# Target Window Class:"))
                        sequence.TargetWindowClass = line.Substring(22).Trim();
                    else if (line.StartsWith("# Auto Find Target:"))
                    {
                        if (bool.TryParse(line.Substring(19).Trim(), out bool autoFind))
                            sequence.AutoFindTarget = autoFind;
                    }
                    else if (line.StartsWith("# Max Wait Time:"))
                    {
                        if (int.TryParse(line.Substring(16).Trim(), out int maxWait))
                            sequence.MaxWaitTimeSeconds = maxWait;
                    }

                    continue;
                }

                var command = Command.FromFileFormat(line);
                if (command != null)
                {
                    sequence.Commands.Add(command);
                }
            }

            return sequence; // Pridané return statement
        }

        /// <summary>
        /// Gets next step number for new command
        /// </summary>
        public int GetNextStepNumber()
        {
            if (Commands.Count == 0)
                return 1;

            int maxStep = 0;
            foreach (var cmd in Commands)
            {
                if (cmd.StepNumber > maxStep)
                    maxStep = cmd.StepNumber;
            }

            return maxStep + 1;
        }

        /// <summary>
        /// Validates command sequence for common issues
        /// </summary>
        public List<string> Validate()
        {
            var issues = new List<string>();

            // Check for duplicate step numbers
            var stepNumbers = new HashSet<int>();
            foreach (var cmd in Commands)
            {
                if (stepNumbers.Contains(cmd.StepNumber))
                    issues.Add($"Duplicate step number: {cmd.StepNumber}");
                else
                    stepNumbers.Add(cmd.StepNumber);
            }

            // Check for loop consistency
            int loopStartCount = 0;
            int loopEndCount = 0;
            foreach (var cmd in Commands)
            {
                if (cmd.IsLoopStart)
                    loopStartCount++;
                if (cmd.Type == CommandType.LoopEnd)
                    loopEndCount++;
            }

            if (loopStartCount != loopEndCount)
                issues.Add($"Unmatched loops: {loopStartCount} starts, {loopEndCount} ends");

            return issues;
        }

        public override string ToString()
        {
            return $"{Name} ({Commands.Count} commands)";
        }
    }

    /// <summary>
    /// Statistics for recorded UI elements usage
    /// </summary>
    public class ElementUsageStats
    {
        public string ElementName { get; set; } = string.Empty;
        public string ElementType { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public int ClickCount { get; set; }
        public int KeyPressCount { get; set; }
        public int TotalUsage { get; set; }
        public DateTime FirstUsed { get; set; }
        public DateTime LastUsed { get; set; }
        public List<string> ActionsPerformed { get; set; } = new List<string>();

        public void IncrementUsage(CommandType actionType)
        {
            TotalUsage++;
            LastUsed = DateTime.Now;

            if (FirstUsed == DateTime.MinValue)
                FirstUsed = DateTime.Now;

            switch (actionType)
            {
                case CommandType.Click:
                case CommandType.DoubleClick:
                case CommandType.RightClick:
                case CommandType.MouseClick:
                    ClickCount++;
                    break;
                case CommandType.KeyPress:
                    KeyPressCount++;
                    break;
            }

            if (!ActionsPerformed.Contains(actionType.ToString()))
                ActionsPerformed.Add(actionType.ToString());
        }

        public override string ToString()
        {
            return $"{ElementName} ({ControlType}): {TotalUsage} uses";
        }
    }
}
