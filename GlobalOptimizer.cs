using System;
using ScopeCLI.Services;
using Spectre.Console;

namespace ScopeCLI
{
    public class GlobalOptimizer
    {
        private readonly SystemOptimizer _optimizer = new();

        public void Optimize()
        {
            try
            {
                var result = _optimizer.OptimizeAsync(aggressiveKill: true).GetAwaiter().GetResult();
                AnsiConsole.MarkupLine($"[green]Optimization finished:[/] trimmed {result.TrimmedProcessesCount} processes, terminated {result.TerminatedProcessesCount} non-whitelisted processes.");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error during optimization:[/] {ex.Message.EscapeMarkup()}");
            }
        }
    }
}
