using OpenTyrian.Core;

var startup = new GameStartupState();
var parsed = GameStartupArguments.Parse(startup, ["OpenTyrian", .. args], Console.Out, Console.Error);
if (parsed.ExitCode is int exitCode) return exitCode;
Console.Error.WriteLine("The C# game host parses native options, but playable game flows have not yet been ported.");
return 1;
