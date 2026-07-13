using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 7 - Agents, and the many ways to run them.
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/agents/?pivots=programming-language-csharp
///   https://learn.microsoft.com/en-us/agent-framework/agents/running-agents?pivots=programming-language-csharp
///
/// The big idea:
///   An <see cref="AIAgent"/> is the core abstraction of the framework. Once you
///   have one, there is a small, consistent set of ways to "run" it. This lesson
///   walks through each of them so the mechanics are crystal clear:
///
///   Part 1 - Inspect the agent (Id / Name / Description are part of every agent).
///   Part 2 - Run with a single string prompt (the simplest call).
///   Part 3 - Run with explicit ChatMessage objects (control the role, send many).
///   Part 4 - Stream the reply with RunStreamingAsync (text as it is generated).
///   Part 5 - Keep context across calls with an AgentSession (a "thread").
///   Part 6 - Pass run options (e.g. Temperature) per call.
///   Part 7 - Cancel a run with a CancellationToken.
/// </summary>
public sealed class Day7_RunningAgents : ILesson
{
    public string Title => "Day 7 - Agents + the ways to run them";

    public async Task RunAsync()
    {
        AIAgent agent = AgentFactory.CreateAgent(
            name: "Haiku",
            instructions: "You are a helpful assistant. Keep your answers brief.");

        // Part 1 - Every agent exposes identity metadata.
        // These come "for free" with any AIAgent and are handy for logging,
        // routing between agents, or showing which agent produced a reply.
        Console.WriteLine("Part 1: Inspect the agent");
        Console.WriteLine("-------------------------");
        Console.WriteLine($"Id:          {agent.Id}");
        Console.WriteLine($"Name:        {agent.Name}");
        Console.WriteLine($"Description:  {agent.Description}");

        // Part 2 - The simplest run: one string in, one response out.
        // RunAsync is non-streaming: it waits for the whole reply, then returns an
        // AgentResponse. Calling .ToString() (or printing it) gives the text.
        Console.WriteLine();
        Console.WriteLine("Part 2: Run with a single string prompt");
        Console.WriteLine("---------------------------------------");

        AgentResponse response = await agent.RunAsync("Name three primary colors.");
        Console.WriteLine($"Agent: {response}");

        // Part 3 - Run with explicit ChatMessage objects.
        // A string is just shorthand for a single user message. When you need to set
        // the role yourself, or send several messages at once, build ChatMessages.
        // Here we seed one turn of context and then ask a follow-up in the same call.
        Console.WriteLine();
        Console.WriteLine("Part 3: Run with explicit ChatMessages");
        Console.WriteLine("--------------------------------------");

        List<ChatMessage> messages =
        [
            new(ChatRole.User, "My favorite number is 7."),
            new(ChatRole.User, "Multiply my favorite number by 6."),
        ];

        AgentResponse mathResponse = await agent.RunAsync(messages);
        Console.WriteLine($"Agent: {mathResponse}");

        // Part 4 - Streaming.
        // RunStreamingAsync yields AgentResponseUpdate chunks as the model
        // produces them, so the answer appears progressively instead of all at once.
        Console.WriteLine();
        Console.WriteLine("Part 4: Stream the reply");
        Console.WriteLine("------------------------");

        Console.Write("Agent: ");
        await foreach (AgentResponseUpdate update in
            agent.RunStreamingAsync("Explain what an AI agent is in one sentence."))
        {
            Console.Write(update);
        }
        Console.WriteLine();

        // Part 5 - Threads (sessions) keep context between runs.
        // By default each run is stateless. Create a session once and pass it into
        // every run to carry the conversation history forward - the agent then
        // "remembers" earlier turns (this is the same idea covered on Day 3).
        Console.WriteLine();
        Console.WriteLine("Part 5: Preserve context with a session (thread)");
        Console.WriteLine("------------------------------------------------");

        AgentSession session = await agent.CreateSessionAsync();

        Console.WriteLine("You:   Remember the code word is 'falcon'.");
        Console.WriteLine($"Agent: {await agent.RunAsync("Remember the code word is 'falcon'.", session)}");

        Console.WriteLine("You:   What is the code word?");
        Console.WriteLine($"Agent: {await agent.RunAsync("What is the code word?", session)}");

        // Part 6 - Per-run options.
        // AgentRunOptions lets you tweak how a single run behaves without changing
        // the agent itself. Here we raise the temperature for a more creative reply.
        // ChatClientAgentRunOptions carries the underlying ChatOptions to the model.
        Console.WriteLine();
        Console.WriteLine("Part 6: Pass run options (temperature)");
        Console.WriteLine("--------------------------------------");

        ChatClientAgentRunOptions creativeOptions = new(new ChatOptions
        {
            Temperature = 0.9f,
            MaxOutputTokens = 60,
        });

        AgentResponse creative = await agent.RunAsync(
            "Invent a playful name for a coffee mug.",
            options: creativeOptions);
        Console.WriteLine($"Agent: {creative}");

        // Part 7 - Cancellation.
        // Every run accepts a CancellationToken. Passing one lets you abort a run
        // that takes too long. Here we give it a short timeout to demonstrate how a
        // cancelled run surfaces as an OperationCanceledException.
        Console.WriteLine();
        Console.WriteLine("Part 7: Cancel a run with a CancellationToken");
        Console.WriteLine("---------------------------------------------");

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(1));
        try
        {
            AgentResponse cancelled = await agent.RunAsync(
                "Write a long essay about the history of computing.",
                cancellationToken: cts.Token);
            Console.WriteLine($"Agent: {cancelled}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("The run was cancelled before it finished (as expected).");
        }
    }
}
