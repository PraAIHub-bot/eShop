using System.IO;
using System.Runtime.CompilerServices;

namespace eShop.Ordering.UnitTests;

[TestClass]
public class AppHostWiringTests
{
    [TestMethod]
    public void AppHost_RegistersOrderProcessor_WithRabbitMqAndOrderingDbWaits()
    {
        var source = ReadAppHostProgram();

        StringAssert.Contains(
            source,
            "AddProject<Projects.OrderProcessor>(\"order-processor\")",
            "AppHost must register the OrderProcessor project so the BackgroundService starts.");

        var orderProcessorBlock = ExtractAddProjectBlock(source, "Projects.OrderProcessor");

        StringAssert.Contains(
            orderProcessorBlock,
            ".WithReference(rabbitMq)",
            "OrderProcessor must reference the RabbitMQ event bus.");

        StringAssert.Contains(
            orderProcessorBlock,
            ".WaitFor(rabbitMq)",
            "OrderProcessor must WaitFor RabbitMQ before starting.");

        StringAssert.Contains(
            orderProcessorBlock,
            ".WithReference(orderDb)",
            "OrderProcessor must reference the ordering database.");

        StringAssert.Contains(
            orderProcessorBlock,
            ".WaitFor(orderDb)",
            "OrderProcessor must WaitFor the ordering database before starting.");
    }

    [TestMethod]
    public void AppHostCsproj_HasProjectReferenceToOrderProcessor()
    {
        var csprojPath = Path.Combine(RepoRoot(), "src", "eShop.AppHost", "eShop.AppHost.csproj");
        var content = File.ReadAllText(csprojPath);

        StringAssert.Contains(
            content,
            "OrderProcessor.csproj",
            "AppHost csproj must include a ProjectReference to OrderProcessor.");
    }

    private static string ReadAppHostProgram()
    {
        var path = Path.Combine(RepoRoot(), "src", "eShop.AppHost", "Program.cs");
        return File.ReadAllText(path);
    }

    private static string RepoRoot([CallerFilePath] string thisFile = "")
    {
        // thisFile = <repo>/tests/Ordering.UnitTests/AppHostWiringTests.cs
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        return dir.Parent!.Parent!.FullName;
    }

    private static string ExtractAddProjectBlock(string source, string projectMarker)
    {
        var startIdx = source.IndexOf("AddProject<" + projectMarker + ">", System.StringComparison.Ordinal);
        Assert.IsTrue(startIdx >= 0, $"AddProject<{projectMarker}> not found in AppHost Program.cs");

        var endIdx = source.IndexOf(';', startIdx);
        Assert.IsTrue(endIdx > startIdx, "Could not find end of AddProject statement.");

        return source.Substring(startIdx, endIdx - startIdx);
    }
}
