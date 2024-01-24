using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace Hexagrams.Nuke.Components;

/// <summary>
/// Targets and configuration for restoring dependencies for the solution.
/// </summary>
public interface IRestore : IHasSolution
{
    /// <summary>
    /// Restore dependencies using <c>dotnet restore</c>.
    /// </summary>
    Target Restore => t => t
        .Executes(() =>
        {
            DotNetRestore(s => s
                .Apply(RestoreSettingsBase)
                .Apply(RestoreSettings));
        });

    /// <summary>
    /// Settings for configuring the <c>dotnet restore</c> command.
    /// </summary>
    sealed Configure<DotNetRestoreSettings> RestoreSettingsBase => t => t
        .SetProjectFile(Solution)
        .SetIgnoreFailedSources(IgnoreFailedSources);

    /// <summary>
    /// Additional settings for configuring the <c>dotnet restore</c> command.
    /// </summary>
    Configure<DotNetRestoreSettings> RestoreSettings => t => t;

    /// <summary>
    /// Whether or not to ignore failed sources during restore. Defaults to <c>false</c>.
    /// </summary>
    [Parameter("Ignore unreachable sources during " + nameof(Restore))]
    bool IgnoreFailedSources => TryGetValue<bool?>(() => IgnoreFailedSources) ?? false;
}
