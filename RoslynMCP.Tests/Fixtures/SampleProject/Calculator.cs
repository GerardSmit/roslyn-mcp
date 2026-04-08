namespace SampleProject;

public class Calculator
{
    public int Add(int a, int b) => a + b;

    public int Subtract(int a, int b) => a - b;

    public Result Compute(int a, int b)
    {
        return new Result(Add(a, b), Subtract(a, b));
    }
}
