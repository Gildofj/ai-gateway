using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public static class TimeSkill
{
    [Description("Gets the current UTC date and time.")]
    public static string GetCurrentUtcTime()
    {
        return DateTime.UtcNow.ToString("O");
    }

    [Description("Gets the current local date and time. Use this when the user asks for the current time.")]
    public static string GetCurrentLocalTime()
    {
        return DateTime.Now.ToString("O");
    }

    /// <summary>
    /// Provides all time-related tools.
    /// </summary>
    public static IEnumerable<AITool> GetTools()
    {
        return new AITool[]
        {
            AIFunctionFactory.Create(GetCurrentUtcTime),
            AIFunctionFactory.Create(GetCurrentLocalTime)
        };
    }
}
