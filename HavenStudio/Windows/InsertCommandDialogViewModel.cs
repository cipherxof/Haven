using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Windows;

public sealed class InsertCommandDialogViewModel : INotifyPropertyChanged
{
    private int _selectedCommandIndex;
    private int _selectedInsertModeIndex;
    private int _selectedCallProcPositionIndex;
    private int _selectedTargetProcedureIndex;
    private string _callProcIdText = string.Empty;
    private byte[]? _generatedBytes;

    public ObservableCollection<InsertModeItem> InsertModes { get; } = new()
    {
        new InsertModeItem("Command", InsertMode.Command),
        new InsertModeItem("Call Proc", InsertMode.CallProc)
    };

    public ObservableCollection<CommandTypeItem> CommandTypes { get; } = new()
    {
        new CommandTypeItem("NewPutObject", GcxCommandType.NewPutObject),
        new CommandTypeItem("NewPutStageModelSet", GcxCommandType.NewPutStageModelSet),
        new CommandTypeItem("NewCamera", GcxCommandType.NewCamera),
        new CommandTypeItem("NewSky", GcxCommandType.NewSky),
    };

    public ObservableCollection<InsertPositionItem> CallProcPositions { get; } = new()
    {
        new InsertPositionItem("Start", InsertPosition.Start),
        new InsertPositionItem("End", InsertPosition.End)
    };

    public ObservableCollection<ParameterViewModel> Parameters { get; } = new();
    public ObservableCollection<string> TargetProcedures { get; } = new();

    public int SelectedInsertModeIndex
    {
        get => _selectedInsertModeIndex;
        set
        {
            if (_selectedInsertModeIndex != value && value >= 0 && value < InsertModes.Count)
            {
                _selectedInsertModeIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCommandMode));
                OnPropertyChanged(nameof(IsCallProcMode));
            }
        }
    }

    public int SelectedCommandIndex
    {
        get => _selectedCommandIndex;
        set
        {
            if (_selectedCommandIndex != value && value >= 0 && value < CommandTypes.Count)
            {
                _selectedCommandIndex = value;
                OnPropertyChanged();
                LoadParameters();
            }
        }
    }

    public int SelectedCallProcPositionIndex
    {
        get => _selectedCallProcPositionIndex;
        set
        {
            if (_selectedCallProcPositionIndex != value && value >= 0 && value < CallProcPositions.Count)
            {
                _selectedCallProcPositionIndex = value;
                OnPropertyChanged();
            }
        }
    }

    public string CallProcIdText
    {
        get => _callProcIdText;
        set
        {
            if (_callProcIdText != value)
            {
                _callProcIdText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCommandMode => InsertModes[_selectedInsertModeIndex].Mode == InsertMode.Command;
    public bool IsCallProcMode => InsertModes[_selectedInsertModeIndex].Mode == InsertMode.CallProc;
    public bool HasTargetProcedures => TargetProcedures.Count > 0;
    public string? SelectedTargetProcedure => _selectedTargetProcedureIndex >= 0 &&
        _selectedTargetProcedureIndex < TargetProcedures.Count
            ? TargetProcedures[_selectedTargetProcedureIndex]
            : null;
    public uint SelectedModelHash
    {
        get
        {
            var parameter = Parameters.FirstOrDefault(item => item.Name == "model" && item.HasValue);
            return parameter?.GetValue() switch
            {
                uint value => value,
                int value => unchecked((uint)value),
                _ => 0
            };
        }
    }
    public int SelectedTargetProcedureIndex
    {
        get => _selectedTargetProcedureIndex;
        set
        {
            if (_selectedTargetProcedureIndex != value && value >= 0 && value < TargetProcedures.Count)
            {
                _selectedTargetProcedureIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTargetProcedure));
            }
        }
    }

    public bool InsertAtStart { get; private set; }

    public byte[]? GeneratedBytes => _generatedBytes;

    public InsertCommandDialogViewModel()
    {
        _selectedInsertModeIndex = 0;
        _selectedCommandIndex = 0;
        _selectedCallProcPositionIndex = 1;
        LoadParameters();
    }

    public void ConfigureNewPutObject(
        uint modelHash,
        Vector3 position,
        IEnumerable<string> targetProcedures,
        string? defaultTargetProcedure)
    {
        SelectedInsertModeIndex = 0;
        SelectedCommandIndex = CommandTypes
            .Select((item, index) => (item, index))
            .First(pair => pair.item.Type == GcxCommandType.NewPutObject)
            .index;
        SetParameter("model", $"0x{modelHash:X}");
        SetParameter("x", MathF.Round(position.X).ToString(System.Globalization.CultureInfo.InvariantCulture));
        SetParameter("z", MathF.Round(position.Z).ToString(System.Globalization.CultureInfo.InvariantCulture));
        SetParameter("y", MathF.Round(position.Y).ToString(System.Globalization.CultureInfo.InvariantCulture));

        TargetProcedures.Clear();
        foreach (var procedure in targetProcedures)
        {
            TargetProcedures.Add(procedure);
        }
        _selectedTargetProcedureIndex = Math.Max(0, TargetProcedures.IndexOf(defaultTargetProcedure ?? string.Empty));
        OnPropertyChanged(nameof(TargetProcedures));
        OnPropertyChanged(nameof(HasTargetProcedures));
        OnPropertyChanged(nameof(SelectedTargetProcedureIndex));
        OnPropertyChanged(nameof(SelectedTargetProcedure));
    }

    private void SetParameter(string name, string value)
    {
        var parameter = Parameters.FirstOrDefault(item => item.Name == name);
        if (parameter != null)
        {
            parameter.TextValue = value;
        }
    }

    private void LoadParameters()
    {
        Parameters.Clear();

        if (_selectedCommandIndex < 0 || _selectedCommandIndex >= CommandTypes.Count)
        {
            return;
        }

        var commandType = CommandTypes[_selectedCommandIndex].Type;
        var paramDefs = GcxCommandBuilder.GetParameters(commandType);

        foreach (var param in paramDefs)
        {
            Parameters.Add(new ParameterViewModel(param));
        }
    }

    public byte[] BuildCommand()
    {
        if (IsCallProcMode)
        {
            InsertAtStart = CallProcPositions[_selectedCallProcPositionIndex].Position == InsertPosition.Start;
            _generatedBytes = BuildCallProcBytes();
            return _generatedBytes;
        }

        InsertAtStart = false;
        var values = new Dictionary<string, object>();
        foreach (var param in Parameters)
        {
            if (param.HasValue)
            {
                values[param.Name] = param.GetValue();
            }
        }

        var commandType = CommandTypes[_selectedCommandIndex].Type;
        _generatedBytes = GcxCommandBuilder.BuildCommand(commandType, values);
        return _generatedBytes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private byte[] BuildCallProcBytes()
    {
        if (!TryParseProcId(_callProcIdText, out var procId))
        {
            return Array.Empty<byte>();
        }

        return new[]
        {
            (byte)0x73,
            (byte)(procId & 0xFF),
            (byte)((procId >> 8) & 0xFF),
            (byte)((procId >> 16) & 0xFF)
        };
    }

    private static bool TryParseProcId(string value, out int procId)
    {
        procId = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hex))
            {
                procId = hex;
                return procId >= 0 && procId <= 0xFFFFFF;
            }

            return false;
        }

        if (int.TryParse(value, out var dec))
        {
            procId = dec;
            return procId >= 0 && procId <= 0xFFFFFF;
        }

        return false;
    }
}

