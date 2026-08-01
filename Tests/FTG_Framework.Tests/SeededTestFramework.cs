#nullable enable
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework("FTG_Framework.Tests.SeededTestFramework", "FTG_Framework.Tests")]

namespace FTG_Framework.Tests;

public sealed class SeededTestFramework : XunitTestFramework
{
    public SeededTestFramework(IMessageSink messageSink) : base(messageSink) { }

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName) =>
        new SeededTestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
}

internal sealed class SeededTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    public SeededTestFrameworkExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink) { }

    protected override void RunTestCases(
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        int seed = ResolveSeed();
        var random = new Random(seed);
        var materialized = testCases.ToList();
        var singletonCases = materialized.Where(IsEventBusCase).ToArray();
        for (int index = singletonCases.Length - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (singletonCases[index], singletonCases[swapIndex]) = (singletonCases[swapIndex], singletonCases[index]);
        }
        using var enumerator = ((IEnumerable<IXunitTestCase>)singletonCases).GetEnumerator();
        var resolved = materialized.Select(testCase =>
        {
            if (!IsEventBusCase(testCase)) return testCase;
            enumerator.MoveNext();
            return enumerator.Current;
        }).ToArray();

        DiagnosticMessageSink.OnMessage(new DiagnosticMessage(
            $"[SeededOrder] Seed={seed}; EventBusCases={singletonCases.Length}; "
            + $"Order={string.Join(" | ", singletonCases.Select(item => item.DisplayName))}; "
            + $"Replay=$env:FTG_TEST_SEED='{seed}'; dotnet test tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj"));
        base.RunTestCases(resolved, executionMessageSink, executionOptions);
    }

    private static bool IsEventBusCase(IXunitTestCase testCase) =>
        testCase.TestMethod.TestClass.TestCollection.DisplayName.Contains(EventBusTestCollection.Name, StringComparison.Ordinal);

    private static int ResolveSeed()
    {
        string? configured = Environment.GetEnvironmentVariable("FTG_TEST_SEED");
        if (configured is null)
            return 2202;
        if (int.TryParse(configured, out int seed))
            return seed;
        throw new InvalidOperationException($"FTG_TEST_SEED must be a 32-bit integer, but was '{configured}'.");
    }
}
