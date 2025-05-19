using Microsoft.Win32;
using System;
using System.Diagnostics;
using Serilog;
using System.Security.Cryptography;

namespace Haven
{
    public static class FileSelector
    {
        public static bool IsRunningUnderWine()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Wine"))
            {
                return key != null;
            }
        }

        public static string? GetFolderPath(bool forceNativeWindows = false)
        {
            if (forceNativeWindows || !IsRunningUnderWine())
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        return fbd.SelectedPath;
                    }
                }

                return null;
            }

            return LaunchZenity("--file-selection --directory", null, true);
        }

        public static string? GetFilePath(string? filter = "All files (*.*)|*.*", bool forceNativeWindows = false)
        {
            if (forceNativeWindows || !IsRunningUnderWine())
            {
                using (var ofd = new OpenFileDialog())
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        ofd.Filter = filter;
                        ofd.FilterIndex = 1;
                        ofd.RestoreDirectory = true;
                    }

                    if (ofd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(ofd.FileName))
                    {
                        return ofd.FileName;
                    }
                }

                return null;
            }

            return LaunchZenity("--file-selection", filter, false);
        }

        private static string? LaunchZenity(string arguments, string? filter, bool isDirectory)
        {
            try
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    arguments += ConvertFilterToZenity(filter);
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "zenity",
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                if (!process.Start())
                {
                    Log.Debug("Failed to start Zenity.");
                    return null;
                }

                string result = process.StandardOutput.ReadLine() ?? string.Empty;
                string errorOutput = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.Debug($"Zenity exited with error code {process.ExitCode}: {errorOutput}");
                    return null;
                }

                if (isDirectory && !Directory.Exists(result))
                {
                    Log.Debug($"Selected path is not a directory: {result}");
                    return null;
                }

                if (!isDirectory && !File.Exists(result))
                {
                    Log.Debug($"Selected path is not a valid file: {result}");
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Debug($"Exception while launching Zenity: {ex.Message}");
                return null;
            }
        }

        private static string ConvertFilterToZenity(string filter)
        {
            string[] filterParts = filter.Split('|');
            if (filterParts.Length % 2 != 0) return string.Empty;

            string zenityFilter = "";

            for (int i = 0; i < filterParts.Length; i += 2)
            {
                string description = filterParts[i];
                string pattern = filterParts[i + 1];

                zenityFilter += $" --file-filter=\"{description} | {pattern}\"";
            }

            return zenityFilter;
        }
    }
}
