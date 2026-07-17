using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Versioning;

namespace Emergence.Model;

public static class SessionTechnicalLimits
{
    /// <summary>Maximum systems in one deterministic scheduler graph.</summary>
    public const int MaxSystems = 256;
    /// <summary>Maximum same-phase dependencies declared by one system.</summary>
    public const int MaxDependenciesPerSystem = 64;
    /// <summary>Maximum commands queued by one session; this is not a biological capacity.</summary>
    public const int MaxPendingCommands = 4096;
    /// <summary>Maximum due commands consumed in one tick.</summary>
    public const int MaxCommandsPerTick = 1024;
    /// <summary>Maximum event proposals emitted by one system execution in one tick.</summary>
    public const int MaxEventProposalsPerSystemPerTick = 4096;
    /// <summary>Maximum events committed atomically in one tick.</summary>
    public const int MaxCommittedEventsPerTick = 16384;
    /// <summary>Maximum structured issues retained for one session fault.</summary>
    public const int MaxFaultIssues = 128;
    /// <summary>Maximum informational and warning issues preserved in one successful tick receipt.</summary>
    public const int MaxReceiptIssuesPerTick = 128;
    /// <summary>Maximum explicitly compiled command processors in one immutable registry.</summary>
    public const int MaxCommandProcessors = 256;
}

[JsonConverter(typeof(SimulationSystemIdJsonConverter))]
public readonly record struct SimulationSystemId : IComparable<SimulationSystemId>
{
    public SimulationSystemId(string value) { SessionDottedName.Validate(value, nameof(value)); Value = value; }
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);
    public static SimulationSystemId Parse(string text) => new(text);
    public static bool TryParse(string? text, out SimulationSystemId value) => SessionDottedName.TryParse(text, static x => new SimulationSystemId(x), out value);
    public int CompareTo(SimulationSystemId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(SessionCommandTypeIdJsonConverter))]
public readonly record struct SessionCommandTypeId : IComparable<SessionCommandTypeId>
{
    public SessionCommandTypeId(string value) { SessionDottedName.Validate(value, nameof(value)); Value = value; }
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);
    public static SessionCommandTypeId Parse(string text) => new(text);
    public static bool TryParse(string? text, out SessionCommandTypeId value) => SessionDottedName.TryParse(text, static x => new SessionCommandTypeId(x), out value);
    public int CompareTo(SessionCommandTypeId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(WorldEventTypeIdJsonConverter))]
public readonly record struct WorldEventTypeId : IComparable<WorldEventTypeId>
{
    public WorldEventTypeId(string value) { SessionDottedName.Validate(value, nameof(value)); Value = value; }
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);
    public static WorldEventTypeId Parse(string text) => new(text);
    public static bool TryParse(string? text, out WorldEventTypeId value) => SessionDottedName.TryParse(text, static x => new WorldEventTypeId(x), out value);
    public int CompareTo(WorldEventTypeId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(SimulationPhaseJsonConverter))]
public enum SimulationPhase
{
    Commands = 0,
    Prepare = 1,
    Evaluate = 2,
    Resolve = 3,
    Commit = 4,
    Finalize = 5,
}

[JsonConverter(typeof(WorldSessionStatusJsonConverter))]
public enum WorldSessionStatus
{
    Paused = 0,
    Ready = 1,
    Faulted = 2,
}

internal static class SessionDottedName
{
    public static void Validate(string? value, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 96) throw new ArgumentException("Value length must be 1 through 96.", parameterName);
        foreach (string segment in value.Split('.'))
        {
            if (segment.Length is 0 or > 32 || segment[0] is < 'a' or > 'z'
                || segment.Skip(1).Any(static c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            {
                throw new ArgumentException("Value must contain canonical lowercase ASCII dotted segments.", parameterName);
            }
        }
    }

    public static bool TryParse<T>(string? text, Func<string, T> factory, out T value)
    {
        try { value = factory(text!); return true; }
        catch (ArgumentException) { value = default!; return false; }
    }
}

internal abstract class ExactStringJsonConverter<T> : JsonConverter<T>
{
    protected abstract T ParseExact(string value);
    protected abstract bool IsValid(T value);
    protected abstract string Format(T value);

    public sealed override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException($"{typeof(T).Name} must be an exact canonical JSON string.");
        try { return ParseExact(reader.GetString()!); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new JsonException($"Malformed {typeof(T).Name}.", exception);
        }
    }

    public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!IsValid(value)) throw new JsonException($"Invalid {typeof(T).Name} values cannot be written.");
        writer.WriteStringValue(Format(value));
    }
}

internal sealed class SimulationSystemIdJsonConverter : ExactStringJsonConverter<SimulationSystemId>
{
    protected override SimulationSystemId ParseExact(string value) => new(value);
    protected override bool IsValid(SimulationSystemId value) => value.IsValid;
    protected override string Format(SimulationSystemId value) => value.ToString();
}

internal sealed class SessionCommandTypeIdJsonConverter : ExactStringJsonConverter<SessionCommandTypeId>
{
    protected override SessionCommandTypeId ParseExact(string value) => new(value);
    protected override bool IsValid(SessionCommandTypeId value) => value.IsValid;
    protected override string Format(SessionCommandTypeId value) => value.ToString();
}

internal sealed class WorldEventTypeIdJsonConverter : ExactStringJsonConverter<WorldEventTypeId>
{
    protected override WorldEventTypeId ParseExact(string value) => new(value);
    protected override bool IsValid(WorldEventTypeId value) => value.IsValid;
    protected override string Format(WorldEventTypeId value) => value.ToString();
}

internal sealed class SimulationPhaseJsonConverter : ExactStringJsonConverter<SimulationPhase>
{
    protected override SimulationPhase ParseExact(string value) => value switch
    {
        "Commands" => SimulationPhase.Commands,
        "Prepare" => SimulationPhase.Prepare,
        "Evaluate" => SimulationPhase.Evaluate,
        "Resolve" => SimulationPhase.Resolve,
        "Commit" => SimulationPhase.Commit,
        "Finalize" => SimulationPhase.Finalize,
        _ => throw new FormatException("Unknown simulation phase."),
    };
    protected override bool IsValid(SimulationPhase value) => Enum.IsDefined(value);
    protected override string Format(SimulationPhase value) => value.ToString();
}

internal sealed class WorldSessionStatusJsonConverter : ExactStringJsonConverter<WorldSessionStatus>
{
    protected override WorldSessionStatus ParseExact(string value) => value switch
    {
        "Paused" => WorldSessionStatus.Paused,
        "Ready" => WorldSessionStatus.Ready,
        "Faulted" => WorldSessionStatus.Faulted,
        _ => throw new FormatException("Unknown world-session status."),
    };
    protected override bool IsValid(WorldSessionStatus value) => Enum.IsDefined(value);
    protected override string Format(WorldSessionStatus value) => value.ToString();
}
