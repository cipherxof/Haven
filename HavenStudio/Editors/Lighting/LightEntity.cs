using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia3DControl.Core.Models;
using HavenStudio.Formats.Lit;
using OpenTK.Mathematics;

namespace HavenStudio.Editors;

public sealed record LightEntity : MapEntity, INotifyPropertyChanged
{
    private readonly Action<LightEntity, string, Action, Action>? _applyEdit;

    public LightEntity(
        Lighting.LitDocumentSession session,
        int? groupIndex,
        int? recordIndex,
        string displayName,
        Action<LightEntity, string, Action, Action>? applyEdit = null)
        : base(displayName)
    {
        Session = session;
        GroupIndex = groupIndex;
        RecordIndex = recordIndex;
        _applyEdit = applyEdit;
    }

    public Lighting.LitDocumentSession Session { get; }
    public int? GroupIndex { get; }
    public int? RecordIndex { get; }
    public bool IsGlobal => GroupIndex == null;
    public bool CanEditStructure => !IsGlobal;
    public LitGroup? Group => GroupIndex is { } index ? Session.Document.Groups[index] : null;
    public LitLight? Light => Group != null && RecordIndex is { } index ? Group.Lights[index] : null;
    public IReadOnlyList<Model3D> Models { get; internal set; } = [];
    public string FileName => Session.DisplayName;
    public string Kind => IsGlobal ? "Global" : Group?.Type switch
    {
        1 => "Point",
        2 => "Spot",
        4 => "Line",
        8 or 16 => "Black point",
        32 => "Parallel",
        64 => "Projection (raw)",
        { } type => $"Unknown ({type})",
        _ => "Unknown"
    };
    public string PositionText => GetPosition() is { } position
        ? $"{position.X:0.###}, {position.Y:0.###}, {position.Z:0.###}"
        : "Not positional";
    public string DirectionText => GetDirection() is { } direction
        ? $"{direction.X:0.######}, {direction.Y:0.######}, {direction.Z:0.######}, w={direction.W:0.######}"
        : "—";
    public string ColorText => GetColor() is { } color
        ? $"{color.R}, {color.G}, {color.B}, {color.A}"
        : "—";
    public string AmbientText => IsGlobal
        ? FormatColor(Session.Document.Ambient)
        : Light is LitParallelLight parallel ? FormatColor(parallel.Ambient) : "—";
    public string BoundsText => Group is { } group
        ? $"min {FormatVector(group.BoundsMin)} / max {FormatVector(group.BoundsMax)}"
        : "—";
    public string ParametersText => Light switch
    {
        LitPointLight point => $"range={point.Range:0.###}, extended={point.ExtendedRange:0.###}",
        LitSpotLight spot => $"umbra={spot.Umbra:0.######}, penumbra={spot.Penumbra:0.######}",
        LitLineLight line => $"range={line.Range:0.###}, pad=0x{line.Pad:X8}",
        LitBlackPoint blackPoint => $"range={blackPoint.Range:0.###}, pads=0x{blackPoint.Pad0:X8}, 0x{blackPoint.Pad1:X8}",
        LitParallelLight parallel => $"force={parallel.Force:0.######}",
        LitRawLight raw => $"{raw.Data.Length} raw bytes",
        _ when IsGlobal => $"direction.w={Session.Document.Direction.W:0.######}",
        _ => "—"
    };
    public string FlagsText => Light switch
    {
        LitPointLight point => $"0x{point.Flag:X8}",
        LitSpotLight spot => $"0x{spot.Flag:X8}",
        LitLineLight line => $"0x{line.Flag:X8}",
        LitBlackPoint blackPoint => $"0x{blackPoint.Flag:X8}",
        LitParallelLight parallel => $"0x{parallel.Flag:X8}",
        _ when IsGlobal => $"header pad 0x{Session.Document.HeaderPad:X8}",
        _ => "—"
    };
    public string UnknownDataText => Light switch
    {
        LitRawLight raw => Convert.ToHexString(raw.Data),
        { VariantExtra.Length: > 0 } light => Convert.ToHexString(light.VariantExtra),
        _ => "None"
    };
    public bool CanEditPosition => Light is LitPointLight or LitSpotLight or LitLineLight or LitBlackPoint;
    public bool CanEditDirection => IsGlobal || Light is LitSpotLight or LitLineLight or LitParallelLight;
    public bool CanEditColor => IsGlobal || Light is LitPointLight or LitSpotLight or LitLineLight or LitParallelLight;
    public bool CanEditAmbient => IsGlobal || Light is LitParallelLight;
    public bool HasRange => Light is LitPointLight or LitLineLight or LitBlackPoint;
    public bool HasExtendedRange => Light is LitPointLight;
    public bool HasUmbra => Light is LitSpotLight;
    public bool HasPenumbra => Light is LitSpotLight;
    public bool HasForce => Light is LitParallelLight;
    public bool IsOutsideGroupBounds => GetPosition() is { } position && Group is { } group && !group.Contains(position);

