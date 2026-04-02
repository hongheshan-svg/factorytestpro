using System.Collections.Generic;

namespace UTF.Configuration.Models;

public class TestConfig
{
    public string ProjectId { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public List<TestStepConfig> Steps { get; set; } = new();
}

public class TestStepConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Command { get; set; } = "";
    public string? Expected { get; set; }
    public int Timeout { get; set; } = 30000;
    public bool Enabled { get; set; } = true;
}
