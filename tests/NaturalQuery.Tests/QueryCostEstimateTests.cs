using System.Globalization;
using FluentAssertions;
using NaturalQuery.Models;

namespace NaturalQuery.Tests;

public class QueryCostEstimateTests
{
    // FormattedSize uses the current culture for decimal formatting.
    // We use Regex to accept either "." or "," as decimal separator.

    [Fact]
    public void FormattedSize_Should_Return_B_For_Small_Values()
    {
        var estimate = new QueryCostEstimate(500, 0.0m);

        estimate.FormattedSize.Should().Be("500 B");
    }

    [Fact]
    public void FormattedSize_Should_Return_B_For_Zero()
    {
        var estimate = new QueryCostEstimate(0, 0.0m);

        estimate.FormattedSize.Should().Be("0 B");
    }

    [Fact]
    public void FormattedSize_Should_Return_KB_For_1024_Plus()
    {
        var estimate = new QueryCostEstimate(1024, 0.0m);

        estimate.FormattedSize.Should().MatchRegex(@"^1[.,]0 KB$");
    }

    [Fact]
    public void FormattedSize_Should_Return_KB_For_Values_Under_1MB()
    {
        var estimate = new QueryCostEstimate(512 * 1024, 0.0m);

        estimate.FormattedSize.Should().MatchRegex(@"^512[.,]0 KB$");
    }

    [Fact]
    public void FormattedSize_Should_Return_MB_For_1MB_Plus()
    {
        var estimate = new QueryCostEstimate(1048576, 0.0m);

        estimate.FormattedSize.Should().MatchRegex(@"^1[.,]0 MB$");
    }

    [Fact]
    public void FormattedSize_Should_Return_MB_For_Values_Under_1GB()
    {
        var estimate = new QueryCostEstimate(500 * 1024 * 1024L, 0.0m);

        estimate.FormattedSize.Should().MatchRegex(@"^500[.,]0 MB$");
    }

    [Fact]
    public void FormattedSize_Should_Return_GB_For_Large_Values()
    {
        var estimate = new QueryCostEstimate(1024L * 1024 * 1024, 0.0m);

        estimate.FormattedSize.Should().MatchRegex(@"^1[.,]00 GB$");
    }

    [Fact]
    public void FormattedSize_Should_Return_GB_For_Multi_GB()
    {
        var estimate = new QueryCostEstimate(5L * 1024 * 1024 * 1024, 0.0m);

        estimate.FormattedSize.Should().MatchRegex(@"^5[.,]00 GB$");
    }

    [Fact]
    public void EstimatedCostUsd_Should_Be_Preserved()
    {
        var estimate = new QueryCostEstimate(1024L * 1024 * 1024, 0.005m);

        estimate.EstimatedCostUsd.Should().Be(0.005m);
    }

    [Fact]
    public void FormattedSize_Should_Show_Fractional_KB()
    {
        var estimate = new QueryCostEstimate(1536, 0.0m); // 1.5 KB

        estimate.FormattedSize.Should().MatchRegex(@"^1[.,]5 KB$");
    }

    [Fact]
    public void FormattedSize_Should_End_With_Correct_Unit()
    {
        new QueryCostEstimate(100, 0m).FormattedSize.Should().EndWith("B");
        new QueryCostEstimate(2048, 0m).FormattedSize.Should().EndWith("KB");
        new QueryCostEstimate(2 * 1024 * 1024, 0m).FormattedSize.Should().EndWith("MB");
        new QueryCostEstimate(2L * 1024 * 1024 * 1024, 0m).FormattedSize.Should().EndWith("GB");
    }
}
