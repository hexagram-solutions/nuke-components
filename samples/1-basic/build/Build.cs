using Hexagrams.Nuke.Components;
using Nuke.Common;
using Nuke.Common.ProjectModel;

class Build : NukeBuild, ICompile
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => ((ICompile) x).Compile);

    // The path to the solution file must be explicitly specified because this project is nested below the main build
    // project for the repository. Normally, you don't have to specify a relative path to the solution file.
    [Solution("./Hexagrams.Nuke.Samples.Basic.sln")]
    readonly Solution Solution;
    Solution IHasSolution.Solution => Solution;

    Target ICompile.Compile => t => t
        .Inherit<ICompile>();
}
