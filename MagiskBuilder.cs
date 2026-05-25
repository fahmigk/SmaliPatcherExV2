using System;
using System.IO;
using System.IO.Compression;

namespace SmaliPatcherEx
{
    public static class MagiskBuilder
    {
        private const string UpdateBinary = @"#!/sbin/sh
SKIPUNZIP=1
MODDIR=""${MODPATH}""

ui_print ""************************************""
ui_print ""      SmaliPatcherEx v2.0           ""
ui_print ""      Android 16 Compatible         ""
ui_print ""************************************""

unzip -d ""$TMPDIR"" -q ""$ZIPFILE"" fingerprint 2>/dev/null
if [ -f ""$TMPDIR/fingerprint"" ]; then
  fp_module=$(cat ""$TMPDIR/fingerprint"")
  fp_system=$(getprop ro.build.fingerprint)
  if [ ""$fp_module"" != ""$fp_system"" ]; then
    ui_print ""! Fingerprint mismatch!""
    ui_print ""  Module: $fp_module""
    ui_print ""  Device: $fp_system""
    abort ""! Wrong device or build!""
  fi
  ui_print ""- Fingerprint: OK""
fi

unzip -o ""$ZIPFILE"" 'system/*' -d ""$MODPATH"" 2>/dev/null
ui_print ""- Files extracted""

ui_print ""- Clearing dalvik-cache...""
find /data/dalvik-cache -iname '*@services.jar*class*' 2>/dev/null | while read f; do
  rm -f ""$f""
done
find /data/misc/apexdata -iname '*@services.jar*class*' 2>/dev/null | while read f; do
  rm -f ""$f""
done
find /data/misc/apexdata/com.android.os -name '*.vdex' 2>/dev/null | \
  grep -i services | while read f; do rm -f ""$f""; done

set_perm_recursive ""$MODPATH"" root root 0755 0644
ui_print ""- Permissions set""

ts=$(stat -c '%y' /system/framework/framework.jar 2>/dev/null \
     | awk '{print $1$2}' | tr -d ':-.' | cut -c1-12)
if [ -n ""$ts"" ]; then
  find ""$MODDIR"" -name '*.*' 2>/dev/null | while read f; do
    touch -amt ""$ts"" ""$f"" 2>/dev/null
  done
  ui_print ""- Timestamps matched""
fi

ui_print """"
ui_print ""[OK] SmaliPatcherEx installed — reboot to activate""
";

        private const string UpdaterScript = "#MAGISK\n";

        public static string Build(
            string patchedJarPath,
            string outputDir,
            string fingerprint,
            string[] appliedPatches,
            Action<string>? log = null)
        {
            Directory.CreateDirectory(outputDir);
            var outZip = Path.Combine(outputDir, "SmaliPatcherEx-module.zip");

            if (File.Exists(outZip)) File.Delete(outZip);

            var desc = appliedPatches.Length > 0
                ? "Patches: " + string.Join(", ", appliedPatches)
                : "no patches applied";

            var moduleProp =
                "id=SmaliPatcherEx\n" +
                "name=SmaliPatcherEx\n" +
                "version=v2.0.0\n" +
                "versionCode=200\n" +
                "author=sabpprook+rebuild\n" +
                $"description=Android 16 smali patcher — {desc}\n" +
                "minMagisk=24000\n";

            log?.Invoke("[*] Building Magisk module ...");

            using var zip = ZipFile.Open(outZip, ZipArchiveMode.Create);

            AddText(zip, "META-INF/com/google/android/update-binary",  UpdateBinary);
            AddText(zip, "META-INF/com/google/android/updater-script", UpdaterScript);
            AddText(zip, "module.prop", moduleProp);

            if (!string.IsNullOrEmpty(fingerprint))
                AddText(zip, "fingerprint", fingerprint);

            zip.CreateEntryFromFile(patchedJarPath,
                "system/framework/services.jar",
                CompressionLevel.Optimal);

            log?.Invoke($"[✓] Module: {outZip}");
            return outZip;
        }

        private static void AddText(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var sw = new StreamWriter(entry.Open());
            sw.Write(content.Replace("\r\n", "\n"));
        }
    }
}
