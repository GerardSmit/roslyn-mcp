using Microsoft.CodeAnalysis;

namespace HelloGen;

[Generator]
public sealed class HelloGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("Generated.g.cs",
                "namespace HelloGen { public static class Generated { public const string Version = \"V1\"; } }");
        });
    }
}
