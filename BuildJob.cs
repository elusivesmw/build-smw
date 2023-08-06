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
    private Config _config;
    private Options _options;

    private Dictionary<string, DateTime> _lastReadTimes = new();
    const int FILE_WATCHER_DEBOUNCE = 500;

    private FileSystemWatcher? romWatcher;

    public BuildJob(Config config, Options options)
    {
        _config = config;
        _options = options;
    }

    public async Task RunJob()
    {
        // insert and exit if not watching
        if (!_options.WatchForChanges)
        {
            if (_options.InsertMusic) await InsertMusic(_options.IsVerbose);
            if (_options.InsertSprites) await InsertSprites(_options.IsVerbose);
            if (_options.InsertBlocks) await InsertBlocks(_options.IsVerbose);
            if (_options.InsertUberAsm) await InsertUberAsm(_options.IsVerbose);
            if (_options.InsertPatches) await InsertPatches(_options.IsVerbose);
            return;
        }

        // init watchers
        if (_options.InsertMusic) InitAddmusickWatcher();
        if (_options.InsertSprites) InitPixiWatcher();
        if (_options.InsertBlocks) InitGpsWatcher();
        if (_options.InsertUberAsm) InitUberasmWatcher();
        if (_options.InsertPatches) InitAsarWatcher();
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

    private void InitAsarWatcher()
    {
        var asarWatcher = InitToolWatcher(_config.Asar);
        if (asarWatcher == null) return;
        asarWatcher.Changed += async (s, e) =>
        {
            if (Watcher_DebounceChanged(e))
            {
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
        if (_options.InsertPatches) await InsertPatches(true);
        if (_options.RunEmulator) RunEmulator();
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
        await RunExeAsync(exe, args);
    }

    private async Task InsertBlocks(bool verbose)
    {
        if (_config.Gps == null) return;
        if (string.IsNullOrEmpty(_config.Gps.Exe) || string.IsNullOrEmpty(_config.Gps.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Gps.Exe);
        string args = _config.Gps.Args + $" -l {_config.Gps.ListFile} {_config.AbsInputRom}";
        await RunExeAsync(exe, args);
    }

    private async Task InsertSprites(bool verbose)
    {
        if (_config.Pixi == null) return;
        if (string.IsNullOrEmpty(_config.Pixi.Exe) || string.IsNullOrEmpty(_config.Pixi.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Pixi.Exe);
        string args = _config.Pixi.Args + (verbose ? " -d" : "") + $" -l {_config.Pixi.ListFile} {_config.AbsInputRom}";
        await RunExeAsync(exe, args);
    }

    private async Task InsertUberAsm(bool verbose)
    {
        if (_config.Uberasm == null) return;
        if (string.IsNullOrEmpty(_config.Uberasm.Exe) || string.IsNullOrEmpty(_config.Uberasm.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Uberasm.Exe);
        string args = _config.Uberasm.Args + $" {_config.Uberasm.ListFile} {_config.AbsInputRom}";
        await RunExeAsync(exe, args, true);
    }

    private async Task InsertPatches(bool verbose)
    {
        File.Copy(_config.AbsInputRom, _config.AbsOutputRom, true);
        Console.WriteLine($"Copied from {_config.InputRom} to {_config.OutputRom}");

        if (_config.Asar == null) return;
        if (string.IsNullOrEmpty(_config.Asar.Exe) || string.IsNullOrEmpty(_config.Asar.ListFile)) return;

        string exe = Path.Combine(_config.ProjectPath, _config.Asar.Exe);
        string args = _config.Asar.Args;
        args += (verbose ? " --verbose" : "");

        string? exeDir = Path.GetDirectoryName(exe);
        if (exeDir == null) return;

        var list = Path.Combine(exeDir, _config.Asar.ListFile);
        var patches = ReadAllLines(list);

        foreach (var patch in patches)
        {
            if (string.IsNullOrWhiteSpace(patch)) continue;

            string asmPath = Path.Combine(exeDir, patch);
            // run patch inserts on copied rom only (output rom)
            string cmd = $"{args} {asmPath} {_config.AbsOutputRom}";
            await RunExeAsync(exe, cmd);
        }
    }

    private string[] ReadAllLines(string path)
    {
        // allow read/write in other filestreams, which is not the case with System.IO.File.ReadAllLines
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var sr = new StreamReader(fs))
        {
            var lines = new List<string>();
            while (!sr.EndOfStream)
            {
                string? line = sr.ReadLine();
                if (line == null) continue;

                lines.Add(line);
            }
            return lines.ToArray();
        }
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

    private async Task RunExeAsync(string exe, string args, bool sendEnter = false)
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

    private Process CreateProcess(string exe, string args, bool redirectSti)
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
        if (romWatcher == null) return;
        romWatcher.EnableRaisingEvents = enable;
    }
    #endregion

    #region Messages
    private void WriteCommand(string exe, string args)
    {
        Console.WriteLine("Running command:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"{exe} {args}\n");
        Console.ForegroundColor = ConsoleColor.DarkGray;
    }

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