using build_smw;
using CommandLine;
using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        var buildConfig = await LoadConfig();
        if (buildConfig == null) return;

        var argsResult = Parser.Default.ParseArguments<Options>(args);
        var buildArgs = argsResult.Value;
        if (buildArgs == null) return;

        // init job from args
        var job = new BuildJob(buildConfig, buildArgs);
        await job.RunJob();
    }

    static async Task<Config?> LoadConfig()
    {
        var pwd = Environment.CurrentDirectory;
        var configFile = Path.Combine(pwd, "config.json");
        var fileContents = await File.ReadAllTextAsync(configFile);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, AllowTrailingCommas = true };
        var config = JsonSerializer.Deserialize<Config>(fileContents, options);
        if (config == null) return null;

        if (string.IsNullOrEmpty(config.ProjectPath)) config.ProjectPath = pwd;
        return config;
    }
}