namespace StudyRoom.Core.Interfaces;

public interface IFractionalIndexingHelper
{
    double CalculateNewRank(double? prevRank, double? nextRank);
    bool NeedsRebalance(IEnumerable<double> ranks);
    List<double> Rebalance(int count);
}
