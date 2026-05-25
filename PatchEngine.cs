using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmaliPatcherEx
{
    public class SmaliPatch
    {
        public string Name        { get; init; } = "";
        public string Description { get; init; } = "";
        public string FileGlob    { get; init; } = "";
        public string Search      { get; init; } = "";
        public string Replace     { get; init; } = "";
        public int    AndroidMin  { get; init; } = 1;
        public int    AndroidMax  { get; init; } = 99;
        public bool   Multi       { get; init; } = false;
    }

    public class PatchResult
    {
        public SmaliPatch Patch   { get; init; } = null!;
        public bool Applied       { get; set; }
        public List<string> Files { get; } = new();
        public string Reason      { get; set; } = "";
    }

    public static class PatchDefinitions
    {
        public static readonly List<SmaliPatch> All = new()
        {
            new SmaliPatch
            {
                Name        = "mock_location_appops",
                Description = "Mock Location — flip addTestProvider AppOps branch only",
                FileGlob    = "location/LocationManagerService.smali",
                Search      = "if-nez p5, :cond_13",
                Replace     = "if-eqz p5, :cond_13",
                AndroidMin  = 33,
                AndroidMax  = 36
            }
        };

        public static readonly Dictionary<string, SmaliPatch> Map =
            All.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public class PatchEngine
    {
        private readonly string _smaliRoot;
        private readonly int _api;
        public Action<string>? Log { get; set; }

        public PatchEngine(string smaliRoot, int api)
        {
            _smaliRoot = smaliRoot;
            _api = api;
        }

        private void Write(string msg) => Log?.Invoke(msg);

        private IEnumerable<string> GlobFiles(string pattern)
        {
            var normalizedPattern = pattern.Replace('\\', '/').TrimStart('/');
            var allFiles = Directory.GetFiles(_smaliRoot, "*.smali", SearchOption.AllDirectories);

            return allFiles.Where(file =>
            {
                var relative = Path.GetRelativePath(_smaliRoot, file).Replace('\\', '/');
                return relative.EndsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase);
            });
        }

        public PatchResult Apply(SmaliPatch patch)
        {
            var result = new PatchResult { Patch = patch };

            if (_api < patch.AndroidMin || _api > patch.AndroidMax)
            {
                result.Reason = $"API {_api} out of range [{patch.AndroidMin}–{patch.AndroidMax}]";
                return result;
            }

            var targets = GlobFiles(patch.FileGlob).ToList();
            if (targets.Count == 0)
            {
                result.Reason = $"No file matched: {patch.FileGlob}";
                return result;
            }

            foreach (var file in targets)
            {
                var text = File.ReadAllText(file);

                if (!text.Contains(patch.Search))
                    continue;

                var patched = patch.Multi
                    ? text.Replace(patch.Search, patch.Replace)
                    : ReplaceFirst(text, patch.Search, patch.Replace);

                if (patched != text)
                {
                    File.WriteAllText(file, patched);
                    var relative = Path.GetRelativePath(_smaliRoot, file).Replace('\\', '/');
                    result.Files.Add($"{relative} (1×)");
                    result.Applied = true;
                }
            }

            if (!result.Applied)
                result.Reason = "Pattern not found in matched file(s)";

            return result;
        }

        private static string ReplaceFirst(string text, string search, string replace)
        {
            var index = text.IndexOf(search, StringComparison.Ordinal);
            if (index < 0)
                return text;

            return text.Substring(0, index) + replace + text.Substring(index + search.Length);
        }

        public Dictionary<string, PatchResult> RunAll(IEnumerable<string> names)
        {
            var results = new Dictionary<string, PatchResult>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (!PatchDefinitions.Map.TryGetValue(name, out var patch))
                {
                    Write($"[!] Unknown patch: {name}");
                    continue;
                }

                Write($"[*] Applying: {name} ...");
                var r = Apply(patch);
                results[name] = r;

                if (r.Applied)
                    Write($"[✓] {name}: {string.Join(", ", r.Files)}");
                else
                    Write($"[-] {name}: skipped — {r.Reason}");
            }

            return results;
        }
    }
}
