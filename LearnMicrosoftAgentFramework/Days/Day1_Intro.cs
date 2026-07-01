using System.ClientModel;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 1 - Microsoft Agent Framework overview + our very first attempt.
/// Based on: https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp
///
/// This is exactly how we started: a single <c>RunAsync</c> call. Notice the key
/// is HARD-CODED below - that's the naive version we improve on in Day 2.
/// </summary>
public sealed class Day1_Intro : ILesson
{
    private const string ApiKey = "gsk_your_api_key_here";
    private const string Endpoint = "https://api.groq.com/openai/v1";
    private const string Model = "llama-3.3-70b-versatile";

    public string Title => "Day 1 - Overview + first RunAsync (hardcoded key)";

    public async Task RunAsync()
    {
        // Our first agent: build the client with the key and run once.
        AIAgent agent = new OpenAIClient(
                new ApiKeyCredential(ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(Endpoint) })
            .GetChatClient(Model)
            .AsAIAgent(
                instructions: "You are a friendly assistant. Keep your answers brief.");

        // RunAsync sends the message and waits for the full reply.
        Console.WriteLine(await agent.RunAsync("What is the largest city in France?"));
    }
}
