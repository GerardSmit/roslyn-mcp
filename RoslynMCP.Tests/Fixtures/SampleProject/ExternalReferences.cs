using Aardvark.Empty;

namespace SampleProject;

public class ExternalReferences
{
    public int ComputeWithExternal(int value)
    {
        var math = new ExternalMath();
        return math.AddTen(value);
    }
}
