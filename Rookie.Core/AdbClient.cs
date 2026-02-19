using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Rookie.Core;

public record AdbDevice(string Id, string State);

public class AdbClient
{
    private static ProcessStartInfo P(string file, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    private static async Task<(int code, string stdout, string stderr)> RunAsync(params string[] args)
    {
        using var p = Process.Start(P("adb", args)) ?? throw new Exception("Failed to start adb");
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, await stdoutTask, await stderrTask);
    }

    public async Task<List<AdbDevice>> ListDevicesAsync()
    {
        var (code, stdout, stderr) = await RunAsync("devices");
        if (code != 0) throw new Exception($"adb devices failed: {stderr}");

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                          .Select(l => l.Trim())
                          .Where(l => l.Length > 0 && !l.StartsWith("List of devices"))
                          .ToList();

        return lines.Select(l =>
        {
            var parts = l.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            var id = parts.Length > 0 ? parts[0] : "?";
            var state = parts.Length > 1 ? parts[1] : "?";
            return new AdbDevice(id, state);
        }).ToList();
    }

    public async Task InstallApkAsync(string deviceId, string apkPath)
    {
        var (code, stdout, stderr) = await RunAsync("-s", deviceId, "install", "-r", apkPath);
        if (code != 0) throw new Exception($"Install failed: {stderr}");
        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout.Trim());
    }

    public async Task UninstallAsync(string deviceId, string packageName)
    {
        var (code, stdout, stderr) = await RunAsync("-s", deviceId, "uninstall", packageName);
        if (code != 0) throw new Exception($"Uninstall failed: {stderr}");
        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout.Trim());
    }

    public async Task<List<string>> ListPackagesAsync(string deviceId)
    {
        var (code, stdout, stderr) = await RunAsync("-s", deviceId, "shell", "pm", "list", "packages");
        if (code != 0) throw new Exception($"List packages failed: {stderr}");

        return stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Select(l => l.Trim())
                     .Where(l => l.StartsWith("package:"))
                     .Select(l => l.Substring("package:".Length))
                     .ToList();
    }
}
