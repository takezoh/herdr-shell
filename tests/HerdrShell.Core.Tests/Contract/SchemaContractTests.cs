using System.Text.Json;
using HerdrShell.Core.Protocol;
using HerdrShell.Core.Supervision;
using Xunit;

namespace HerdrShell.Core.Tests.Contract;

/// <summary>
/// Pins the client against the captured herdr API schema
/// (schema/herdr-api-schema-v20.json). Refresh the snapshot with
/// `herdr api schema --json` when bumping the supported protocol.
/// </summary>
public sealed class SchemaContractTests
{
    private static readonly JsonDocument Schema = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "herdr-api-schema-v20.json")));

    [Fact]
    public void SnapshotIsProtocol20()
    {
        Assert.Equal(20, Schema.RootElement.GetProperty("protocol").GetInt32());
    }

    [Fact]
    public void EveryClientMethodExistsInSchema()
    {
        var schemaMethods = Schema.RootElement
            .GetProperty("schemas").GetProperty("request").GetProperty("oneOf")
            .EnumerateArray()
            .Select(v => v.GetProperty("properties").GetProperty("method")
                .GetProperty("const").GetString())
            .ToHashSet();

        string[] used =
        [
            HerdrMethods.Ping,
            HerdrMethods.AgentList,
            HerdrMethods.AgentFocus,
            HerdrMethods.EventsSubscribe,
            HerdrMethods.ClientWindowTitleSet,
            HerdrMethods.ClientWindowTitleClear,
        ];
        foreach (var method in used)
            Assert.Contains(method, schemaMethods);
    }

    [Fact]
    public void EveryFeedSubscriptionExistsInSchema()
    {
        var schemaSubscriptions = Schema.RootElement
            .GetProperty("schemas").GetProperty("request").GetProperty("$defs")
            .GetProperty("Subscription").GetProperty("oneOf")
            .EnumerateArray()
            .Select(v => v.GetProperty("properties").GetProperty("type")
                .GetProperty("const").GetString())
            .ToHashSet();

        foreach (var subscription in HerdrSupervisionFeed.RequiredSubscriptions)
            Assert.Contains(subscription, schemaSubscriptions);
    }

    [Fact]
    public void AgentStatusEnumMatchesSchema()
    {
        var schemaValues = Schema.RootElement
            .GetProperty("schemas").GetProperty("success_response").GetProperty("$defs")
            .GetProperty("AgentStatus").GetProperty("enum")
            .EnumerateArray()
            .Select(v => v.GetString()!)
            .ToHashSet();

        var ourValues = Enum.GetValues<HerdrAgentStatus>()
            .Select(v => JsonSerializer.Serialize(v, HerdrJson.Options).Trim('"'))
            .ToHashSet();

        Assert.Equal(schemaValues, ourValues);
    }

    [Fact]
    public void WindowTitleReasonEnumMatchesSchema()
    {
        var schemaValues = Schema.RootElement
            .GetProperty("schemas").GetProperty("success_response").GetProperty("$defs")
            .GetProperty("ClientWindowTitleReason").GetProperty("enum")
            .EnumerateArray()
            .Select(v => v.GetString()!)
            .ToHashSet();

        var ourValues = Enum.GetValues<ClientWindowTitleReason>()
            .Select(v => JsonSerializer.Serialize(v, HerdrJson.Options).Trim('"'))
            .ToHashSet();

        Assert.Equal(schemaValues, ourValues);
    }

    [Fact]
    public void RequestAndResponseEnvelopesRequireId()
    {
        var request = Schema.RootElement.GetProperty("schemas").GetProperty("request");
        Assert.Contains(
            "id",
            request.GetProperty("required").EnumerateArray().Select(v => v.GetString()));

        var success = Schema.RootElement.GetProperty("schemas").GetProperty("success_response");
        var successRequired = success.GetProperty("required")
            .EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.Contains("id", successRequired);
        Assert.Contains("result", successRequired);
    }
}
