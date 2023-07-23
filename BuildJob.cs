using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace build_smw;

internal class BuildJob
{
    private Config _config;
    private BuildArgs _args;

    private Dictionary<string, DateTime> _lastReadTimes = new();
    const int FILE_WATCHER_DEBOUNCE = 500;

    private FileSystemWatcher? romWatcher;

    public BuildJob(Config config, BuildArgs args)
    {
        _config = config;
        _args = args;
    }

    public async Task RunJob()
    {
        // insert and exit if not watching
        if (!_args.WatchForChanges)
        {
            if (_args.InsertMusic) await InsertMusic(_args.IsVerbose);
            if (_args.InsertSprites) await InsertSprites(_args.IsVerbose);
            if (_args.InsertBlocks) await InsertBlocks(_args.IsVerbose);
            if (_args.InsertUberAsm) await InsertUberAsm(_args.IsVerbose);
            if (_args.InsertPatches) await InsertPatches(_args.IsVerbose);
            return;
        }

        // init watchers
        if (_args.InsertMusic) InitAddmusickWatcher();
        if (_args.InsertSprites) InitPixiWatcher();
        if (_args.InsertBlocks) InitGpsWatcher();
        if (_args.InsertUberAsm) InitUberasmWatcher();
        InitRomWatcher();

        WriteWatchingMessage();
        Console.ReadLine();
    }



    #region Watchers
    private void InitAddmusickWatcher()
    {
        var musicWatcher = InitToolWatcher(_config.Addmusick);
        if (musicWatcher == null) return;
        musicWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await InsertMusic(true);
                await CopyPatchRun();
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
                await CopyPatchRun();
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
                await InsertBlocks(true);
                await CopyPatchRun();
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
                await InsertUberAsm(true);
                await CopyPatchRun();
            }
        };
    }

    private void InitRomWatcher()
    {
        var fileInfo = new FileInfo(_config.AbsInputRom);
        if (fileInfo.DirectoryName == null) return;

        romWatcher = new FileSystemWatcher(fileInfo.DirectoryName, fileInfo.Name);
        romWatcher.EnableRaisingEvents = true;
        romWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
                await CopyPatchRun();
            }
        };
    }

    private async Task CopyPatchRun()
    {
        await InsertPatches(true);
        await RunEmulator();
        WriteTime();
        WriteWatchingMessage();
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
        DateTime lastReadTime;
        _lastReadTimes.TryGetValue(e.FullPath, out lastReadTime);
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
    private async Task InsertMusic(bool verbose)
    {
        if (_config.Addmusick == null) return;
        if (string.IsNullOrEmpty(_config.Addmusick.Exe)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Addmusick.Exe);
        string args = _config.Addmusick.Args + $" {_config.AbsInputRom}";
        await RunExe(exe, args);
    }

    private async Task InsertBlocks(bool verbose)
    {
        if (_config.Gps == null) return;
        if (string.IsNullOrEmpty(_config.Gps.Exe) || string.IsNullOrEmpty(_config.Gps.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Gps.Exe);
        string args = _config.Gps.Args + $" -l {_config.Gps.ListFile} {_config.AbsInputRom}";
        await RunExe(exe, args);
    }

    private async Task InsertSprites(bool verbose)
    {
        if (_config.Pixi == null) return;
        if (string.IsNullOrEmpty(_config.Pixi.Exe) || string.IsNullOrEmpty(_config.Pixi.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Pixi.Exe);
        string args = _config.Pixi.Args + (verbose ? " -d" : "") + $" -l {_config.Pixi.ListFile} {_config.AbsInputRom}";
        await RunExe(exe, args);
    }

    private async Task InsertUberAsm(bool verbose)
    {
        if (_config.Uberasm == null) return;
        if (string.IsNullOrEmpty(_config.Uberasm.Exe) || string.IsNullOrEmpty(_config.Uberasm.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Uberasm.Exe);
        string args = _config.Uberasm.Args + $" {_config.Uberasm.ListFile} {_config.AbsInputRom}";
        await RunExe(exe, args, true);
    }

    private async Task InsertPatches(bool verbose)
    {
        File.Copy(_config.AbsInputRom, _config.AbsOutputRom, true);
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
            string cmd = $"{args} {asmPath} {_config.AbsOutputRom}";
            await RunExe(exe, cmd);
        }
    }

    private async Task RunEmulator()
    {
        if (_config.Emulator == null) return;
        if (string.IsNullOrEmpty(_config.Emulator.Exe)) return;

        string args = _config.AbsOutputRom;
        if (string.IsNullOrEmpty(_config.Emulator.Args)) args += $" {_config.Emulator.Args}";
        await RunExe(_config.Emulator.Exe, args);
    }

    private async Task RunExe(string exe, string args, bool sendEnter = false)
    {
        // stop watching the rom we are about to modify
        EnableRomWatcherEvents(false);

        Console.WriteLine("Running command:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"{exe} {args}\n");
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

        // start watching the rom again
        EnableRomWatcherEvents(true);
    }

    private void EnableRomWatcherEvents(bool enable)
    {
        if (romWatcher == null) return;
        romWatcher.EnableRaisingEvents = enable;
    }
    #endregion

    #region Messages
    private void WriteWatchingMessage()
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