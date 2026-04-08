# Roslyn Code Analysis MCP Server

## Overview
A Model Context Protocol (MCP) server that provides C# code analysis capabilities using the Roslyn compiler platform. This tool helps validate C# files, find symbol references, and perform static code analysis within the context of a .NET project.

Inspired by [egorpavlikhin/roslyn-mcp](https://github.com/egorpavlikhin/roslyn-mcp).

## Install as a .NET tool

Install from NuGet:
```
dotnet tool install --global roslyn-mcp
```

Update an existing global install:
```
dotnet tool update --global roslyn-mcp
```

## Example MCP config
For an installed tool:
```json
{
    "servers": {
        "RoslynMCP": {
            "type": "stdio",
            "command": "roslyn-mcp"
        }
    }
}
```

## Features
- **Code Validation**: Analyze C# files for syntax errors, semantic issues, and compiler warnings
- **Structured Diagnostics**: Get diagnostics in a compact, filterable markdown table format
- **Symbol Reference Finding**: Locate all usages of a symbol across a project
- **Go To Definition**: Navigate to a symbol's definition with code context
- **Symbol Search**: Discover symbols by name pattern across a project
- **Semantic Symbol Search**: Ranked, solution-oriented symbol search using name, signature, docs, and source cues
- **File Outline**: Get a compact, token-efficient view of a file's structure
- **Project Context Analysis**: Validate files within their project context
- **Code Analyzer Support**: Run Microsoft recommended code analyzers
- **MCP Resources**: Attach project structure and file outlines as context
- **MCP Prompts**: Workflow prompts for post-edit validation and symbol investigation

## Tools
- `ValidateFile`: Validates a C# file using Roslyn and runs code analyzers
- `GetRoslynDiagnostics`: Returns diagnostics in a compact markdown table with severity counts and optional filtering
- `FindUsages`: Finds all references to a symbol identified by a markup snippet
- `GoToDefinition`: Navigates to a source definition or auto-decompiles referenced assembly symbols into reusable generated source
- `FindSymbol`: Searches for symbol declarations by name pattern (exact, prefix, substring, camelCase)
- `SemanticSymbolSearch`: Ranked, phrase-tolerant symbol search (see [details below](#semanticsymbolsearch))
- `GetFileOutline`: Returns a compact tree-style outline of a file's namespaces, types, and members with line numbers

## Resources
MCP resources provide stable, attachable context that clients can include in conversations.

- `project-structure` (`roslyn://project-structure/{filePath}`): Returns the file/folder structure of the .NET project containing the given file. Grouped by directory with document counts.
- `file-outline` (`roslyn://file-outline/{filePath}`): Returns a compact structural outline of a C# file (namespaces, types, members with line numbers). Same data as the `GetFileOutline` tool, available as attachable context.

## Prompts
MCP prompts provide reusable workflow templates that guide the LLM through multi-step operations.

- `validate-after-edit`: Generates step-by-step instructions to validate a C# file after editing — runs diagnostics, checks the file outline, and summarizes pass/fail status. Arguments: `filePath`.
- `investigate-symbol`: Generates a multi-step investigation workflow for a symbol — finds declarations, navigates to definitions, locates usages, and summarizes the symbol's role. Arguments: `filePath`, `symbolName`.
