namespace UTF.Configuration.Models;

public class SystemConfig
{
    public string LogLevel { get; set; } = "Info";
    public bool AutoSaveResults { get; set; } = true;
    public string ResultsPath { get; set; } = "./test-results";
    public string DefaultLanguage { get; set; } = "zh-CN";
    public string Theme { get; set; } = "Light";
}
