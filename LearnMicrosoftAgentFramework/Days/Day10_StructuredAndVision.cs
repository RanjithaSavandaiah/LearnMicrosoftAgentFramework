using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 10 - Structured outputs + vision (multimodal), fused into one scenario.
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs?pivots=programming-language-csharp
///   https://learn.microsoft.com/en-us/agent-framework/agents/... (using images with an agent)
///
/// The big idea:
///   Free form text is great for humans but terrible for software, you can't
///   reliably parse "the total was about forty bucks" into a decimal. STRUCTURED
///   OUTPUTS make the model return data that deserializes straight into your C#
///   types, schema validated. VISION lets the agent read an IMAGE. Put them
///   together and you get something genuinely useful: an agent that LOOKS at a
///   receipt and hands you a typed, ready to persist expense record.
///
///   This lesson escalates through seven techniques:
///     Part 1 - RunAsync<T>: the compile time typed happy path (text -> object).
///     Part 2 - ResponseFormat with ForJsonSchema<T>: configure the schema on the
///              agent so it applies to every call (still a compile time type).
///     Part 3 - STREAMING structured output: stream updates, assemble with
///              ToAgentResponseAsync(), then deserialize once complete.
///     Part 4 - RUNTIME schema: the shape isn't a C# type at all - build a JSON
///              schema at runtime (ForJsonSchema(JsonElement)) and read the reply
///              dynamically with JsonDocument. Perfect for user/config defined forms.
///     Part 5 - Vision: send an image + prompt so the agent can analyze pixels.
///     Part 6 - TEXT -> IMAGE: send a text prompt and get a GENERATED image back,
///              saved to disk (Gemini image model).
///     Part 7 - vision + structured output together photograph a
///              receipt, get back a strongly typed, validated ExpenseReport, and
///              run a real business rule (policy check) against the parsed data.
///
/// NOTE: Needs GROQ_API_KEY. Structured parts use the tool capable model, vision
/// parts use a multimodal model (see AgentFactory.VisionModel).
/// </summary>
public sealed class Day10_StructuredAndVision : ILesson
{
    public string Title => "Day 10 - Structured outputs + vision (multimodal)";

