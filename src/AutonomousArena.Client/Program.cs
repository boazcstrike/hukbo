using AutonomousArena.Client;

try
{
    using var game = new ArenaGame();
    game.Run();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Autonomous Arena failed to start: {exception}");
    Environment.ExitCode = 1;
}
