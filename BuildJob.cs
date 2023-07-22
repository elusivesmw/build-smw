using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace build_smw;

internal class BuildJob
{
    public bool InsertMusic { get; set; } = false;
    public bool InsertSprites { get; set; } = false;
    public bool InsertUberAsm { get; set; } = false;
    public bool InsertBlocks { get; set; } = false;
    public bool InsertPatches { get; set; } = false;
    public bool IsVerbose { get; set; } = false;
    public bool WatchForChanges { get; set; } = true;

    public BuildJob(string[] args)
    {
        if (args.Length > 0)
        {
            foreach (string arg in args)
            {
                switch (arg)
                {
                    case "-m":
                        this.InsertMusic = true;
                        break;
                    case "-s":
                        this.InsertSprites = true;
                        break;
                    case "-u":
                        this.InsertUberAsm = true;
                        break;
                    case "-b":
                        this.InsertBlocks = true;
                        break;
                    case "-p":
                        this.InsertPatches = true;
                        break;
                    case "-v":
                        this.IsVerbose = true;
                        break;
                    case "-w":
                        this.WatchForChanges = true;
                        break;
                    default:
                        Console.WriteLine($"Invalid arg: {arg}");
                        Console.WriteLine("Use: build-smw [options]");
                        Console.WriteLine(" -m: addMusick");
                        Console.WriteLine(" -s: Sprites");
                        Console.WriteLine(" -u: Uberasm");
                        Console.WriteLine(" -b: Blocks");
                        Console.WriteLine(" -p: Patches");
                        Console.WriteLine(" -v: Verbose");
                        Console.WriteLine("No options will run all with verbosity off.");
                        break;
                }
            }
        }
        else
        {
            this.InsertMusic = true;
            this.InsertSprites = true;
            this.InsertUberAsm = true;
            this.InsertBlocks = true;
            this.InsertPatches = true;
        }
    }
}
