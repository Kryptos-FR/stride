// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System.CommandLine;
using Stride.Cli.Core;

// template: add, list and remove the third-party template packages that `stride new` and Game Studio find (the steps are
// in TemplateCommandFlow).
internal static class TemplateCommand
{
    public static Command Create(StrideVersionManager manager)
    {
        var versionOptions = new CliVersion.Options($"Stride version the template packages are checked against instead of the newest installed release; {CliVersion.Options.LineHelp}.");
        var yesOption = new Option<bool>("--yes", "-y") { Description = "Do it without asking (needed when the input is not a console)." };

        var command = new Command("template", "Add, list and remove the third-party template packages of Stride.");

        var packageArgument = new Argument<string>("package") { Description = "A .nupkg file, or the id of a package in the configured package sources." };
        var packageVersionOption = new Option<string?>("--package-version") { Description = "The version of the package to add by id, instead of its newest version made for this Stride version." };
        var addCommand = new Command("add", "Add a template package: its templates show in 'stride new list' and in Game Studio.");
        addCommand.Arguments.Add(packageArgument);
        addCommand.Options.Add(packageVersionOption);
        addCommand.Options.Add(yesOption);
        versionOptions.AddTo(addCommand);
        addCommand.SetAction((parseResult, cancellationToken) => Run(manager, versionOptions, parseResult,
            flow => flow.AddAsync(parseResult.GetValue(packageArgument)!, parseResult.GetValue(packageVersionOption), parseResult.GetValue(yesOption), cancellationToken)));
        command.Subcommands.Add(addCommand);

        var listCommand = new Command("list", "List the third-party template packages found for a Stride version.");
        versionOptions.AddTo(listCommand);
        listCommand.SetAction((parseResult, _) => Run(manager, versionOptions, parseResult, flow => Task.FromResult(flow.List())));
        command.Subcommands.Add(listCommand);

        var idArgument = new Argument<string>("id") { Description = "The id of the template package." };
        var removeCommand = new Command("remove", "Remove every version of a template package from the global packages folder.");
        removeCommand.Arguments.Add(idArgument);
        removeCommand.Options.Add(yesOption);
        versionOptions.AddTo(removeCommand);
        removeCommand.SetAction((parseResult, cancellationToken) => Run(manager, versionOptions, parseResult,
            flow => flow.RemoveAsync(parseResult.GetValue(idArgument)!, parseResult.GetValue(yesOption), cancellationToken)));
        command.Subcommands.Add(removeCommand);

        return command;
    }

    private static async Task<int> Run(StrideVersionManager manager, CliVersion.Options versionOptions, ParseResult parseResult, Func<TemplateCommandFlow, Task<int>> step)
    {
        var version = versionOptions.ResolveOrReport(manager, parseResult);
        if (version is null)
            return 1;
        var flow = new TemplateCommandFlow(manager.CreateTemplatePackageInstaller(version), Console.Out, Console.Error, Console.ReadLine, Console.IsInputRedirected);
        return await step(flow);
    }
}
