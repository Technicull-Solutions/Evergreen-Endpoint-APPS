using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Consolonia;
using Consolonia.Core.Infrastructure;
using Consolonia.PlatformSupport;

namespace UniGetUI.Tui.Infrastructure;

// Windows 10+ supports VT output in both conhost and Windows Terminal. Select it explicitly:
// Consolonia's legacy output adapter builds Win32 exceptions through reflective Vanara helpers.
internal static partial class TuiConsolePlatform
{
    internal static AppBuilder UseEvergreenConsole(this AppBuilder builder)
    {
        IConsole console = OperatingSystem.IsWindows()
            ? new EvergreenWindowsConsole(new AnsiConsoleOutput())
            : new CursesConsole();
        return builder.UseConsole(console).UseAutoDetectClipboard().UseAutoDetectConsoleColorMode();
    }

    internal static IDisposable? EnterOutputMode()
    {
        if (!OperatingSystem.IsWindows()) return null;
        nint handle = GetStdHandle(-11);
        if (!GetConsoleMode(handle, out uint mode)
            || !SetConsoleMode(handle, mode | 0x0001 | 0x0004))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        return new OutputModeScope(handle, mode);
    }

    private sealed class OutputModeScope(nint handle, uint mode) : IDisposable
    {
        public void Dispose() => SetConsoleMode(handle, mode);
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetStdHandle(int handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint handle, out uint mode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint handle, uint mode);
}
