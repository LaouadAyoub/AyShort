using System;
using System.Runtime.CompilerServices;

// Ensures integration tests use a dedicated test database connection string.
// Set the environment variable TEST_DB_CONNECTION before running tests, e.g.:
// PowerShell:
//   $env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=ayshort_test;User Id=ays_test;Password=supersecret"
// This value will be mapped to ConnectionStrings:Default for the WebApi host.

internal static class TestDatabaseInitializer
{
    private const string SourceVar = "TEST_DB_CONNECTION";
    private const string TargetVarDoubleUnderscore = "ConnectionStrings__Default";
    private const string TargetVarColon = "ConnectionStrings:Default";

    [ModuleInitializer]
    public static void Init()
    {
        var testCs = Environment.GetEnvironmentVariable(SourceVar);
        if (string.IsNullOrWhiteSpace(testCs))
        {
            throw new InvalidOperationException(
                $"Integration tests require the '{SourceVar}' environment variable pointing to a dedicated PostgreSQL test database.\n" +
                "Example (PowerShell):\n" +
                "$env:TEST_DB_CONNECTION=\"Host=localhost;Port=5432;Database=ayshort_test;User Id=ays_test;Password=supersecret\"\n" +
                "This will be injected as ConnectionStrings:Default.\n" +
                "Do NOT use a production or development database.");
        }

        // Safety: ensure database name indicates test usage to avoid accidental prod/dev access.
        // Simple heuristic: must contain '_test' (case-insensitive).
        var lowered = testCs.ToLowerInvariant();
        if (!lowered.Contains("_test"))
        {
            throw new InvalidOperationException(
                "Refusing to run integration tests: TEST_DB_CONNECTION must point to a database whose name contains '_test'.");
        }

        // Map to both env var syntaxes recognized by the .NET configuration binder.
        // Some hosts / test runners may read the colon form directly; others rely on double underscore.
        Environment.SetEnvironmentVariable(TargetVarDoubleUnderscore, testCs);
        Environment.SetEnvironmentVariable(TargetVarColon, testCs);
    }
}
