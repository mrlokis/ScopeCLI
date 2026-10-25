using System;
using System.Collections.Generic;
using Humanizer;
using ScopeCLI.Models;
using Spectre.Console;
using Profile = ScopeCLI.Models.Profile;

namespace ScopeCLI.UI
{
    public static class ConsoleUi
    {
        public const string AppVersion = "0.00.1-dev";

        public static void RenderHeader()
        {
            if (!Console.IsOutputRedirected)
            {
                try { AnsiConsole.Clear(); } catch { }
            }

            var rule = new Rule($"[bold yellow]⚡ ScopeCLI[/] [grey]v{AppVersion}[/] - [cyan]Lightweight Minecraft Launcher[/]")
                .LeftJustified();
            AnsiConsole.Write(rule);
        }

        public static void RenderMemoryStatus()
        {
            long available = NativeHelpers.GetAvailableRam();
            long total = NativeHelpers.GetDeviceRam();

            if (total <= 0)
                return;

            double usedPercent = (double)(total - available) / total * 100;
            double freePercent = (double)available / total * 100;

            string color = freePercent switch
            {
                < 15 => "red",
                < 30 => "yellow",
                _ => "green"
            };

            var table = new Table().Border(TableBorder.Rounded).Expand();
            table.AddColumn(new TableColumn("[bold grey]System RAM Status[/]").Centered());

            string content = $"Free RAM: [{color}]{available.Bytes()}[/] / [grey]{total.Bytes()}[/] " +
                             $"([{color}]{freePercent:F1}% free[/], Used: {usedPercent:F1}%)";

            table.AddRow(new Markup(content));
            AnsiConsole.Write(table);

            if (freePercent < 20)
            {
                AnsiConsole.MarkupLine("[bold red]⚠ Warning:[/] Low available memory (< 20%). Running optimization is recommended before launching.");
            }
        }

        public static void RenderProfilesTable(List<Profile> profiles)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("[bold]ID[/]");
            table.AddColumn("[bold]Name[/]");
            table.AddColumn("[bold]Nickname[/]");
            table.AddColumn("[bold]Mod Loader[/]");
            table.AddColumn("[bold]Version[/]");
            table.AddColumn("[bold]Mods[/]");
            table.AddColumn("[bold]Isolation[/]");

            foreach (var p in profiles)
            {
                table.AddRow(
                    $"[yellow]{p.ShortId.EscapeMarkup()}[/]",
                    $"[bold white]{p.Name.EscapeMarkup()}[/]",
                    $"[cyan]{p.Nickname.EscapeMarkup()}[/]",
                    $"[green]{p.ModLoader.EscapeMarkup()}[/]",
                    $"[grey]{p.GameVersion.EscapeMarkup()}[/]",
                    $"[yellow]{p.Mods.Count}[/]",
                    p.IsIsolationEnabled ? "[green]Enabled[/]" : "[grey]Disabled[/]"
                );
            }

            AnsiConsole.Write(table);
        }

        public static void RenderHelp()
        {
            RenderHeader();
            AnsiConsole.MarkupLine(@"
[bold]Usage:[/]
  [yellow]ScopeCLI[/]                                  Run interactive launcher (TUI)
  [yellow]ScopeCLI[/] [green]launch[/] [cyan]<profile_or_id>[/]           Directly launch game with profile
  [yellow]ScopeCLI[/] [green]profiles list[/]                     List all configured profiles
  [yellow]ScopeCLI[/] [green]profiles show[/] [cyan]<id>[/]                Show profile details
  [yellow]ScopeCLI[/] [green]profiles delete[/] [cyan]<id>[/]              Delete a profile
  [yellow]ScopeCLI[/] [green]optimize[/] [[[grey]--kill[/]]]                  Optimize system RAM (safe / aggressive)
  [yellow]ScopeCLI[/] [green]--help[/], [green]-h[/]                         Show this help message

[bold]Options:[/]
  [cyan]-p, --profile <id_or_name>[/]  Specify profile
  [cyan]-s, --server <host:port>[/]    Auto-connect to server
  [cyan]-r, --ram <megabytes>[/]       Allocate RAM in MB
  [cyan]-o, --optimize[/]              Run memory optimization before launching
  [cyan]-a, --kill, --aggressive[/]    Terminate non-whitelisted background apps (optimize)
");
        }
    }
}