    public float PositionX { get => GetPosition()?.X ?? 0; set => SetPositionComponent(0, value); }
    public float PositionY { get => GetPosition()?.Y ?? 0; set => SetPositionComponent(1, value); }
    public float PositionZ { get => GetPosition()?.Z ?? 0; set => SetPositionComponent(2, value); }
    public float DirectionX { get => GetDirection()?.X ?? 0; set => SetDirectionComponent(0, value); }
    public float DirectionY { get => GetDirection()?.Y ?? 0; set => SetDirectionComponent(1, value); }
    public float DirectionZ { get => GetDirection()?.Z ?? 0; set => SetDirectionComponent(2, value); }
    public int ColorR { get => GetColor()?.R ?? 0; set => SetColorComponent(0, value, ambient: false); }
    public int ColorG { get => GetColor()?.G ?? 0; set => SetColorComponent(1, value, ambient: false); }
    public int ColorB { get => GetColor()?.B ?? 0; set => SetColorComponent(2, value, ambient: false); }
    public int ColorA { get => GetColor()?.A ?? 0; set => SetColorComponent(3, value, ambient: false); }
    public int AmbientR { get => GetAmbient()?.R ?? 0; set => SetColorComponent(0, value, ambient: true); }
    public int AmbientG { get => GetAmbient()?.G ?? 0; set => SetColorComponent(1, value, ambient: true); }
    public int AmbientB { get => GetAmbient()?.B ?? 0; set => SetColorComponent(2, value, ambient: true); }
    public int AmbientA { get => GetAmbient()?.A ?? 0; set => SetColorComponent(3, value, ambient: true); }
    public float RangeValue
    {
        get => Light switch
        {
            LitPointLight point => point.Range,
            LitLineLight line => line.Range,
            LitBlackPoint blackPoint => blackPoint.Range,
            _ => 0
        };
        set
        {
            var before = RangeValue;
            Edit("edit light range", before, value, updated =>
            {
                switch (Light)
                {
                    case LitPointLight point: point.Range = updated; break;
                    case LitLineLight line: line.Range = updated; break;
                    case LitBlackPoint blackPoint: blackPoint.Range = updated; break;
                }
            });
        }
    }
    public float ExtendedRangeValue
    {
        get => (Light as LitPointLight)?.ExtendedRange ?? 0;
        set
        {
            if (Light is LitPointLight point)
            {
                Edit("edit extended light range", point.ExtendedRange, value, updated => point.ExtendedRange = updated);
            }
        }
    }
    public float UmbraValue
    {
        get => (Light as LitSpotLight)?.Umbra ?? 0;
        set
        {
            if (Light is LitSpotLight spot)
            {
                Edit("edit spot umbra", spot.Umbra, value, updated => spot.Umbra = updated);
            }
        }
    }
    public float PenumbraValue
    {
        get => (Light as LitSpotLight)?.Penumbra ?? 0;
        set
        {
            if (Light is LitSpotLight spot)
            {
                Edit("edit spot penumbra", spot.Penumbra, value, updated => spot.Penumbra = updated);
            }
        }
    }
    public float ForceValue
    {
        get => (Light as LitParallelLight)?.Force ?? 0;
        set
        {
            if (Light is LitParallelLight parallel)
            {
                Edit("edit parallel light force", parallel.Force, value, updated => parallel.Force = updated);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Vector3? GetPosition() => Light switch
    {
        LitPointLight point => point.Point.Xyz,
        LitSpotLight spot => spot.Point.Xyz,
        LitLineLight line => line.Point.Xyz,
        LitBlackPoint blackPoint => blackPoint.Point.Xyz,
        LitParallelLight when Group is { } group => (group.BoundsMin.Xyz + group.BoundsMax.Xyz) * 0.5f,
        LitRawLight when Group is { } group => (group.BoundsMin.Xyz + group.BoundsMax.Xyz) * 0.5f,
        _ when IsGlobal => Vector3.Zero,
        _ => null
    };

    public Vector4? GetDirection() => Light switch
    {
        LitSpotLight spot => spot.Direction,
        LitLineLight line => line.Direction,
        LitParallelLight parallel => parallel.Direction,
        _ when IsGlobal => Session.Document.Direction,
        _ => null
    };

    public LitColor? GetColor() => Light switch
    {
        LitPointLight point => point.Color,
        LitSpotLight spot => spot.Color,
        LitLineLight line => line.Color,
        LitParallelLight parallel => parallel.Color,
        _ when IsGlobal => Session.Document.Color,
        _ => null
    };

    public LitColor? GetAmbient() => IsGlobal
        ? Session.Document.Ambient
        : Light is LitParallelLight parallel ? parallel.Ambient : null;

    internal void NotifyAllChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    internal bool SetPositionDirect(Vector3 position)
    {
        if (!CanEditPosition || GetPointVector() is not { } point)
        {
            return false;
        }
        SetPointVector(new Vector4(position, point.W));
        NotifyAllChanged();
        return true;
    }

    private void SetPositionComponent(int component, float value)
    {
        if (!CanEditPosition || !float.IsFinite(value) || GetPointVector() is not { } before)
        {
            return;
        }
        var after = before;
        after[component] = value;
        Edit("move light", before, after, SetPointVector);
    }

    private void SetDirectionComponent(int component, float value)
    {
        if (!CanEditDirection || !float.IsFinite(value) || GetDirection() is not { } before)
        {
            return;
        }
        var after = before;
        after[component] = value;
        Edit("edit light direction", before, after, SetDirectionVector);
    }

    private void SetColorComponent(int component, int value, bool ambient)
    {
        value = Math.Clamp(value, byte.MinValue, byte.MaxValue);
        var before = ambient ? GetAmbient() : GetColor();
        if (before is not { } current)
        {
            return;
        }
        var values = new[] { current.R, current.G, current.B, current.A };
        values[component] = (byte)value;
        var after = new LitColor(values[0], values[1], values[2], values[3]);
        Edit(ambient ? "edit light ambient" : "edit light color", current, after,
            updated => SetColor(updated, ambient));
    }

    private Vector4? GetPointVector() => Light switch
    {
        LitPointLight point => point.Point,
        LitSpotLight spot => spot.Point,
        LitLineLight line => line.Point,
        LitBlackPoint blackPoint => blackPoint.Point,
        _ => null
    };

    private void SetPointVector(Vector4 value)
    {
        switch (Light)
        {
            case LitPointLight point: point.Point = value; break;
            case LitSpotLight spot: spot.Point = value; break;
            case LitLineLight line: line.Point = value; break;
            case LitBlackPoint blackPoint: blackPoint.Point = value; break;
        }
    }

    private void SetDirectionVector(Vector4 value)
    {
        if (IsGlobal)
        {
            Session.Document.Direction = value;
            return;
        }
        switch (Light)
        {
            case LitSpotLight spot: spot.Direction = value; break;
            case LitLineLight line: line.Direction = value; break;
            case LitParallelLight parallel: parallel.Direction = value; break;
        }
    }

    private void SetColor(LitColor color, bool ambient)
    {
        if (IsGlobal)
        {
            if (ambient) Session.Document.Ambient = color;
            else Session.Document.Color = color;
            return;
        }
        if (ambient && Light is LitParallelLight ambientParallel)
        {
            ambientParallel.Ambient = color;
            return;
        }
        switch (Light)
        {
            case LitPointLight point: point.Color = color; break;
            case LitSpotLight spot: spot.Color = color; break;
            case LitLineLight line: line.Color = color; break;
            case LitParallelLight parallel: parallel.Color = color; break;
        }
    }

    private void Edit<T>(string description, T before, T after, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(before, after))
        {
            return;
        }

        void Apply(T value)
        {
            setter(value);
            Session.MarkDirty();
            NotifyAllChanged();
        }

        if (_applyEdit == null)
        {
            Apply(after);
            return;
        }
        _applyEdit(this, description, () => Apply(after), () => Apply(before));
    }

    private static string FormatColor(LitColor color) => $"{color.R}, {color.G}, {color.B}, {color.A}";
    private static string FormatVector(Vector4 value) =>
        $"({value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}, {value.W:0.###})";
}

public sealed class LightFileOutline
{
    public LightFileOutline(Lighting.LitDocumentSession session)
    {
        Session = session;
    }

    public Lighting.LitDocumentSession Session { get; }
    public string DisplayName => Session.DisplayName;
    public bool IsExpanded { get; set; }
    public ObservableCollection<object> Children { get; } = [];
}

public sealed class LightGroupOutline
{
    public LightGroupOutline(int index, LitGroup group)
    {
        Index = index;
        Group = group;
    }

    public int Index { get; }
    public LitGroup Group { get; }
    public string DisplayName => $"Group {Index} — type {Group.Type} ({Group.Lights.Count})";
    public bool IsExpanded { get; set; }
    public ObservableCollection<object> Children { get; } = [];
}
