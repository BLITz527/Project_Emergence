using System.Collections.ObjectModel;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Results;
using Emergence.Foundation.Time;
using Emergence.Model;
using Emergence.Model.Environment;

namespace Emergence.Simulation;

public sealed class SimulationExecutionContext
{
    private readonly ReadOnlyCollection<AcceptedSessionCommand> _dueCommands;

    internal SimulationExecutionContext(
        WorldSessionDefinition definition,
        SimulationTick tick,
        SimulationPhase phase,
        IEnumerable<AcceptedSessionCommand> dueCommands,
        WorldEnvironmentState? environmentState = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        ArgumentNullException.ThrowIfNull(dueCommands);
        AcceptedSessionCommand[] commands = dueCommands.ToArray();
        if (commands.Any(static item => item is null)) throw new ArgumentException("Due commands cannot contain null.", nameof(dueCommands));
        Tick = tick;
        Phase = phase;
        _dueCommands = Array.AsReadOnly(commands);
        if (definition.HasEnvironment != (environmentState is not null))
            throw new ArgumentException("Execution environment must match the session definition.", nameof(environmentState));
        EnvironmentState = environmentState?.Capture();
        Rng = new DeterministicAddressedRng(definition.RootSeed, definition.SelectedRuleset.RngDomains);
    }

    public WorldSessionDefinition Definition { get; }
    public SimulationTick Tick { get; }
    public SimulationPhase Phase { get; }
    public IReadOnlyList<AcceptedSessionCommand> DueCommands => _dueCommands;
    public DeterministicAddressedRng Rng { get; }
    public WorldEnvironmentState? EnvironmentState { get; }
}

public sealed class SimulationSystemOutput
{
    private readonly ReadOnlyCollection<WorldEventProposal> _eventProposals;

    public SimulationSystemOutput(IEnumerable<WorldEventProposal> eventProposals)
    {
        ArgumentNullException.ThrowIfNull(eventProposals);
        WorldEventProposal[] proposals = eventProposals.ToArray();
        if (proposals.Length > SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick) throw new ArgumentException($"A system cannot exceed {SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick} event proposals per tick.", nameof(eventProposals));
        if (proposals.Any(static item => item is null)) throw new ArgumentException("Event proposals cannot contain null.", nameof(eventProposals));
        _eventProposals = Array.AsReadOnly(proposals);
    }

    public IReadOnlyList<WorldEventProposal> EventProposals => _eventProposals;
    public static SimulationSystemOutput Empty { get; } = new([]);
}

/// <summary>
/// Stateless simulation behavior that may only inspect the supplied context and propose output.
/// Implementations must not retain or mutate an authoritative <see cref="WorldSession"/>.
/// </summary>
public interface ISimulationSystem
{
    SimulationSystemDescriptor Descriptor { get; }
    OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context);
}

public sealed class CommandProcessorOutput
{
    private readonly ReadOnlyCollection<WorldEventProposal> _eventProposals;

    public CommandProcessorOutput(IEnumerable<WorldEventProposal> eventProposals)
    {
        ArgumentNullException.ThrowIfNull(eventProposals);
        WorldEventProposal[] proposals = eventProposals.ToArray();
        if (proposals.Length > SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick) throw new ArgumentException($"A command processor cannot exceed {SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick} event proposals per command.", nameof(eventProposals));
        if (proposals.Any(static item => item is null)) throw new ArgumentException("Command proposals cannot contain null.", nameof(eventProposals));
        _eventProposals = Array.AsReadOnly(proposals);
    }

    public IReadOnlyList<WorldEventProposal> EventProposals => _eventProposals;
    public static CommandProcessorOutput Empty { get; } = new([]);
}

/// <summary>
/// Stateless command behavior that may only inspect the supplied inputs and propose output.
/// Implementations must not retain or mutate an authoritative <see cref="WorldSession"/>.
/// </summary>
public interface ISessionCommandProcessor
{
    SessionCommandTypeId CommandType { get; }
    OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command);
}

public sealed class CommandProcessorRegistry
{
    private readonly ReadOnlyCollection<ISessionCommandProcessor> _processors;
    private readonly Dictionary<SessionCommandTypeId, ISessionCommandProcessor> _byType;

    public CommandProcessorRegistry(IEnumerable<ISessionCommandProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);
        ISessionCommandProcessor?[] source = processors.Cast<ISessionCommandProcessor?>().ToArray();
        if (source.Length > SessionTechnicalLimits.MaxCommandProcessors) throw new ArgumentException($"A command registry cannot exceed {SessionTechnicalLimits.MaxCommandProcessors} processors.", nameof(processors));
        if (source.Any(static item => item is null)) throw new ArgumentException("Command processors cannot contain null.", nameof(processors));
        ISessionCommandProcessor[] sorted = source.Select(static item => item!).OrderBy(static item => item.CommandType).ToArray();
        if (sorted.Any(static item => !item.CommandType.IsValid)) throw new ArgumentException("Command processors must expose valid type IDs.", nameof(processors));
        if (sorted.Select(static item => item.CommandType).Distinct().Count() != sorted.Length) throw new ArgumentException("Duplicate command processor types are not allowed.", nameof(processors));
        _processors = Array.AsReadOnly(sorted);
        _byType = sorted.ToDictionary(static item => item.CommandType);
        Catalog = new CommandProcessorCatalog(sorted.Select(static item => item.CommandType));
    }

    public IReadOnlyList<ISessionCommandProcessor> Processors => _processors;
    public CommandProcessorCatalog Catalog { get; }
    public bool Contains(SessionCommandTypeId type) => type.IsValid && _byType.ContainsKey(type);
    public bool TryGet(SessionCommandTypeId type, out ISessionCommandProcessor? processor) => _byType.TryGetValue(type, out processor);
}
