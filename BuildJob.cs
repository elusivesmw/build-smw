using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace build_smw;

internal class BuildJob
{
    private readonly Config _config;
    private readonly Options _options;

    private readonly Dictionary<string, DateTime> _lastReadTimes = [];
    const int FILE_WATCHER_DEBOUNCE = 500;

    private FileSystemWatcher? _romWatcher;

    internal BuildJob(Config config, Options options)
    {
        _config = config;
        _options = options;
    }

    internal async Task RunJob()
    {
        // insert and exit if not watching
        if (!_options.WatchForChanges)
        {
            if (_options.InsertMusic)
            {
                var success = await InsertMusic();
                if (!success) Environment.Exit(1);
            }
            if (_options.InsertSprites)
            {
                var success = await InsertSprites(_options.IsVerbose);
                if (!success) Environment.Exit(1);
            }
            if (_options.InsertBlocks)
            {
                var success = await InsertBlocks();
                if (!success) Environment.Exit(1);
            }
            if (_options.CreatePatch)
            {
                var success = await CreatePatch();
                if (!success) Environment.Exit(1);
            }
            await CopyHijackRun();
            return;
        }

        // init watchers
        if (_options.InsertMusic) InitAddmusickWatcher();
        if (_options.InsertSprites) InitPixiWatcher();
        if (_options.InsertBlocks) InitGpsWatcher();
        if (_options.InsertUberAsm) InitUberasmWatcher();
        if (_options.InsertHijacks) InitAsarWatcher();
        InitRomWatcher();

        WriteWatchingMessage();
        Console.ReadLine();
    }

    //async Task AssertSuccess(Func<Task<bool>> f)
    //{
    //    var success = await f.Invoke();
    //    if (!success) Environment.Exit(1);
    //}

