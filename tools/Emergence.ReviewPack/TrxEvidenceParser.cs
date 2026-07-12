using System.Globalization;
using System.Xml.Linq;

namespace Emergence.ReviewPack;

public static class TrxEvidenceParser
{
    public static TestEvidence Parse(
        string project,
        string command,
        string configuration,
        string trxFile,
        string coverageFile,
        string trxRelativePath,
        string coverageRelativePath)
    {
        if (!File.Exists(trxFile))
        {
            return Missing(project, command, configuration, trxRelativePath, coverageRelativePath, "TRX evidence is missing.");
        }

        try
        {
            XDocument document = XDocument.Load(trxFile, LoadOptions.None);
            XElement? summary = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "ResultSummary");
            XElement? counters = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Counters");
            if (summary is null || counters is null)
            {
                return Missing(project, command, configuration, trxRelativePath, coverageRelativePath, "TRX has no ResultSummary/Counters evidence.") with
                {
                    Status = EvidenceStatus.Incomplete,
                    TrxSha256 = EvidencePaths.HashFile(trxFile),
                };
            }

            int total = Attribute(counters, "total");
            int executed = Attribute(counters, "executed");
            int passed = Attribute(counters, "passed");
            int failed = Attribute(counters, "failed");
            int notExecuted = Math.Max(
                Attribute(counters, "notExecuted"),
                Attribute(counters, "skipped") + Attribute(counters, "inconclusive") + Attribute(counters, "pending"));
            string outcome = summary.Attribute("outcome")?.Value ?? string.Empty;
            bool aborted = outcome.Equals("Aborted", StringComparison.OrdinalIgnoreCase)
                || Attribute(counters, "aborted") > 0
                || Attribute(counters, "disconnected") > 0;
            bool infrastructureFailure = Attribute(counters, "error") > 0 || Attribute(counters, "timeout") > 0;

            EvidenceStatus status;
            if (failed > 0 || infrastructureFailure || aborted || outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase) || outcome.Equals("Error", StringComparison.OrdinalIgnoreCase))
            {
                status = EvidenceStatus.Failed;
            }
            else if (total <= 0
                     || executed != total
                     || passed != total
                     || notExecuted > 0
                     || !(outcome.Equals("Completed", StringComparison.OrdinalIgnoreCase) || outcome.Equals("Passed", StringComparison.OrdinalIgnoreCase)))
            {
                status = EvidenceStatus.Incomplete;
            }
            else
            {
                status = EvidenceStatus.Passed;
            }

            if (!File.Exists(coverageFile) && status == EvidenceStatus.Passed)
            {
                status = EvidenceStatus.Incomplete;
            }

            return new TestEvidence(
                project,
                command,
                configuration,
                status,
                total,
                executed,
                passed,
                failed,
                notExecuted,
                trxRelativePath,
                coverageRelativePath,
                EvidencePaths.HashFile(trxFile),
                File.Exists(coverageFile) ? EvidencePaths.HashFile(coverageFile) : string.Empty,
                $"TRX outcome={outcome}; coverage={(File.Exists(coverageFile) ? "present" : "missing")}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException or FormatException)
        {
            return Missing(project, command, configuration, trxRelativePath, coverageRelativePath, $"TRX could not be parsed: {exception.Message}") with
            {
                Status = EvidenceStatus.Incomplete,
                TrxSha256 = EvidencePaths.HashFile(trxFile),
            };
        }
    }

    private static int Attribute(XElement element, string name)
    {
        string? value = element.Attribute(name)?.Value;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
    }

    private static TestEvidence Missing(
        string project,
        string command,
        string configuration,
        string trxRelativePath,
        string coverageRelativePath,
        string detail) =>
        new(project, command, configuration, EvidenceStatus.Missing, 0, 0, 0, 0, 0, trxRelativePath, coverageRelativePath, string.Empty, string.Empty, detail);
}
