using System;

namespace Avalonia3DControl.Rendering.OpenGL
{
    /// <summary>
    /// File diagnostics ("HavenStudio-render.log" next to the executable) for
    /// the render pipeline: execution ORDER of the passes, glare state, and
    /// effective defaults. Console output is invisible in a GUI build, so all
    /// decisions land in this file, deduplicated (a line is written only when
    /// its message changes) and hard-capped. Same pattern as ShadowLog.
    /// </summary>
    public static class RenderLog
    {
        private static readonly object Sync = new();
        private static string? _lastOrder;
        private static string? _lastState;
        private static string? _lastGlare;
        private static int _budget = 400;
        private static bool _header;

        public static void Order(string message) => Write(ref _lastOrder, "[ORDER] " + message);
        public static void State(string message) => Write(ref _lastState, "[STATE] " + message);
        public static void Glare(string message) => Write(ref _lastGlare, "[GLARE] " + message);

        private static void Write(ref string? last, string line)
        {
            lock (Sync)
            {
                if (line == last || _budget <= 0)
                {
                    return;
                }
                last = line;
                _budget--;
                try
                {
                    var path = System.IO.Path.Combine(AppContext.BaseDirectory, "HavenStudio-render.log");
                    if (!_header)
                    {
                        _header = true;
                        System.IO.File.AppendAllText(path,
                            $"{Environment.NewLine}==== render session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===={Environment.NewLine}");
                    }
                    System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
                }
                catch
                {
                }
            }
        }
    }
}
