namespace SkillForge.Domain.Modeling;

/// <summary>
/// Which model produced a result, recorded in every report that carries one.
/// </summary>
/// <remarks>
/// A model-derived number without the model beside it is not a measurement, it is a rumour: "fires 7 times out of 10"
/// means something different for a 4B local model and for a frontier one. So this travels with the result rather than
/// being available somewhere else.
/// </remarks>
/// <param name="Endpoint">The endpoint that was asked.</param>
/// <param name="Name">The model identifier that was asked for.</param>
public sealed record ModelIdentity(string Endpoint, string Name);
