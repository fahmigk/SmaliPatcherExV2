using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class SmaliHelper
{
    public static string? FindLocationProviderManager(string root)
    {
        if (!Directory.Exists(root)) return null;

        return Directory
            .EnumerateFiles(root, "LocationProviderManager.smali", SearchOption.AllDirectories)
            .FirstOrDefault(f =>
                f.Replace('\\', '/').Contains("/com/android/server/location/provider/"));
    }

    public static string? GetMethodBlock(string smaliPath, string methodName)
    {
        if (!File.Exists(smaliPath)) return null;

        var text = File.ReadAllText(smaliPath, Encoding.UTF8);
        var pattern = @$"(?ms)^\.method\b[^\n]*\b{Regex.Escape(methodName)}\b[^\n]*\n.*?^\.end method\s*$";
        var match = Regex.Match(text, pattern, RegexOptions.Multiline);
        return match.Success ? match.Value : null;
    }

    public static void Main(string[] args)
    {
        var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var file = FindLocationProviderManager(root);

        if (file == null)
        {
            Console.WriteLine("LocationProviderManager.smali not found.");
            return;
        }

        Console.WriteLine("Found:");
        Console.WriteLine(file);
        Console.WriteLine();

        var m1 = GetMethodBlock(file, "setMockProviderAllowed");
        var m2 = GetMethodBlock(file, "setMockProviderLocation");

        Console.WriteLine("=== setMockProviderAllowed ===");
        Console.WriteLine(m1 ?? "Method not found");
        Console.WriteLine();
        Console.WriteLine("=== setMockProviderLocation ===");
        Console.WriteLine(m2 ?? "Method not found");
    }
}
