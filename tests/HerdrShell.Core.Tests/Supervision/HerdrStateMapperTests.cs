using HerdrShell.Core.Protocol;
using HerdrShell.Core.Supervision;
using Xunit;

namespace HerdrShell.Core.Tests.Supervision;

public sealed class HerdrStateMapperTests
{
    [Theory]
    [InlineData(HerdrAgentStatus.Working, SessionPhase.Running, false)]
    [InlineData(HerdrAgentStatus.Blocked, SessionPhase.Waiting, true)]
    [InlineData(HerdrAgentStatus.Idle, SessionPhase.Waiting, false)]
    [InlineData(HerdrAgentStatus.Done, SessionPhase.Done, false)]
    public void Map_TrackedStatuses(
        HerdrAgentStatus status, SessionPhase phase, bool humanInput)
    {
        var mapped = HerdrStateMapper.Map(status);

        Assert.NotNull(mapped);
        Assert.Equal(phase, mapped.Value.Phase);
        Assert.Equal(humanInput, mapped.Value.HumanInput);
    }

    [Fact]
    public void Map_UnknownIsUntracked()
    {
        Assert.Null(HerdrStateMapper.Map(HerdrAgentStatus.Unknown));
    }

    [Fact]
    public void Map_EveryStatusValueIsDecided()
    {
        // Guards against a new herdr status silently falling into the
        // untracked bucket: extend the mapper (and this list) deliberately.
        var decided = new[]
        {
            HerdrAgentStatus.Idle,
            HerdrAgentStatus.Working,
            HerdrAgentStatus.Blocked,
            HerdrAgentStatus.Done,
            HerdrAgentStatus.Unknown,
        };
        Assert.Equal(
            Enum.GetValues<HerdrAgentStatus>().OrderBy(v => v),
            decided.OrderBy(v => v));
    }

    [Fact]
    public void Map_OnlyBlockedRaisesAttention()
    {
        var attention = Enum.GetValues<HerdrAgentStatus>()
            .Select(HerdrStateMapper.Map)
            .Where(m => m is { HumanInput: true })
            .ToList();

        Assert.Single(attention);
    }
}
