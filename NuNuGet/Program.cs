namespace NuNuGet;

using Microsoft.Extensions.Logging;
using System.CommandLine;

internal static class Program
{
    /// <summary>
    /// Entry point for the NuNuGet application. Processes command-line arguments and executes the specified command.
    /// </summary>
    /// <remarks>If no subcommand is provided, the application displays an error message and shows the help
    /// information. All exceptions are caught and logged to the standard error output before returning a non-zero exit
    /// code.</remarks>
    /// <param name="args">An array of command-line arguments provided to the application. Each element represents a single argument.</param>
    /// <returns>An integer exit code indicating the result of the application's execution. Returns 0 on success; otherwise,
    /// returns 1 if an error occurs or no subcommand is specified.</returns>
    private static int Main(string[] args)
    {
        try
        {
            // Set 'NUGET_USE_SYSTEM_TEXT_JSON_DESERIALIZATION' to 'true' for the current process to force NuGet Client
            // SDK to use System.Text.Json for [de]serialization - which is AOT friendly - rather than Newtonsoft.Json.
            Environment.SetEnvironmentVariable("NUGET_USE_SYSTEM_TEXT_JSON_DESERIALIZATION", "true");

            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = false;
                    options.SingleLine = true;
                    options.TimestampFormat = null;
                }));

            RootCommand rootCommand = new()
            {
                Description = "NuNuGet - A simple NuGet client",
                TreatUnmatchedTokensAsErrors = true,
            };
            rootCommand.SetAction(parseResult =>
            {
                Console.Error.Write("Error: A subcommand is required.\n\n");
                _ = rootCommand.Parse("--help").Invoke();

                return 1;
            });
            rootCommand.Subcommands.Add(new Commands.InstallCommand(loggerFactory));

            return rootCommand.Parse(args).Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());

            return 1;
        }
    }
}

