using System.Net;
using System.Text;
using SkillForge.Application.Modeling;
using SkillForge.Domain.Modeling;
using SkillForge.Infrastructure.Modeling;

namespace SkillForge.Infrastructure.Tests.Modeling;

/// <summary>
/// The adapter against a handler that answers like an endpoint. No socket is opened, because a test suite that needs a
/// model running is a test suite that gets skipped.
/// </summary>
public sealed class OpenAiCompatibleModelRunnerTests
{
    private const string Endpoint = "http://localhost:11434/v1";

    [Fact]
    public async Task ReadsTheReplyAndTheTokenCounts()
    {
        var runner = Runner(Responds("""
            {
              "choices": [ { "message": { "role": "assistant", "content": "  demo-skill  " } } ],
              "usage": { "prompt_tokens": 412, "completion_tokens": 3 }
            }
            """));

        var completion = await runner.CompleteAsync(new ModelPrompt("system", "user"));

        completion.Text.Should().Be("demo-skill");
        completion.PromptTokens.Should().Be(412);
        completion.CompletionTokens.Should().Be(3);
    }

    [Fact]
    public async Task TreatsMissingUsageAsUnreportedRatherThanFailing()
    {
        // Plenty of local runners omit usage. That is not a reason to refuse the answer.
        var runner = Runner(Responds("""
            { "choices": [ { "message": { "content": "none" } } ] }
            """));

        var completion = await runner.CompleteAsync(new ModelPrompt("system", "user"));

        completion.Text.Should().Be("none");
        completion.PromptTokens.Should().Be(0);
    }

    [Fact]
    public async Task PostsTheModelTheMessagesAndTemperatureZero()
    {
        var handler = Responds("""{ "choices": [ { "message": { "content": "x" } } ] }""");
        var runner = Runner(handler);

        await runner.CompleteAsync(new ModelPrompt("choose a skill", "review my API"));

        handler.LastRequestBody.Should().Contain("\"model\":\"qwen3:8b\"");
        handler.LastRequestBody.Should().Contain("\"temperature\":0");
        handler.LastRequestBody.Should().Contain("choose a skill").And.Contain("review my API");
        handler.LastRequestUri.Should().Be("http://localhost:11434/v1/chat/completions");
    }

    [Fact]
    public async Task SaysWhichEndpointCouldNotBeReached()
    {
        var runner = Runner(Throws(new HttpRequestException("connection refused")));

        var act = async () => await runner.CompleteAsync(new ModelPrompt("s", "u"));

        (await act.Should().ThrowAsync<ModelRunnerException>())
            .Which.Message.Should().Contain(Endpoint).And.Contain("connection refused");
    }

    [Fact]
    public async Task ReportsARefusalWithTheStatusAndTheBody()
    {
        var runner = Runner(Responds("""{ "error": { "message": "model not found" } }""", HttpStatusCode.NotFound));

        var act = async () => await runner.CompleteAsync(new ModelPrompt("s", "u"));

        (await act.Should().ThrowAsync<ModelRunnerException>())
            .Which.Message.Should().Contain("404").And.Contain("model not found");
    }

    [Fact]
    public async Task RefusesToGuessWhenTheAnswerIsNotTheShapeItUnderstands()
    {
        // An endpoint speaking a different dialect must not look like a model that declined to choose a skill.
        var runner = Runner(Responds("""{ "output": "demo-skill" }"""));

        var act = async () => await runner.CompleteAsync(new ModelPrompt("s", "u"));

        (await act.Should().ThrowAsync<ModelRunnerException>())
            .Which.Message.Should().Contain("does not understand");
    }

    [Fact]
    public void ReportsTheModelItWillAsk()
    {
        Runner(Responds("{}")).Identity.Should().Be(new ModelIdentity(Endpoint, "qwen3:8b"));
    }

    private static OpenAiCompatibleModelRunner Runner(FakeHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri(Endpoint + "/") },
            new ModelSettings(Endpoint, "qwen3:8b", null));

    private static FakeHandler Responds(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(json, status, null);

    private static FakeHandler Throws(Exception exception) => new(null, HttpStatusCode.OK, exception);

    private sealed class FakeHandler(string? json, HttpStatusCode status, Exception? throws) : HttpMessageHandler
    {
        internal string? LastRequestBody { get; private set; }

        internal string? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (throws is not null)
            {
                throw throws;
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json ?? "{}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