public sealed class CommandTypeItem
{
    public string DisplayName { get; }
    public GcxCommandType Type { get; }

    public CommandTypeItem(string displayName, GcxCommandType type)
    {
        DisplayName = displayName;
        Type = type;
    }
}

public sealed class InsertModeItem
{
    public string DisplayName { get; }
    public InsertMode Mode { get; }

    public InsertModeItem(string displayName, InsertMode mode)
    {
        DisplayName = displayName;
        Mode = mode;
    }
}

public sealed class InsertPositionItem
{
    public string DisplayName { get; }
    public InsertPosition Position { get; }

    public InsertPositionItem(string displayName, InsertPosition position)
    {
        DisplayName = displayName;
        Position = position;
    }
}

public enum InsertMode
{
    Command,
    CallProc
}

public enum InsertPosition
{
    Start,
    End
}

public sealed class ParameterViewModel : INotifyPropertyChanged
{
    private readonly GcxCommandParameter _parameter;
    private string _textValue = string.Empty;
    private bool _boolValue;

    public string Name => _parameter.Name;
    public string Label => _parameter.Label;
    public GcxParamType Type => _parameter.Type;

    public bool IsTextInput => Type == GcxParamType.StrCode || Type == GcxParamType.Int32;
    public bool IsCheckBox => Type == GcxParamType.Boolean;
    public bool IsModelParam => Name == "model" && Type == GcxParamType.StrCode;
    public bool HasValue => Type switch
    {
        GcxParamType.StrCode => !string.IsNullOrWhiteSpace(_textValue),
        GcxParamType.Int32 => !string.IsNullOrWhiteSpace(_textValue),
        GcxParamType.Boolean => _boolValue,
        _ => false
    };

    public string TextValue
    {
        get => _textValue;
        set
        {
            if (_textValue != value)
            {
                _textValue = value;
                OnPropertyChanged();
            }
        }
    }

    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (_boolValue != value)
            {
                _boolValue = value;
                OnPropertyChanged();
            }
        }
    }

    public ParameterViewModel(GcxCommandParameter parameter)
    {
        _parameter = parameter;
        _textValue = string.Empty;
        _boolValue = false;
    }

    public object GetValue()
    {
        return Type switch
        {
            GcxParamType.StrCode => ParseHexOrDecimal(_textValue),
            GcxParamType.Int32 => ParseInt(_textValue),
            GcxParamType.Boolean => _boolValue,
            _ => 0
        };
    }

    private static uint ParseHexOrDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (uint.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hex))
            {
                return hex;
            }
        }

        if (uint.TryParse(value, out var dec))
        {
            return dec;
        }

        return 0;
    }

    private static int ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (int.TryParse(value.Trim(), out var result))
        {
            return result;
        }

        return 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
