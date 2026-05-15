using System.ComponentModel;
using AiGateway.Api.Features.Memory;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public class MemorySkill
{
    private readonly IMemoryStore _store;

    public MemorySkill(IMemoryStore store)
    {
        _store = store;
    }

    [Description("Stores a piece of information in the persistent memory.")]
    public async Task<string> StoreInfo(
        [Description("The key to identify the information")] string key,
        [Description("The content to store")] string value)
    {
        await _store.SetAsync(key, value);
        return $"Stored '{key}'.";
    }

    [Description("Retrieves a piece of information from the persistent memory.")]
    public async Task<string> GetInfo([Description("The key of the information to retrieve")] string key)
    {
        var entry = await _store.GetAsync(key);
        return entry != null ? entry.Value : $"No entry for '{key}'.";
    }

    [Description("Lists all keys currently stored in memory.")]
    public async Task<string> ListMemory()
    {
        var entries = await _store.ListAsync();
        var keys = entries.Select(e => e.Key).ToList();
        return keys.Count > 0
            ? $"Keys: {string.Join(", ", keys)}"
            : "Memory is empty.";
    }

    public IEnumerable<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(StoreInfo),
        AIFunctionFactory.Create(GetInfo),
        AIFunctionFactory.Create(ListMemory)
    ];
}
