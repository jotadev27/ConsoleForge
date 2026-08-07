using ConsoleForge.Core.Profiles;

namespace ConsoleForge.UI.Models;

public sealed record ResolutionOption(string Label, VideoResolution? Resolution)
{
    public override string ToString() => Label;
}
