using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HavenStudio.Rendering;

/// <summary>
/// Lighting-pipeline diagnostics. Writes to the console, to Debug output and to
/// "HavenStudio-lighting.log" next to the executable, so the actual values in
/// use can be inspected instead of assumed.
///
/// Every reverse-engineered stage of the MGS4 lighting port reports here:
/// .abc ambient-cube discovery, LT3 selection and per-class record accounting,
/// the participation gate, the RGBM colour decode, and one fully expanded
/// sample of the final lighting equation.
/// </summary>
public static class Mgs4Diagnostics
{
    private static readonly object Sync = new();
    private static string? _logPath;
    private static bool _headerWritten;

    public static bool Enabled { get; set; } = true;

    public static string LogPath
    {
        get
        {
            if (_logPath != null)
            {
                return _logPath;
            }
            try
            {
                var dir = AppContext.BaseDirectory;
                _logPath = Path.Combine(dir, "HavenStudio-lighting.log");
            }
            catch
            {
                _logPath = "HavenStudio-lighting.log";
            }
            return _logPath;
        }
    }

    public static void Log(string category, string message)
    {
        if (!Enabled)
        {
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] [{category}] {message}";
        Console.WriteLine(line);
        System.Diagnostics.Debug.WriteLine(line);

        lock (Sync)
        {
            try
            {
                if (!_headerWritten)
                {
                    _headerWritten = true;
                    File.AppendAllText(LogPath,
                        $"{Environment.NewLine}==== HavenStudio lighting session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===={Environment.NewLine}");
                }
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch
            {
                // logging must never break rendering
            }
        }
    }

    public static void LogVector(string category, string name, OpenTK.Mathematics.Vector3 v) =>
        Log(category, $"{name} = ({v.X.ToString("F4", CultureInfo.InvariantCulture)}, " +
                      $"{v.Y.ToString("F4", CultureInfo.InvariantCulture)}, " +
                      $"{v.Z.ToString("F4", CultureInfo.InvariantCulture)})");

    /// <summary>Full accounting of an LT3 document: per class, how many records
    /// exist, how many pass the engine gate, and how many are neutralised by an
    /// RGBM scale of zero. This is the table that tells us whether the preview is
    /// summing the same light set the engine does.</summary>
    public static void LogLitInventory(string fileName, HavenStudio.Formats.Lit.LitFile file)
    {
        if (!Enabled || file == null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append($"LT3 '{fileName}': groups={file.Groups.Count}");
        Log("LT3", sb.ToString());

        int gPoint = 0, gSpot = 0, gLine = 0, gPar = 0, gBlack = 0, gHemi = 0;
        int kPoint = 0, kSpot = 0, kLine = 0, kPar = 0, kBlack = 0;
        int zPoint = 0, zSpot = 0, zLine = 0;

        foreach (var group in file.Groups)
        {
            foreach (var light in group.Lights)
            {
                var flag = HavenStudio.Formats.Lit.LitFlags.GetRuntimeFlag(light);
                var passes = HavenStudio.Formats.Lit.LitFlags.Applies(
                    flag, HavenStudio.Formats.Lit.LitLightingTarget.Background);
                switch (light)
                {
                    case HavenStudio.Formats.Lit.LitPointLight p:
                        gPoint++;
                        if (passes) { kPoint++; if (p.Color.A == 0) zPoint++; }
                        break;
                    case HavenStudio.Formats.Lit.LitSpotLight s:
                        gSpot++;
                        if (passes) { kSpot++; if (s.Color.A == 0) zSpot++; }
                        break;
                    case HavenStudio.Formats.Lit.LitLineLight l:
                        gLine++;
                        if (passes) { kLine++; if (l.Color.A == 0) zLine++; }
                        break;
                    case HavenStudio.Formats.Lit.LitParallelLight:
                        gPar++;
                        if (passes) kPar++;
                        break;
                    case HavenStudio.Formats.Lit.LitBlackPoint:
                        gBlack++;
                        if (passes) kBlack++;
                        break;
                    case HavenStudio.Formats.Lit.LitHemiLight:
                        gHemi++;
                        break;
                }
            }
        }

        Log("LT3", $"POINT    total={gPoint,4}  pass gate={kPoint,4}  of which RGBM A=0 -> dead={zPoint}");
        Log("LT3", $"SPOT     total={gSpot,4}  pass gate={kSpot,4}  of which RGBM A=0 -> dead={zSpot}");
        Log("LT3", $"LINE     total={gLine,4}  pass gate={kLine,4}  of which RGBM A=0 -> dead={zLine}");
        Log("LT3", $"PARALLEL total={gPar,4}  pass gate={kPar,4}");
        Log("LT3", $"BLACK    total={gBlack,4}  pass gate={kBlack,4}");
        Log("LT3", $"HEMI     total={gHemi,4}");
        Log("LT3", $"header sun dir={file.Direction} colour(RGBM)={file.Color.ToScaledVector3()} " +
                   $"raw={file.Color}  ambient(RGBM)={file.Ambient.ToScaledVector3()} raw={file.Ambient}");
    }

    /// <summary>One fully expanded evaluation of the lighting equation, so the
    /// relative weight of every term is visible instead of inferred.</summary>
    public static void LogSample(
        OpenTK.Mathematics.Vector3 position,
        OpenTK.Mathematics.Vector3 normal,
        SampledLighting lighting)
    {
        if (!Enabled || lighting == null)
        {
            return;
        }

        Log("SAMPLE", $"position=({position.X:F1}, {position.Y:F1}, {position.Z:F1}) " +
                      $"normal=({normal.X:F3}, {normal.Y:F3}, {normal.Z:F3})");

        if (lighting.AmbientCube is { } cube)
        {
            Log("SAMPLE", $"ambient cube L={cube.Left} R={cube.Right} T={cube.Top} " +
                          $"B={cube.Bottom} F={cube.Front} Bk={cube.Back}");
        }
        else
        {
            Log("SAMPLE", "ambient cube = NONE (falling back to flat ambient)");
        }

        var ambient = lighting.SampleAmbient(normal);
        LogVector("SAMPLE", "ambient at normal", ambient);

        var total = ambient;
        var count = 0;
        foreach (var light in lighting.BakeLights)
        {
            var ndotl = MathF.Max(0f, OpenTK.Mathematics.Vector3.Dot(normal, light.Direction));
            var contribution = light.Color * ndotl;
            total += contribution;
            if (count < 8)
            {
                Log("SAMPLE", $"  light[{count}] dir=({light.Direction.X:F2},{light.Direction.Y:F2}," +
                              $"{light.Direction.Z:F2}) colour=({light.Color.X:F3},{light.Color.Y:F3}," +
                              $"{light.Color.Z:F3}) N.L={ndotl:F3} sun={light.CastsProjectedShadow}");
            }
            count++;
        }

        Log("SAMPLE", $"bake light count = {count} (reduced runtime set = {lighting.DirectionalLights.Count})");
        LogVector("SAMPLE", "TOTAL lighting", total);
        Log("SAMPLE", "engine packing: colour_buffer = clamp(lighting,0,1) * 255 " +
                      "(pool SCALE = 255,255,255,255); vcolor_to_lsc_scl = (0,0,0,1) " +
                      "so the MDN vertex colour does NOT modulate RGB on this path.");
        Log("SAMPLE", "note: a total near (1,1,1) means the preview will look the same " +
                      "with Game lighting on or off, because the MDN vertex colours are modulated by it.");

        // Ambient anisotropy: if the six faces are identical the ambient term
        // cannot produce any directional variation, whatever the lights do.
        if (lighting.AmbientCube is { } c2)
        {
            var min = MathF.Min(MathF.Min(c2.Left.X, c2.Right.X),
                      MathF.Min(MathF.Min(c2.Top.X, c2.Bottom.X),
                                MathF.Min(c2.Front.X, c2.Back.X)));
            var max = MathF.Max(MathF.Max(c2.Left.X, c2.Right.X),
                      MathF.Max(MathF.Max(c2.Top.X, c2.Bottom.X),
                                MathF.Max(c2.Front.X, c2.Back.X)));
            Log("SAMPLE", max - min < 0.001f
                ? $"ambient cube is UNIFORM ({min:F3}) -> no directional variation from ambient"
                : $"ambient cube anisotropy: min={min:F3} max={max:F3} ratio={(min > 0 ? max / min : 0):F2}");
        }
    }
}
