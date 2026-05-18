using CommandLine;
using System.Text.Json;

namespace build_smw;

public class Program
{
    public static async Task Main(string[] args)
    {
        // get cli args
        var argsResult = Parser.Default.ParseArguments<Options>(args);
        var buildArgs = argsResult.Value;
        if (buildArgs == null) Environment.Exit(1);

        // load config
        var buildConfig = await LoadConfig(buildArgs.ConfigFile ?? "config.json");
        if (buildConfig == null) Environment.Exit(1);

        // init job
        var job = new BuildJob(buildConfig, buildArgs);
        await job.RunJob();
    }

    private static readonly JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, AllowTrailingCommas = true };
    private static async Task<Config?> LoadConfig(string configFile)
    {
        // get config file path
        var path = Path.GetFullPath(configFile, AppContext.BaseDirectory);
        if (!File.Exists(path))
        {
            Console.WriteLine($"{path} does not exist.");
            Console.WriteLine("Press any key to exit...");
            _ = Console.ReadKey(); // wait for input before exiting
            Environment.Exit(1);
        }

        // read and deserialize
        var fileContents = await File.ReadAllTextAsync(path);
        var config = JsonSerializer.Deserialize<Config>(fileContents, options);
        if (config == null) return null;

        // fallback project path (not necessarily the base directory)
        if (string.IsNullOrEmpty(config.ProjectPath)) config.ProjectPath = Environment.CurrentDirectory;
        return config;
    }
}