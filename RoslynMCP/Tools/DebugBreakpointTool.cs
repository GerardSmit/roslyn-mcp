using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

[McpServerToolType]
public static class DebugBreakpointTool
{
    /// <summary>
    /// Sets a breakpoint in the active debug session.
    /// </summary>
    [McpServerTool, Description(
        "Set a breakpoint at a specific file and line in the active debug session.")]
    public static async Task<string> DebugSetBreakpoint(
        [Description("Path to the source file.")] string filePath,
        [Description("Line number for the breakpoint.")] int line,
        [Description("Optional condition expression. Breakpoint only triggers when expression is true (e.g. 'x > 5').")]
        string? condition = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = DebugSessionManager.GetSession();
            if (session is null)
                return "Error: No active debug session. Use DebugStartTest or DebugAttach first.";

            return await session.SetBreakpointAsync(filePath, line, condition, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Removes a breakpoint from the active debug session.
    /// </summary>
    [McpServerTool, Description(
        "Remove a breakpoint by its ID from the active debug session.")]
    public static async Task<string> DebugRemoveBreakpoint(
        [Description("Breakpoint ID to remove (shown when setting breakpoints).")] int breakpointId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = DebugSessionManager.GetSession();
            if (session is null)
                return "Error: No active debug session. Use DebugStartTest or DebugAttach first.";

            return await session.RemoveBreakpointAsync(breakpointId, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
