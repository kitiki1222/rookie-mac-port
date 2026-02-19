using System.Collections.Generic;

namespace Rookie.Core;

public class ServersConfig
{
    public string? ActiveServer { get; set; }
    public Dictionary<string, ServerEntry> Servers { get; set; } = new();
}

public class ServerEntry
{
    public string? Name { get; set; }
    public string? IndexUrl { get; set; }
}
