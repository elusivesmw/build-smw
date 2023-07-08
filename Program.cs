using build_smw;
using System.Diagnostics;
using System.Text.Json;

class Program
{
    static string? projectPath;
    static async Task Main(string[] args)
    {
        var config = await LoadConfig();
        if (config == null) return;

        projectPath = Path.GetDirectoryName(config.InputRom);
        if (projectPath == null) return;

        await InsertSprites(config, false);
        await InsertUberAsm(config, false);
        await InsertBlocks(config, false);


        Console.WriteLine("Done.");
    }


    static async Task<Config?> LoadConfig()
    {
        var pwd = Environment.CurrentDirectory;
        var configFile = Path.Combine(pwd, "config.json");
        var fileContents = await File.ReadAllTextAsync(configFile);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, AllowTrailingCommas = true };
        var config = JsonSerializer.Deserialize<Config>(fileContents, options);
        return config;
    }


    static async Task InsertBlocks(Config config, bool verbose)
    {
        Console.WriteLine("Inserting blocks...");

        if (config.Gps == null) return;
        if (string.IsNullOrEmpty(config.Gps.Exe) || string.IsNullOrEmpty(config.Gps.ListFile)) return;

        string exe = Path.Combine(projectPath!, config.Gps.Exe);
        string args = config.Gps.Args + $" -l {config.Gps.ListFile} {config.InputRom}";
        await RunExe(exe, args, verbose);

        Console.WriteLine("Blocks inserted...");
    }

    static async Task InsertSprites(Config config, bool verbose)
    {
        Console.WriteLine("Inserting sprites...");
        
        if (config.Pixi == null) return;
        if (string.IsNullOrEmpty(config.Pixi.Exe) || string.IsNullOrEmpty(config.Pixi.ListFile)) return;

        string exe = Path.Combine(projectPath!, config.Pixi.Exe);
        string args = config.Pixi.Args + (verbose ? " -d" : "") + $" -l {config.Pixi.ListFile} {config.InputRom}";
        await RunExe(exe, args, verbose);

        Console.WriteLine("Sprites inserted...");
    }

    static async Task InsertUberAsm(Config config, bool verbose)
    {
        Console.WriteLine("Inserting UberASM...");

        if (config.Uberasm == null) return;
        if (string.IsNullOrEmpty(config.Uberasm.Exe) || string.IsNullOrEmpty(config.Uberasm.ListFile)) return;

        string exe = Path.Combine(projectPath!, config.Uberasm.Exe);
        string args = config.Uberasm.Args + $" {config.Uberasm.ListFile} {config.InputRom}";
        await RunExe(exe, args, verbose);

        Console.WriteLine("UberASM inserted...");
    }

    static async Task RunExe(string exe, string args, bool verbose)
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = verbose;
        p.StartInfo.FileName = exe;
        p.StartInfo.Arguments = args;
        p.StartInfo.WorkingDirectory = Path.GetDirectoryName(exe);
        p.Start();

        if (verbose)
        {
            Console.WriteLine(p.StandardOutput.ReadToEnd());
        }
        await p.WaitForExitAsync();
    }
}