using Microsoft.Agents.AI;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 3 - Multi-turn conversations (giving the agent a memory).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/get-started/multi-turn?pivots=programming-language-csharp
///
/// The big idea:
///   Each call to <c>RunAsync</c> is stateless by default - the agent forgets
///   everything the moment it replies. To hold a real conversation we need an
///   <see cref="AgentSession"/>. A session stores the running history of messages,
///   and by passing the SAME session into every call the agent "remembers" what
///   was said before.

/// This lesson shows three things:
///   Part 1 - No session: the agent forgets between calls (the problem).
///   Part 2 - One shared session: the agent remembers (the fix).
///   Part 3 - An interactive chat loop that keeps the conversation going.
/// </summary>
/// 
public sealed class Day3_MultiTurn : ILesson
{
    public string Title => "Day 3 - Multi-turn conversations (sessions)";

    public async Task RunAsync()
    {
        AIAgent agent = AgentFactory.CreateAgent(
            name: "Assistant",
            instructions: "You are a friendly assistant. Keep your answers brief.");

        // Part 1 - Without a session the agent has no memory.
        // We tell it our name, then ask for it back. Because each RunAsync is
        // independent, the second answer will NOT know the name.
        Console.WriteLine("Part 1: No session - the agent forgets");
        Console.WriteLine("--------------------------------------");

        Console.WriteLine("You:   My name is Ranjitha. Remember it.");
        Console.WriteLine($"Agent: {await agent.RunAsync("My name is Ranjitha. Remember it.")}");

        Console.WriteLine("You:   What is my name?");
        Console.WriteLine($"Agent: {await agent.RunAsync("What is my name?")}");
        Console.WriteLine("(Notice: without a session, the agent can't recall the name.)");

        // Part 2 - With a session the agent remembers.
        // CreateSessionAsync() starts a fresh conversation. We pass that SAME session
        // into every RunAsync, so the history builds up and the agent can use it.
        Console.WriteLine();
        Console.WriteLine("Part 2: A shared session - the agent remembers");
        Console.WriteLine("----------------------------------------------");

        AgentSession session = await agent.CreateSessionAsync();

        Console.WriteLine("You:   My name is Ranjitha. Remember it.");
        Console.WriteLine($"Agent: {await agent.RunAsync("My name is Ranjitha. Remember it.", session)}");

        Console.WriteLine("You:   What is my name?");
        Console.WriteLine($"Agent: {await agent.RunAsync("What is my name?", session)}");
        Console.WriteLine("(This time the agent recalls the name from the session's history.)");

        // Part 3 - An interactive chat loop.
        // Same idea, but now YOU drive the conversation. As long as we keep passing
        // the same session, the agent remembers the whole exchange. We stream the
        // reply so it appears as it is generated.
        Console.WriteLine();
        Console.WriteLine("Part 3: Chat with the agent (type 'exit' to stop)");
        Console.WriteLine("-------------------------------------------------");

        AgentSession chatSession = await agent.CreateSessionAsync();

        while (true)
        {
            Console.Write("You:   ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Ending the chat. The session (and its memory) is discarded.");
                break;
            }

            Console.Write("Agent: ");
            await foreach (AgentResponseUpdate update in
                agent.RunStreamingAsync(input, chatSession))
            {
                Console.Write(update);
            }
            Console.WriteLine();
        }
    }
}