    // A shared JSON options: camelCase names, case insensitive reads.
    // JsonSerializerDefaults.Web handles both without any extra flags.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunAsync()
    {
        // Part 1 - RunAsync<T>: strongly typed result in one call.
        // When you know the shape at compile time, RunAsync<T> is the cleanest path
        // the framework attaches the JSON schema for T, asks the model to conform,
        // and deserializes the reply into T for you. response.Result is a real object.
        Console.WriteLine("Part 1: RunAsync<T> - text in, typed object out");
        Console.WriteLine("-----------------------------------------------");

        AIAgent extractor = AgentFactory.CreateAgent(
            name: "Extractor",
            instructions: "Extract structured contact details from the user's text.",
            model: AgentFactory.ToolCapableModel);

        AgentResponse<ContactCard> typed = await extractor.RunAsync<ContactCard>(
            "Hi, I'm Dr. Ranjitha Savandaiah, cardiologist at Lakeside Clinic. "
          + "Reach me at ranjitha.s@lakeside.example or +91 9008870915.",
            serializerOptions: JsonOptions);

        ContactCard card = typed.Result;
        Console.WriteLine($"   Name:   {card.FullName}");
        Console.WriteLine($"   Role:   {card.Title}");
        Console.WriteLine($"   Email:  {card.Email}");
        Console.WriteLine($"   Phone:  {card.Phone}");

        Pause();

        // Part 2 - ResponseFormat with an explicit JSON schema.
        // Sometimes you can't use RunAsync<T> e.g. the shape is decided at runtime,
        // or you only want the raw JSON text without deserializing. Here we set
        // ChatOptions.ResponseFormat to a schema built for a type, so EVERY run of
        // this agent returns JSON matching that schema. We then parse it ourselves.
        Console.WriteLine("Part 2: ResponseFormat - configure the JSON schema on the agent");
        Console.WriteLine("---------------------------------------------------------------");

        ChatResponseFormat sentimentSchema = ChatResponseFormat.ForJsonSchema<SupportTriage>(JsonOptions);

        AIAgent triageAgent = AgentFactory.CreateAgent(new ChatClientAgentOptions
        {
            Name = "Triage",
            ChatOptions = new ChatOptions
            {
                Instructions =
                    "You triage inbound support messages. Classify sentiment, urgency (1-5), "
                  + "and the single best team to route to.",
                ResponseFormat = sentimentSchema,
            },
        },
        model: AgentFactory.ToolCapableModel);

        AgentResponse raw = await triageAgent.RunAsync(
            "This is the THIRD time my production database is down and support hasn't called back. "
          + "I'm losing customers by the minute!!");

        // The response is guaranteed shaped JSON text; parse it however we like.
        SupportTriage triage = JsonSerializer.Deserialize<SupportTriage>(raw.ToString(), JsonOptions)!;
        Console.WriteLine($"   Sentiment: {triage.Sentiment}");
        Console.WriteLine($"   Urgency:   {triage.Urgency}/5");
        Console.WriteLine($"   Route to:  {triage.RouteToTeam}");
        Console.WriteLine($"   Reason:    {triage.Justification}");

        Pause();

        // Part 3 - STREAMING structured output.
        // With streaming, the model sends the JSON in fragments (AgentResponseUpdate
        // chunks). You CANNOT deserialize mid stream because the JSON is incomplete.
        // The pattern is:
        //   1. Collect every update while printing raw chunks so the user sees
        //      something happening (the JSON building token by token).
        //   2. Call ToAgentResponseAsync() to assemble all updates into one
        //      AgentResponse whose .Text is the complete, valid JSON string.
        //   3. THEN deserialize once.
        // This matters for long structured responses (reports, lists, deeply nested
        // objects) where waiting for RunAsync<T> blocks the UI for seconds.
        Console.WriteLine("Part 3: Streaming structured output - stream first, deserialize once done");
        Console.WriteLine("------------------------------------------------------------------------");

        // Reuse the same triage agent (ResponseFormat = sentimentSchema is already set).
        // A different angry message so the output is visibly different from Part 2.
        const string StreamPrompt =
            "My entire dev team has been locked out of the repository for two hours "
          + "and our sprint deadline is tomorrow. Nobody is responding to our tickets!";

        Console.WriteLine($"Input: \"{StreamPrompt}\"");
        Console.WriteLine();
        Console.WriteLine("   [streaming] raw JSON fragments as they arrive:");
        Console.Write("   ");

        IAsyncEnumerable<AgentResponseUpdate> updates =
            triageAgent.RunStreamingAsync(StreamPrompt);

        // Stream live so the console animates - each chunk is a fragment of JSON.
        await foreach (AgentResponseUpdate update in updates)
        {
            Console.Write(update);
        }
        Console.WriteLine();
        Console.WriteLine();

        // NOW assemble and deserialize - the key difference from plain streaming.
        // ToAgentResponseAsync re enumerates the same stream OR we call it upfront.
        // Because we already consumed the stream above, we call the agent again and
        // this time hand the IAsyncEnumerable straight to ToAgentResponseAsync.
        AgentResponse assembled = await triageAgent
            .RunStreamingAsync(StreamPrompt)
            .ToAgentResponseAsync();

        SupportTriage streamedTriage =
            JsonSerializer.Deserialize<SupportTriage>(assembled.ToString(), JsonOptions)!;

        Console.WriteLine("   [deserialized after assembly]");
        Console.WriteLine($"   Sentiment: {streamedTriage.Sentiment}");
        Console.WriteLine($"   Urgency:   {streamedTriage.Urgency}/5");
        Console.WriteLine($"   Route to:  {streamedTriage.RouteToTeam}");
        Console.WriteLine($"   Reason:    {streamedTriage.Justification}");

        Pause();

        // Part 4 - RUNTIME schema (the shape is NOT a C# type).
        // Parts 1-3 all used compile time types (ContactCard, SupportTriage). But
        // sometimes you DON'T know the shape at compile time the fields come from a
        // database, a config file, or a form a user designed at runtime. In that case
        // you can't write a class or use RunAsync<T> / ForJsonSchema<T>.
        // Instead you BUILD a JSON schema as data (a JsonElement) and pass it to the
        // NON-generic ForJsonSchema(JsonElement) overload. The model still returns
        // schema valid JSON, but you read it DYNAMICALLY with JsonDocument because
        // there's no type to deserialize into.
        Console.WriteLine("Part 4: Runtime schema - shape decided at runtime, read dynamically");
        Console.WriteLine("-------------------------------------------------------------------");

        // Imagine these field definitions arrived from a config file or a form builder
        // at runtime not hard coded classes.
        (string Name, string Type, string Description)[] runtimeFields =
        [
            ("productName", "string", "The name of the product being reviewed."),
            ("rating", "integer", "A star rating from 1 to 5."),
            ("prosCons", "string", "A short summary of the pros and cons."),
            ("wouldRecommend", "boolean", "Whether the reviewer would recommend it."),
        ];

        // Build a JSON Schema document at runtime from those fields.
        string schemaJson = BuildRuntimeSchema(runtimeFields);
        Console.WriteLine("   [runtime schema built]");
        Console.WriteLine($"   {schemaJson}");
        Console.WriteLine();

        using JsonDocument schemaDoc = JsonDocument.Parse(schemaJson);
        ChatResponseFormat runtimeFormat = ChatResponseFormat.ForJsonSchema(
            schema: schemaDoc.RootElement,
            schemaName: "ProductReview",
            schemaDescription: "A structured product review defined at runtime.");

        AIAgent runtimeAgent = AgentFactory.CreateAgent(new ChatClientAgentOptions
        {
            Name = "RuntimeExtractor",
            ChatOptions = new ChatOptions
            {
                Instructions = "Extract a structured product review from the user's text.",
                ResponseFormat = runtimeFormat,
            },
        },
        model: AgentFactory.ToolCapableModel);

        AgentResponse runtimeRaw = await runtimeAgent.RunAsync(
            "I bought the AcousticPro X2 headphones last week. Sound quality is superb and "
          + "battery lasts for days, but they're a bit heavy on long calls. Solid 4 stars "
          + "I'd definitely recommend them.");

        // No C# type to deserialize into, so read the fields dynamically.
        using JsonDocument resultDoc = JsonDocument.Parse(runtimeRaw.ToString());
        JsonElement root = resultDoc.RootElement;
        Console.WriteLine("   [read dynamically with JsonDocument]");
        foreach ((string name, _, _) in runtimeFields)
        {
            if (root.TryGetProperty(name, out JsonElement value))
            {
                Console.WriteLine($"   {name}: {value}");
            }
        }

        Pause();

        // Part 5 - Vision: let the agent read an image.
        // A ChatMessage can carry mixed content, TextContent (the prompt) plus
        // UriContent (a hosted image). Give it to a multimodal model and it analyzes
        // the pixels. Here we ask for a quick description to prove vision works.
        Console.WriteLine("Part 5: Vision - the agent analyzes an image");
        Console.WriteLine("--------------------------------------------");

        AIAgent visionAgent = AgentFactory.CreateVisionAgent(
            name: "VisionAgent",
            instructions: "You are a helpful assistant that can analyze images. Be concise.");

        const string ImageUrl =
            "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-sample-data-files/master/ComputerVision/Images/landmark.jpg";

        ChatMessage lookMessage = new(ChatRole.User,
        [
            new TextContent("In one sentence, what is in this image?"),
            new UriContent(ImageUrl, "image/jpeg"),
        ]);

        if (await RunVisionAsync(visionAgent, lookMessage) is { } description)
        {
            Console.WriteLine($"Agent: {description}");
        }

        Pause();

        // Part 6 - TEXT -> IMAGE generation.
        // Every part so far took/returned TEXT (or read an image). Here we flip it:
        // give a text PROMPT and get back a freshly GENERATED image. We use
        // Pollinations.ai - a free, no-API-key image endpoint so this works on any
        // machine with internet, no billing required. We just GET the image bytes and
        // save a real file you can open.
        Console.WriteLine("Part 6: Text -> image - generate a picture from a prompt");
        Console.WriteLine("--------------------------------------------------------");

        const string ImagePrompt =
            "A friendly robot reading a book under a tree, warm sunset lighting, flat vector art.";
        Console.WriteLine($"Prompt: \"{ImagePrompt}\"");

        string? savedImagePath = await GenerateImageAsync(ImagePrompt);
        if (savedImagePath is not null)
        {
            Console.WriteLine($"   [saved] {savedImagePath}");
        }

        Pause();

        // Part 7 - THE FINALE: vision + structured output = a receipt auditor.
        Console.WriteLine("Part 7: Receipt auditor - read an image, return a typed expense report");
        Console.WriteLine("---------------------------------------------------------------------");

        AIAgent auditor = AgentFactory.CreateVisionAgent(
            name: "ExpenseAuditor",
            instructions:
                "You are an expense auditor. Read the receipt image and extract the merchant, "
              + "date, currency, line items, and total. Categorize the expense. If a value is "
              + "not legible, use your best estimate and set a low confidence.");

        // A real, publicly hosted sample receipt image.
        const string ReceiptUrl =
            "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-REST-api-samples/master/curl/form-recognizer/rest-api/receipt.png";

        ChatMessage receiptMessage = new(ChatRole.User,
        [
            new TextContent("Extract this receipt into a structured expense report."),
            new UriContent(ReceiptUrl, "image/png"),
        ]);

        ExpenseReport? report = null;
        try
        {
            AgentResponse<ExpenseReport> reportResponse =
                await auditor.RunAsync<ExpenseReport>(receiptMessage, serializerOptions: JsonOptions);
            report = reportResponse.Result;
        }
        catch (Exception ex) when (IsVisionUnsupported(ex))
        {
            WriteVisionUnsupportedNote();
        }

        if (report is null)
        {
            Console.WriteLine();
            Console.WriteLine("Takeaway: structured outputs turn an LLM into a reliable data source, and");
            Console.WriteLine("vision lets it read the real world. Fuse them and an agent can look at a");
            Console.WriteLine("receipt and hand your code typed, policy checkable data no glue parsing.");
            return;
        }

        Console.WriteLine($"   Merchant:  {report.Merchant}");
        Console.WriteLine($"   Date:      {report.Date}");
        Console.WriteLine($"   Category:  {report.Category}");
        Console.WriteLine($"   Total:     {report.Total:0.00} {report.Currency}");
        Console.WriteLine($"   Items:     {report.LineItems.Count}");
        foreach (ExpenseLineItem item in report.LineItems)
        {
            Console.WriteLine($"      - {item.Description}: {item.Amount:0.00} {report.Currency}");
        }
        Console.WriteLine($"   Confidence: {report.Confidence:P0}");

        // The payoff: a business rule that runs against the PARSED, typed data no
        // fragile string parsing, no human eyeballing. This is why structured output
        // matters: the model's answer is now just data your code can reason about.
        Console.WriteLine();
        Console.WriteLine("   --- Policy check ---");
        foreach (string finding in PolicyEngine.Evaluate(report))
        {
            Console.WriteLine($"   {finding}");
        }

        Console.WriteLine();
        Console.WriteLine("Takeaway: structured outputs turn an LLM into a reliable data source, and");
        Console.WriteLine("vision lets it read the real world. Fuse them and an agent can look at a");
        Console.WriteLine("receipt and hand your code typed, policy checkable data no glue parsing.");
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter for the next part...");
        Console.ReadLine();
        Console.WriteLine();
    }

