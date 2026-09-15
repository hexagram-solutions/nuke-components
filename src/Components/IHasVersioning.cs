using System;
using System.Collections.Concurrent;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MinVer;
using static Nuke.Common.Tools.MinVer.MinVerTasks;

namespace Hexagrams.Nuke.Components;

/// <summary>
/// Provides properties for accessing versioning information calculated with
/// <see href="https://github.com/adamralph/minver">MinVer</see>.
/// </summary>
/// <remarks>
/// Requires the <see href="https://github.com/adamralph/minver#can-i-use-minver-to-version-software-which-is-not-built-using-a-net-sdk-style-project">minver-cli</see>
/// tool to be available to the build project, for example:
/// <code>
/// &lt;PackageDownload Include="minver-cli" Version="[x.y.z]" /&gt;
/// </code>
/// </remarks>
public interface IHasVersioning : INukeBuild
{
    // Lazy value: ConcurrentDictionary.GetOrAdd may invoke its factory more than once under
    // contention, but only the winning Lazy is ever observed, so minver-cli runs exactly once.
    private static readonly ConcurrentDictionary<string, Lazy<MinVer>> VersionCache = new();

    /// <summary>
    /// The prefix stripped from Git tags when calculating the version. Defaults to <c>"v"</c>, so a
    /// <c>v1.2.3</c> tag yields version <c>1.2.3</c>.
    /// </summary>
    string TagPrefix => "v";

    /// <summary>
    /// The versioning information for the current commit or tag.
    /// </summary>
    MinVer Versioning => VersionCache.GetOrAdd(TagPrefix,
        prefix => new Lazy<MinVer>(() => MinVer(s => s
            .SetTagPrefix(prefix)
            .DisableProcessOutputLogging()).Result)).Value;
}
