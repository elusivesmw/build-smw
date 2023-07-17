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

    static Dictionary<string, DateTime> lastReadTimes;
    static FileSystemWatcher uberasmWatcher;
    const int FILE_WATCHER_DEBOUNCE = 500;

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

        // cache a debounce timer
        lastReadTimes = new();

        var job = new BuildJob(args);

        if (job.InsertMusic) await InsertMusic(job.IsVerbose);
        if (job.InsertSprites) await InsertSprites(job.IsVerbose);
        if (job.InsertUberAsm) await InsertUberAsm(job.IsVerbose);
        if (job.InsertBlocks) await InsertBlocks(job.IsVerbose);
        if (job.InsertPatches) await InsertPatches(job.IsVerbose);

        // exit if not watching
        if (!job.WatchForChanges) return;

        uberasmWatcher = GetWatcher(_config.ProjectPath, _config.Uberasm.Exe, _config.Uberasm.ListFile);
        uberasmWatcher.Changed += async (s, e) => await UberasmWatcher_Changed(s, e);

        Console.WriteLine("Watching for changes...");
        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();
    }

    private static FileSystemWatcher GetWatcher(string projectPath, string exePath, string listPath)
    {
        var fileInfo = new FileInfo(Path.Combine(projectPath, Path.GetDirectoryName(exePath), listPath));
        var watcher = new FileSystemWatcher(fileInfo.DirectoryName, fileInfo.Name);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }


    private static async Task UberasmWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        var lastWriteTime = File.GetLastWriteTime(e.FullPath);
        DateTime lastReadTime;
        lastReadTimes.TryGetValue(e.FullPath, out lastReadTime);
        var diff = lastWriteTime - lastReadTime;
        if (diff.TotalMilliseconds > FILE_WATCHER_DEBOUNCE)
        {
            Console.WriteLine($"{e.FullPath} has changed.");
            lastReadTimes[e.FullPath] = lastWriteTime;
            await InsertUberAsm(true);
        }
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

    static async Task InsertMusic(bool verbose)
    {
        if (_config.Addmusick == null) return;
        if (string.IsNullOrEmpty(_config.Addmusick.Exe)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Addmusick.Exe);
        string args = _config.Addmusick.Args + $" {_absInputRom}";
        await RunExe(exe, args);
    }

    static async Task InsertBlocks(bool verbose)
    {
        if (_config.Gps == null) return;
        if (string.IsNullOrEmpty(_config.Gps.Exe) || string.IsNullOrEmpty(_config.Gps.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Gps.Exe);
        string args = _config.Gps.Args + $" -l {_config.Gps.ListFile} {_absInputRom}";
        await RunExe(exe, args);
    }

    static async Task InsertSprites(bool verbose)
    {
        if (_config.Pixi == null) return;
        if (string.IsNullOrEmpty(_config.Pixi.Exe) || string.IsNullOrEmpty(_config.Pixi.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Pixi.Exe);
        string args = _config.Pixi.Args + (verbose ? " -d" : "") + $" -l {_config.Pixi.ListFile} {_absInputRom}";
        await RunExe(exe, args);
    }

    static async Task InsertUberAsm(bool verbose)
    {
        if (_config.Uberasm == null) return;
        if (string.IsNullOrEmpty(_config.Uberasm.Exe) || string.IsNullOrEmpty(_config.Uberasm.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Uberasm.Exe);
        string args = _config.Uberasm.Args + $" {_config.Uberasm.ListFile} {_absInputRom}";
        await RunExe(exe, args, true);
    }

    static async Task InsertPatches(bool verbose)
    {
        File.Copy(_absInputRom, _absOutputRom, true);
        Console.WriteLine($"Copied from {_config.InputRom} to {_config.OutputRom}");

        if (_config.Asar == null) return;
        if (string.IsNullOrEmpty(_config.Asar.Exe) || string.IsNullOrEmpty(_config.Asar.PatchFolder)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Asar.Exe);
        string args = _config.Asar.Args;
        args += (verbose ? " --verbose" : "");
        string folder = _config.Asar.PatchFolder;
        var patches = _config.Asar.AsmFiles;

        foreach (var patch in patches)
        {
            string asmPath = Path.Combine(_config.ProjectPath, folder, patch);
            // run patch inserts on copied rom only (output rom)
            string cmd = $"{args} {asmPath} {_absOutputRom}";
            await RunExe(exe, cmd);
        }
    }

    static async Task RunExe(string exe, string args, bool sendEnter = false)
    {
        Console.WriteLine("Running command:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {exe}{args}\n");
        Console.ForegroundColor = ConsoleColor.DarkGray;

        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardInput = sendEnter;
        p.StartInfo.FileName = exe;
        p.StartInfo.Arguments = args;
        p.StartInfo.WorkingDirectory = Path.GetDirectoryName(exe);
        p.Start();

        await p.WaitForExitAsync();
        Console.ResetColor();

        if (sendEnter)
        {
            using var sw = p.StandardInput;
            if (!sw.BaseStream.CanWrite) return;
            {
                // send enter to continue
                sw.WriteLine();
                // and add some space
                Console.WriteLine();
            }
        }
    }
}