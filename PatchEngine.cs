using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmaliPatcherEx
{
    public class SmaliPatch
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string FileGlob { get; set; } = "";
        public string Search { get; set; } = "";
        public string Replace { get; set; } = "";
        public bool EnabledByDefault { get; set; } = true;
    }

    public static class PatchDefinitions
    {
        public static readonly List<SmaliPatch> All = new List<SmaliPatch>();
    }

    public class PatchResult
    {
        public string PatchName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public bool Applied { get; set; }
        public string Message { get; set; } = "";
    }

    public static class PatchEngine
    {
        public static List<string> FindCandidateFiles(string rootPath, string fileGlob)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return new List<string>();

            if (string.IsNullOrWhiteSpace(fileGlob))
                return new List<string>();

            string normalized = fileGlob.Replace('\\', '/').Trim();

            if (normalized.Contains('/'))
            {
                string fileName = Path.GetFileName(normalized);
                string partialDir = normalized.Substring(0, normalized.Length - fileName.Length).Trim('/');

                return Directory.EnumerateFiles(rootPath, fileName, SearchOption.AllDirectories)
                    .Where(f => f.Replace('\\', '/').IndexOf(partialDir, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return Directory.EnumerateFiles(rootPath, normalized, SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static PatchResult ApplyPatch(string rootPath, SmaliPatch patch)
        {
            var result = new PatchResult
            {
                PatchName = patch?.Name ?? "",
                Applied = false
            };

            if (patch == null)
            {
                result.Message = "Patch is null.";
                return result;
            }

            var files = FindCandidateFiles(rootPath, patch.FileGlob);
            if (files.Count == 0)
            {
                result.Message = "No file matched: " + patch.FileGlob;
                return result;
            }

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);

                if (string.IsNullOrEmpty(patch.Search))
                {
                    result.FilePath = file;
                    result.Message = "Search text is empty.";
                    return result;
                }

                if (text.IndexOf(patch.Search, StringComparison.Ordinal) < 0)
                    continue;

                var updated = text.Replace(patch.Search, patch.Replace ?? "");

                if (updated == text)
                    continue;

                File.WriteAllText(file, updated);
                result.FilePath = file;
                result.Applied = true;
                result.Message = "Patch applied.";
                return result;
            }

            result.Message = "Search pattern not found in any matched file.";
            return result;
        }

        public static List<PatchResult> ApplyPatches(string rootPath, IEnumerable<SmaliPatch> patches)
        {
            var results = new List<PatchResult>();

            if (patches == null)
                return results;

            foreach (var patch in patches)
                results.Add(ApplyPatch(rootPath, patch));

            return results;
        }
    }
}
