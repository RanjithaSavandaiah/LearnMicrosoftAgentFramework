using System.Numerics.Tensors;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 14 - Retrieval Augmented Generation (RAG).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/agents/rag?pivots=programming-language-csharp
///
/// The big idea:
///   An LLM only knows what it was trained on it has never seen your private
///   docs, and it happily makes things up ("hallucinates") when asked about them.
///   RAG fixes this by RETRIEVING the most relevant snippets from YOUR data at
///   query time and stuffing them into the prompt as grounding, so the model
///   answers from facts you supplied instead of guessing.
///
///   The retrieval step uses EMBEDDINGS: each piece of text becomes a vector, and
///   two texts that mean similar things end up close together. So we:
///     1. Embed every document in the knowledge base once (indexing).
///     2. Embed the user's question.
///     3. Find the documents whose vectors are most similar (cosine similarity).
///     4. Feed those documents to the agent as grounding context.
///
///   This lesson shows two ways to wire step 4 into an agent:
///     Part 1 - Build the vector index and inspect raw retrieval (see the scores).
///     Part 2 - Manual RAG: retrieve, then hand the context to RunAsync yourself.
///     Part 3 - Pipeline RAG: the built in TextSearchProvider (+ TextSearchProviderOptions)
///              searches and injects the retrieved snippets automatically before every
///              call - the idiomatic, reusable pattern from the docs.
///     Part 4 - Grounding guardrail: ask something NOT in the knowledge base and
///              watch the agent decline instead of hallucinating.
///
/// NOTE: retrieval uses Google Gemini embeddings (GOOGLE_API_KEY) because the Groq
/// free tier has no embedding model. The chat agent still runs on Groq.
/// </summary>
public sealed class Day14_Rag : ILesson
{
    // A tiny "knowledge base" - private facts the LLM was never trained on. In a
    // real app these would be chunks of your docs, wiki, or database rows.
    private static readonly string[] KnowledgeBase =
    [
        "Contoso's flagship product, the AeroDesk 3000, ships with a 3 year limited warranty covering manufacturing defects.",
        "The AeroDesk 3000 sit stand desk adjusts from 60cm to 125cm and supports a maximum load of 120kg.",
        "Contoso offers free shipping on all orders over $250 within the continental United States.",
        "Returns are accepted within 45 days of delivery, provided the item is in its original packaging.",
        "The AeroDesk 3000 motor has a soft start feature and operates at under 45 decibels while adjusting.",
        "Contoso customer support is available Monday to Friday, 8am to 6pm Pacific Time, at support@contoso.example.",
        "The AeroDesk companion app stores up to four custom height presets per user profile.",
        "Assembly of the AeroDesk 3000 takes about 20 minutes and requires only the included hex key.",
    ];

    public string Title => "Day 14 - Retrieval Augmented Generation (RAG)";

