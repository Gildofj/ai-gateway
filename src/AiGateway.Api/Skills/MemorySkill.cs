using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public class MemorySkill
{
    private readonly Dictionary<string, string> _memory = new();

    [Description("Stores a piece of information in the session memory.")]
    public string StoreInfo(
        [Description("The key to identify the information")] string key,
        [Description("The content to store")] string value)
    {
        _memory[key] = value;
        return $"Stored '{key}'.";
    }

    [Description("Retrieves a piece of information from the session memory.")]
    public string GetInfo([Description("The key of the information to retrieve")] string key)
    {
        return _memory.TryGetValue(key, out var value) ? value : $"No entry for '{key}'.";
    }

    [Description("Lists all keys currently stored in memory.")]
    public string ListMemory()
    {
        return _memory.Count > 0
            ? $"Keys: {string.Join(", ", _memory.Keys)}"
            : "Memory is empty.";
    }

    public IEnumerable<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(StoreInfo),
        AIFunctionFactory.Create(GetInfo),
        AIFunctionFactory.Create(ListMemory)
    ];
}
