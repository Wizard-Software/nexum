using System.Text.RegularExpressions;

namespace Nexum.Tests;

/// <summary>
/// Regression guard for CONSTITUTION Z1: every internal await in library code
/// MUST use <c>.ConfigureAwait(false)</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class ConfigureAwaitComplianceTests
{
    private static readonly string s_sourceDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Nexum"));

    [Fact]
    public void AllAwaitStatements_InNexumProject_UseConfigureAwaitFalse()
    {
        // Arrange
        var sourceDir = new DirectoryInfo(s_sourceDirectory);
        sourceDir.Exists.Should().BeTrue($"source directory should exist at {s_sourceDirectory}");

        var csFiles = sourceDir.GetFiles("*.cs", SearchOption.AllDirectories);
        csFiles.Should().NotBeEmpty("Nexum project should contain .cs files");

        var violations = new List<string>();

        // Act
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file.FullName);

            // Strip block comments from entire content for analysis
            var stripped = BlockCommentPattern().Replace(content, " ");

            // Re-split stripped content to analyze line by line
            var strippedLines = stripped.Split('\n');

            for (var i = 0; i < strippedLines.Length; i++)
            {
                var line = strippedLines[i];

                // Skip single-line comments
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//"))
                {
                    continue;
                }

                // Strip inline comments before checking
                var commentIndex = line.IndexOf("//");
                var effectiveLine = commentIndex >= 0 ? line[..commentIndex] : line;

                // Skip lines inside string literals (basic heuristic)
                if (effectiveLine.Contains("@\"") || effectiveLine.Contains("\"\"\""))
                {
                    continue;
                }

                // Check for await keyword on this line
                if (!AwaitPattern().IsMatch(effectiveLine))
                {
                    continue;
                }

                // Check if ConfigureAwait(false) appears on this line OR any of the following
                // continuation lines (multi-line await statements end with `;`)
                var hasConfigureAwait = false;
                for (var j = i; j < strippedLines.Length; j++)
                {
                    var checkLine = strippedLines[j];
                    if (ConfigureAwaitFalsePattern().IsMatch(checkLine))
                    {
                        hasConfigureAwait = true;
                        break;
                    }

                    // If we hit a semicolon, the statement is complete
                    if (checkLine.Contains(';'))
                    {
                        break;
                    }
                }

                if (!hasConfigureAwait)
                {
                    var relativePath = Path.GetRelativePath(s_sourceDirectory, file.FullName);
                    violations.Add($"{relativePath}:{i + 1}: {trimmed}");
                }
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "all await statements in src/Nexum/ must use .ConfigureAwait(false) per CONSTITUTION Z1. " +
            $"Violations found:\n{string.Join("\n", violations)}");
    }

    [GeneratedRegex(@"\bawait\b")]
    private static partial Regex AwaitPattern();

    [GeneratedRegex(@"\.ConfigureAwait\(false\)")]
    private static partial Regex ConfigureAwaitFalsePattern();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentPattern();
}
