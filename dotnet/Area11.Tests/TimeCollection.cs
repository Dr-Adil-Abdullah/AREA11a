using Xunit;

namespace Area11.Tests;

/// <summary>
/// Clock-changing tests must not overlap ANY other collection: ordinary posting/drawer tests
/// also read the process-wide TimeSource. A shared name alone serializes only its own members.
/// Other collections retain parallel execution with one another; production clocks are unchanged.
/// </summary>
[CollectionDefinition("Time", DisableParallelization = true)]
public sealed class TimeCollection
{
}
