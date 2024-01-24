using System.Globalization;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace Hexagrams.Nuke.Components;

/// <summary>
/// Targets and configuration for executing unit tests in the solution.
/// </summary>
public interface ITest : IHasArtifacts, ICompile
{
    /// <summary>
    /// The test results output directory.
    /// </summary>
    AbsolutePath TestResultDirectory => ArtifactsDirectory / "test-results";

    /// <summary>
    /// The collection of projects that contain tests.
    /// </summary>
    IEnumerable<Project> TestProjects { get; }

    /// <summary>
    /// The degree of parallelism to use when executing tests. Defaults to <c>1</c>.
    /// </summary>
    int TestDegreeOfParallelism => 1;

    /// <summary>
    /// Execute unit tests in the solution using <c>dotnet test</c>.
    /// </summary>
    Target Test => t => t
        .DependsOn(Compile)
        .Produces(TestResultDirectory / "*.trx")
        .Produces(TestResultDirectory / "*.xml")
        .Executes(() =>
        {
            try
            {
                DotNetTest(s => s
                        .Apply(TestSettingsBase)
                        .Apply(TestSettings)
                        .CombineWith(TestProjects, (x, v) => x
                            .Apply(TestProjectSettingsBase, v)
                            .Apply(TestProjectSettings, v)),
                    completeOnFailure: true,
                    degreeOfParallelism: TestDegreeOfParallelism);
            }
            finally
            {
                ReportTestResults();
                ReportTestCount();
            }
        });

    private void ReportTestResults()
    {
        TestResultDirectory.GlobFiles("*.trx").ForEach(x =>
            AzurePipelines.Instance?.PublishTestResults(
                type: AzurePipelinesTestResultsType.VSTest,
                title: $"{Path.GetFileNameWithoutExtension(x)} ({AzurePipelines.Instance.StageDisplayName})",
                files: new string[] { x }));
    }

    private void ReportTestCount()
    {
        static IEnumerable<string> GetOutcomes(AbsolutePath file)
        {
            return XmlTasks.XmlPeek(
                file,
                "/xn:TestRun/xn:Results/xn:UnitTestResult/@outcome",
                ("xn", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"));
        }

        var resultFiles = TestResultDirectory.GlobFiles("*.trx");
        var outcomes = resultFiles.SelectMany(GetOutcomes).ToList();
        var passedTests = outcomes.Count(x => x == "Passed");
        var failedTests = outcomes.Count(x => x == "Failed");
        var skippedTests = outcomes.Count(x => x == "NotExecuted");

        ReportSummary(d => d
            .When(failedTests > 0, x => x
                .AddPair("Failed", failedTests.ToString(CultureInfo.InvariantCulture)))
            .AddPair("Passed", passedTests.ToString(CultureInfo.InvariantCulture))
            .When(skippedTests > 0, x => x
                .AddPair("Skipped", skippedTests.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Settings for controlling test execution behavior.
    /// </summary>
    sealed Configure<DotNetTestSettings> TestSettingsBase => t => t
        .SetConfiguration(Configuration)
        .SetNoBuild(SucceededTargets.Contains(Compile))
        .ResetVerbosity()
        .SetResultsDirectory(TestResultDirectory)
        .When(ExecutionPlan.Contains((this as IReportCoverage)?.ReportCoverage) || IsServerBuild, s => s
            .EnableCollectCoverage()
            .SetCoverletOutputFormat(CoverletOutputFormat.cobertura)
            .SetExcludeByFile("*.Generated.cs")
            .When(TeamCity.Instance is not null, x => x
                .SetCoverletOutputFormat($"\\\"{CoverletOutputFormat.cobertura},{CoverletOutputFormat.teamcity}\\\""))
            .When(IsServerBuild, x => x
                .EnableUseSourceLink()));

    /// <summary>
    /// Settings for configuring test projects.
    /// </summary>
    sealed Configure<DotNetTestSettings, Project> TestProjectSettingsBase => (s, v) => s
        .SetProjectFile(v)
        // https://github.com/Tyrrrz/GitHubActionsTestLogger
        .When(GitHubActions.Instance is not null && v.HasPackageReference("GitHubActionsTestLogger"), x => x
            .AddLoggers("GitHubActions;report-warnings=false"))
        // https://github.com/JetBrains/TeamCity.VSTest.TestAdapter
        .When(TeamCity.Instance is not null && v.HasPackageReference("TeamCity.VSTest.TestAdapter"), x => x
            .AddLoggers("TeamCity")
            // https://github.com/xunit/visualstudio.xunit/pull/108
            .AddRunSetting("RunConfiguration.NoAutoReporters", bool.TrueString))
        .AddLoggers($"trx;LogFileName={v.Name}.trx")
        .When(ExecutionPlan.Contains((this as IReportCoverage)?.ReportCoverage) || IsServerBuild, x => x
            .SetCoverletOutput(TestResultDirectory / $"{v.Name}.xml"));

    /// <summary>
    /// Additional settings for controlling test execution behavior.
    /// </summary>
    Configure<DotNetTestSettings> TestSettings => t => t;

    /// <summary>
    /// Additional settings for configuring test projects.
    /// </summary>
    Configure<DotNetTestSettings, Project> TestProjectSettings => (s, _) => s;
}
