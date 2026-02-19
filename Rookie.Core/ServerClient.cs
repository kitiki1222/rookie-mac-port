using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rookie.Core;

public class ServerClient
{
    private readonly HttpClient _http = new();

    public ServersConfig LoadServersConfig(string path)
    {
        if (!File.Exists(path))
            throw new Exception("Missing servers config: " + path);

        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<ServersConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (cfg == null) throw new Exception("Invalid servers config JSON.");
        return cfg;
    }

    public async Task<ServerIndex> LoadIndexAsync(string indexUrl)
    {
        var json = await _http.GetStringAsync(indexUrl);
        var index = JsonSerializer.Deserialize<ServerIndex>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (index == null) throw new Exception("Invalid server index JSON.");
        return index;
    }

    public async Task<string> DownloadApkAsync(string apkUrl, string downloadFolder)
    {
        Directory.CreateDirectory(downloadFolder);

        var fileName = Path.GetFileName(new Uri(apkUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "download.apk";

        var outPath = Path.Combine(downloadFolder, fileName);

        var data = await _http.GetByteArrayAsync(apkUrl);
        await File.WriteAllBytesAsync(outPath, data);

        return outPath;
    }
}
