using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Rookie.Core;

namespace Rookie.App.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly AdbClient _adb = new();
    private readonly ServerClient _server = new();

    public ObservableCollection<string> Devices { get; } = new();
    public ObservableCollection<string> Packages { get; } = new();
    public ObservableCollection<ServerApp> ServerApps { get; } = new();
    public ObservableCollection<string> Log { get; } = new();

    private string? _selectedDevice;
    public string? SelectedDevice
    {
        get => _selectedDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
    }

    private string _packageFilter = "";
    public string PackageFilter
    {
        get => _packageFilter;
        set => this.RaiseAndSetIfChanged(ref _packageFilter, value);
    }

    private string? _selectedPackage;
    public string? SelectedPackage
    {
        get => _selectedPackage;
        set => this.RaiseAndSetIfChanged(ref _selectedPackage, value);
    }

    private ServerApp? _selectedServerApp;
    public ServerApp? SelectedServerApp
    {
        get => _selectedServerApp;
        set => this.RaiseAndSetIfChanged(ref _selectedServerApp, value);
    }

    private string _serverName = "Server";
    public string ServerName
    {
        get => _serverName;
        set => this.RaiseAndSetIfChanged(ref _serverName, value);
    }

    private string _serverUrl = "";
    public string ServerUrl
    {
        get => _serverUrl;
        set => this.RaiseAndSetIfChanged(ref _serverUrl, value);
    }

    private string _status = "Ready.";
    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    private void LogLine(string s)
    {
        Log.Add($"[{DateTime.Now:HH:mm:ss}] {s}");
        Status = s;
    }

    public MainWindowViewModel()
    {
        LoadServerFromTemplate();
    }

    public void LoadServerFromTemplate()
    {
        try
        {
            var cfgPath = Path.Combine(AppContext.BaseDirectory, "config", "servers.json");

            // When running from project, BaseDirectory points to bin/... so we also support relative to repo root
            if (!File.Exists(cfgPath))
            {
                // fallback: repo-root relative
                cfgPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "config", "servers.json"));
            }

            var cfg = _server.LoadServersConfig(cfgPath);
            var key = cfg.ActiveServer ?? "default";

            if (!cfg.Servers.TryGetValue(key, out var server))
                throw new Exception("Active server key not found in servers.json: " + key);

            ServerName = server.Name ?? key;
            ServerUrl = server.IndexUrl ?? "";
            LogLine($"Server set: {ServerName}");
        }
        catch (Exception ex)
        {
            LogLine("ERROR: " + ex.Message);
        }
    }

    public async Task LoadServerAppsAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                LogLine("ServerUrl is empty. Edit config/servers.json.");
                return;
            }

            LogLine("Loading server index...");
            ServerApps.Clear();

            var index = await _server.LoadIndexAsync(ServerUrl);
            if (!string.IsNullOrWhiteSpace(index.Name))
                ServerName = index.Name;

            foreach (var a in index.Apps)
                ServerApps.Add(a);

            LogLine($"Loaded {ServerApps.Count} server app(s).");
        }
        catch (Exception ex)
        {
            LogLine("ERROR: " + ex.Message);
        }
    }

    public async Task DownloadAndInstallSelectedAsync()
    {
        try
        {
            if (SelectedServerApp?.Apk is null || string.IsNullOrWhiteSpace(SelectedServerApp.Apk))
            {
                LogLine("Select a server app first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedDevice))
            {
                LogLine("Select a device first.");
                return;
            }

            LogLine("Downloading APK...");
            Progress = 0.2;

            var dlFolder = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "downloads"));
            var apkPath = await _server.DownloadApkAsync(SelectedServerApp.Apk, dlFolder);

            LogLine("Installing APK...");
            Progress = 0.6;

            await _adb.InstallApkAsync(SelectedDevice, apkPath);

            Progress = 1.0;
            LogLine("Done.");
            Progress = 0;

            await LoadPackagesAsync();
        }
        catch (Exception ex)
        {
            LogLine("ERROR: " + ex.Message);
            Progress = 0;
        }
    }

    public async Task RefreshDevicesAsync()
    {
        try
        {
            LogLine("Refreshing devices...");
            Devices.Clear();

            var devs = await _adb.ListDevicesAsync();
            foreach (var d in devs.Where(d => d.State == "device"))
                Devices.Add(d.Id);

            SelectedDevice = Devices.FirstOrDefault();
            LogLine(Devices.Count > 0 ? $"Found {Devices.Count} device(s)." : "No authorized devices.");
        }
        catch (Exception ex)
        {
            LogLine("ERROR: " + ex.Message);
        }
    }

    public async Task LoadPackagesAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedDevice))
            {
                LogLine("Pick device first.");
                return;
            }

            LogLine("Loading packages...");
            Packages.Clear();

            var pkgs = await _adb.ListPackagesAsync(SelectedDevice);

            if (!string.IsNullOrWhiteSpace(PackageFilter))
            {
                var f = PackageFilter.Trim().ToLowerInvariant();
                pkgs = pkgs.Where(p => p.ToLowerInvariant().Contains(f)).ToList();
            }

            foreach (var p in pkgs)
                Packages.Add(p);

            LogLine($"Loaded {Packages.Count} package(s).");
        }
        catch (Exception ex)
        {
            LogLine("ERROR: " + ex.Message);
        }
    }

    public async Task UninstallSelectedAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedDevice))
            {
                LogLine("Select a device first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedPackage))
            {
                LogLine("Select a package to uninstall.");
                return;
            }

            LogLine($"Uninstalling {SelectedPackage}...");
            Progress = 0.2;

            await _adb.UninstallAsync(SelectedDevice, SelectedPackage);

            Progress = 1.0;
            Packages.Remove(SelectedPackage);
            SelectedPackage = null;

            LogLine("Uninstall complete.");
            Progress = 0;
        }
        catch (Exception ex)
        {
            LogLine("ERROR: " + ex.Message);
            Progress = 0;
        }
    }

    public async Task InstallApkAsync(string apkPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedDevice))
            {
                LogLine("Select a device first.");
                return;
            }

            if (!File.Exists(apkPath))
            {
                LogLine("APK not found: " + apkPath);
                return;
            }

            LogLine("Installing local APK...");
            Progress = 0.2;

            await _adb.InstallApkAsync(SelectedDevice, apkPath);

            Progress = 1.0;
            LogLine("Install complete.");
            Progress = 0;

            await LoadPackagesAsync();
        }
        catch (Exception ex)
        {
            LogLine("ERROR: " + ex.Message);
            Progress = 0;
        }
    }
}
