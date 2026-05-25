using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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
        public SmaliPatch    Patch   { get; init; } = null!;
        public bool          Applied { get; set; }
        public List<string>  Files   { get; } = new();
        public string        Reason  { get; set; } = "";
    }

    public static class PatchDefinitions
    {
        public static readonly List<SmaliPatch> All = new()
        {
            // ── MOCK LOCATION ──────────────────────────────────────────────────────
            // Android 13–16: addTestProvider uses AppOpsHelper.noteOp(...)Z.
            new SmaliPatch
{
    Name        = "mock_location_appops",
    Description = "Mock Location — invert noteOp gate (Android 13–16)",
    FileGlob    = "location/LocationManagerService.smali",
    Search      = @"(move-result p5\s*\n\s*)if-nez p5, (:cond_13)",
    Replace     = "$1if-eqz p5, $2",
    AndroidMin  = 13,
}

            // AppOpsHelper noteOp itself – force allow.
            new SmaliPatch
            {
                Name        = "mock_location_appops_helper",
                Description = "Mock Location — AppOpsHelper.noteOp always allow (Android 14–16)",
                FileGlob    = "location/injector/AppOpsHelper.smali",
                Search      = @"(\.method public(?: final)? noteOp\(ILandroid/location/util/identity/CallerIdentity;\)Z\s*\n)(\s*\.registers \d+|\s*\.locals \d+)",
                Replace     = @"$1    .locals 1
    const/4 v0, 0x1
    return v0

    # patched: AppOpsHelper noteOp allow
",
                AndroidMin  = 14,
            },

            // Keep placeholder for LocationProviderManager if you later want to patch it.
            new SmaliPatch
            {
                Name        = "mock_location_provider_manager",
                Description = "Mock Location — LocationProviderManager setMockProvider path (Android 13–16)",
                FileGlob    = "location/provider/LocationProviderManager.smali",
                Search      = @"(\.method .* setMockProvider\(Lcom/android/server/location/provider/MockLocationProvider;\)V\s*\n)(\s*\.registers \d+|\s*\.locals \d+)",
                Replace     = "$1$2\n",
                AndroidMin  = 13,
            },

            // ── MOCK PERMISSION ────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "mock_permission_dpm",
                Description = "Mock Permission — DevicePolicyManager DISALLOW_MOCK_LOCATION",
                FileGlob    = "devicepolicy/DevicePolicyManagerService.smali",
                Search      = @"(const-string [vp]\d+, ""no_mock_location""\s*\n)(\s*invoke-\w+ \{[^}]+\}[^\\n]+\n)(\s*(?:move-result|move-result-object) [vp]\d+\s*\n)(\s*if-(?:eq|ne)z? [vp]\d+[^:]*:cond_\w+)",
                Replace     = @"$1    # SmaliPatcherEx: mock_permission bypass
    goto :goto_mock_perm_ok",
                AndroidMin  = 5,
                Multi       = true,
            },
            new SmaliPatch
            {
                Name        = "mock_permission_restrictions",
                Description = "Mock Permission — UserRestrictionsUtils (Android 11+)",
                FileGlob    = "pm/UserRestrictionsUtils.smali",
                Search      = @"(const-string [vp]\d+, ""no_mock_location""\s*\n)((?:.*\n)*?)(\s*invoke-\w+ \{[^}]+\}[^\\n]*\(Landroid/os/Bundle;Ljava/lang/String;\)[^\\n]*\n)",
                Replace     = @"$1$2    # patched: mock_permission_restrictions
",
                AndroidMin  = 11,
            },

            // Force LOCATION_BYPASS permission success in LocationPermissions.
            new SmaliPatch
            {
                Name        = "mock_permission_location_bypass",
                Description = "Mock Permission — force LOCATION_BYPASS success in LocationPermissions",
                FileGlob    = "location/LocationPermissions.smali",
                Search      = @"(\.method public static enforceBypassPermission\(Landroid/content/Context;II\)V\s*\n)(\s*\.registers \d+|\s*\.locals \d+)",
                Replace     = @"$1    .locals 0
    return-void

    # patched: LOCATION_BYPASS always allowed
",
                AndroidMin  = 13,
            },

            // ── GNSS ───────────────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "gnss_mock_provider",
                Description = "GNSS Mock Provider — GnssManagerService (Android 13–16)",
                FileGlob    = "location/gnss/GnssManagerService.smali",
                Search      = @"(invoke-virtual \{[vp]\d+(?:, [vp]\d+)*\}, Landroid/app/AppOpsManager;->(?:checkOp|noteOp|noteOpNoThrow)\([^)]+\)[IZ]\s*\n)(\s*move-result [vp]\d+\s*\n)(\s*(?:const/4|const/16) [vp]\d+, 0x0\s*\n)(\s*if-(?:eq|ne) [vp]\d+, [vp]\d+[^:]*:cond_\w+)",
                Replace     = @"$1$2$3    goto :goto_gnss_ok",
                AndroidMin  = 13,
                Multi       = true,
            },

            // ── SIGNATURE SPOOFING ─────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "signature_spoofing_pms",
                Description = "Signature Spoofing — PackageManagerService (Android 5–13)",
                FileGlob    = "pm/PackageManagerService.smali",
                Search      = @"(\.method public checkSignatures\(II\)I\s*\n)(\s*\.locals \d+|\s*\.registers \d+)",
                Replace     = @"$1    .locals 1
    const/4 v0, 0x0
    return v0

    # SmaliPatcherEx: sig_spoof
