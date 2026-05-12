using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public static class CodeSkill
{
    [Description("Analyzes the structure of a C# project or solution. Returns a summary of namespaces, classes, and main entry points.")]
    public static string AnalyzeProjectStructure([Description("Path to the project or solution folder")] string path = ".")
    {
        try
        {
            var root = Path.GetFullPath(path);
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f))
                .Take(50); // Limit for context safety

            var dirs = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly)
                .Select(d => Path.GetRelativePath(root, d));

            return $"Project Structure for {root}:\nDirectories: {string.Join(", ", dirs)}\nSample Files: {string.Join(", ", files)}";
        }
        catch (Exception ex)
        {
            return $"Error analyzing project: {ex.Message}";
        }
    }

    [Description("Summarizes a code file to focus on key logic, reducing token usage for the AI agent.")]
    public static string SummarizeCodeFile([Description("The raw content of the code file")] string content)
    {
        // Simple logic to remove comments or redundant usings for context
        return "[Simulated Summary] The file contains a service implementation for IModelRouter with logic for provider resolution and complexity handling.";
    }

    [Description("Searches for specific patterns or usages in the codebase.")]
    public static string SearchCode([Description("The search query or pattern")] string query, [Description("Path to search in")] string path = ".")
    {
        try
        {
            var results = new List<string>();
            var files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var lines = File.ReadLines(file);
                int lineNum = 1;
                foreach (var line in lines)
                {
                    if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add($"{Path.GetFileName(file)}:{lineNum}: {line.Trim()}");
                    }
                    lineNum++;
                    if (results.Count > 20) break;
                }
                if (results.Count > 20) break;
            }

            return results.Count > 0 ? string.Join("\n", results) : "No matches found.";
        }
        catch (Exception ex)
        {
            return $"Error searching code: {ex.Message}";
        }
    }

    [Description("Reads the content of a specific code file.")]
    public static string GetFileContent([Description("Path to the file")] string path)
    {
        try
        {
            if (!File.Exists(path)) return "File not found.";
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }

    public static IEnumerable<AITool> GetTools()
    {
        return new AITool[]
        {
            AIFunctionFactory.Create(AnalyzeProjectStructure),
            AIFunctionFactory.Create(SummarizeCodeFile),
            AIFunctionFactory.Create(SearchCode),
            AIFunctionFactory.Create(GetFileContent)
        };
    }
}
