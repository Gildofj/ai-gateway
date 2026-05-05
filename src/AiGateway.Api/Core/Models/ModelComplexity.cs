namespace AiGateway.Api.Core.Models;

public enum ModelComplexity
{
    /// <summary>
    /// Use for simple tasks like summarization, translation, simple data extraction.
    /// Routes to cheaper/faster models.
    /// </summary>
    Low,

    /// <summary>
    /// Use for complex tasks like coding, logic reasoning, or complex system design.
    /// Routes to capable/expensive models.
    /// </summary>
    High
}
