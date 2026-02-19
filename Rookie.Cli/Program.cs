using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rookie.Core;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var adb = new AdbClient();
            var devices = await adb.ListDevicesAsync();
            var connected = devices.Where(d => d.State == "device").ToList();

            string PickDevice()
            {
                if (connected.Count == 0) throw new Exception("No authorized devices. Check USB debugging prompt.");
                return connected[0].Id;
            }

            if (args.Length == 0)
            {
                Console.WriteLine("Commands:");
                Console.WriteLine("  devices");
                Console.WriteLine("  install /path/to/app.apk");
                Console.WriteLine("  packages [filter]");
                Console.WriteLine("  uninstall com.package.name");
                Console.WriteLine("  uninstall-find keyword");
                return 1;
            }

            if (args[0] == "devices")
            {
                if (devices.Count == 0) { Console.WriteLine("No devices found."); return 2; }
                Console.WriteLine("Connected devices:");
                foreach (var d in devices) Console.WriteLine($"- {d.Id} [{d.State}]");
                return 0;
            }

            if (args[0] == "install")
            {
                if (args.Length < 2) throw new Exception("Usage: install /path/to/app.apk");
                var apk = args[1];
                if (!File.Exists(apk)) throw new Exception($"APK not found: {apk}");

                var deviceId = PickDevice();
                Console.WriteLine($"Installing on {deviceId}...");
                await adb.InstallApkAsync(deviceId, apk);
                Console.WriteLine("Done.");
                return 0;
            }

            if (args[0] == "packages")
            {
                var deviceId = PickDevice();
                var pkgs = await adb.ListPackagesAsync(deviceId);

                if (args.Length >= 2)
                {
                    var f = args[1].ToLowerInvariant();
                    pkgs = pkgs.Where(p => p.ToLowerInvariant().Contains(f)).ToList();
                }

                foreach (var p in pkgs) Console.WriteLine(p);
                return 0;
            }

            if (args[0] == "uninstall")
            {
                if (args.Length < 2) throw new Exception("Usage: uninstall com.package.name");
                var pkg = args[1];

                var deviceId = PickDevice();
                Console.WriteLine($"Uninstalling from {deviceId}...");
                await adb.UninstallAsync(deviceId, pkg);
                Console.WriteLine("Done.");
                return 0;
            }

            if (args[0] == "uninstall-find")
            {
                if (args.Length < 2) throw new Exception("Usage: uninstall-find keyword");
                var keyword = args[1].ToLowerInvariant();

                var deviceId = PickDevice();
                var pkgs = await adb.ListPackagesAsync(deviceId);
                var matches = pkgs.Where(p => p.ToLowerInvariant().Contains(keyword)).ToList();

                if (matches.Count == 0) { Console.WriteLine("No matching packages."); return 3; }
                if (matches.Count > 1)
                {
                    Console.WriteLine("Multiple matches. Be specific:");
                    foreach (var m in matches) Console.WriteLine(m);
                    return 4;
                }

                Console.WriteLine($"Uninstalling {matches[0]} ...");
                await adb.UninstallAsync(deviceId, matches[0]);
                Console.WriteLine("Done.");
                return 0;
            }

            Console.WriteLine("Unknown command.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 99;
        }
    }
}
