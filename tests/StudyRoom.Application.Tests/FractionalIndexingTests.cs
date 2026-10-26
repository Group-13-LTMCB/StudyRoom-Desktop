using StudyRoom.Application.Helpers;

namespace StudyRoom.Application.Tests;

public class FractionalIndexingTests
{
    private readonly FractionalIndexingHelper _helper = new();

    [Fact]
    public void CalculateNewRank_EmptyList_ReturnsOne()
    {
        double rank = _helper.CalculateNewRank(null, null);
        Assert.Equal(1.0, rank);
    }

    [Fact]
    public void CalculateNewRank_InsertAtStart_ReturnsHalfOfNextRank()
    {
        double rank = _helper.CalculateNewRank(null, 2.0);
        Assert.Equal(1.0, rank);

        double rankFromOne = _helper.CalculateNewRank(null, 1.0);
        Assert.Equal(0.5, rankFromOne);
    }

    [Fact]
    public void CalculateNewRank_InsertAtEnd_ReturnsPrevRankPlusOne()
    {
        double rank = _helper.CalculateNewRank(2.5, null);
        Assert.Equal(3.5, rank);
    }

    [Fact]
    public void CalculateNewRank_InsertBetween_ReturnsAverage()
    {
        double rank = _helper.CalculateNewRank(1.0, 2.0);
        Assert.Equal(1.5, rank);

        double rank2 = _helper.CalculateNewRank(1.25, 1.50);
        Assert.Equal(1.375, rank2);
    }

    [Fact]
    public void NeedsRebalance_WhenRanksAreFarApart_ReturnsFalse()
    {
        var ranks = new[] { 1.0, 1.5, 2.0, 3.0 };
        Assert.False(_helper.NeedsRebalance(ranks));
    }

    [Fact]
    public void NeedsRebalance_WhenRanksAreTooClose_ReturnsTrue()
    {
        var ranks = new[] { 1.0, 1.00000001, 2.0 };
        Assert.True(_helper.NeedsRebalance(ranks));
    }

    [Fact]
    public void NeedsRebalance_WhenLessThanTwoItems_ReturnsFalse()
    {
        Assert.False(_helper.NeedsRebalance(new[] { 1.0 }));
        Assert.False(_helper.NeedsRebalance(Array.Empty<double>()));
    }

    [Fact]
    public void Rebalance_ReturnsSequentialRanks()
    {
        var result = _helper.Rebalance(4);
        Assert.Equal(new[] { 1.0, 2.0, 3.0, 4.0 }, result);
    }
}
