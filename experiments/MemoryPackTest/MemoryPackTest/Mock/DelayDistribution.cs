using System;
using System.Threading.Tasks;

namespace FrameworkTest;

class DelayDistribution
{
    public required double ExpectedValue { get; init; }
    public required double Variance  { get; init; }

    readonly Random random_;

    public DelayDistribution(Random? random = null)
    {
        random_ = random ?? new();
    }

    public void RunDelayed(Action action) => Task.Delay(Sample()).ContinueWith((_) => action());

    public TimeSpan Sample()
    {
        double value;

        do
        {
            double uniform = random_.NextDouble();
            value = ExpectedValue + Variance * Math.Log(uniform / (1d - uniform));
        } while (double.IsNegative(value) || !double.IsFinite(value));

        return TimeSpan.FromSeconds(value);
    }
}
