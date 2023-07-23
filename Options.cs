using CommandLine;

namespace build_smw;

internal class Options
{
    [Option('m', "music", HelpText = "Insert music via Addmusick.")]
    public bool InsertMusic { get; set; } = false;

    [Option('s', "sprites", HelpText = "Insert sprites via Pixi.")]
    public bool InsertSprites { get; set; } = false;

    [Option('b', "blocks", HelpText = "Insert music via Addmusick.")]
    public bool InsertBlocks { get; set; } = false;

    [Option('u', "uberasm", HelpText = "Insert Uberasm.")]
    public bool InsertUberAsm { get; set; } = false;

    [Option('p', "patches", HelpText = "Insert global patches.")]
    public bool InsertPatches { get; set; } = false;

    [Option('v', "verbose", HelpText = "Sets verbosity to true.")]
    public bool IsVerbose { get; set; } = false;

    [Option('w', "watch", HelpText = "Watch for changes.")]
    public bool WatchForChanges { get; set; } = false;

    [Option('r', "run", HelpText = "Run emulator after build tasks.")]
    public bool RunEmulator { get; set; } = false;
}
