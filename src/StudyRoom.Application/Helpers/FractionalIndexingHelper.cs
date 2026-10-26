using StudyRoom.Core.Interfaces;

namespace StudyRoom.Application.Helpers;

public class FractionalIndexingHelper : IFractionalIndexingHelper
{
    private const double Epsilon = 1e-6;

    public double CalculateNewRank(double? prevRank, double? nextRank)
    {
        if (prevRank.HasValue && nextRank.HasValue)
        {
            return (prevRank.Value + nextRank.Value) / 2.0;
        }

        if (prevRank.HasValue && !nextRank.HasValue)
        {
            return prevRank.Value + 1.0;
        }

        if (!prevRank.HasValue && nextRank.HasValue)
        {
            return nextRank.Value / 2.0;
        }

        return 1.0;
    }

    public bool NeedsRebalance(IEnumerable<double> ranks)
    {
        var sorted = ranks.OrderBy(r => r).ToList();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (Math.Abs(sorted[i + 1] - sorted[i]) < Epsilon)
            {
                return true;
            }
        }

        return false;
    }

    public List<double> Rebalance(int count)
    {
        var result = new List<double>(count);
        for (int i = 1; i <= count; i++)
        {
            result.Add(i * 1.0);
        }

        return result;
    }
}
