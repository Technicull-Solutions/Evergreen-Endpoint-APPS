using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Vanara.PInvoke.Kernel32;

namespace UniGetUI.Tui.Tests;

public class WindowsConsoleAbiTests
{
    [Fact]
    public void ConsoleInputRecordsMatchWindowsAbi()
    {
        Assert.Equal(20, Unsafe.SizeOf<INPUT_RECORD>());
        Assert.Equal(4, Marshal.OffsetOf<INPUT_RECORD>(nameof(INPUT_RECORD.Event)).ToInt32());
        Assert.Equal(16, Unsafe.SizeOf<KEY_EVENT_RECORD>());
        Assert.Equal(16, Unsafe.SizeOf<MOUSE_EVENT_RECORD>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<INPUT_RECORD>());
    }
}
