using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 15 - Declarative agents (define an agent as DATA, not code).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/agents/declarative?pivots=programming-language-csharp
///
/// The big idea:
///   Every day so far NEWed up agents in C#: name, instructions, model and tools
///   all hard coded. The DECLARATIVE approach flips that you describe the agent
///   in a configuration FILE (YAML/JSON), and build the live agent from that
///   definition at runtime. The prompt, model, and which tools to wire up become
///   DATA you can edit, review, and ship without recompiling. Product teams tweak
///   YAML engineers keep the tool implementations in code.
///
///   This lesson shows the full loop:
///     Part 1 - Load a YAML agent definition from disk and inspect it.
///     Part 2 - Build a live AIAgent from that definition (binding named tools to
///              real C# implementations) and run it.
///     Part 3 - Change behaviour with NO code change edit the definition in
///              memory (as if someone edited the file) and rebuild the agent.
///
/// NOTE: the framework's first party declarative loaders target Azure AI Foundry /
/// hosted agents. To keep this lesson runnable on the Groq/Gemini setup used across
/// the journey, we parse the same style of definition ourselves with YamlDotNet and
/// bind it through AgentFactory the concept is identical: config in, agent out.
/// </summary>
public sealed class Day15_DeclarativeAgents : ILesson
{
    public string Title => "Day 15 - Declarative agents (config-driven)";

    // A registry mapping tool NAMES (from the definition) to real C# implementations.
    // The definition only references tools by name; code owns what they actually do.
    private static readonly Dictionary<string, AITool> ToolRegistry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["get_weather"] = AIFunctionFactory.Create(GetWeather, "get_weather"),
    };

    public async Task RunAsync()
    {
        // Part 1 - Load the declarative definition from a file.
        // The YAML lives next to the app (copied to the output folder). We read it
        // and deserialize into a plain AgentDefinition object pure data so far.
        Console.WriteLine("Part 1: Load a declarative agent definition (YAML)");
        Console.WriteLine("--------------------------------------------------");

        string yamlPath = Path.Combine(AppContext.BaseDirectory, "Agents", "weatherbot.yaml");
        if (!File.Exists(yamlPath))
        {
            Console.WriteLine($"   [error] Definition not found at {yamlPath}");
            return;
        }

        string yaml = await File.ReadAllTextAsync(yamlPath);
        AgentDefinition definition = ParseDefinition(yaml);

        Console.WriteLine($"   Name:         {definition.Name}");
        Console.WriteLine($"   Description:  {definition.Description}");
        Console.WriteLine($"   Model:        {definition.Model}");
        Console.WriteLine($"   Tools:        {string.Join(", ", definition.Tools.Select(t => t.Name))}");
        Console.WriteLine($"   Instructions: {definition.Instructions.Trim().Split('\n')[0]} ...");

        Pause();

        // Part 2 - Build a live agent from the definition and run it.
        // BuildAgent turns the DATA into a real AIAgent: it resolves each named tool
        // against the registry and hands the whole thing to AgentFactory.
        Console.WriteLine("Part 2: Build a live agent from the definition and run it");
        Console.WriteLine("---------------------------------------------------------");

        AIAgent agent = BuildAgent(definition);

        Console.WriteLine("User: What's the weather in Paris?");
        Console.WriteLine($"Agent: {await agent.RunAsync("What's the weather in Paris?")}");

        Pause();

        // Part 3 - Change behaviour WITHOUT touching code.
        // Imagine a product owner edits the YAML to re brand the bot and change its
        // style. We simulate that by editing the definition in memory and rebuilding.
        // Same code, same tools completely different persona, driven by config.
        Console.WriteLine("Part 3: Re-configure via the definition - no code change");
        Console.WriteLine("--------------------------------------------------------");

        definition.Name = "Captain Cirrus";
        definition.Instructions =
            "You are Captain Cirrus, a dramatic pirate weather caster. Always use the "
          + "get_weather tool, then report the forecast like a swashbuckling sea captain. "
          + "One or two sentences, matey.";

        AIAgent repersonaAgent = BuildAgent(definition);

        Console.WriteLine("User: What's the weather in Paris?");
        Console.WriteLine($"Agent: {await repersonaAgent.RunAsync("What's the weather in Paris?")}");

        Console.WriteLine();
        Console.WriteLine("Takeaway: declarative agents move the prompt, model and tool wiring OUT of");
        Console.WriteLine("code and into a definition file. Non engineers can tune behaviour, changes");
        Console.WriteLine("are reviewable as data, and the same binaries serve many agent personas.");
    }

    /// <summary>Parses a YAML declarative definition into a plain data object.</summary>
    private static AgentDefinition ParseDefinition(string yaml)
    {
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<AgentDefinition>(yaml);
    }

    /// <summary>
    /// Turns a declarative <see cref="AgentDefinition"/> into a live <see cref="AIAgent"/>.
    /// Named tools in the definition are resolved to real implementations from the
    /// registry unknown tool names are skipped with a warning.
    /// </summary>
    private static AIAgent BuildAgent(AgentDefinition definition)
    {
        List<AITool> tools = [];
        foreach (ToolDefinition toolDef in definition.Tools)
        {
            if (ToolRegistry.TryGetValue(toolDef.Name, out AITool? tool))
            {
                tools.Add(tool);
            }
            else
            {
                Console.WriteLine($"   [warn] definition references unknown tool '{toolDef.Name}' - skipped");
            }
        }

        return AgentFactory.CreateAgent(
            name: definition.Name,
            instructions: definition.Instructions,
            tools: tools.Count > 0 ? tools : null,
            model: string.IsNullOrWhiteSpace(definition.Model) ? null : definition.Model);
    }

    [Description("Gets the current weather for a city.")]
    private static string GetWeather(
        [Description("The city name, e.g. Paris.")] string city)
    {
        Console.WriteLine($"   [tool get_weather called for '{city}']");
        return $"{city}: 18C, partly cloudy with a light breeze.";
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter for the next part...");
        Console.ReadLine();
        Console.WriteLine();
    }

    // ---- Plain data shapes that mirror the YAML definition ----

    private sealed class AgentDefinition
    {
        public string Name { get; set; } = "Assistant";
        public string Description { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<ToolDefinition> Tools { get; set; } = [];
    }

    private sealed class ToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
