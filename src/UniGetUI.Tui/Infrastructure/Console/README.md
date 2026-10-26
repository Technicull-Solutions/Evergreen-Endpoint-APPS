# Windows terminal compatibility source

`EvergreenWindowsConsole.cs` and `Win32ConsoleInput.cs` adapt the Windows input
adapter from Consolonia 12.0.3.14, commit
`5c317423e7fe9896d9a4fab412b3fba8c7ef1e7b`:
https://github.com/consolonia/consolonia/tree/5c317423e7fe9896d9a4fab412b3fba8c7ef1e7b

The original MIT licenses for Consolonia and its Terminal.Gui-derived input
helper are retained here and shipped with the application.

The adapter creates `Win32Exception` directly and uses source-generated Win32
imports with raw handles, avoiding Vanara's reflection-based exception factory
and handle type converters. Its pinned input records retain the Windows ABI;
`WindowsConsoleAbiTests` checks their sizes, union offset, and blittability.
The adapter restores the original input mode on exit.
Clipboard sniffing is omitted, matching Evergreen's existing Windows behavior:
the terminal delivers typing and paste as input events, while explicit Copy
actions use the Windows clipboard. This also avoids Avalonia's private locator.

`TuiConsolePlatform` selects ANSI output on supported Windows 10+ systems,
enables virtual terminal processing, and restores the original output mode.
Consolonia still supplies its console base, output renderer, and non-Windows
adapter. Exercise changes through the packaged ConPTY smoke test as well as the
headless TUI regression tests.
