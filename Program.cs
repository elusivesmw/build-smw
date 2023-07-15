using build_smw;
using System.Diagnostics;
using System.Text.Json;

class Program
{
#nullable disable
    static Config _config;
#nullable enable
    static string _absInputRom = string.Empty;
    static string _absOutputRom = string.Empty;

    static async Task Main(string[] args)
    {
        var config = await LoadConfig();
        if (config == null) return;
        _config = config;

        if (_config.ProjectPath == null) return;

        if (_config.InputRom == null) return;
        _absInputRom = Path.Combine(_config.ProjectPath, _config.InputRom);

        if (_config.OutputRom == null) return;
        _absOutputRom = Path.Combine(_config.ProjectPath, _config.OutputRom);

        bool verbose = true;
        await InsertSprites(verbose);
        await InsertUberAsm(verbose);
        await InsertBlocks(verbose);
        await InsertPatches(verbose);
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


    static async Task InsertBlocks(bool verbose)
    {
        if (_config.Gps == null) return;
        if (string.IsNullOrEmpty(_config.Gps.Exe) || string.IsNullOrEmpty(_config.Gps.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Gps.Exe);
        string args = _config.Gps.Args + $" -l {_config.Gps.ListFile} {_absInputRom}";
        await RunExe(exe, args, verbose);
    }

    static async Task InsertSprites(bool verbose)
    {
        if (_config.Pixi == null) return;
        if (string.IsNullOrEmpty(_config.Pixi.Exe) || string.IsNullOrEmpty(_config.Pixi.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Pixi.Exe);
        string args = _config.Pixi.Args + (verbose ? " -d" : "") + $" -l {_config.Pixi.ListFile} {_absInputRom}";
        await RunExe(exe, args, verbose);
    }

    static async Task InsertUberAsm(bool verbose)
    {
        if (_config.Uberasm == null) return;
        if (string.IsNullOrEmpty(_config.Uberasm.Exe) || string.IsNullOrEmpty(_config.Uberasm.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Uberasm.Exe);
        string args = _config.Uberasm.Args + $" {_config.Uberasm.ListFile} {_absInputRom}";
        await RunExe(exe, args, false); // RedirectStandardOutput doesn't seem to work here
    }

    static async Task InsertPatches(bool verbose)
    {
        File.Copy(_absInputRom!, _absOutputRom!, true);
        Console.WriteLine($"Copied from {_config.InputRom} to {_config.OutputRom}");

        if (_config.Asar == null) return;
        if (string.IsNullOrEmpty(_config.Asar.Exe) || string.IsNullOrEmpty(_config.Asar.PatchFolder)) return;

        string exe = Path.Combine(_config.ProjectPath!, _config.Asar.Exe);
        string args = _config.Asar.Args;
        args += (verbose ? " --verbose" : "");
        string folder = _config.Asar.PatchFolder;
        var patches = _config.Asar.AsmFiles;

        foreach (var patch in patches)
        {
            string asmPath = Path.Combine(_config.ProjectPath, folder, patch);
            // run patch inserts on copied rom only (output rom)
            string cmd = $"{args} {asmPath} {_absOutputRom}";
            await RunExe(exe, cmd, verbose);
        }
    }

    static async Task RunExe(string exe, string args, bool verbose)
    {
        Console.WriteLine("Running command:");
        Console.WriteLine($"  {exe}{args}\n");

        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = verbose;
        p.StartInfo.FileName = exe;
        p.StartInfo.Arguments = args;
        p.StartInfo.WorkingDirectory = Path.GetDirectoryName(exe);
        p.Start();

        if (verbose)
        {
            Console.WriteLine(await p.StandardOutput.ReadToEndAsync());
        }
        await p.WaitForExitAsync();
    }
}