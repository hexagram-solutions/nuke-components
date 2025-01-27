using Hexagrams.Nuke.Components;
using Nuke.Common.CI.GitHubActions;

[GitHubActions(
    "continuous",
    GitHubActionsImage.WindowsLatest,
    GitHubActionsImage.UbuntuLatest,
    GitHubActionsImage.MacOsLatest,
    FetchDepth = 0,
    OnPullRequestBranches = ["main"],
    OnPushBranches = ["main", "release/v*"],
    PublishArtifacts = false,
    InvokedTargets = [nameof(ITest.Test)],
    CacheKeyFiles = ["global.json", "src/**/*.csproj"])]
// TODO: Package signing
[GitHubActions(
    "release",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    OnPushTags = ["v*"],
    PublishArtifacts = true,
    InvokedTargets = [nameof(ITest.Test), nameof(IPack.Pack), nameof(IPush.Push)],
    CacheKeyFiles = ["global.json", "src/**/*.csproj"],
    ImportSecrets = [nameof(IPush.NuGetApiKey)])]
partial class Build;