    #region Watchers
    private void InitAddmusickWatcher()
    {
        var musicWatcher = InitToolWatcher(_config.Addmusick);
        if (musicWatcher == null) return;
        musicWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await InsertMusic();
                await CopyHijackRun();
            }
        };
    }

    private void InitPixiWatcher()
    {
        var spriteWatcher = InitToolWatcher(_config.Pixi, false);
        if (spriteWatcher == null) return;
        spriteWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await InsertSprites(true);
                await CopyHijackRun();
            }
        };
    }

    private void InitGpsWatcher()
    {
        var gpsWatcher = InitToolWatcher(_config.Gps);
        if (gpsWatcher == null) return;
        gpsWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await InsertBlocks();
                await CopyHijackRun();
            }
        };
    }

    private void InitUberasmWatcher()
    {
        var uberasmWatcher = InitToolWatcher(_config.Uberasm);
        if (uberasmWatcher == null) return;
        uberasmWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await CopyHijackRun();
            }
        };
    }

    private void InitAsarWatcher()
    {
        var asarWatcher = InitToolWatcher(_config.Asar);
        if (asarWatcher == null) return;
        asarWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await CopyHijackRun();
            }
        };
    }

    private void InitRomWatcher()
    {
        var fileInfo = new FileInfo(_config.AbsInputRom);
        if (fileInfo.DirectoryName == null) return;

        _romWatcher = new FileSystemWatcher(fileInfo.DirectoryName, fileInfo.Name);
        _romWatcher.EnableRaisingEvents = true;
        _romWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                if (_options.CreatePatch) await CreatePatch();
                await CopyHijackRun();
            }
        };
    }

    private async Task CopyHijackRun()
    {
        var copySuccess = CopyRom();
        if (!copySuccess) Environment.Exit(1);
        // run post copy tasks on output rom
        if (_options.InsertUberAsm)
        {
            var success = await InsertUberAsm();
            if (!success) Environment.Exit(1);
        }
        if (_options.InsertHijacks)
        {
            var success = await InsertHijacks(true);
            if (!success) Environment.Exit(1);
        }
        if (_options.RunEmulator)
        {
            RunEmulator();
        }
    }

    private FileSystemWatcher? InitToolWatcher(ToolConfig? tool, bool exeRelative = true)
    {
        if (tool == null) return null;

        string? exeDir = Path.GetDirectoryName(tool.Exe);
        if (exeDir == null) return null;

        FileInfo fileInfo;
        if (exeRelative)
        {
            fileInfo = new FileInfo(Path.Combine(_config.ProjectPath, exeDir, tool.ListFile));
        }
        else
        {
            fileInfo = new FileInfo(Path.Combine(_config.ProjectPath, tool.ListFile));
        }
        if (fileInfo.DirectoryName == null) return null;

        var watcher = new FileSystemWatcher(fileInfo.DirectoryName, fileInfo.Name);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }
    #endregion

    #region Events

    private bool Watcher_DebounceChanged(FileSystemEventArgs e)
    {
        var lastWriteTime = File.GetLastWriteTime(e.FullPath);
        _lastReadTimes.TryGetValue(e.FullPath, out var lastReadTime);
        var diff = lastWriteTime - lastReadTime;
        if (diff.TotalMilliseconds > FILE_WATCHER_DEBOUNCE)
        {
            Console.WriteLine($"{e.FullPath} has changed.");
            _lastReadTimes[e.FullPath] = lastWriteTime;

            // also say rom has changed because it is about to change
            _lastReadTimes[_config.AbsInputRom] = lastWriteTime;
            return true;
        }
        return false;
    }
    #endregion

    #region Tools
    private async Task<bool> InsertMusic()
    {
        if (_config.Addmusick == null) return false;
        if (string.IsNullOrEmpty(_config.Addmusick.Exe)) return false;

        string exe = Path.Combine(_config.ProjectPath, _config.Addmusick.Exe);
        string args = $"{_config.Addmusick.Args} {_config.AbsInputRom}";

        int exitCode = await RunExeAsync(exe, args);
        return exitCode == 0;
    }

    private async Task<bool> InsertBlocks()
    {
        if (_config.Gps == null) return false;
        if (string.IsNullOrEmpty(_config.Gps.Exe)) return false;

        string exe = Path.Combine(_config.ProjectPath, _config.Gps.Exe);
        string args = $"{_config.Gps.Args} {_config.AbsInputRom}";

        int exitCode = await RunExeAsync(exe, args);
        return exitCode == 0;
    }

    private async Task<bool> InsertSprites(bool verbose)
    {
        if (_config.Pixi == null) return false;
        if (string.IsNullOrEmpty(_config.Pixi.Exe)) return false;

        string exe = Path.Combine(_config.ProjectPath, _config.Pixi.Exe);
        string args = $"{_config.Pixi.Args} {_config.AbsInputRom}";

        int exitCode = await RunExeAsync(exe, args);
        return exitCode == 0;
    }

    private async Task<bool> CreatePatch()
    {
        if (_config.Flips == null) return false;
        if (string.IsNullOrEmpty(_config.Flips.Exe)) return false;

        string exe = Path.Combine(_config.ProjectPath, _config.Flips.Exe);
        string smwOrig = Path.Combine(_config.ProjectPath, "sysLMRestore", "smwOrig.smc");
        string args = $"--create {smwOrig} {_config.AbsInputRom} levels_diff.bps";

        int exitCode = await RunExeAsync(exe, args);
        return exitCode == 0;
    }

    private bool CopyRom()
    {
        try
        {
            File.Copy(_config.AbsInputRom, _config.AbsOutputRom, true);
            Console.WriteLine($"Copied from {_config.InputRom} to {_config.OutputRom}\n");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    private async Task<bool> InsertUberAsm()
    {
        if (_config.Uberasm == null) return false;
        if (string.IsNullOrEmpty(_config.Uberasm.Exe)) return false;

        string exe = Path.Combine(_config.ProjectPath, _config.Uberasm.Exe);
        string args = $"{_config.Uberasm.Args} {_config.AbsOutputRom}";

        int exitCode = await RunExeAsync(exe, args);
        return exitCode == 0;
    }

    private async Task<bool> InsertHijacks(bool verbose)
    {
        if (_config.Asar == null) return false;
        // NOTE: args needed for asar, no implied list file
        if (string.IsNullOrEmpty(_config.Asar.Exe) || string.IsNullOrEmpty(_config.Asar.Args)) return false;

        string exe = Path.Combine(_config.ProjectPath, _config.Asar.Exe);
        string? exeDir = Path.GetDirectoryName(exe);
        if (exeDir == null) return false;

        string cmd = $"{_config.Asar.Args} {_config.AbsOutputRom}";

        var exitCode = await RunExeAsync(exe, cmd);
        return exitCode == 0;
    }

    private void RunEmulator()
    {
        if (_config.Emulator == null) return;
        if (string.IsNullOrEmpty(_config.Emulator.Exe)) return;

        string args = _config.AbsOutputRom;
        if (!string.IsNullOrEmpty(_config.Emulator.Args)) args += $" {_config.Emulator.Args}";

        RunExe(_config.Emulator.Exe, args);
    }

    private void RunExe(string exe, string args)
    {
        // stop watching the rom we are about to modify
        EnableRomWatcherEvents(false);
        WriteCommand(exe, args);

        var p = CreateProcess(exe, args, false);
        p.Start();

        Console.ResetColor();
        // start watching the rom again
        EnableRomWatcherEvents(true);
    }

    private async Task<int> RunExeAsync(string exe, string args, bool sendEnter = false)
    {
        // stop watching the rom we are about to modify
        EnableRomWatcherEvents(false);
        WriteCommand(exe, args);

        var p = CreateProcess(exe, args, sendEnter);
        p.Start();

        await p.WaitForExitAsync();
        Console.ResetColor();

        if (sendEnter)
        {
            if (!p.HasExited && p.StandardInput.BaseStream.CanWrite)
            {
                // "Press any key to continue..."
                p.StandardInput.WriteLine();
            }
            // extra newline needed here
            Console.WriteLine();
        }
        // spacing between processes 
        Console.WriteLine();

        // start watching the rom again
        EnableRomWatcherEvents(true);

        return p.ExitCode;
    }

    private static Process CreateProcess(string exe, string args, bool redirectSti)
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardInput = redirectSti;
        p.StartInfo.FileName = exe;
        p.StartInfo.Arguments = args;
        p.StartInfo.WorkingDirectory = Path.GetDirectoryName(exe);
        return p;
    }

    private void EnableRomWatcherEvents(bool enable)
    {
        if (_romWatcher == null) return;
        _romWatcher.EnableRaisingEvents = enable;
    }
    #endregion

    #region Messages
    private static void WriteCommand(string exe, string args)
    {
        Console.WriteLine("Running command:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"{exe} {args}\n");
        Console.ForegroundColor = ConsoleColor.DarkGray;
    }

    private static void WriteWatchingMessage()
    {
        Console.WriteLine("Watching for changes...");
        Console.WriteLine("Press enter to exit.");
    }

    private static void WriteTime()
    {
        Console.WriteLine($"Finished running at {DateTime.Now:T}.");
    }
    #endregion
}