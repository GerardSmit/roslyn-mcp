namespace Aardvark.Empty;

public class ExternalMath
{
    public int AddTen(int value)
    {
        return ApplyOffset(value, 10);
    }

    private int ApplyOffset(int value, int offset)
    {
        return value + offset;
    }
}
