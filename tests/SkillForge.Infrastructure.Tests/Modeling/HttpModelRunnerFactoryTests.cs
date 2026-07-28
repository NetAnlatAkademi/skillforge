using SkillForge.Application.Abstractions;
using SkillForge.Application.Modeling;
using SkillForge.Domain.Modeling;
using SkillForge.Infrastructure.Modeling;

namespace SkillForge.Infrastructure.Tests.Modeling;

/// <summary>
/// The factory is where the API key is read, so it is where the promise about keys has to be tested.
/// </summary>
public sealed class HttpModelRunnerFactoryTests
{
    private const string Secret = "sk-live-do-not-print-me";

    [Fact]
    public void CreatesARunnerForAnEndpointThatNeedsNoKey()
    {
        // The ordinary local case: Ollama on localhost, no key anywhere.
        using var factory = new HttpModelRunnerFactory(new StubEnvironment());

        var runner = factory.Create(new ModelSettings("http://localhost:11434/v1", "qwen3:8b", null));

        runner.Identity.Name.Should().Be("qwen3:8b");
    }

    [Fact]
    public void FailsBeforeSendingAnythingWhenTheNamedVariableIsNotSet()
    {
        // Surfacing the endpoint's 401 instead would tell the user their endpoint rejected them, when the truth is
        // their shell has no key in it.
        using var factory = new HttpModelRunnerFactory(new StubEnvironment());

        var act = () => factory.Create(
            new ModelSettings("https://api.openai.com/v1", "gpt-5", "OPENAI_API_KEY"));

        act.Should().Throw<ModelRunnerException>()
            .WithMessage("*OPENAI_API_KEY*is not set*");
    }

    [Fact]
    public void TheKeyReachesTheRequestHeaderAndNothingElse()
    {
        using var factory = new HttpModelRunnerFactory(new StubEnvironment(("OPENAI_API_KEY", Secret)));

        var runner = factory.Create(
            new ModelSettings("https://api.openai.com/v1", "gpt-5", "OPENAI_API_KEY"));

        // Everything the report and the model layer can see, checked for the secret. The settings themselves only
        // carry the variable's name, and ModelIdentity has no field that could hold a key.
        runner.Identity.ToString().Should().NotContain(Secret);
        runner.Identity.Endpoint.Should().Be("https://api.openai.com/v1");
    }

    [Fact]
    public void KeepsTheEndpointsPathWhenAppendingTheCompletionsRoute()
    {
        // A missing trailing slash silently drops the /v1, and the endpoint answers 404 for a reason nobody can see.
        using var factory = new HttpModelRunnerFactory(new StubEnvironment());

        factory.Create(new ModelSettings("http://localhost:1234/v1", "local", null))
            .Identity.Endpoint.Should().Be("http://localhost:1234/v1");
    }

    private sealed class StubEnvironment(params (string Name, string Value)[] variables) : IUserEnvironment
    {
        public string HomeDirectory => "/home/stub";

        public string? GetEnvironmentVariable(string name) =>
            variables.FirstOrDefault(variable => variable.Name == name).Value;
    }
}
