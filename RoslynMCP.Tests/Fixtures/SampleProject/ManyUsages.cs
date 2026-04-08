namespace SampleProject;

public class ManyUsages
{
    public int UseAddManyTimes(Calculator calculator, int a, int b)
    {
        int total = calculator.Add(a, b);
        total += calculator.Add(total, b);
        total += calculator.Add(a, total);
        total += calculator.Add(total, total);
        return calculator.Add(total, a);
    }
}
