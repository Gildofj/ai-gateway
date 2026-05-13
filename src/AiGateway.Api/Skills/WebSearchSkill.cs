using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public static class WebSearchSkill
{
    [Description("Searches the web for the given query and returns a summary of the results. Use this when you need up-to-date information that you don't know.")]
    public static async Task<string> SearchWebAsync([Description("The query to search for")] string query)
    {
        await Task.Delay(800); // Simulate network delay

        // Mocking some financial data if it looks like a financial query
        if (query.Contains("SELIC", StringComparison.OrdinalIgnoreCase) || query.Contains("taxa", StringComparison.OrdinalIgnoreCase))
        {
            return $"[Web Search Results for '{query}']\n" +
                   "1. Central Bank of Brazil: SELIC rate is currently at 10.75% per year.\n" +
                   "2. Financial News: Analysts expect a further decrease in the next Copom meeting.";
        }

        return $"[Web Search Results for '{query}']\n" +
               $"1. Top result for {query}: Current trends show increasing interest in AI integration.\n" +
               "2. Related news: AI Gateway solutions are becoming essential for cost management.";
    }

    public static IEnumerable<AITool> GetTools()
    {
        return new AITool[]
        {
            AIFunctionFactory.Create(SearchWebAsync)
        };
    }
}
