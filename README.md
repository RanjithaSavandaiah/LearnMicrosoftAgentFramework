# Learn Microsoft Agent Framework

A day-by-day learning journey for the [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp).
Each "day" adds new concepts so you can see how the agent evolves.

## Setup

The samples use an OpenAI-compatible endpoint (Groq). Set your API key once, then
restart the terminal:

```powershell
setx GROQ_API_KEY "your-key-here"
```

> Day 2 reads the key from this environment variable (the recommended approach).
> Day 1 hard-codes it on purpose to show where we started - never commit real keys.

**Day 10 (vision) also needs a Google Gemini key.** The Groq free tier has no
vision model, so the vision parts run on Gemini (`gemini-2.5-flash`). Set it once:

```powershell
setx GOOGLE_API_KEY "your-gemini-key-here"
```

> If `GOOGLE_API_KEY` is missing, Day 10's vision parts are skipped gracefully.

## Run it

```powershell
dotnet run
```

You'll get a menu to pick a day. You can also run a day directly:

```powershell
dotnet run 2      # runs Day 2
```

## The journey

Each day reflects how the code actually evolved:

| Day | File | What's new | Docs |
| --- | ---- | ---------- | ---- |
| 1 | `Days/Day1_Intro.cs` | Overview + our first `RunAsync` call. Key is **hard-coded** (the naive start). | [Overview](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp) |
| 2 | `Days/Day2_FirstAgent.cs` | Key from an **environment variable**, `RunStreamingAsync`, and **adding a tool** the agent can call. | [Your first agent](https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent?pivots=programming-language-csharp) · [Add tools](https://learn.microsoft.com/en-us/agent-framework/get-started/add-tools?pivots=programming-language-csharp) |
| 3 | `Days/Day3_MultiTurn.cs` | **Multi-turn conversations**: an `AgentSession` so the agent remembers earlier messages, plus an interactive chat loop. | [Multi-turn](https://learn.microsoft.com/en-us/agent-framework/get-started/multi-turn?pivots=programming-language-csharp) |
| 4 | `Days/Day4_MemoryPersistence.cs` | **Memory & persistence**: serialize an `AgentSession` to JSON, save it, then restore it after a simulated restart so the conversation survives. | [Memory](https://learn.microsoft.com/en-us/agent-framework/get-started/memory?pivots=programming-language-csharp) |
| 5 | `Days/Day5_Workflows.cs` | **Workflows**: chain steps (executors) into a pipeline - a `Func`based `Uppercase` step feeding a class based `Reverse` step. Runs offline (no API key). | [Workflows](https://learn.microsoft.com/en-us/agent-framework/get-started/workflows?pivots=programming-language-csharp) |
| 6 | `Days/Day6_Hosting.cs` | **Hosting**: register an agent in the dependency injection container with `AddAIAgent`, then resolve it back by name - the foundation for hosting in ASP.NET Core. | [Hosting](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting?pivots=programming-language-csharp) |
| 7 | `Days/Day7_RunningAgents.cs` | **Running agents**: the many ways to run an `AIAgent` - single string, explicit `ChatMessage` lists, `RunStreamingAsync`, sessions, `ChatClientAgentRunOptions`, and cancellation. | [Agents](https://learn.microsoft.com/en-us/agent-framework/agents/?pivots=programming-language-csharp) · [Running agents](https://learn.microsoft.com/en-us/agent-framework/agents/running-agents?pivots=programming-language-csharp) |
| 8 | `Days/Day8_Harness.cs` | **Agent harness**: a self driving loop with `LoopAgent`. Completion-marker, delegate, and AI-judge evaluators, plus every `LoopAgentOptions`. *(experimental, MAAI001)* | [Harness](https://learn.microsoft.com/en-us/agent-framework/agents/harness?pivots=programming-language-csharp) |
| 9 | `Days/Day9_Pipeline.cs` | **Pipeline architecture**: agent middleware, context providers (date/style/RAG), role-based **tool filtering** with enforcement, **tool discovery**, **dynamic tool adding**, and restricting tool calls (`AllowMultipleToolCalls = false`). | [Pipeline](https://learn.microsoft.com/en-us/agent-framework/agents/pipeline?pivots=programming-language-csharp) |
| 10 | `Days/Day10_StructuredAndVision.cs` | **Structured outputs + vision**: typed responses, JSON-schema `ResponseFormat`, streaming structured output, a **runtime-defined schema**, **text-to-image generation**, and a Gemini-powered receipt auditor. | [Structured outputs](https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs?pivots=programming-language-csharp) |
| 11 | `Days/Day11_BackgroundResponses.cs` | **Background responses**: long-running work with continuation tokens, polling, and resuming after an interruption. *(experimental, MEAI001)* | [Background responses](https://learn.microsoft.com/en-us/agent-framework/agents/background-responses?pivots=programming-language-csharp) |
| 12 | `Days/Day12_AgentAsTool.cs` | **Agent as a tool**: wrap specialist agents with `AsAIFunction()` so a coordinator agent can delegate to them - the basis of multi-agent systems. | [Agent as tool](https://learn.microsoft.com/en-us/agent-framework/agents/agent-as-tool?pivots=programming-language-csharp) |
| 13 | `Days/Day13_McpTools.cs` | **MCP as a tool**: connect to an external Model Context Protocol server, discover its tools with `ListToolsAsync`, and hand them to an agent. *(needs Node.js/npx)* | [MCP](https://learn.microsoft.com/en-us/agent-framework/agents/mcp?pivots=programming-language-csharp) |

> Day 1 hard-codes the key on purpose,
> `gsk_your_api_key_here` in `Day1_Intro.cs` if you want to run it. Day 2 reads
> the key from `GROQ_API_KEY` instead - the recommended approach.

### A note on models and tools

The chat parts use `llama-3.3-70b-versatile`, but that model is unreliable at
**tool calling** (it loops and emits malformed tool calls). So `AgentFactory`
exposes `ToolCapableModel` (`openai/gpt-oss-20b`), and Day 2's tool sample uses
it. Tool calls also run with `RunAsync` rather than streaming, which is more
reliable for function invocation.

### A note on sessions (Day 3)

The Multi turn article calls the conversation object an **`AgentSession`** and
created with `await agent.CreateSessionAsync()`. Creating a session is now asynchronous.

Day 4 builds on this to add **persistence**: `await agent.SerializeSessionAsync(session)`
returns a `JsonElement` you can save anywhere, and
`await agent.DeserializeSessionAsync(json)` restores it later - even in a fresh
agent instance - so a conversation can survive an app restart.

### A note on workflows (Day 5)

Workflows live in a separate package, **`Microsoft.Agents.AI.Workflows`**, which
Day 5 adds. A workflow chains **executors** into a pipeline write a step as a
`Func` and wrap it with `BindAsExecutor("name")`, or as a class deriving from
`Executor<TInput, TOutput>`. Wire steps together with `WorkflowBuilder.AddEdge`,
mark the final output with `WithOutputFrom`, then run it with
`InProcessExecution.RunAsync`. Day 5's sample is pure string processing, so it
needs no API key and runs offline.

### A note on hosting (Day 6)

Hosting lives in **`Microsoft.Agents.AI.Hosting`**, which is currently a
**preview** package (`1.11.1-preview`). Its core idea is dependency injection:
`builder.AddAIAgent(name, factory)` registers an agent by name as a keyed
service, and consumers resolve it with
`services.GetRequiredKeyedService<AIAgent>(name)`. Day 6 shows that loop with a
Generic Host so it fits the console menu.

The article's next step is to **expose** that hosted agent over HTTP (the A2A
protocol) with ASP.NET Core - that needs a web project and a server that runs
forever, so it lives outside this console app. The DI registration shown in Day 6
is exactly what those web endpoints build on.

### A note on vision (Day 10)

Vision uses **Google Gemini** via the `Mscc.GenerativeAI.Microsoft` package
(`AgentFactory.CreateVisionAgent`), because the Groq free tier has no vision
model. The text/structured output parts still run on Groq including a
**runtime schema** part (Part 4) that builds a JSON schema as data and reads the
reply dynamically with `JsonDocument`, for shapes not known at compile time. The
vision parts (5 and 7) read real images (a landmark photo and a receipt) and
return typed data, and need `GOOGLE_API_KEY`. Part 6 flips direction and does
**text-to-image generation** using **Pollinations.ai** - a free, no-API-key
image endpoint - so it works on any machine with internet and no billing. It
`GET`s the image bytes for a prompt and saves `generated-image.jpg` to disk.
(Gemini/Imagen image output was intentionally avoided: its free-tier limit is
literally zero and it requires a billing-enabled key.)

### A note on background responses (Day 11)

Background responses are only truly asynchronous on the **OpenAI / Azure OpenAI
Responses API**. On Groq/Gemini the framework completes immediately, so Day 11
exercises the same API surface (`AllowBackgroundResponses`, `ContinuationToken`,
resume-after-interruption) via an immediate-completion path. Continuation also
requires an `AgentSession`.

### A note on MCP (Day 13)

Day 13 consumes tools from an external **Model Context Protocol** server. It
launches the reference `@modelcontextprotocol/server-everything` server via Node's
`npx`, so it needs [Node.js](https://nodejs.org) on your PATH. The **first run
downloads the server**, which can take a while, so the lesson uses a generous
`InitializationTimeout`. If `npx` isn't found, the lesson explains the fix and
exits gracefully instead of crashing.


## Project layout

- `AgentFactory.cs` - shared setup that builds an `AIAgent` (reads the key, picks the model, accepts tools)
- `ILesson.cs` - tiny interface each day implements so the menu can list and run them
- `Days/` - one file per day; add a new `DayN_*.cs` and register it in `Program.cs`
- `Program.cs` - the menu that ties everything together

## Adding a new day

1. Create `Days/Day14_Something.cs` implementing `ILesson`.
2. Add `new Day14_Something()` to the `lessons` list in `Program.cs`.

