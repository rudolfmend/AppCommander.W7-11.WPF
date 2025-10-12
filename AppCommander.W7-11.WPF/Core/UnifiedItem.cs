using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Unified item for mixing commands and sequence references in one table
    /// </summary>
    public class UnifiedItem : INotifyPropertyChanged
    {
        #region Enums

        public enum ItemType
        {
            Command,           // Individual recorded command
            SequenceReference, // Reference to saved sequence file
            LoopStart,         // Loop start marker
            LoopEnd,           // Loop end marker
            Wait,              // Wait command
            LiveRecording      // Live recording marker 
        }

        #endregion

        #region Private Fields

        private int _stepNumber;
        private ItemType _type;
        private string _name;
        private string _action;
        private string _value;
        private int _repeatCount;
        private string _status;
        private DateTime _timestamp;
        private string _filePath;
        private int? _elementX;
        private int? _elementY;
        private string _elementId;
        private string _className;
        private bool _isLiveRecording;
        private CommandSequence _liveSequenceReference;
        private int? _unifiedItems;

        #endregion

        #region Public Properties

        public int StepNumber
        {
            get { return _stepNumber; }
            set { _stepNumber = value; OnPropertyChanged(nameof(StepNumber)); }
        }

        public ItemType Type
        {
            get { return _type; }
            set { _type = value; OnPropertyChanged(nameof(Type)); OnPropertyChanged(nameof(TypeDisplay)); }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string Action
        {
            get { return _action; }
            set { _action = value; OnPropertyChanged(nameof(Action)); }
        }

        public string Value
        {
            get { return _value; }
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }

        public int RepeatCount
        {
            get { return _repeatCount; }
            set
            {
                if (value < 1)
                    throw new ArgumentException("Repeat count must be at least 1");
                _repeatCount = value;
                OnPropertyChanged(nameof(RepeatCount));
            }
        }

        public string Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public DateTime Timestamp
        {
            get { return _timestamp; }
            set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); }
        }

        public string FilePath
        {
            get { return _filePath; }
            set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
        }

        public int? ElementX
        {
            get { return _elementX; }
            set { _elementX = value; OnPropertyChanged(nameof(ElementX)); }
        }

        public int? ElementY
        {
            get { return _elementY; }
            set { _elementY = value; OnPropertyChanged(nameof(ElementY)); }
        }

        public string ElementId
        {
            get { return _elementId; }
            set { _elementId = value; OnPropertyChanged(nameof(ElementId)); }
        }

        public string ClassName
        {
            get { return _className; }
            set { _className = value; OnPropertyChanged(nameof(ClassName)); }
        }

        public bool IsLiveRecording
        {
            get { return _isLiveRecording; }
            set { _isLiveRecording = value; OnPropertyChanged(nameof(IsLiveRecording)); }
        }

        public CommandSequence LiveSequenceReference
        {
            get { return _liveSequenceReference; }
            set { _liveSequenceReference = value; OnPropertyChanged(nameof(LiveSequenceReference)); }
        }

        #endregion

        #region Display Properties

        /// <summary>
        /// User-friendly display of item type
        /// </summary>
        public string TypeDisplay
        {
            get
            {
                switch (Type)
                {
                    case ItemType.Command:
                        return "📋 Command";
                    case ItemType.SequenceReference:
                        return "🎬 Sequence";
                    case ItemType.LoopStart:
                        return "🔄 Loop Start";
                    case ItemType.LoopEnd:
                        return "🔚 Loop End";
                    case ItemType.Wait:
                        return "⏱ Wait";
                    case ItemType.LiveRecording:
                        return "🔴 Live Recording"; 
                    default:
                        return Type.ToString();
                }
            }
        }

        /// <summary>
        /// Determines if item can be moved up
        /// </summary>
        public bool CanMoveUp => StepNumber > 1;

        /// <summary>
        /// Determines if item can be moved down (needs to be set externally)
        /// </summary>
        public bool CanMoveDown { get; set; }

        /// <summary>
        /// Display coordinates if available
        /// </summary>
        public string CoordinatesDisplay
        {
            get
            {
                if (ElementX.HasValue && ElementY.HasValue)
                    return $"({ElementX}, {ElementY})";
                return "-";
            }
        }

        #endregion

        #region Constructors

        public UnifiedItem()
        {
            _stepNumber = 1;
            _type = ItemType.Command;
            _name = "";
            _action = "";
            _value = "";
            _repeatCount = 1;
            _status = "Ready";
            _timestamp = DateTime.Now;
            _filePath = "";
            _elementId = "";
            _className = "";
        }

        public UnifiedItem(ItemType type) : this()
        {
            Type = type;
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates UnifiedItem from Command
        /// </summary>
        public static UnifiedItem FromCommand(Command command, int stepNumber)
        {
            var itemType = GetItemTypeFromCommand(command.Type);

            return new UnifiedItem(itemType)
            {
                StepNumber = stepNumber,
                Name = command.ElementName ?? "Unknown Element",
                Action = command.Type.ToString(),
                Value = command.Value ?? "",
                RepeatCount = command.RepeatCount > 0 ? command.RepeatCount : 1,
                Status = "From Recording",
                Timestamp = command.Timestamp,
                ElementX = command.ElementX,
                ElementY = command.ElementY,
                ElementId = command.ElementId ?? "",
                ClassName = command.ElementClass ?? ""
            };
        }

        /// <summary>
        /// Creates UnifiedItem from sequence file
        /// </summary>
        public static UnifiedItem FromSequenceFile(string filePath, int stepNumber)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);

            return new UnifiedItem(ItemType.SequenceReference)
            {
                StepNumber = stepNumber,
                Name = fileName,
                Action = "Execute Sequence",
                Value = filePath,
                RepeatCount = 1,
                Status = "Ready",
                Timestamp = DateTime.Now,
                FilePath = filePath
            };
        }

        /// <summary>
        /// Creates loop start item
        /// </summary>
        public static UnifiedItem CreateLoopStart(int stepNumber, int repeatCount)
        {
            return new UnifiedItem(ItemType.LoopStart)
            {
                StepNumber = stepNumber,
                Name = "Loop Start",
                Action = "Loop",
                Value = repeatCount.ToString(),
                RepeatCount = repeatCount,
                Status = "Ready",
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Creates loop end item
        /// </summary>
        public static UnifiedItem CreateLoopEnd(int stepNumber)
        {
            return new UnifiedItem(ItemType.LoopEnd)
            {
                StepNumber = stepNumber,
                Name = "Loop End",
                Action = "End Loop",
                Value = "",
                RepeatCount = 1,
                Status = "Ready",
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Creates wait item
        /// </summary>
        public static UnifiedItem CreateWait(int stepNumber, int milliseconds)
        {
            return new UnifiedItem(ItemType.Wait)
            {
                StepNumber = stepNumber,
                Name = "Wait",
                Action = "Delay",
                Value = $"{milliseconds}ms",
                RepeatCount = 1,
                Status = "Ready",
                Timestamp = DateTime.Now
            };
        }

        #endregion

        #region Helper Methods

        private static ItemType GetItemTypeFromCommand(CommandType commandType)
        {
            switch (commandType)
            {
                case CommandType.LoopStart:
                    return ItemType.LoopStart;
                case CommandType.LoopEnd:
                    return ItemType.LoopEnd;
                case CommandType.Wait:
                    return ItemType.Wait;
                default:
                    return ItemType.Command;
            }
        }

        /// <summary>
        /// Converts back to Command object
        /// </summary>
        public Command ToCommand()
        {
            if (Type != ItemType.Command && Type != ItemType.LoopStart && Type != ItemType.LoopEnd && Type != ItemType.Wait)
            {
                throw new InvalidOperationException("Cannot convert sequence reference to command");
            }

            var commandType = GetCommandTypeFromItemType(Type);

            return new Command
            {
                StepNumber = StepNumber,
                Type = commandType,
                ElementName = Name,
                Value = Value,
                RepeatCount = RepeatCount,
                Timestamp = Timestamp,
                ElementX = ElementX ?? 0,
                ElementY = ElementY ?? 0,
                ElementId = ElementId,
                ElementClass = ClassName,
                IsLoopStart = Type == ItemType.LoopStart,
                IsLoopEnd = Type == ItemType.LoopEnd
            };
        }

        private CommandType GetCommandTypeFromItemType(ItemType itemType)
        {
            switch (itemType)
            {
                case ItemType.LoopStart:
                    return CommandType.LoopStart;
                case ItemType.LoopEnd:
                    return CommandType.LoopEnd;
                case ItemType.Wait:
                    return CommandType.Wait;
                case ItemType.Command:
                    // Try to parse from Action string
                    if (Enum.TryParse<CommandType>(Action, out CommandType parsed))
                        return parsed;
                    return CommandType.Click; // default
                default:
                    throw new ArgumentException($"Cannot convert {itemType} to CommandType");
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates the unified item
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(Name))
            {
                errorMessage = "Name cannot be empty";
                return false;
            }

            if (RepeatCount < 1)
            {
                errorMessage = "Repeat count must be at least 1";
                return false;
            }

            if (Type == ItemType.SequenceReference && string.IsNullOrWhiteSpace(FilePath))
            {
                errorMessage = "Sequence reference must have a file path";
                return false;
            }

            if (Type == ItemType.SequenceReference && !System.IO.File.Exists(FilePath))
            {
                errorMessage = "Referenced sequence file does not exist";
                return false;
            }

            return true;
        }

        #endregion

        #region Object Overrides

        public override string ToString()
        {
            return $"{StepNumber}. {TypeDisplay}: {Name}";
        }

        public override bool Equals(object obj)
        {
            if (obj is UnifiedItem other)
            {
                return StepNumber == other.StepNumber &&
                       Type == other.Type &&
                       Name == other.Name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (StepNumber, Type, Name).GetHashCode();
        }

        #endregion


        //#region Equality Members

        ///// <summary>
        ///// Overrides Equals to compare UnifiedItems based on their unique properties
        ///// </summary>
        //public override bool Equals(object obj)
        //{
        //    if (obj == null || !(obj is UnifiedItem))
        //        return false;

        //    if (ReferenceEquals(this, obj))
        //        return true;

        //    UnifiedItem other = (UnifiedItem)obj;

        //    // Compare based on StepNumber and Timestamp to ensure uniqueness
        //    // StepNumber alone may not be unique during operations
        //    return this.StepNumber == other.StepNumber &&
        //           this.Timestamp == other.Timestamp &&
        //           this.Type == other.Type;
        //}

        ///// <summary>
        ///// Overrides GetHashCode to generate unique hash based on key properties
        ///// </summary>
        //public override int GetHashCode()
        //{
        //    unchecked
        //    {
        //        // Use multiple properties to generate a more unique hash
        //        int hash = 17;
        //        hash = hash * 23 + StepNumber.GetHashCode();
        //        hash = hash * 23 + Timestamp.GetHashCode();
        //        hash = hash * 23 + Type.GetHashCode();
        //        return hash;
        //    }
        //}

        ///// <summary>
        ///// Implements equality operator
        ///// </summary>
        //public static bool operator ==(UnifiedItem left, UnifiedItem right)
        //{
        //    if (ReferenceEquals(left, null))
        //        return ReferenceEquals(right, null);

        //    return left.Equals(right);
        //}

        ///// <summary>
        ///// Implements inequality operator
        ///// </summary>
        //public static bool operator !=(UnifiedItem left, UnifiedItem right)
        //{
        //    return !(left == right);
        //}

        //#endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Set of unified items
    /// </summary>
    public class UnifiedSequence
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<UnifiedItem> Items { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public string FilePath { get; set; }
        public string TargetProcessName { get; set; }
        public string TargetWindowTitle { get; set; }

        public UnifiedSequence()
        {
            Name = "";
            Description = "";
            Items = new List<UnifiedItem>();
            Created = DateTime.Now;
            LastModified = DateTime.Now;
            FilePath = "";
            TargetProcessName = "";
            TargetWindowTitle = "";
        }

        public UnifiedSequence(string name) : this()
        {
            Name = name;
        }

        /// <summary>
        /// Recalculates step numbers for all items
        /// </summary>
        public void RecalculateStepNumbers()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].StepNumber = i + 1;
                Items[i].CanMoveDown = i < Items.Count - 1;
            }
        }

        /// <summary>
        /// Validates the entire sequence
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            for (int i = 0; i < Items.Count; i++)
            {
                if (!Items[i].IsValid(out string itemError))
                {
                    errors.Add($"Item {i + 1}: {itemError}");
                }
            }

            // Check loop integrity
            var loopStarts = Items.Count(item => item.Type == UnifiedItem.ItemType.LoopStart);
            var loopEnds = Items.Count(item => item.Type == UnifiedItem.ItemType.LoopEnd);

            if (loopStarts != loopEnds)
            {
                errors.Add($"Loop mismatch: {loopStarts} loop starts, {loopEnds} loop ends");
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Converts to legacy CommandSequence format
        /// </summary>
        public CommandSequence ToCommandSequence()
        {
            var commands = new List<Command>();

            Debug.WriteLine($"🔄 Converting UnifiedSequence '{Name}' with {Items.Count} items to CommandSequence...");

            foreach (var item in Items)
            {
                try
                {
                    // === Handling pre LiveRecording ===
                    if (item.Type == UnifiedItem.ItemType.LiveRecording)
                    {
                        Debug.WriteLine($"  Processing LiveRecording item: {item.Name}");

                        // Ak má LiveRecording item referenciu na CommandSequence, načítaj z nej príkazy
                        if (item.LiveSequenceReference != null && item.LiveSequenceReference.Commands != null)
                        {
                            Debug.WriteLine($"  Adding {item.LiveSequenceReference.Commands.Count} commands from LiveSequenceReference");

                            // Opakuj príkazy podľa RepeatCount
                            for (int i = 0; i < item.RepeatCount; i++)
                            {
                                foreach (var cmd in item.LiveSequenceReference.Commands)
                                {
                                    commands.Add(cmd);
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"  Warning: LiveRecording item '{item.Name}' has no LiveSequenceReference");
                        }

                        continue; // Prejdi na ďalší item
                    }

                    // === Handling pre SequenceReference ===
                    if (item.Type == UnifiedItem.ItemType.SequenceReference)
                    {
                        Debug.WriteLine($"  Processing SequenceReference: {item.Name}");

                        // Načítaj a rozbal príkazy zo súboru sekvencie
                        if (!string.IsNullOrEmpty(item.FilePath) && System.IO.File.Exists(item.FilePath))
                        {
                            var json = System.IO.File.ReadAllText(item.FilePath);
                            var loadedSequence = Newtonsoft.Json.JsonConvert.DeserializeObject<CommandSequence>(json);

                            if (loadedSequence != null && loadedSequence.Commands != null)
                            {
                                Debug.WriteLine($"  Loaded {loadedSequence.Commands.Count} commands from file");

                                // Pridaj všetky príkazy zo súboru
                                // Opakuj ich podľa RepeatCount
                                for (int i = 0; i < item.RepeatCount; i++)
                                {
                                    foreach (var cmd in loadedSequence.Commands)
                                    {
                                        commands.Add(cmd);
                                    }
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"  Warning: File '{item.FilePath}' loaded but has no commands");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"  Error: File not found: {item.FilePath}");
                            throw new InvalidOperationException(
                                $"Cannot load sequence reference '{item.Name}': File not found at '{item.FilePath}'");
                        }

                        continue; // Prejdi na ďalší item
                    }

                    // === Handling pre Command items ===
                    if (item.Type == UnifiedItem.ItemType.Command ||
                        item.Type == UnifiedItem.ItemType.LoopStart ||
                        item.Type == UnifiedItem.ItemType.LoopEnd ||
                        item.Type == UnifiedItem.ItemType.Wait)
                    {
                        Debug.WriteLine($"  Processing {item.Type}: {item.Name}");

                        var command = item.ToCommand();
                        if (command != null)
                        {
                            // Opakuj príkaz podľa RepeatCount (ak nie je loop marker)
                            int repeat = (item.Type == UnifiedItem.ItemType.LoopStart ||
                                          item.Type == UnifiedItem.ItemType.LoopEnd) ? 1 : item.RepeatCount;

                            for (int i = 0; i < repeat; i++)
                            {
                                commands.Add(command);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"  Error processing item '{item.Name}': {ex.Message}");
                    throw new InvalidOperationException(
                        $"Cannot convert item '{item.Name}' (type: {item.Type}): {ex.Message}", ex);
                }
            }

            Debug.WriteLine($"✅ Converted to {commands.Count} total commands");

            // Vytvor CommandSequence s výslednými príkazmi
            var result = new CommandSequence
            {
                Name = this.Name,
                Commands = commands,
                TargetProcessName = this.TargetProcessName,
                TargetWindowTitle = this.TargetWindowTitle
            };

            // Renumber commands
            for (int i = 0; i < result.Commands.Count; i++)
            {
                result.Commands[i].StepNumber = i + 1;
            }

            return result;
        }
    }

    #endregion
}
