using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmaliPatcherEx
{
    public class SmaliPatch
    {
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string FileGlob { get; init; } = "";
        public string Search { get; init; } = "";
        public string Replace { get; init; } = "";
        public int AndroidMin { get; init; } = 1;
        public int AndroidMax { get; init; } = 99;
        public bool Multi { get; init; } = false;
    }

    public class PatchResult
    {
        public SmaliPatch Patch { get; init; } = null!;
        public bool Applied { get; set; }
        public List<string> Files { get; } = new();
        public string Reason { get; set; } = "";
    }

    public static class PatchDefinitions
    {
        public static readonly List<SmaliPatch> All = new()
        {
            new SmaliPatch
            {
                Name = "template",
                Description = "Template patch entry",
                FileGlob = "LocationProviderManager.smali",
                Search = "THIS_TEXT_DOES_NOT_EXIST",
                Replace = "THIS_TEXT_DOES_NOT_EXIST",
                AndroidMin = 1,
                AndroidMax = 99,
                Multi = false
            }
        };

        public static readonly Dictionary<string, SmaliPatch> Map =
            All.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public class PatchEngine
    {
        private readonly string smaliRoot;
        private readonly int api;

        public Action<string>? Log { get; set; }

        public PatchEngine(string smaliRoot, int api)
        {
            this.smaliRoot = smaliRoot;
            this.api = api;
        }

        private void Write(string msg) => Log?.Invoke(msg);

        private IEnumerable<string> GlobFiles(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || !Directory.Exists(smaliRoot))
                return Enumerable.Empty<string>();

            var normalized = pattern.Replace('\\', '/').Trim();

            if (!normalized.Contains('/'))
            {
                return Directory.GetFiles(smaliRoot, normalized, SearchOption.AllDirectories);
            }

            var parts = normalized.Split('/');
            var fileName = parts[^1];
            var subPath = string.Join("/", parts.Take(parts.Length - 1));

            return Directory.GetFiles(smaliRoot, fileName, SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/').Contains(subPath, StringComparison.OrdinalIgnoreCase));
        }

        public PatchResult Apply(SmaliPatch patch)
        {
            var result = new PatchResult { Patch = patch };

            if (api < patch.AndroidMin || api > patch.AndroidMax)
            {
                result.Reason = $"API {api} out of range ({patch.AndroidMin}-{patch.AndroidMax})";
                return result;
            }

            var targets = GlobFiles(patch.FileGlob).ToList();
            bool anyFile = false;

            foreach (var file in targets)
            {
                anyFile = true;
                var text = File.ReadAllText(file);
                string patched = text;
                int count = 0;

                if (patch.Multi)
                {
                    count = Regex.Matches(text, patch.Search, RegexOptions.Multiline | RegexOptions.Singleline).Count;
                    patched = Regex.Replace(text, patch.Search, patch.Replace, RegexOptions.Multiline | RegexOptions.Singleline);
                }
                else
                {
                    if (Regex.IsMatch(text, patch.Search, RegexOptions.Multiline | RegexOptions.Singleline))
                    {
                        patched = Regex.Replace(
                            text,
                            patch.Search,
                            patch.Replace,
                            RegexOptions.Multiline | RegexOptions.Singleline,
                            TimeSpan.FromSeconds(10));
                        count = patched != text ? 1 : 0;
                    }
                }

                if (patched != text)
                {
                    File.WriteAllText(file, patched);
                    result.Files.Add(Path.GetFileName(file) + (count > 1 ? $" ({count}x)" : ""));
                    result.Applied = true;
                }
            }

            if (!anyFile)
                result.Reason = "No file matched " + patch.FileGlob;
            else if (!result.Applied)
                result.Reason = "Pattern not found in matched files";

            return result;
        }

        public Dictionary<string, PatchResult> RunAll(IEnumerable<string> names)
        {
            var results = new Dictionary<string, PatchResult>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (!PatchDefinitions.Map.TryGetValue(name, out var patch))
                {
                    Write("! Unknown patch: " + name);
                    continue;
                }

                Write("Applying " + name + " ...");
                var r = Apply(patch);
                results[name] = r;

                if (r.Applied)
                    Write("+ " + name + ": " + string.Join(", ", r.Files));
                else
                    Write("- " + name + ": skipped (" + r.Reason + ")");
            }

            return results;
        }
    }
}
