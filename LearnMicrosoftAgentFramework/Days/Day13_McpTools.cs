using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 13 - MCP as a tool (Model Context Protocol).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/agents/mcp?pivots=programming-language-csharp
///   https://modelcontextprotocol.io/
///
/// The big idea:
///   Instead of hand writing every tool in C#, MCP lets an agent consume tools
///   published by an EXTERNAL server over a standard protocol. Countless MCP servers
///   already exist (filesystem, GitHub, databases, web search...). You connect a
///   client, DISCOVER the server's tools, and hand them to the agent which then
///   calls them exactly like local AIFunctions, because McpClientTool derives from
///   AIFunction.
///
///   This lesson connects to the reference "everything" MCP server (which ships a
///   handful of demo tools like 'get sum' and 'echo'), lists its tools, and lets
///   the agent use them.
///
/// PREREQUISITE: this spins up the server with Node's npx, so you need Node.js
/// installed (https://nodejs.org). If npx isn't found, the lesson explains the fix
/// and exits gracefully rather than crashing.
/// </summary>
public sealed class Day13_McpTools : ILesson
{
    public string Title => "Day 13 - MCP as a tool (external tool server)";

    public async Task RunAsync()
    {
        // Part 1 - Connect to an MCP server over stdio.
        // StdioClientTransport launches the server process and talks to it over
        // stdin/stdout. Here we use npx to fetch and run the reference server.
        Console.WriteLine("Part 1: Connect to an MCP server");
        Console.WriteLine("--------------------------------");

        StdioClientTransport transport = new(new StdioClientTransportOptions
        {
            Name = "Everything",
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"],
        });

        McpClient client;
        try
        {
            Console.WriteLine("Starting MCP server via npx (first run downloads it)...");

            // Generous init timeout: the very first run downloads the server via npx,
            // which can easily exceed the default connect budget.
            McpClientOptions clientOptions = new()
            {
                InitializationTimeout = TimeSpan.FromMinutes(3),
            };

            client = await McpClient.CreateAsync(transport, clientOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Could not start the MCP server. This lesson needs Node.js (npx) on PATH.");
            Console.WriteLine("Install it from https://nodejs.org and re run. Details:");
            Console.WriteLine($"   {ex.Message}");
            return;
        }

        await using (client)
        {
            Console.WriteLine($"Connected to '{client.ServerInfo?.Name}' ({client.ServerInfo?.Version}).");

            Pause();

            // Part 2 - Tool discovery over MCP.
            // ListToolsAsync asks the server what it can do. Each McpClientTool is an
            // AIFunction with a Name + Description the same metadata a model uses.
            Console.WriteLine("Part 2: Discover the server's tools");
            Console.WriteLine("-----------------------------------");

            IList<McpClientTool> mcpTools = await client.ListToolsAsync();
            Console.WriteLine($"Discovered {mcpTools.Count} MCP tool(s):");
            foreach (McpClientTool tool in mcpTools)
            {
                Console.WriteLine($"   - {tool.Name}: {tool.Description}");
            }

            Pause();

            // Part 3 - Give the MCP tools to an agent and let it use them.
            // Because McpClientTool IS an AITool, we pass them straight in. The agent
            // calls them like any local function the framework routes the call over
            // MCP to the server and back.
            Console.WriteLine("Part 3: An agent that uses MCP tools");
            Console.WriteLine("------------------------------------");

            AIAgent agent = AgentFactory.CreateAgent(
                name: "McpAssistant",
                instructions:
                    "You are an assistant with access to external MCP tools. Use them to answer "
                  + "the user's request, then report the result plainly.",
                tools: [.. mcpTools.Cast<AITool>()],
                model: AgentFactory.ToolCapableModel);

            Console.WriteLine("User: Add 41 and 1 using the sum tool.");
            Console.WriteLine($"Agent: {await agent.RunAsync("Add 41 and 1 using the sum tool.")}");

            Console.WriteLine();
            Console.WriteLine("Takeaway: MCP lets an agent consume tools from any external server over a");
            Console.WriteLine("standard protocol. Discover them with ListToolsAsync and hand them to the");
            Console.WriteLine("agent as ordinary tools no bespoke integration code per server.");
        }
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter for the next part...");
        Console.ReadLine();
        Console.WriteLine();
    }
}
