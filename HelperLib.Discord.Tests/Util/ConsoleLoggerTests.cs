using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Tests.Util;

[Collection(ConsoleAndStaticStateCollection.Name)]
public class ConsoleLoggerTests
{
    // The foreground color is not asserted: Console.ForegroundColor has no seam to observe it.
    [Fact]
    public void Log_writes_the_formatted_message_and_the_exception_to_the_console()
    {
        var output = CaptureConsoleOutput(() =>
            ConsoleLogger.Instance.LogError(new InvalidOperationException("boom"), "No such role {RoleId}", 42));

        Assert.Contains("No such role 42", output);
        Assert.Contains("boom", output);
    }

    [Fact]
    public void Log_writes_the_formatted_message_for_information()
    {
        var output = CaptureConsoleOutput(() =>
            ConsoleLogger.Instance.LogInformation("Default GuildId was successfully set to {DefaultGuildId}", 42));

        Assert.Contains("Default GuildId was successfully set to 42", output);
    }

    private static string CaptureConsoleOutput(Action log)
    {
        var originalOutput = Console.Out;
        using var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);

        try
        {
            log();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        return capturedOutput.ToString();
    }
}
