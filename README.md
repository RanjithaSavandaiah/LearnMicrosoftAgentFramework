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

> Day 1 hard-codes the key on purpose, to show where we started. Replace
> `gsk_your_api_key_here` in `Day1_Intro.cs` if you want to run it. Day 2 reads
> the key from `GROQ_API_KEY` instead - the recommended approach.

### A note on models and tools

The chat parts use `llama-3.3-70b-versatile`, but that model is unreliable at
**tool calling** (it loops and emits malformed tool calls). So `AgentFactory`
exposes `ToolCapableModel` (`openai/gpt-oss-20b`), and Day 2's tool sample uses
it. Tool calls also run with `RunAsync` rather than streaming, which is more
reliable for function invocation.



## Project layout

- `AgentFactory.cs` - shared setup that builds an `AIAgent` (reads the key, picks the model, accepts tools)
- `ILesson.cs` - tiny interface each day implements so the menu can list and run them
- `Days/` - one file per day; add a new `DayN_*.cs` and register it in `Program.cs`
- `Program.cs` - the menu that ties everything together

## Adding a new day

1. Create `Days/Day3_Something.cs` implementing `ILesson`.
2. Add `new Day3_Something()` to the `lessons` list in `Program.cs`.

