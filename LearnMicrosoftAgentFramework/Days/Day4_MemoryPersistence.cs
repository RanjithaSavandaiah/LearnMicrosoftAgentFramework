using System.Text.Json;
using Microsoft.Agents.AI;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 4 - Memory and persistence (saving a conversation so it survives a restart).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/get-started/memory?pivots=programming-language-csharp
///
/// Day 3 showed that an <see cref="AgentSession"/> gives the agent a memory WHILE
/// the program runs. But that memory lives in RAM - close the app and it's gone.
///
/// This lesson adds PERSISTENCE:
///   - SerializeSessionAsync turns a session (its whole history) into JSON.
///   - We save that JSON to a file (a real app would use a database or blob store).
///   - DeserializeSessionAsync rebuilds the session from that JSON later, even in a
///     brand new agent instance - so the conversation continues as if the app
///     never stopped.
///
///  Persistence uses SerializeSessionAsync /
///   DeserializeSessionAsync.
/// </summary>
public sealed class Day4_MemoryPersistence : ILesson
{
    public string Title => "Day 4 - Memory & persistence (save/restore a session)";

    public async Task RunAsync()
    {
        // Where we'll save the conversation. A real app would use a database or
        // blob store, a file keeps this sample simple and lets you SEE the JSON.
        string savePath = Path.Combine(Environment.CurrentDirectory, "day4-conversation.json");

        AIAgent agent = AgentFactory.CreateAgent(
            name: "Assistant",
            instructions: "You are a friendly assistant. Keep your answers brief.");

        // Part 1 - Have a short conversation in a session (Day 3 recap).
        // We give the agent a couple of facts it will need to recall later.
        Console.WriteLine("Part 1: Start a conversation and give the agent some facts");
        Console.WriteLine("----------------------------------------------------------");

        AgentSession session = await agent.CreateSessionAsync();

        const string facts = "My name is Ranjitha and my favourite colour is teal.";
        Console.WriteLine($"You:   {facts}");
        Console.WriteLine($"Agent: {await agent.RunAsync(facts, session)}");

        // Part 2 - Persist the session to a file.
        // SerializeSessionAsync captures the whole conversation as JSON. We write it
        // to disk so it outlives this process.
        Console.WriteLine();
        Console.WriteLine("Part 2: Save the conversation to disk");
        Console.WriteLine("-------------------------------------");

        JsonElement serialized = await agent.SerializeSessionAsync(session);
        await File.WriteAllTextAsync(savePath, serialized.GetRawText());
        Console.WriteLine($"Saved the conversation to: {savePath}");

        // Part 3 - Simulate an app restart.
        // We throw away the old session and build a FRESH agent, then restore the
        // session from the saved JSON. The restored session still knows the facts,
        // proving the memory survived the "restart".
        Console.WriteLine();
        Console.WriteLine("Part 3: 'Restart' the app and restore the conversation");
        Console.WriteLine("------------------------------------------------------");

        AIAgent restoredAgent = AgentFactory.CreateAgent(
            name: "Assistant",
            instructions: "You are a friendly assistant. Keep your answers brief.");

        string savedJson = await File.ReadAllTextAsync(savePath);
        using JsonDocument document = JsonDocument.Parse(savedJson);
        AgentSession restoredSession =
            await restoredAgent.DeserializeSessionAsync(document.RootElement);

        Console.WriteLine("You:   What is my name and favourite colour?");
        Console.WriteLine($"Agent: {await restoredAgent.RunAsync("What is my name and favourite colour?", restoredSession)}");
        Console.WriteLine("(The facts came from the restored session - memory survived the 'restart'.)");
    }
}
