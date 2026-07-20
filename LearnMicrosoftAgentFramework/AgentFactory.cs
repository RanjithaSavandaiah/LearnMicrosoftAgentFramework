using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace LearnMicrosoftAgentFramework;

/// <summary>
/// Shared helper that builds an <see cref="AIAgent"/> for every day's sample.
/// Keeping this in one place means each "day" file can focus on the new concept
/// it introduces, instead of repeating the client/credential setup.
/// </summary>
public static class AgentFactory
{
    // We use an OpenAI-compatible endpoint (Groq) so any OpenAI-style model works.
    private const string DefaultModel = "llama-3.3-70b-versatile";
    private const string Endpoint = "https://api.groq.com/openai/v1";

    /// <summary>
    /// A model that reliably supports structured tool/function calling. The default
    /// llama model tends to loop and emit malformed tool calls, so tool samples use
    /// this one instead.
    /// </summary>
    public const string ToolCapableModel = "openai/gpt-oss-20b";

    /// <summary>
    /// Creates an agent. Pass an optional <paramref name="name"/>,
    /// <paramref name="instructions"/> to give the agent its own persona,
    /// optional <paramref name="tools"/> the agent can call during a run, and an
    /// optional <paramref name="model"/> to override the default model.
    /// </summary>
    public static AIAgent CreateAgent(
        string? name = null,
        string? instructions = null,
        IList<AITool>? tools = null,
        string? model = null)
    {
        // Read the API key from an environment variable instead of hard-coding it.
        // PowerShell: setx GROQ_API_KEY "your-key-here"
        string apiKey = GetApiKey()
            ?? throw new InvalidOperationException(
                "GROQ_API_KEY is not set. Either run:  $env:GROQ_API_KEY = \"your-key\"  in " +
                "this terminal, or set it with setx and open a NEW terminal, then try again.");

        return new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri(Endpoint) })
            .GetChatClient(model ?? DefaultModel)
            .AsAIAgent(
                instructions: instructions ?? "You are a friendly assistant. Keep your answers brief.",
                name: name,
                tools: tools);
    }

    /// <summary>
    /// Creates an agent from a fully specified <see cref="ChatClientAgentOptions"/>.
    /// This is the richer entry point used when you need to configure pipeline pieces
    /// like <c>AIContextProviders</c> (the context layer) or a custom chat history
    /// provider that the simpler <see cref="CreateAgent(string?, string?, IList{AITool}?, string?)"/>
    /// overload doesn't expose.
    /// </summary>
    public static AIAgent CreateAgent(ChatClientAgentOptions options, string? model = null)
    {
        string apiKey = GetApiKey()
            ?? throw new InvalidOperationException(
                "GROQ_API_KEY is not set. Either run:  $env:GROQ_API_KEY = \"your-key\"  in " +
                "this terminal, or set it with setx and open a NEW terminal, then try again.");

        return new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri(Endpoint) })
            .GetChatClient(model ?? DefaultModel)
            .AsAIAgent(options);
    }

    /// <summary>
    /// Builds a raw <see cref="IChatClient"/> against the same Groq endpoint. Useful
    /// for components that talk to the model directly rather than through an agent -
    /// for example an "LLM-as-judge" that scores another agent's work.
    /// </summary>
    public static IChatClient CreateChatClient(string? model = null)
    {
        string apiKey = GetApiKey()
            ?? throw new InvalidOperationException(
                "GROQ_API_KEY is not set. Either run:  $env:GROQ_API_KEY = \"your-key\"  in " +
                "this terminal, or set it with setx and open a NEW terminal, then try again.");

        return new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri(Endpoint) })
            .GetChatClient(model ?? DefaultModel)
            .AsIChatClient();
    }

    /// <summary>
    /// Looks up GROQ_API_KEY. Checks the current process first, then the User and
    /// Machine scopes. The extra scopes matter because <c>setx</c> updates the User
    /// scope but not the terminal that is already open, so a plain process-only read
    /// would miss it until the terminal is restarted.
    /// </summary>
    private static string? GetApiKey()
    {
        foreach (EnvironmentVariableTarget scope in new[]
                 {
                     EnvironmentVariableTarget.Process,
                     EnvironmentVariableTarget.User,
                     EnvironmentVariableTarget.Machine,
                 })
        {
            string? value = Environment.GetEnvironmentVariable("GROQ_API_KEY", scope);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}


