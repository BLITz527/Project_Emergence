using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Emergence.ReviewPack;

namespace Emergence.ReviewPack.Tests;

public sealed class Phase04EvidenceTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void ExactPhase04FixturePassesIndependentValidation()
    {
        using Fixture fixture = new();
        fixture.WriteValid();

        SessionEvidence result = Phase04EvidenceValidator.Evaluate(fixture.Root, Commit);

        Assert.True(result.Status == EvidenceStatus.Passed, result.Detail);
        Assert.Equal(Phase04EvidenceValidator.EventIds, result.EventIds);
        Assert.True(result.PresentationSnapshotValid);
    }

    [Theory]
    [InlineData("success", "false")]
    [InlineData("phase", "M0 Phase 0.4")]
    [InlineData("phase", "M0 Phase 0.3")]
    [InlineData("version", "0.3.0-dev")]
    [InlineData("gitCommit", "ffffffffffffffffffffffffffffffffffffffff")]
    public void FalseOrStaleSessionIdentityIsRejected(string property, string replacement)
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject json = fixture.Session();
        json[property] = property == "success" ? bool.Parse(replacement) : replacement;
        fixture.WriteSession(json);

        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Theory]
    [InlineData("algorithmCatalogDigest")]
    [InlineData("schedulerGraphDigest")]
    [InlineData("sessionDefinitionDigest")]
    [InlineData("sessionTraceDigest")]
    [InlineData("finalStateDigest")]
    public void AnyDigestMismatchIsRejected(string property)
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject json = fixture.Session(); json[property] = new string('0', 64); fixture.WriteSession(json);

        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Theory]
    [InlineData("finalTick", "3")]
    [InlineData("acceptedCommands", "5")]
    [InlineData("committedEvents", "9")]
    public void AnyFinalCounterMismatchIsRejected(string property, string replacement)
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject json = fixture.Session();
        json[property] = property == "finalTick" ? replacement : int.Parse(replacement, System.Globalization.CultureInfo.InvariantCulture);
        fixture.WriteSession(json);

        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Fact]
    public void MissingEventIdIsRejected()
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject json = fixture.Session(); json["eventIds"]!.AsArray().RemoveAt(9); fixture.WriteSession(json);
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Fact]
    public void ReorderedEventIdsAreRejected()
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject json = fixture.Session(); JsonArray ids = json["eventIds"]!.AsArray();
        string first = ids[0]!.GetValue<string>(); ids[0] = ids[1]!.GetValue<string>(); ids[1] = first; fixture.WriteSession(json);
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Fact]
    public void WrongEventIdIsRejected()
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject json = fixture.Session(); json["eventIds"]!.AsArray()[4] = new string('0', 32); fixture.WriteSession(json);
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Theory]
    [InlineData("presentation.snapshot", "stale")]
    [InlineData("presentation.nonbiological", "hasBiologicalState=true")]
    [InlineData("presentation.no-mutation", "before=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;after=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    [InlineData("session.definition", "0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("session.scheduler", "0000000000000000000000000000000000000000000000000000000000000000")]
    public void ContradictoryAppSessionClaimIsRejected(string id, string detail)
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject doctor = fixture.Doctor("app/doctor.json");
        DoctorCheck(doctor, id)!["detail"] = detail;
        fixture.WriteJson("app/doctor.json", doctor);

        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Fact]
    public void MissingRequiredAppSessionCheckIsRejected()
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject doctor = fixture.Doctor("package/packaged-doctor.json");
        JsonArray checks = doctor["checks"]!.AsArray();
        checks.Remove(DoctorCheck(doctor, "session.core-headless"));
        fixture.WriteJson("package/packaged-doctor.json", doctor);

        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Fact]
    public void MissingOrStaleSessionEvidenceIsRejected()
    {
        using Fixture fixture = new(); fixture.WriteValid();
        File.Delete(fixture.PathOf("cli/session-self-test.json"));
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Fact]
    public void FailedSessionCheckOrMissingLogIsRejected()
    {
        using Fixture failedCheck = new(); failedCheck.WriteValid();
        JsonObject session = failedCheck.Session();
        session["checks"]!.AsArray()[0]!.AsObject()["severity"] = "Failure";
        failedCheck.WriteSession(session);
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(failedCheck.Root, Commit).Status);

        using Fixture missingLog = new(); missingLog.WriteValid();
        File.Delete(missingLog.PathOf("cli/session-self-test.log"));
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(missingLog.Root, Commit).Status);
    }

    [Fact]
    public void MissingCorrectionRegressionTestEvidenceIsRejected()
    {
        using Fixture fixture = new(); fixture.WriteValid();
        File.Delete(fixture.PathOf("tests/Emergence.Simulation.Tests/Emergence.Simulation.Tests.trx"));
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Theory]
    [InlineData("success", "false")]
    [InlineData("semanticVersion", "0.3.0-dev")]
    [InlineData("gitCommit", "ffffffffffffffffffffffffffffffffffffffff")]
    public void StaleOrFailedAppDoctorIdentityIsRejected(string property, string replacement)
    {
        using Fixture fixture = new(); fixture.WriteValid();
        JsonObject doctor = fixture.Doctor("app/doctor.json");
        if (property == "success") doctor[property] = false;
        else doctor["build"]!.AsObject()[property] = replacement;
        fixture.WriteJson("app/doctor.json", doctor);

        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(fixture.Root, Commit).Status);
    }

    [Fact]
    public void UnsuccessfulRequiredAppCheckAndMalformedSnapshotStateAreRejected()
    {
        using Fixture failedCheck = new(); failedCheck.WriteValid();
        JsonObject doctor = failedCheck.Doctor("app/doctor.json");
        DoctorCheck(doctor, "session.core-headless")!["severity"] = "Failure";
        failedCheck.WriteJson("app/doctor.json", doctor);
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(failedCheck.Root, Commit).Status);

        using Fixture malformedState = new(); malformedState.WriteValid();
        JsonObject malformedDoctor = malformedState.Doctor("package/packaged-doctor.json");
        DoctorCheck(malformedDoctor, "presentation.snapshot")!["detail"] =
            $"world=0000000000000000000000000000002a;branch=00000000000000000000000000000007;tick=0;status=Paused;definition={Phase04EvidenceValidator.SessionDefinitionDigest};state=not-a-digest";
        malformedState.WriteJson("package/packaged-doctor.json", malformedDoctor);
        Assert.Equal(EvidenceStatus.Failed, Phase04EvidenceValidator.Evaluate(malformedState.Root, Commit).Status);
    }

    private static JsonObject? DoctorCheck(JsonObject doctor, string id) =>
        doctor["checks"]!.AsArray().Select(node => node!.AsObject()).SingleOrDefault(check => check["id"]!.GetValue<string>() == id);

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"emergence-phase04-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

        public void WriteValid()
        {
            WriteSession(new JsonObject
            {
                ["success"] = true,
                ["phase"] = Phase04EvidenceValidator.CorrectionPhase,
                ["version"] = "0.4.0-dev",
                ["gitCommit"] = Commit,
                ["algorithmCatalogDigest"] = Phase04EvidenceValidator.AlgorithmCatalogDigest,
                ["schedulerGraphDigest"] = Phase04EvidenceValidator.SchedulerGraphDigest,
                ["sessionDefinitionDigest"] = Phase04EvidenceValidator.SessionDefinitionDigest,
                ["sessionTraceDigest"] = Phase04EvidenceValidator.SessionTraceDigest,
                ["finalStateDigest"] = Phase04EvidenceValidator.FinalStateDigest,
                ["finalTick"] = "2",
                ["acceptedCommands"] = 4,
                ["committedEvents"] = 10,
                ["eventIds"] = new JsonArray(Phase04EvidenceValidator.EventIds.Select(id => JsonValue.Create(id)).ToArray()),
                ["checks"] = new JsonArray(new JsonObject { ["id"] = "session.vector", ["severity"] = "Success", ["detail"] = "exact" }),
            });
            Write("cli/session-self-test.log", "PROJECT_EMERGENCE_SESSION_SELF_TEST_OK\n");
            WriteJson("app/doctor.json", ValidDoctor());
            WriteJson("package/packaged-doctor.json", ValidDoctor());
            Write("tests/Emergence.Simulation.Tests/Emergence.Simulation.Tests.trx", "ActiveStepMutationFromCommandProcessorFaultsAtomically ActiveStepMutationFromSimulationSystemFaultsAtomically ReentrantCommandProcessorFaultsWithEmptyOrNonemptyGraph SuccessfulCallbackIssuesArePreservedInDeterministicOrder ReceiptIssueLimitIsExactAndOneOverFaultsAtomically WrongThreadMutationDuringStepIsRejectedWithoutViolatingOwnerTransaction");
            Write("tests/Emergence.Foundation.Tests/Emergence.Foundation.Tests.trx", "IssueSeverityUsesExactClosedJson");
            Write("tests/Emergence.Model.Tests/Emergence.Model.Tests.trx", "TickReceiptDefinitionIdentityIsImmutableAndJsonStable TickReceiptIssuesAreDefensivelyCopied");
            Write("tests/Emergence.Presentation.Contracts.Tests/Emergence.Presentation.Contracts.Tests.trx", "CrossSessionReceiptBindingRejectsOtherBranchOrWorld");
            Write("tests/Emergence.Architecture.Tests/Emergence.Architecture.Tests.trx", "ProductionCallbacksAreDocumentedAndStateless");
        }

        public JsonObject Session() => JsonNode.Parse(File.ReadAllText(PathOf("cli/session-self-test.json")))!.AsObject();
        public JsonObject Doctor(string relative) => JsonNode.Parse(File.ReadAllText(PathOf(relative)))!.AsObject();
        public void WriteSession(JsonObject value) => WriteJson("cli/session-self-test.json", value);

        public void WriteJson(string relative, JsonNode value) => Write(relative, value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        private static JsonObject ValidDoctor()
        {
            string state = new('a', 64);
            string snapshot = $"world=0000000000000000000000000000002a;branch=00000000000000000000000000000007;tick=0;status=Paused;definition={Phase04EvidenceValidator.SessionDefinitionDigest};state={state}";
            return new JsonObject
            {
                ["success"] = true,
                ["build"] = new JsonObject { ["semanticVersion"] = "0.4.0-dev", ["gitCommit"] = Commit },
                ["checks"] = new JsonArray
                {
                    Check("phase.identity", Phase04EvidenceValidator.CorrectionPhase),
                    Check("session.definition", Phase04EvidenceValidator.SessionDefinitionDigest),
                    Check("session.scheduler", Phase04EvidenceValidator.SchedulerGraphDigest),
                    Check("presentation.snapshot", snapshot),
                    Check("presentation.nonbiological", "hasBiologicalState=false"),
                    Check("presentation.no-mutation", $"before={state};after={state}"),
                    Check("session.core-headless", "Emergence.Simulation"),
                },
            };
        }

        private static JsonObject Check(string id, string detail) => new() { ["id"] = id, ["severity"] = "Success", ["detail"] = detail };

        private void Write(string relative, string contents)
        {
            string path = PathOf(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        public void Dispose() => Directory.Delete(Root, true);
    }
}
