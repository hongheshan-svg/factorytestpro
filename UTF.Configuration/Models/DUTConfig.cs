using System.Collections.Generic;

namespace UTF.Configuration.Models;

public class DUTConfig
{
    public string ProductName { get; set; } = "";
    public string ProductModel { get; set; } = "";
    public int MaxConcurrent { get; set; } = 16;
    public List<string> SerialPorts { get; set; } = new();
    public List<string> NetworkHosts { get; set; } = new();
    public string NamingTemplate { get; set; } = "{TypeName}测试工位{Index}";
}
