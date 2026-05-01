using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace eShop.Ordering.UnitTests.Domain;

/// <summary>
/// Static guard tests for TICKET-005 (Audit and prune Ordering.Domain unused events
/// and entities). The audit at docs/audits/ordering-domain-prune.md concluded that
/// every event in src/Ordering.Domain/Events has both a raiser and a handler, so
/// nothing was deleted. These tests encode that conclusion so a future change that
/// removes the only raiser or only handler of a domain event fails CI instead of
/// silently re-introducing the dead code the audit was designed to catch.
/// </summary>
[TestClass]
public class DomainEventPruneAuditTests
{
    private static readonly Regex ClassDeclarationRegex = new(
        @"public\s+(?:sealed\s+|abstract\s+)?(?:record\s+class|record|class)\s+(\w+)",
        RegexOptions.Compiled);

    [TestMethod]
    public void Every_domain_event_in_Ordering_Domain_Events_has_at_least_one_raiser()
    {
        var srcRoot = SrcRoot();
        var events = DiscoverDomainEventTypes(srcRoot).ToList();

        Assert.IsTrue(
            events.Count > 0,
            "Discovered zero domain event types in src/Ordering.Domain/Events. " +
            "The discovery regex is broken or the directory is empty.");

        var missingRaisers = new List<string>();

        foreach (var (typeName, declaringFile) in events)
        {
            var raiserPattern = "new " + typeName + "(";
            if (!HasReferenceOutsideFile(srcRoot, raiserPattern, declaringFile))
            {
                missingRaisers.Add(typeName);
            }
        }

        Assert.AreEqual(
            0,
            missingRaisers.Count,
            "These domain events have zero `new <Event>(` raisers anywhere under src/ " +
            "outside their own file. Per docs/audits/ordering-domain-prune.md they " +
            "should each have at least one. If the raiser was intentionally removed, " +
            "delete the event class as well: " + string.Join(", ", missingRaisers));
    }

    [TestMethod]
    public void Every_domain_event_in_Ordering_Domain_Events_has_at_least_one_INotificationHandler()
    {
        var srcRoot = SrcRoot();
        var events = DiscoverDomainEventTypes(srcRoot).ToList();

        Assert.IsTrue(
            events.Count > 0,
            "Discovered zero domain event types in src/Ordering.Domain/Events. " +
            "The discovery regex is broken or the directory is empty.");

        var missingHandlers = new List<string>();

        foreach (var (typeName, declaringFile) in events)
        {
            var handlerPattern = "INotificationHandler<" + typeName + ">";
            if (!HasReferenceOutsideFile(srcRoot, handlerPattern, declaringFile))
            {
                missingHandlers.Add(typeName);
            }
        }

        Assert.AreEqual(
            0,
            missingHandlers.Count,
            "These domain events have zero `INotificationHandler<Event>` declarations " +
            "anywhere under src/. Per docs/audits/ordering-domain-prune.md they should " +
            "each have at least one handler in Ordering.API/Application/DomainEventHandlers/. " +
            "If the handler was intentionally removed, the event is now dead and should " +
            "be deleted: " + string.Join(", ", missingHandlers));
    }

    [TestMethod]
    public void Audit_document_for_ticket005_is_present_under_docs_audits()
    {
        var auditPath = Path.Combine(RepoRoot(), "docs", "audits", "ordering-domain-prune.md");

        Assert.IsTrue(
            File.Exists(auditPath),
            "Acceptance criterion: docs/audits/ordering-domain-prune.md must exist " +
            "and list every event/entity considered. Missing at: " + auditPath);

        var content = File.ReadAllText(auditPath);
        StringAssert.Contains(
            content,
            "Ordering.Domain",
            "Audit doc must reference the Ordering.Domain scope.");
    }

    [TestMethod]
    public void Ticket005_introduces_no_new_events_or_entities_beyond_the_audited_set()
    {
        // Acceptance criterion: "No new domain events or entities are introduced —
        // this ticket only deletes." The audit summary table records 7 events. This
        // test pins that count so an accidental new event added on this branch is
        // flagged for review (and either added to the audit or removed).
        var srcRoot = SrcRoot();
        var eventsDir = Path.Combine(srcRoot, "Ordering.Domain", "Events");

        var eventFiles = Directory.GetFiles(eventsDir, "*.cs", SearchOption.TopDirectoryOnly);

        Assert.AreEqual(
            7,
            eventFiles.Length,
            "TICKET-005 audited 7 event files in src/Ordering.Domain/Events. The count " +
            "changed — if you added a new event, update docs/audits/ordering-domain-prune.md " +
            "to cover it; if you deleted one, update the count here. Found: " +
            string.Join(", ", eventFiles.Select(Path.GetFileName)));
    }

    private static IEnumerable<(string TypeName, string DeclaringFile)> DiscoverDomainEventTypes(string srcRoot)
    {
        var eventsDir = Path.Combine(srcRoot, "Ordering.Domain", "Events");
        foreach (var file in Directory.GetFiles(eventsDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var content = File.ReadAllText(file);
            foreach (Match match in ClassDeclarationRegex.Matches(content))
            {
                var typeName = match.Groups[1].Value;
                yield return (typeName, file);
            }
        }
    }

    private static bool HasReferenceOutsideFile(string srcRoot, string needle, string declaringFile)
    {
        foreach (var path in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            // Skip files under bin/ and obj/ (build outputs).
            if (path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            if (string.Equals(path, declaringFile, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.ReadAllText(path).Contains(needle))
            {
                return true;
            }
        }
        return false;
    }

    private static string SrcRoot() => Path.Combine(RepoRoot(), "src");

    private static string RepoRoot([CallerFilePath] string thisFile = "")
    {
        // thisFile = <repo>/tests/Ordering.UnitTests/Domain/DomainEventPruneAuditTests.cs
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        return dir.Parent!.Parent!.Parent!.FullName;
    }
}
