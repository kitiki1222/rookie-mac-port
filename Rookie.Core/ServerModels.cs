using System.Collections.Generic;

namespace Rookie.Core;

public class ServerIndex
{
    public string? Name { get; set; }
    public List<ServerApp> Apps { get; set; } = new();
}

public class ServerApp
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Apk { get; set; }
}
