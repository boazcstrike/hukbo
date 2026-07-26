using Hukbo.Client;

try
{
    using var game = new ArenaGame();
    game.Run();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Hukbo failed to start: {exception}");
    Environment.ExitCode = 1;
}