    /// <summary>
    /// Builds a JSON Schema document at runtime from a set of field definitions.
    /// This stands in for fields that come from a database, config, or a form the
    /// user designed a shape that has no compile time C# type. The result is a
    /// JSON string that <see cref="JsonDocument"/> can parse into a JsonElement for
    /// <see cref="ChatResponseFormat.ForJsonSchema(JsonElement, string?, string?)"/>.
    /// </summary>
    private static string BuildRuntimeSchema((string Name, string Type, string Description)[] fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");

            writer.WriteStartObject("properties");
            foreach ((string name, string type, string description) in fields)
            {
                writer.WriteStartObject(name);
                writer.WriteString("type", type);
                writer.WriteString("description", description);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            // Require every field so the model always fills them in.
            writer.WriteStartArray("required");
            foreach ((string name, _, _) in fields)
            {
                writer.WriteStringValue(name);
            }
            writer.WriteEndArray();

            // Strict schemas disallow extra properties.
            writer.WriteBoolean("additionalProperties", false);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // One HttpClient for the whole lesson (creating one per call is an anti pattern).
    private static readonly HttpClient Http = new();

    /// <summary>
    /// Generates an image from a text prompt using Pollinations.ai and saves it to
    /// disk. Pollinations is a free, no-API-key image endpoint: you simply GET
    /// https://image.pollinations.ai/prompt/{prompt} and it streams back the image
    /// bytes. That makes it perfect for a sample it works on any machine with
    /// internet and needs no billing (unlike Gemini/Imagen image output). Returns
    /// the saved file path, or null (with a friendly note) if generation fails.
    /// </summary>
    private static async Task<string?> GenerateImageAsync(string prompt)
    {
        // URL-encode the prompt into the path, and ask for a fixed size + no logo.
        string encodedPrompt = Uri.EscapeDataString(prompt);
        string url =
            $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=768&height=512&nologo=true";

        byte[] bytes;
        try
        {
            // Image generation can take a little while, so allow a generous timeout.
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"   [skipped] Image generation failed (HTTP {(int)response.StatusCode}).");
                return null;
            }

            bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   [skipped] Could not reach the image endpoint: {ex.Message}");
            return null;
        }

        if (bytes.Length == 0)
        {
            Console.WriteLine("   [skipped] The response contained no image data.");
            return null;
        }

        // Pollinations returns JPEG, so save with a .jpg extension.
        string path = Path.Combine(AppContext.BaseDirectory, "generated-image.jpg");
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    /// <summary>
    /// Runs a vision prompt, returning the text or null (with a friendly note) if
    /// the configured endpoint/model can't accept image content. Keeps the lesson
    /// usable even on accounts whose models are text only.
    /// </summary>
    private static async Task<string?> RunVisionAsync(AIAgent agent, ChatMessage message)
    {
        try
        {
            AgentResponse response = await agent.RunAsync(message);
            return response.ToString();
        }
        catch (Exception ex) when (IsVisionUnsupported(ex))
        {
            WriteVisionUnsupportedNote();
            return null;
        }
    }

    /// <summary>
    /// True when the failure is "this model/endpoint doesn't do images" rather than a
    /// real bug so we can explain instead of crash.
    /// </summary>
    private static bool IsVisionUnsupported(Exception ex)
    {
        string m = ex.Message;
        return m.Contains("must be a string", StringComparison.OrdinalIgnoreCase)   // no multimodal content
            || m.Contains("model_not_found", StringComparison.OrdinalIgnoreCase)     // vision model not on account
            || m.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || m.Contains("image", StringComparison.OrdinalIgnoreCase) && m.Contains("support", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteVisionUnsupportedNote()
    {
        Console.WriteLine("   [skipped] The configured endpoint/model can't accept image input.");
        Console.WriteLine($"   Set AgentFactory.VisionModel to a multimodal model your account can use");
        Console.WriteLine("   (e.g. Azure OpenAI gpt-4o, or a Groq Llama-4 vision model), then re run.");
        Console.WriteLine("   The code is exactly what the docs show only the model needs vision support.");
    }
}

// ---------- Structured output types ----------

/// <summary>A compile time known shape for Part 1 (RunAsync<T>).</summary>
internal sealed class ContactCard
{
    public string? FullName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

/// <summary>Triage result for Part 2 (schema via ResponseFormat).</summary>
internal sealed class SupportTriage
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Sentiment Sentiment { get; set; }

    [Description("Urgency from 1 (low) to 5 (critical).")]
    public int Urgency { get; set; }

    public string? RouteToTeam { get; set; }

    public string? Justification { get; set; }
}

internal enum Sentiment
{
    Positive,
    Neutral,
    Negative,
}

/// <summary>The typed receipt extraction for Part 4 (vision + RunAsync<T>).</summary>
internal sealed class ExpenseReport
{
    public string? Merchant { get; set; }
    public string? Date { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal Total { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExpenseCategory Category { get; set; }

    public List<ExpenseLineItem> LineItems { get; set; } = [];

    [Description("Model confidence in the extraction, 0.0 to 1.0.")]
    public double Confidence { get; set; }
}

internal sealed class ExpenseLineItem
{
    public string? Description { get; set; }
    public decimal Amount { get; set; }
}

internal enum ExpenseCategory
{
    Meals,
    Travel,
    Lodging,
    Supplies,
    Software,
    Other,
}

/// <summary>
/// A plain C# rules engine that operates on the PARSED expense report. The point of
/// the whole lesson: once the model's answer is typed data, ordinary business logic
/// (no AI, no string scraping) can audit it deterministically.
/// </summary>
internal static class PolicyEngine
{
    private const decimal MealsCap = 75m;
    private const decimal ReceiptRequiredOver = 25m;

    public static IEnumerable<string> Evaluate(ExpenseReport report)
    {
        if (report.Category == ExpenseCategory.Meals && report.Total > MealsCap)
        {
            yield return $"[FLAG] Meal total {report.Total:0.00} {report.Currency} exceeds the "
                       + $"{MealsCap:0.00} {report.Currency} per meal cap needs manager approval.";
        }

        if (report.Total > ReceiptRequiredOver && string.IsNullOrWhiteSpace(report.Merchant))
        {
            yield return "[FLAG] Amount requires an itemized receipt but no merchant was detected.";
        }

        if (report.Confidence < 0.6)
        {
            yield return $"[REVIEW] Low extraction confidence ({report.Confidence:P0}) route to a "
                       + "human for manual verification.";
        }

        decimal itemsSum = report.LineItems.Sum(i => i.Amount);
        if (report.LineItems.Count > 0 && Math.Abs(itemsSum - report.Total) > 0.02m)
        {
            yield return $"[FLAG] Line items sum to {itemsSum:0.00} but total is {report.Total:0.00} "
                       + " possible mis read or hidden fee.";
        }

        yield return "[OK] Automated audit complete.";
    }
}
