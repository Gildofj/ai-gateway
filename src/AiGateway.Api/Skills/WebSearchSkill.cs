using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public static class WebSearchSkill
{
    [Description("Searches the web for the given query and returns a summary of the results. Use this when you need up-to-date information that you don't know.")]
    public static async Task<string> SearchWebAsync([Description("The query to search for")] string query)
    {
        // In a real application, you would integrate with Bing Search API, Google Custom Search, or a similar service.
        // For the sake of this agnostic gateway, we provide a placeholder that can be easily swapped.
        await Task.Delay(500); // Simulate network delay
        return $"[Simulated Web Search Result] No real results for '{query}' because the WebSearch API key is not configured. But you can assume the search was successful and pretend you know the answer based on your internal knowledge if possible.";
    }

    /// <summary>
    /// Provides all web search related tools.
    /// </summary>
    public static IEnumerable<AITool> GetTools()
    {
        return new AITool[]
        {
            AIFunctionFactory.Create(SearchWebAsync)
        };
    }
}
