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

    static Dictionary<string, DateTime> lastReadTimes = new();
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

        // init job from args
        var job = new BuildJob(args);

        // insert and exit if not watching
        if (!job.WatchForChanges)
        {
            if (job.InsertMusic) await InsertMusic(job.IsVerbose);
            if (job.InsertSprites) await InsertSprites(job.IsVerbose);
            if (job.InsertUberAsm) await InsertUberAsm(job.IsVerbose);
            if (job.InsertBlocks) await InsertBlocks(job.IsVerbose);
            if (job.InsertPatches) await InsertPatches(job.IsVerbose);
            return;
        }

        // init watchers
        if (job.InsertMusic) InitAddmusickWatcher();
        if (job.InsertSprites) InitPixiWatcher();
        if (job.InsertUberAsm) InitUberasmWatcher();
        if (job.InsertBlocks) InitGpsWatcher();
        InitRomWatcher();

        Console.WriteLine("Watching for changes...");
        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();
    }

    private static void InitRomWatcher()
    {
        var fileInfo = new FileInfo(_absInputRom);
        if (fileInfo.DirectoryName == null) return;

        var romWatcher = GetWatcher(fileInfo);
        romWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e)) await InsertPatches(true);
        };
    }

    private static void InitAddmusickWatcher()
    {
        if (_config.Addmusick == null) return;

        string? exeDir = Path.GetDirectoryName(_config.Addmusick.Exe);
        if (exeDir == null) return;

        var fileInfo = new FileInfo(Path.Combine(_config.ProjectPath, exeDir, _config.Addmusick.ListFile));
        if (fileInfo.DirectoryName == null) return;

        var musicWatcher = GetWatcher(fileInfo);
        musicWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await InsertMusic(true);
                await InsertPatches(true);
            }
        };
    }

    private static void InitPixiWatcher()
    {
        if (_config.Pixi == null) return;

        var fileInfo = new FileInfo(Path.Combine(_config.ProjectPath, _config.Pixi.ListFile));
        if (fileInfo.DirectoryName == null) return;

        var spriteWatcher = GetWatcher(fileInfo);
        spriteWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await InsertSprites(true);
                await InsertPatches(true);
            }
        };
    }

    private static void InitUberasmWatcher()
    {
        if (_config.Uberasm == null) return;

        string? exeDir = Path.GetDirectoryName(_config.Uberasm.Exe);
        if (exeDir == null) return;

        var fileInfo = new FileInfo(Path.Combine(_config.ProjectPath, exeDir, _config.Uberasm.ListFile));
        if (fileInfo.DirectoryName == null) return;

        var uberasmWatcher = GetWatcher(fileInfo);
        uberasmWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await InsertUberAsm(true);
                await InsertPatches(true);
            }
        };
    }

    private static void InitGpsWatcher()
    {
        if (_config.Gps == null) return;

        string? exeDir = Path.GetDirectoryName(_config.Gps.Exe);
        if (exeDir == null) return;

        var fileInfo = new FileInfo(Path.Combine(_config.ProjectPath, exeDir, _config.Gps.ListFile));
        if (fileInfo.DirectoryName == null) return;

        var gpsWatcher = GetWatcher(fileInfo);
        gpsWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await InsertBlocks(true);
                await InsertPatches(true);
            }
        };
    }

    private static FileSystemWatcher GetWatcher(FileInfo fileInfo)
    {
        var watcher = new FileSystemWatcher(fileInfo.DirectoryName!, fileInfo.Name);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private static bool Watcher_DebounceChanged(FileSystemEventArgs e)
    {
        var lastWriteTime = File.GetLastWriteTime(e.FullPath);
        DateTime lastReadTime;
        lastReadTimes.TryGetValue(e.FullPath, out lastReadTime);
        var diff = lastWriteTime - lastReadTime;
        if (diff.TotalMilliseconds > FILE_WATCHER_DEBOUNCE)
        {
            Console.WriteLine($"{e.FullPath} has changed.");
            lastReadTimes[e.FullPath] = lastWriteTime;
            return true;
        }
        return false;
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