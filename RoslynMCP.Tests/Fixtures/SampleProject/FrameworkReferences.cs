using System;
using System.Text;

namespace SampleProject;

public class FrameworkReferences
{
    public void WriteToConsole(string value)
    {
        Console.WriteLine(value);
    }

    public string BuildMessage(string value)
    {
        var builder = new StringBuilder();
        builder.Append(value);
        return builder.ToString();
    }
}
