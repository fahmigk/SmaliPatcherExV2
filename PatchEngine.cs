using System;
using System.Collections.Generic;
using System.IO;
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
        public SmaliPatch Patch    { get; init; } = null!;
        public bool       Applied  { get; set; }
        public List<string> Files  { get; } = new();
        public string     Reason   { get; set; } = "";
    }

    public static class PatchDefinitions
    {
        public static readonly List<SmaliPatch> All = new()
        {
            // ── MOCK LOCATION ──────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "mock_location_appops",
                Description = "Mock Location — bypass AppOps MOCK_LOCATION check",
                FileGlob    = "location/LocationManagerService.smali",
                Search      = @"(invoke-virtual \{[vp]\d+(?:, [vp]\d+)*\}, Landroid/app/AppOpsManager;->(?:checkOp|noteOp|checkOpNoThrow)\([^)]+\)I\s*\n\s*move-result ([vp]\d+)\s*\n)(\s*(?:const/4|const/16) [vp]\d+, 0x0\s*\n)(\s*if-(?:ne|eq)z? \2[^:]*:cond_\w+)",
                Replace     = "$1$3    goto :goto_mock_allowed",
                AndroidMin  = 10,
                Multi       = true,
            },
            new SmaliPatch
            {
                Name        = "mock_location_isprovider",
                Description = "Mock Location — isMockProvider always returns true",
                FileGlob    = "location/LocationManagerService.smali",
                Search      = @"(\.method (?:public|private|protected)(?: \w+)* isMockProvider\(Ljava/lang/String;\)Z\s*\n)(\s*\.locals \d+)",
                Replace     = "$1    .locals 1\n    const/4 v0, 0x1\n    return v0\n\n    # patched by SmaliPatcherEx v2.0\n",
                AndroidMin  = 12,
            },
            new SmaliPatch
            {
                Name        = "mock_location_provider_manager",
                Description = "Mock Location — LocationProviderManager (Android 13–16)",
                FileGlob    = "location/provider/LocationProviderManager.smali",
                Search      = @"(invoke-virtual \{[vp]\d+(?:, [vp]\d+)*\}, Lcom/android/server/location/injector/AppOpsHelper;->checkOpNoThrow\([^)]+\)[IZ]\s*\n)(\s*move-result [vp]\d+\s*\n)(\s*if-(?:ne|eq)z? [vp]\d+[^:]*:cond_\w+)",
                Replace     = "$1$2    goto :goto_mock_ok",
                AndroidMin  = 13,
                Multi       = true,
            },
            new SmaliPatch
            {
                Name        = "mock_location_appops_helper",
                Description = "Mock Location — AppOpsHelper.checkOpNoThrow (Android 14–16)",
                FileGlob    = "location/injector/AppOpsHelper.smali",
                Search      = @"(\.method (?:public)(?: \w+)* checkOpNoThrow\([^)]*\)[IZ]\s*\n)(\s*\.locals \d+)",
                Replace     = "$1    .locals 1\n    const/4 v0, 0x0\n    return v0\n\n    # patched: AppOpsHelper\n",
                AndroidMin  = 14,
            },

            // ── MOCK PERMISSION ────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "mock_permission_dpm",
                Description = "Mock Permission — DevicePolicyManager DISALLOW_MOCK_LOCATION",
                FileGlob    = "devicepolicy/DevicePolicyManagerService.smali",
                Search      = @"(const-string [vp]\d+, ""no_mock_location""\s*\n)(\s*invoke-\w+ \{[^}]+\}[^\n]+\n)(\s*(?:move-result|move-result-object) [vp]\d+\s*\n)(\s*if-(?:eq|ne)z? [vp]\d+[^:]*:cond_\w+)",
                Replace     = "$1    # SmaliPatcherEx: mock_permission bypass\n    goto :goto_mock_perm_ok",
                AndroidMin  = 5,
                Multi       = true,
            },
            new SmaliPatch
            {
                Name        = "mock_permission_restrictions",
                Description = "Mock Permission — UserRestrictionsUtils (Android 11+)",
                FileGlob    = "pm/UserRestrictionsUtils.smali",
                Search      = @"(const-string [vp]\d+, ""no_mock_location""\s*\n)((?:.*\n)*?)(\s*invoke-\w+ \{[^}]+\}[^\n]*\(Landroid/os/Bundle;Ljava/lang/String;\)[^\n]*\n)",
                Replace     = "$1$2    # patched: mock_permission_restrictions\n",
                AndroidMin  = 11,
            },

            // ── GNSS ───────────────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "gnss_mock_provider",
                Description = "GNSS Mock Provider — GnssManagerService (Android 13–16)",
                FileGlob    = "location/gnss/GnssManagerService.smali",
                Search      = @"(invoke-virtual \{[vp]\d+(?:, [vp]\d+)*\}, Landroid/app/AppOpsManager;->(?:checkOp|noteOp|noteOpNoThrow)\([^)]+\)[IZ]\s*\n)(\s*move-result [vp]\d+\s*\n)(\s*(?:const/4|const/16) [vp]\d+, 0x0\s*\n)(\s*if-(?:eq|ne) [vp]\d+, [vp]\d+[^:]*:cond_\w+)",
                Replace     = "$1$2$3    goto :goto_gnss_ok",
                AndroidMin  = 13,
                Multi       = true,
            },
            new SmaliPatch
            {
                Name        = "gnss_location_provider_legacy",
                Description = "GNSS Mock Provider — GnssLocationProvider (Android 9–12)",
                FileGlob    = "location/GnssLocationProvider.smali",
                Search      = @"(invoke-virtual \{[vp]\d+(?:, [vp]\d+)*\}, Landroid/app/AppOpsManager;->checkOpNoThrow\([^)]+\)I\s*\n)(\s*move-result [vp]\d+\s*\n)(\s*if-nez [vp]\d+[^:]*:cond_\w+)",
                Replace     = "$1$2    # gnss_legacy patched",
                AndroidMin  = 9,
                AndroidMax  = 12,
            },

            // ── SIGNATURE SPOOFING ─────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "signature_spoofing_pms",
                Description = "Signature Spoofing — PackageManagerService (Android 5–13)",
                FileGlob    = "pm/PackageManagerService.smali",
                Search      = @"(\.method public checkSignatures\(II\)I\s*\n)(\s*\.locals \d+)",
                Replace     = "$1    .locals 1\n    const/4 v0, 0x0\n    return v0\n\n    # SmaliPatcherEx: sig_spoof\n",
                AndroidMin  = 5,
                AndroidMax  = 13,
            },
            new SmaliPatch
            {
                Name        = "signature_spoofing_computer",
                Description = "Signature Spoofing — ComputerEngine (Android 14–16)",
                FileGlob    = "pm/ComputerEngine.smali",
                Search      = @"(\.method public checkSignatures\(II\)I\s*\n)(\s*\.locals \d+)",
                Replace     = "$1    .locals 1\n    const/4 v0, 0x0\n    return v0\n\n    # SmaliPatcherEx: sig_spoof_computer\n",
                AndroidMin  = 14,
            },
            new SmaliPatch
            {
                Name        = "signature_spoofing_snapshot",
                Description = "Signature Spoofing — Snapshot (Android 14–16)",
                FileGlob    = "pm/Snapshot.smali",
                Search      = @"(\.method public checkSignatures\(II\)I\s*\n)(\s*\.locals \d+)",
                Replace     = "$1    .locals 1\n    const/4 v0, 0x0\n    return v0\n\n    # SmaliPatcherEx: sig_spoof_snapshot\n",
                AndroidMin  = 14,
            },

            // ── NO PERMISSION REVIEW ───────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "no_permission_review",
                Description = "No Permission Review — skip REVIEW_REQUIRED flag",
                FileGlob    = "permission/PermissionManagerService.smali",
                Search      = @"(const-string [vp]\d+, ""android\.permission\.REVIEW_REQUIRED""\s*\n)((?:.*\n)*?)(\s*if-(?:eq|ne)z? [vp]\d+[^:]*:cond_\w+)",
                Replace     = "$1$2    # SmaliPatcherEx: no_perm_review skipped\n    goto :goto_perm_ok",
                AndroidMin  = 10,
                Multi       = true,
            },

            // ── DOZE WHITELIST ─────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "doze_whitelist",
                Description = "Doze Whitelist — DeviceIdleController.isWhitelisted always true",
                FileGlob    = "DeviceIdleController.smali",
                Search      = @"(\.method public isWhitelisted\(Ljava/lang/String;Z\)Z\s*\n)(\s*\.locals \d+)",
                Replace     = "$1    .locals 1\n    const/4 v0, 0x1\n    return v0\n\n    # SmaliPatcherEx: doze_whitelist\n",
                AndroidMin  = 6,
            },

            // ── UNTRUSTED TOUCH ────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "untrusted_touch",
                Description = "Untrusted Touch — InputManagerService block bypass (Android 12–16)",
                FileGlob    = "input/InputManagerService.smali",
                Search      = @"(invoke-virtual \{[vp]\d+(?:, [vp]\d+)*\}, Landroid/view/InputEventReceiver;->shouldBlockUntrustedTouch\([^)]+\)Z\s*\n)(\s*move-result [vp]\d+\s*\n)(\s*if-(?:eq|ne)z? [vp]\d+[^:]*:cond_\w+)",
                Replace     = "$1    const/4 v0, 0x0\n    # untrusted_touch patched",
                AndroidMin  = 12,
                Multi       = true,
            },
            new SmaliPatch
            {
                Name        = "untrusted_touch_wms",
                Description = "Untrusted Touch — WindowManagerService overlay bypass (Android 12–16)",
                FileGlob    = "wm/WindowManagerService.smali",
                Search      = @"(const-string [vp]\d+, ""Untrusted touch due to""\s*\n)((?:.*\n){0,8}?)(\s*if-(?:eq|ne)z? [vp]\d+[^:]*:cond_\w+)",
                Replace     = "$1$2    # untrusted_touch_wms patched\n    goto :goto_touch_ok",
                AndroidMin  = 12,
                Multi       = true,
            },

            // ── OVERLAY ────────────────────────────────────────────────────────────
            new SmaliPatch
            {
                Name        = "overlay_any",
                Description = "Overlay — allow unsigned overlays (Android 10–16)",
                FileGlob    = "om/OverlayManagerService.smali",
                Search      = @"(invoke-\w+ \{[^}]+\}[^\n]*->isSignatureMatch\([^)]+\)[IZB]\s*\n)(\s*move-result [vp]\d+\s*\n)(\s*if-(?:eq|ne)z? [vp]\d+[^:]*:cond_\w+)",
                Replace     = "$1    const/4 v0, 0x1\n    # overlay_any patched\n",
                AndroidMin  = 10,
                Multi       = true,
            },
        };

        public static readonly Dictionary<string, SmaliPatch> Map =
            All.ToDictionary(p => p.Name);
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
            var parts = pattern.Split('/');
            return Directory.GetFiles(_smaliRoot, parts[^1],
                SearchOption.AllDirectories);
        }

        public PatchResult Apply(SmaliPatch patch)
        {
            var result = new PatchResult { Patch = patch };

            if (_api < patch.AndroidMin || _api > patch.AndroidMax)
            {
                result.Reason = $"API {_api} out of range [{patch.AndroidMin}–{patch.AndroidMax}]";
                return result;
            }

            var targets = GlobFiles(patch.FileGlob);
            bool anyFile = false;

            foreach (var file in targets)
            {
                anyFile = true;
                var text = File.ReadAllText(file);
                string patched;
                int count = 0;

                if (patch.Multi)
                {
                    patched = Regex.Replace(text, patch.Search, patch.Replace,
                        RegexOptions.Multiline | RegexOptions.Singleline);
                    // estimate count
                    count = Regex.Matches(text, patch.Search,
                        RegexOptions.Multiline | RegexOptions.Singleline).Count;
                }
                else
                {
                    patched = Regex.Replace(text, patch.Search, patch.Replace,
                        RegexOptions.Multiline | RegexOptions.Singleline, TimeSpan.FromSeconds(10));
                    count = patched != text ? 1 : 0;
                }

                if (patched != text)
                {
                    File.WriteAllText(file, patched);
                    result.Files.Add($"{Path.GetFileName(file)} ({count}×)");
                    result.Applied = true;
                }
            }

            if (!anyFile)
                result.Reason = $"No file matched: {patch.FileGlob}";
            else if (!result.Applied)
                result.Reason = "Pattern not found in matched file(s)";

            return result;
        }

        public Dictionary<string, PatchResult> RunAll(IEnumerable<string> names)
        {
            var results = new Dictionary<string, PatchResult>();
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