",
                AndroidMin  = 5,
                AndroidMax  = 13,
            },
            new SmaliPatch
            {
                Name        = "signature_spoofing_computer",
                Description = "Signature Spoofing — ComputerEngine (Android 14–16)",
                FileGlob    = "pm/ComputerEngine.smali",
                Search      = @"(\.method public checkSignatures\(II\)I\s*\n)(\s*\.locals \d+|\s*\.registers \d+)",
                Replace     = @"$1    .locals 1
    const/4 v0, 0x0
    return v0

    # SmaliPatcherEx: sig_spoof_computer
",
                AndroidMin  = 14,
            },

            // ── NO PERMISSION REVIEW ───────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "no_permission_review",
                Description = "No Permission Review — skip REVIEW_REQUIRED flag",
                FileGlob    = "permission/PermissionManagerService.smali",
                Search      = @"(const-string [vp]\d+, ""android\.permission\.REVIEW_REQUIRED""\s*\n)((?:.*\n)*?)(\s*if-(?:eq|ne)z? [vp]\d+[^:]*:cond_\w+)",
                Replace     = @"$1$2    # SmaliPatcherEx: no_perm_review skipped
    goto :goto_perm_ok",
                AndroidMin  = 10,
                Multi       = true,
            },

            // ── DOZE WHITELIST ─────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "doze_whitelist",
                Description = "Doze Whitelist — DeviceIdleController.isWhitelisted always true",
                FileGlob    = "DeviceIdleController.smali",
                Search      = @"(\.method public isWhitelisted\(Ljava/lang/String;Z\)Z\s*\n)(\s*\.locals \d+|\s*\.registers \d+)",
                Replace     = @"$1    .locals 1
    const/4 v0, 0x1
    return v0

    # SmaliPatcherEx: doze_whitelist
",
                AndroidMin  = 6,
            },
        };

        public static readonly Dictionary<string, SmaliPatch> Map =
            All.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public class PatchEngine
    {
        private readonly string _smaliRoot;
        private readonly int    _api;
        public Action<string>?  Log { get; set; }

        public PatchEngine(string smaliRoot, int api)
        {
            _smaliRoot = smaliRoot;
            _api       = api;
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
                var text    = File.ReadAllText(file);
                var options = RegexOptions.Multiline | RegexOptions.Singleline;

                var matches = Regex.Matches(text, patch.Search, options, TimeSpan.FromSeconds(10));
                var count   = matches.Count;
                if (count == 0)
                    continue;

                string patched;
                if (patch.Multi)
                {
                    patched = Regex.Replace(text, patch.Search, patch.Replace, options, TimeSpan.FromSeconds(10));
                }
                else
                {
                    var rx = new Regex(patch.Search, options, TimeSpan.FromSeconds(10));
                    patched = rx.Replace(text, patch.Replace, 1);
                    count = 1;
                }

                if (patched != text)
                {
                    File.WriteAllText(file, patched);
                    var relative = Path.GetRelativePath(_smaliRoot, file).Replace('\\', '/');
                    result.Files.Add($"{relative} ({count}×)");
                    result.Applied = true;
                }
            }

            if (!result.Applied)
                result.Reason = "Pattern not found in matched file(s)";

            return result;
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
