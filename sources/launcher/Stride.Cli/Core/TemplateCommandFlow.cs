// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using Stride.Assets.Templates;
using Stride.Core;

namespace Stride.Cli.Core;

// The steps of `stride template add`, `list` and `remove`, apart from System.CommandLine so that the tests reach them. The
// checks and the texts are the installer's, the ones of Game Studio's Template packages window.
internal sealed class TemplateCommandFlow(TemplatePackageInstaller installer, TextWriter output, TextWriter error, Func<string?> readLine, bool inputRedirected)
{
    public const string NoPackages = "No template packages besides Stride's.";

    // What to do before a change: go on (--yes), ask on the console, or refuse because there is no console to ask on.
    public enum Gate { Proceed, Ask, Refuse }

    public static Gate Decide(bool yes, bool inputRedirected)
        => yes ? Gate.Proceed : inputRedirected ? Gate.Refuse : Gate.Ask;

    public static bool IsYes(string? answer)
        => answer?.Trim() is { } trimmed && (trimmed.Equals("y", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase));

    // A path to a .nupkg (one that exists, or a name ending in .nupkg), else a package id of the configured sources.
    public async Task<int> AddAsync(string target, string? packageVersion, bool yes, CancellationToken cancellationToken)
    {
        TemplatePackageInfo package;
        try
        {
            if (File.Exists(target))
            {
                package = installer.InspectFile(target);
            }
            else if (target.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                error.WriteLine($"{Path.GetFullPath(target)} does not exist.");
                return 1;
            }
            else
            {
                PackageVersion? version = null;
                if (!string.IsNullOrWhiteSpace(packageVersion))
                {
                    if (!PackageVersion.TryParse(packageVersion.Trim(), out var parsed))
                    {
                        error.WriteLine($"{packageVersion.Trim()} is not a package version.");
                        return 1;
                    }
                    version = parsed;
                }
                package = await installer.FindByIdAsync(target.Trim(), version, cancellationToken);
            }
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }

        if (installer.Refusal(package) is { } refusal)
        {
            error.WriteLine(refusal);
            return 1;
        }

        switch (Decide(yes, inputRedirected))
        {
            case Gate.Refuse:
                error.WriteLine($"The question cannot be asked: the input is not a console. Run the command again with --yes to install {package.Id} {package.Version} without it.");
                return 1;
            case Gate.Ask:
                output.WriteLine(installer.Confirmation(package));
                output.WriteLine();
                output.Write("Install? [y/N] ");
                if (!IsYes(readLine()))
                {
                    output.WriteLine("Nothing was installed.");
                    return 1;
                }
                break;
        }

        output.WriteLine($"Installing {package.Id} {package.Version} from {package.Origin}...");
        try
        {
            await installer.AddAsync(package, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The installer's refusals, and the restore's own failures (an unreachable source, a broken package)
            error.WriteLine(exception is InvalidOperationException ? exception.Message : $"Could not install {package.Id} {package.Version}: {exception.Message}");
            return 1;
        }

        output.WriteLine($"Installed {package.Id} {package.Version}. Its templates show in 'stride new list' and on the start screen of Game Studio.");
        return 0;
    }

    // One line per package (id, version, authors, in columns), and the local-folder source it is found in on the next line.
    public int List()
    {
        var packages = installer.Listed();
        if (packages.Count == 0)
        {
            output.WriteLine(NoPackages);
            return 0;
        }

        var idWidth = packages.Max(package => package.Id.Length);
        var versionWidth = packages.Max(package => package.Version.ToString().Length);
        foreach (var package in packages)
        {
            output.WriteLine($"{package.Id.PadRight(idWidth)}  {package.Version.ToString().PadRight(versionWidth)}  {package.Authors}".TrimEnd());
            if (package.LocalFolderSource is { } folder)
                output.WriteLine($"  from {folder}");
        }
        return 0;
    }

    public async Task<int> RemoveAsync(string id, bool yes, CancellationToken cancellationToken)
    {
        var removal = installer.PlanRemoval(id.Trim());
        if (removal.Kind != TemplatePackageRemovalKind.Uninstall)
        {
            error.WriteLine(removal.Message);
            return 1;
        }

        switch (Decide(yes, inputRedirected))
        {
            case Gate.Refuse:
                error.WriteLine($"The question cannot be asked: the input is not a console. Run the command again with --yes to remove {removal.Id} without it.");
                return 1;
            case Gate.Ask:
                output.Write($"{removal.Message} [y/N] ");
                if (!IsYes(readLine()))
                {
                    output.WriteLine("Nothing was removed.");
                    return 1;
                }
                break;
        }

        try
        {
            await installer.RemoveAsync(removal, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Could not remove {removal.Id}: {exception.Message}");
            return 1;
        }

        output.WriteLine($"Removed {removal.Id} ({string.Join(", ", removal.Installed.Select(package => package.Version.ToString()))}).");
        return 0;
    }
}
