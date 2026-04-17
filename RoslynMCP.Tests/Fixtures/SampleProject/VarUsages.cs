namespace SampleProject;

public class VarUsages
{
    public int VarMethod()
    {
        var calc = new Calculator();
        var result = calc.Add(1, 2);
        var text = result.ToString();
        return result;
    }

    public void ForeachMethod()
    {
        var numbers = new[] { 1, 2, 3 };
        foreach (var n in numbers)
        {
            var doubled = n * 2;
        }
    }
}
