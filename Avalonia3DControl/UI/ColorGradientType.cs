namespace Avalonia3DControl.UI;

public enum GradientBaseType
{
    Classic,
    Thermal,
    Rainbow,
    Monochrome,
    Ocean,
    Fire
}

public readonly record struct ColorGradientType(
    GradientBaseType BaseType,
    bool IsSymmetric = false)
{
    public static readonly ColorGradientType Classic = new(GradientBaseType.Classic);
    public static readonly ColorGradientType ClassicSymmetric = new(GradientBaseType.Classic, true);
    public static readonly ColorGradientType Thermal = new(GradientBaseType.Thermal);
    public static readonly ColorGradientType ThermalSymmetric = new(GradientBaseType.Thermal, true);
    public static readonly ColorGradientType Rainbow = new(GradientBaseType.Rainbow);
    public static readonly ColorGradientType RainbowSymmetric = new(GradientBaseType.Rainbow, true);
    public static readonly ColorGradientType Monochrome = new(GradientBaseType.Monochrome);
    public static readonly ColorGradientType MonochromeSymmetric = new(GradientBaseType.Monochrome, true);
    public static readonly ColorGradientType Ocean = new(GradientBaseType.Ocean);
    public static readonly ColorGradientType OceanSymmetric = new(GradientBaseType.Ocean, true);
    public static readonly ColorGradientType Fire = new(GradientBaseType.Fire);
    public static readonly ColorGradientType FireSymmetric = new(GradientBaseType.Fire, true);

    public override string ToString() => IsSymmetric ? $"{BaseType}Symmetric" : BaseType.ToString();
}
