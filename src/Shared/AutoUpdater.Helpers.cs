using System.Runtime.InteropServices;
using Microsoft.Win32;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;

namespace UniGetUI.Shared;

internal static class AutoUpdaterHelpers
{
    internal static bool IsSourceUrlAllowed(string url, bool allowUnsafeUrls)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (allowUnsafeUrls)
        {
            Logger.Warn($"Registry override enabled: allowing potentially unsafe updater URL {url}");
            return true;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string path = uri.AbsolutePath;
        return uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                && path.StartsWith(
                    "/Technicull-Solutions/PackageManager/",
                    StringComparison.OrdinalIgnoreCase
                )
            || uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                && path.StartsWith(
                    "/Technicull-Solutions/PackageManager/",
                    StringComparison.OrdinalIgnoreCase
                )
            || uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
                && path.StartsWith(
                    "/repos/Technicull-Solutions/PackageManager/",
                    StringComparison.OrdinalIgnoreCase
                );
    }

    internal static bool IsUpdateAvailable(
        string currentVersion,
        int currentBuildNumber,
        string availableVersion,
        int availableBuildNumber
    )
    {
        if (SemanticVersion.TryParse(currentVersion, out SemanticVersion currentSemantic)
            && SemanticVersion.TryParse(availableVersion, out SemanticVersion availableSemantic))
        {
            int comparison = availableSemantic.CompareTo(currentSemantic);
            return comparison > 0 || comparison == 0 && availableBuildNumber > currentBuildNumber;
        }

        Version current = ParseVersionOrFallback(currentVersion, new Version(0, 0, 0));
        Version available = ParseVersionOrFallback(availableVersion, new Version(0, 0, 0));
        int releaseComparison = available.CompareTo(current);
        return releaseComparison > 0
            || releaseComparison == 0 && availableBuildNumber > currentBuildNumber;
    }

    internal static ProductInfoFile SelectInstallerFile(List<ProductInfoFile> files)
    {
        string targetArch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => "x64",
        };

        ProductInfoFile? match = files.FirstOrDefault(file =>
            file.Type.Equals("exe", StringComparison.OrdinalIgnoreCase)
            && file.Arch.Equals(targetArch, StringComparison.OrdinalIgnoreCase)
        );

        match ??= files.FirstOrDefault(file =>
            file.Type.Equals("exe", StringComparison.OrdinalIgnoreCase)
            && file.Arch.Equals("Any", StringComparison.OrdinalIgnoreCase)
        );

        match ??= files.FirstOrDefault(file =>
            file.Type.Equals("msi", StringComparison.OrdinalIgnoreCase)
            && file.Arch.Equals(targetArch, StringComparison.OrdinalIgnoreCase)
        );

        match ??= files.FirstOrDefault(file =>
            file.Type.Equals("msi", StringComparison.OrdinalIgnoreCase)
            && file.Arch.Equals("Any", StringComparison.OrdinalIgnoreCase)
        );

        if (match is null)
        {
            throw new KeyNotFoundException(
                $"No compatible installer file found in productinfo for architecture '{targetArch}'"
            );
        }

        return match;
    }

    internal static Version ParseVersionOrFallback(string rawVersion, Version fallbackVersion)
    {
        if (Version.TryParse(rawVersion, out Version? parsed))
        {
            return CoreTools.NormalizeVersionForComparison(parsed);
        }

        string sanitized = rawVersion.Trim().TrimStart('v', 'V');
        if (Version.TryParse(sanitized, out parsed))
        {
            return CoreTools.NormalizeVersionForComparison(parsed);
        }

        Logger.Warn($"Could not parse version '{rawVersion}', using fallback '{fallbackVersion}'");
        return fallbackVersion;
    }

    internal static string NormalizeThumbprint(string thumbprint)
    {
        char[] normalized = thumbprint.ToLowerInvariant().Where(char.IsAsciiHexDigit).ToArray();

        return new string(normalized);
    }

    internal static string? GetRegistryString(RegistryKey? key, string valueName)
    {
#pragma warning disable CA1416
        object? value = key?.GetValue(valueName);
#pragma warning restore CA1416
        if (value is null)
        {
            return null;
        }

        string? parsedValue = value.ToString();
        if (string.IsNullOrWhiteSpace(parsedValue))
        {
            return null;
        }

        return parsedValue.Trim();
    }

#if DEBUG
    internal static bool GetRegistryBool(RegistryKey? key, string valueName)
    {
#pragma warning disable CA1416
        object? value = key?.GetValue(valueName);
#pragma warning restore CA1416
        if (value is null)
        {
            return false;
        }

        if (value is int intValue)
        {
            return intValue != 0;
        }

        if (value is long longValue)
        {
            return longValue != 0;
        }

        string normalized = value.ToString()?.Trim() ?? "";
        return normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
#endif

    internal sealed class ProductInfoFile
    {
        public string Arch { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
    }
}
