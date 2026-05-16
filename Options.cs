using CommandLine;

namespace build_smw;

internal class Options
{
    [Option('m', "music", HelpText = "Insert music via Addmusick.")]
    public bool InsertMusic { get; set; } = false;

    [Option('s', "sprites", HelpText = "Insert sprites via Pixi.")]
    public bool InsertSprites { get; set; } = false;

    [Option('b', "blocks", HelpText = "Insert blocks via GPS.")]
    public bool InsertBlocks { get; set; } = false;

    [Option('u', "uberasm", HelpText = "Insert Uberasm.")]
    public bool InsertUberAsm { get; set; } = false;

    [Option('h', "hijacks", HelpText = "Insert global hijacks.")]
    public bool InsertHijacks { get; set; } = false;

    [Option('p', "patch", HelpText = "Create a .bps patch file.")]
    public bool CreatePatch { get; set; } = false;

    [Option('v', "verbose", HelpText = "Sets verbosity to true.")]
    public bool IsVerbose { get; set; } = false;

    [Option('w', "watch", HelpText = "Watch for changes.")]
    public bool WatchForChanges { get; set; } = false;

    [Option('r', "run", HelpText = "Run emulator after build tasks.")]
    public bool RunEmulator { get; set; } = false;
}
