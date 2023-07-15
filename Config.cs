using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace build_smw;

internal class Config
{
    public string ProjectPath { get; set; } = string.Empty;
    public string InputRom { get; set; } = string.Empty;
    public string OutputRom { get; set; } = string.Empty;
    public ToolConfig? Addmusick { get; set; }
    public ToolConfig? Gps { get; set; }
    public ToolConfig? Pixi { get; set; }
    public ToolConfig? Uberasm { get; set; }
    public AsarConfig? Asar { get; set; }

}

internal class ToolConfig
{
    public string Exe { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
    public string ListFile { get; set; } = string.Empty;
}

internal class AsarConfig
{
    public string Exe { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
    public string PatchFolder { get; set; } = string.Empty;
    public List<string> AsmFiles { get; set; } = new();
}
