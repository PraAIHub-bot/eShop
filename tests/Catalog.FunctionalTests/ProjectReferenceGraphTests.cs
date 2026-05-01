using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace eShop.Catalog.FunctionalTests;

/// <summary>
/// Static guard tests that protect the Catalog.API / ServiceDefaults / EventBusRabbitMQ
/// dependency graph from regressing into the circular shape flagged by TICKET-003.
///
/// These tests parse the three .csproj files directly and assert structural invariants —
/// they do not need a running host, database, or RabbitMQ broker.
/// See docs/audits/catalog-api-deps.md for the full graph.
/// </summary>
public sealed class ProjectReferenceGraphTests
{
    private static readonly string RepoRoot = LocateRepoRoot();

    [Fact]
    public void ServiceDefaults_HasNoProjectReferenceToEventBusRabbitMQOrAnyApi()
    {
        var refs = ReadProjectReferences("src/eShop.ServiceDefaults/eShop.ServiceDefaults.csproj");

        Assert.DoesNotContain(refs, r => r.Contains("EventBusRabbitMQ", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(refs, r => r.Contains(".API", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EventBusRabbitMQ_DependsOnEventBusButNotServiceDefaultsOrApi()
    {
        var refs = ReadProjectReferences("src/EventBusRabbitMQ/EventBusRabbitMQ.csproj");

        Assert.Contains(refs, r => r.EndsWith("EventBus.csproj", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(refs, r => r.Contains("ServiceDefaults", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(refs, r => r.Contains(".API", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CatalogApi_ReferencesAreSingleDirection_NoCycleBackToCatalog()
    {
        var catalogRefs = ReadProjectReferences("src/Catalog.API/Catalog.API.csproj");

        Assert.DoesNotContain(catalogRefs, r => r.Contains("Catalog.API", System.StringComparison.OrdinalIgnoreCase));

        // Walk the closure of each direct reference: none of the transitively reachable
        // projects may reference Catalog.API.
        var visited = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var queue = new System.Collections.Generic.Queue<string>(catalogRefs);
        while (queue.Count > 0)
        {
            var relPath = queue.Dequeue();
            if (!visited.Add(relPath))
            {
                continue;
            }

            // Reference paths in csproj are relative to the csproj that owns them.
            // We always rebase from the repo root via the known directory layout.
            var normalized = NormalizeReferenceFromRepoRoot(relPath);
            if (!File.Exists(Path.Combine(RepoRoot, normalized)))
            {
                continue;
            }

            var transitive = ReadProjectReferences(normalized);
            foreach (var t in transitive)
            {
                Assert.False(
                    t.Contains("Catalog.API", System.StringComparison.OrdinalIgnoreCase),
                    $"Cycle detected: {normalized} references {t}, which reaches Catalog.API.");
                queue.Enqueue(t);
            }
        }
    }

    private static string[] ReadProjectReferences(string repoRelativeCsprojPath)
    {
        var fullPath = Path.Combine(RepoRoot, repoRelativeCsprojPath);
        var doc = XDocument.Load(fullPath);

        return doc.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Replace('\\', '/').Trim())
            .ToArray();
    }

    private static string NormalizeReferenceFromRepoRoot(string referencePath)
    {
        // ProjectReference paths in our csproj files use the form "..\Foo\Foo.csproj"
        // (always one level up from src/<Project>). Strip the leading "../" and prepend "src/".
        var p = referencePath.Replace('\\', '/');
        if (p.StartsWith("../", System.StringComparison.Ordinal))
        {
            p = p.Substring(3);
        }
        return Path.Combine("src", p).Replace('\\', '/');
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "eShop.slnx"))
                || File.Exists(Path.Combine(dir.FullName, "eShop.Web.slnf")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // Fallback: walk up from the assembly location (test bin folder).
        dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "eShop.slnx"))
                || File.Exists(Path.Combine(dir.FullName, "eShop.Web.slnf")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new System.InvalidOperationException("Unable to locate repository root from CWD or AppContext.BaseDirectory.");
    }
}