    public async Task RunAsync()
    {
        // Part 1 - Build the vector index.
        // We embed every knowledge base entry ONCE up front. Each entry becomes a
        // vector; we keep the text alongside its vector so we can return it later.
        Console.WriteLine("Part 1: Build the vector index and inspect retrieval");
        Console.WriteLine("----------------------------------------------------");

        IEmbeddingGenerator<string, Embedding<float>> embedder =
            AgentFactory.CreateEmbeddingGenerator();

        Console.WriteLine($"Embedding {KnowledgeBase.Length} knowledge base entries...");

        // Embed each entry individually. (Some embedding endpoints only honour the
        // first item of a batch request, so we generate one vector per document to
        // be safe and keep the text paired with its vector our in memory store.)
        var index = new List<(string Text, ReadOnlyMemory<float> Vector)>();
        foreach (string entry in KnowledgeBase)
        {
            ReadOnlyMemory<float> vector = await embedder.GenerateVectorAsync(entry);
            index.Add((entry, vector));
        }

        // Show retrieval working: for a sample query, rank entries by similarity.
        const string SampleQuery = "How much weight can the desk hold?";
        Console.WriteLine();
        Console.WriteLine($"Query: \"{SampleQuery}\"");
        Console.WriteLine("Top matches by cosine similarity:");

        foreach ((string text, float score) in await SearchAsync(embedder, index, SampleQuery, topK: 3))
        {
            Console.WriteLine($"   [{score:0.000}] {text}");
        }

        Pause();

        // Part 2 - Manual RAG: retrieve, then prompt.
        // The most explicit form: we fetch the relevant snippets ourselves and paste
        // them into the message we send. Nothing hidden you can see exactly what
        // grounding the model receives.
        Console.WriteLine("Part 2: Manual RAG - retrieve, then prompt");
        Console.WriteLine("------------------------------------------");

        AIAgent plainAgent = AgentFactory.CreateAgent(
            name: "SupportBot",
            instructions:
                "You are a Contoso support assistant. Answer ONLY from the provided context. "
              + "If the answer isn't in the context, say you don't have that information.");

        const string Question1 = "What warranty comes with the AeroDesk 3000, and how loud is the motor?";
        string context = string.Join("\n", (await SearchAsync(embedder, index, Question1, topK: 4)).Select(r => $"- {r.Text}"));

        string groundedPrompt =
            $"Context:\n{context}\n\nQuestion: {Question1}";

        Console.WriteLine($"Question: {Question1}");
        Console.WriteLine($"Agent: {await plainAgent.RunAsync(groundedPrompt)}");

        Pause();

        // Part 3 - Pipeline RAG with the built in TextSearchProvider.
        // Pasting context by hand gets repetitive. The framework ships a ready made
        // AIContextProvider for exactly this: TextSearchProvider. You give it a search
        // delegate (our embedding retrieval, returning TextSearchResult items) and
        // TextSearchProviderOptions to control behaviour. With SearchTime =
        // BeforeAIInvoke it automatically searches before EVERY call and injects the
        // results so the agent is "RAG enabled" without the caller doing anything.
        Console.WriteLine("Part 3: Pipeline RAG - the built in TextSearchProvider grounds every call");
        Console.WriteLine("------------------------------------------------------------------------");

        // The search delegate: embed the query, retrieve top matches, map to results.
        async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchDelegate(
            string query, CancellationToken ct)
        {
            List<(string Text, float Score)> hits = await SearchAsync(embedder, index, query, topK: 3);
            Console.WriteLine($"   [textsearch] retrieved {hits.Count} snippet(s) for grounding");
            return hits.Select(h => new TextSearchProvider.TextSearchResult
            {
                Text = h.Text,
                SourceName = "Contoso Knowledge Base",
            });
        }

        var textSearchOptions = new TextSearchProviderOptions
        {
            // Search automatically before each model call (vs. on demand tool calling).
            SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
            ContextPrompt =
                "## Retrieved Contoso knowledge (answer ONLY from this):",
            CitationsPrompt =
                "If you use a snippet, keep your answer grounded in it and do not invent details.",
        };

        var textSearchProvider = new TextSearchProvider(SearchDelegate, textSearchOptions);

        AIAgent ragAgent = AgentFactory.CreateAgent(new ChatClientAgentOptions
        {
            Name = "SupportBot",
            ChatOptions = new ChatOptions
            {
                Instructions =
                    "You are a Contoso support assistant. Answer ONLY from the retrieved context. "
                  + "If the answer isn't in the context, say you don't have that information.",
            },
            AIContextProviders = [textSearchProvider],
        });

        const string Question2 = "Do I get free shipping on a $300 order, and what's the return window?";
        Console.WriteLine($"Question: {Question2}");
        Console.WriteLine($"Agent: {await ragAgent.RunAsync(Question2)}");

        Pause();

        // Part 4 - Grounding guardrail (no hallucination).
        // Ask something the knowledge base simply doesn't cover. Because retrieval
        // returns nothing relevant AND the instructions forbid guessing, the agent
        // declines instead of inventing an answer the whole point of RAG.
        Console.WriteLine("Part 4: Grounding guardrail - declines what it doesn't know");
        Console.WriteLine("-----------------------------------------------------------");

        const string Question3 = "What colour is the CEO of Contoso's car?";
        Console.WriteLine($"Question: {Question3}");
        Console.WriteLine($"Agent: {await ragAgent.RunAsync(Question3)}");

        Console.WriteLine();
        Console.WriteLine("Takeaway: RAG grounds an agent in YOUR data. Embed your documents, retrieve");
        Console.WriteLine("the most relevant ones for each question by vector similarity, and inject");
        Console.WriteLine("them as context ideally with a context provider so it's automatic. The");
        Console.WriteLine("agent answers from facts instead of hallucinating.");
    }

    /// <summary>
    /// Embeds the query and returns the <paramref name="topK"/> most similar entries
    /// from the index, ranked by cosine similarity (1.0 = identical meaning).
    /// </summary>
    private static async Task<List<(string Text, float Score)>> SearchAsync(
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        List<(string Text, ReadOnlyMemory<float> Vector)> index,
        string query,
        int topK)
    {
        ReadOnlyMemory<float> q = await embedder.GenerateVectorAsync(query);

        return index
            .Select(entry => (entry.Text, Score: TensorPrimitives.CosineSimilarity(q.Span, entry.Vector.Span)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter for the next part...");
        Console.ReadLine();
        Console.WriteLine();
    }
}
