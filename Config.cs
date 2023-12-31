﻿namespace build_smw;

internal class Config
{
    public string ProjectPath { get; set; } = string.Empty;
    public string InputRom { get; set; } = string.Empty;
    public string AbsInputRom
    {
        get
        {
            return Path.Combine(ProjectPath, InputRom);
        }
    }
    public string OutputRom { get; set; } = string.Empty;
    public string AbsOutputRom
    {
        get
        {
            return Path.Combine(ProjectPath, OutputRom);
        }
    }

    public ToolConfig? Emulator { get; set; }
    public ToolConfig? Addmusick { get; set; }
    public ToolConfig? Gps { get; set; }
    public ToolConfig? Pixi { get; set; }
    public ToolConfig? Uberasm { get; set; }
    public ToolConfig? Asar { get; set; }
}

internal class ToolConfig
{
    public string Exe { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
    public string ListFile { get; set; } = string.Empty;
}
