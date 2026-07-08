using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 6 - Host your agent (register it in dependency injection).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/get-started/hosting?pivots=programming-language-csharp
///
/// So far we've NEW-ed up agents by hand. Real apps (ASP.NET Core, worker
/// services) instead register the agent once in the dependency injection (DI)
/// container, then let the framework hand it to whatever needs it - a controller,
/// a minimal API endpoint, a background service, etc.
///
/// The hosting library (Microsoft.Agents.AI.Hosting) adds AddAIAgent, which
/// registers an AIAgent by NAME as a keyed service. This lesson shows the whole
/// loop WITHOUT spinning up a blocking web server:
///   1. Build a Generic Host and register the agent with AddAIAgent.
///   2. Resolve it back out of DI by its name (exactly how an endpoint would).
///   3. Run it, proving the DI provided agent works like any other.
///
/// The article goes on to EXPOSE this hosted agent over HTTP (the A2A protocol)
/// using ASP.NET Core. That needs a web project and a server that runs forever, so
/// it doesn't fit this console menu - see the README note for how to take the next
/// step. The core idea, though, is exactly what we do here: register once, resolve
/// by name, use anywhere.
///
/// NOTE: The hosting package is preview (1.11.1-preview). This sample still needs
/// GROQ_API_KEY because the registered agent makes a real call when we run it.
/// </summary>
public sealed class Day6_Hosting : ILesson
{
    private const string AgentName = "PirateAgent";

    public async Task RunAsync()
    {
        // Part 1 - Register the agent in a host's DI container.
        // Host.CreateApplicationBuilder gives us the same builder ASP.NET Core uses.
        // AddAIAgent registers our agent under a NAME, the factory receives the
        // service provider and the agent's key (its name) so we can build it.
        Console.WriteLine("Part 1: Register the agent with AddAIAgent");
        Console.WriteLine("------------------------------------------");

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddAIAgent(
            AgentName,
            (_, name) => AgentFactory.CreateAgent(
                name: name,
                instructions: "You are a pirate. Speak like a pirate. Keep answers brief."));

        using IHost host = builder.Build();
        Console.WriteLine($"Registered an agent named '{AgentName}' in the DI container.");

        // Part 2 - Resolve the agent back out of DI by its name.
        // AddAIAgent registers the agent as a KEYED service (keyed by its name), so
        // this is exactly how a controller or minimal-API endpoint would obtain it.
        Console.WriteLine();
        Console.WriteLine("Part 2: Resolve the agent from DI and run it");
        Console.WriteLine("--------------------------------------------");

        AIAgent agent = host.Services.GetRequiredKeyedService<AIAgent>(AgentName);

        Console.WriteLine("You:   Introduce yourself in one sentence.");
        Console.WriteLine($"Agent: {await agent.RunAsync("Introduce yourself in one sentence.")}");
        Console.WriteLine("(The agent came from the DI container - the foundation of hosting.)");
    }

    public string Title => "Day 6 - Host your agent (register in DI)";
}
