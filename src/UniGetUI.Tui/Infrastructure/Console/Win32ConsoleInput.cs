// Adapted from GUI.cs (MIT license)

// ReSharper disable All
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Vanara.PInvoke.Kernel32;

namespace UniGetUI.Tui.Infrastructure
{
    [SupportedOSPlatform("windows")]
    internal sealed partial class Win32ConsoleInput
    {

        private readonly nint _inputHandle;
        readonly CONSOLE_INPUT_MODE originalConsoleMode;

        public Win32ConsoleInput()
        {
            _inputHandle = GetStdHandle(-10);
            originalConsoleMode = ConsoleMode;
            var newConsoleMode = originalConsoleMode;
            newConsoleMode |= (CONSOLE_INPUT_MODE.ENABLE_MOUSE_INPUT |
                               CONSOLE_INPUT_MODE.ENABLE_EXTENDED_FLAGS);
            newConsoleMode &= ~CONSOLE_INPUT_MODE.ENABLE_QUICK_EDIT_MODE;
            newConsoleMode &= ~CONSOLE_INPUT_MODE.ENABLE_PROCESSED_INPUT;
            // Harden input mode: disable line/echo (this fixes CTRL+S freezes)
            newConsoleMode &= ~CONSOLE_INPUT_MODE.ENABLE_LINE_INPUT;
            newConsoleMode &= ~CONSOLE_INPUT_MODE.ENABLE_ECHO_INPUT;
            //newConsoleMode |= CONSOLE_INPUT_MODE.ENABLE_VIRTUAL_TERMINAL_INPUT;
            ConsoleMode = newConsoleMode;
        }

        public CONSOLE_INPUT_MODE ConsoleMode
        {
            get
            {
                if (!GetConsoleMode(_inputHandle, out CONSOLE_INPUT_MODE v))
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                return v;
            }
            set
            {
                if (!SetConsoleMode(_inputHandle, value))
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }

        internal void RestoreConsoleMode() => SetConsoleMode(_inputHandle, originalConsoleMode);

        internal static bool HasConsoleWindow => GetConsoleWindow() != 0;

        internal unsafe bool WriteInput(INPUT_RECORD[] records)
        {
            fixed (INPUT_RECORD* buffer = records)
                return WriteConsoleInputW(_inputHandle, buffer, (uint)records.Length, out _);
        }

        const int bufferSize = 0xffff;
        private static readonly INPUT_RECORD[] s_inputBuffer = new INPUT_RECORD[bufferSize];

        public unsafe INPUT_RECORD[] ReadConsoleInput()
        {
            uint numberEventsRead;
            fixed (INPUT_RECORD* buffer = s_inputBuffer)
                if (!ReadConsoleInputW(_inputHandle, buffer, bufferSize, out numberEventsRead))
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
            INPUT_RECORD[] recordsToReturn = new INPUT_RECORD[numberEventsRead];
            Array.Copy(s_inputBuffer, recordsToReturn, numberEventsRead);
            return recordsToReturn;
        }

        // INPUT_RECORD is the Windows ABI union (20 bytes on x64 and ARM64).
        // Pin the blittable buffer only for the synchronous call; no native ownership escapes.
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool ReadConsoleInputW(nint handle, INPUT_RECORD* buffer, uint count, out uint read);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool WriteConsoleInputW(nint handle, INPUT_RECORD* buffer, uint count, out uint written);

        [LibraryImport("kernel32.dll")]
        private static partial nint GetStdHandle(int handle);

        [LibraryImport("kernel32.dll")]
        private static partial nint GetConsoleWindow();

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetConsoleMode(nint handle, out CONSOLE_INPUT_MODE mode);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetConsoleMode(nint handle, CONSOLE_INPUT_MODE mode);
    }
}
