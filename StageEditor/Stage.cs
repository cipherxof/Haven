﻿using Haven.Parser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven
{
    public class Stage
    {
        public readonly string Dir;
        public readonly string Key;
        public readonly List<StageFile> Files = new List<StageFile>();
        public readonly StageFile? Geom;

        /// <summary>
        /// Initialzes a new stage.
        /// </summary>
        /// <param name="path">The location of the encrypted stage folder. The folder hierarchy must be correct in order for decryption to work.</param>
        public Stage(string path)
        {
            Dir = path;
            Key = Directory.GetParent(path).Name + "/" + new DirectoryInfo(path).Name;

            var files = Directory.GetFiles(path).ToList();

            foreach (var file in files)
            {
                var stageFile = new StageFile(file);

                if (stageFile.Type == StageFile.FileType.UNK)
                    continue;

                if (file.EndsWith(".geom"))
                    Geom = stageFile;

                Files.Add(stageFile);
            }
        }

        /// <summary>
        /// Decrypts all of the files in the stage.
        /// </summary>
        /// <returns></returns>
        public async Task Decrypt()
        {
            List<Task> tasks = new List<Task>();

            Files.ForEach(file => tasks.Add(Utils.RunProcessAsync("bin/SolidEye.exe", $"-dec {file.SourceFile} -k {Key} -o \"stage\"")));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Encrypts all of the files in the stage.
        /// </summary>
        /// <param name="output"></param>
        /// <returns></returns>
        public async Task Encrypt(string output)
        {
            List<Task> tasks = new List<Task>();

            Files.ForEach(file => tasks.Add(Utils.RunProcessAsync("bin/SolidEye.exe", $"-enc \"{file.GetLocalPath()}\" -k {Key} -o \"{output}\"")));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Unpacks all qar and dar files into their own subdirectory.
        /// </summary>
        /// <returns></returns>
        public async Task Unpack()
        {
            List<Task> tasks = new List<Task>();

            foreach (var file in Files)
            {
                string ext = Path.GetExtension(file.SourceFile);

                if (ext != ".qar" && ext != ".dar") 
                    continue;

                ext = ext.Replace(".", "");

                var outputDir = $"stage/_{file.Name}";

                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);

                tasks.Add(Utils.RunProcessAsync("bin/SolidEye.exe", $"\"{file.GetLocalPath()}\" -f {ext} -o \"{outputDir}\""));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Packs all qar and dar files.
        /// </summary>
        /// <returns></returns>
        public async Task Pack()
        {
            foreach (var file in Files)
            {
                string ext = Path.GetExtension(file.SourceFile);

                if (ext != ".qar" && ext != ".dar")
                    continue;

                ext = ext.Replace(".", "");
                string dst = $"stage/cache.{ext}";

                if (File.Exists(dst))
                    File.Delete(dst);

                await Utils.RunProcessAsync("bin/SolidEye.exe", $"-p \"stage/_{file.Name}/{ext}\" -f {ext} -o \"stage\"");

                string dst2 = file.GetLocalPath();

                if (File.Exists(dst2))
                    File.Delete(dst2);

                File.Move(dst, dst2);
            }
        }
    }

    public class StageFile
    {
        public enum FileType
        {
            UNK,
            CNF,
            DAR,
            DLZ,
            GCX,
            GEOM,
            PTL,
            QAR,
            VFP,
            NNI
        }

        private static Dictionary<string, FileType> Lookup = new Dictionary<string, FileType>()
        {
            { ".cnf", FileType.CNF },
            { ".dar", FileType.DAR },
            { ".dlz", FileType.DLZ },
            { ".gcx", FileType.GCX },
            { ".geom", FileType.GEOM },
            { ".ptl", FileType.PTL },
            { ".qar", FileType.QAR },
            { ".vfp", FileType.VFP },
            { ".nni", FileType.NNI },
        };

        public string Name { get; set; }
        public string SourceFile { get; set; }
        public FileType Type { get; set; }

        /// <summary>
        /// Initialzes a new stage file.
        /// </summary>
        /// <param name="sourceFile"></param>
        public StageFile(string sourceFile)
        {
            SourceFile = sourceFile;
            Name = Path.GetFileName(sourceFile);
            string ext = Path.GetExtension(sourceFile);
            Type = GetTypeFromExt(ext);
        }

        /// <summary>
        /// </summary>
        /// <returns>The local path of the decrypted file.</returns>
        public string GetLocalPath()
        {
            return $"stage\\{Name}.dec";
        }

        /// <summary>
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public static FileType GetTypeFromExt(string ext)
        {
            return Lookup.ContainsKey(ext) ? Lookup[ext] : FileType.UNK;
        }
    }
}
