# RoslynSense

A Model Context Protocol (MCP) server that provides C# code analysis, navigation, refactoring, testing, and debugging capabilities using the Roslyn compiler platform.

Inspired by [egorpavlikhin/roslyn-mcp](https://github.com/egorpavlikhin/roslyn-mcp).

## Install

Install from NuGet as a global .NET tool:

```
dotnet tool install --global RoslynSense
```

Update an existing install:

```
dotnet tool update --global RoslynSense
```

## MCP Configuration

```json
{
    "servers": {
        "RoslynSense": {
            "type": "stdio",
            "command": "roslyn-sense"
        }
    }
}
```

## Tools

### Code Analysis

| Tool | Description |
|------|-------------|
| **ValidateFile** | Validate a C# file using Roslyn and run code analyzers. Also supports ASPX/ASCX and Razor (.razor/.cshtml) files. |
| **GetRoslynDiagnostics** | Get diagnostics in a compact markdown table with severity counts. Accepts a severity filter (error, warning, info, hidden, all). |
| **GetCodeActions** | List available code fixes for a diagnostic. Optionally apply a fix by index. |

### Navigation

| Tool | Description |
|------|-------------|
| **GoToDefinition** | Navigate to a symbol's definition with code context, or auto-decompile referenced assembly symbols. |
| **FindUsages** | Find all references to a symbol across a project. Also searches Razor source-generated files and ASPX inline code. |
| **FindSymbol** | Search for symbol declarations by name pattern (exact, prefix, substring, camelCase). |
| **SemanticSymbolSearch** | Ranked symbol search combining name, signature, docs, and source cues. Supports phrase-style queries. |
| **FindImplementations** | Find all implementations of an interface, abstract class, or virtual/abstract member. |
| **FindTests** | Find test methods that reference a symbol. Returns method names, file paths, and line numbers. |
| **GetCallHierarchy** | Show callers and/or callees of a method or property. |
| **GetTypeHierarchy** | Show the full type hierarchy (base classes, interfaces, derived types). |

### Structure

| Tool | Description |
|------|-------------|
| **GetProjectStructure** | Get an overview of a project: target framework, references, source files, and types by namespace. |
| **GetFileOutline** | Get a compact outline of a C# or ASPX file with namespaces, types, members, and line numbers. |

### Refactoring

| Tool | Description |
|------|-------------|
| **RenameSymbol** | Rename a symbol and all references across the project, including ASPX/ASCX files. Supports dry-run preview. |

### Testing

| Tool | Description |
|------|-------------|
| **RunTests** | Run tests in a .NET test project with optional filter expression. |

### Debugging

Debugging uses [netcoredbg](https://github.com/Samsung/netcoredbg), which is auto-provisioned on first use.

| Tool | Description |
|------|-------------|
| **DebugStartTest** | Start debugging a .NET test project. Builds, launches the test host, and attaches the debugger. |
| **DebugAttach** | Attach the debugger to a running .NET process by PID. |
| **DebugListProcesses** | List running .NET processes that can be attached to. |
| **DebugSetBreakpoint** | Set a breakpoint at a file and line. |
| **DebugRemoveBreakpoint** | Remove a breakpoint by ID. |
| **DebugContinue** | Continue execution until the next breakpoint or exit. |
| **DebugStepIn** | Step into the next function call. |
| **DebugStepOver** | Step over the current line. |
| **DebugStepOut** | Step out of the current function. |
| **DebugEvaluate** | Evaluate an expression in the current debug context. |
| **DebugLocals** | Get local variables and their values. |
| **DebugStackTrace** | Get the call stack. |
| **DebugStatus** | Get debugger status, breakpoints, and current pause position. |
| **DebugStop** | Stop the debug session and clean up. |

## Resources

| Resource | URI Pattern | Description |
|----------|-------------|-------------|
| **project-structure** | `roslyn://project-structure/{filePath}` | Project file/folder structure grouped by directory. |
| **file-outline** | `roslyn://file-outline/{filePath}` | Structural outline of a C# file (same as GetFileOutline tool). |

## Prompts

| Prompt | Description |
|--------|-------------|
| **validate-after-edit** | Step-by-step instructions to validate a C# file after editing. |
| **investigate-symbol** | Multi-step investigation workflow for a symbol. |

## Markup Snippet Convention

Many tools use a `markupSnippet` parameter with `[| |]` delimiters to identify a target symbol:

```
var x = [|Foo|].Bar();          // targets Foo
public interface [|IService|]    // targets IService
void [|ProcessData|](int x)     // targets ProcessData
```

The snippet is matched against the file content. Whitespace differences are tolerated.
