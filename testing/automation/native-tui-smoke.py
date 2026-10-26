"""Exercise the shipped Windows executable through a real ConPTY, with fake data only."""

import argparse
import os
from pathlib import Path
import queue
import tempfile
import threading
import time

import pyte
from winpty import PtyProcess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--executable", type=Path, required=True)
    args = parser.parse_args()
    executable = args.executable.resolve(strict=True)
    with tempfile.TemporaryDirectory(prefix="eepm-native-tui-") as sandbox:
        process = PtyProcess.spawn(
            [str(executable), "tui", "--fake-data", "--fake-data-dir", sandbox, "--page", "installed"],
            dimensions=(50, 160),
            env={**os.environ, "COLORTERM": "truecolor"},
        )
        chunks = queue.Queue()

        def read_output():
            try:
                while process.isalive():
                    chunks.put(process.read(32768))
            except (EOFError, OSError):
                pass

        threading.Thread(target=read_output, daemon=True).start()

        class TerminalScreen(pyte.Screen):
            def write_process_input(self, data):
                process.write(data)

        screen = TerminalScreen(160, 50)
        stream = pyte.Stream(screen)

        def expect(*texts, timeout=45):
            deadline = time.monotonic() + timeout
            while time.monotonic() < deadline:
                try:
                    stream.feed(chunks.get(timeout=0.1))
                    while not chunks.empty():
                        stream.feed(chunks.get_nowait())
                except queue.Empty:
                    pass
                rendered = "\n".join(screen.display)
                if all(text in rendered for text in texts):
                    print("Verified: " + ", ".join(texts), flush=True)
                    return
                if not process.isalive():
                    raise AssertionError("Packaged terminal exited prematurely:\n" + rendered)
            raise AssertionError(f"Timed out waiting for {texts}:\n" + "\n".join(screen.display))

        try:
            expect("[FAKE DATA]", "Installed Packages", "Contoso Editor", "Woodgrove Vault")
            # Filter a compiled string column, open its details overlay, then clear the filter.
            process.write("\x06Woodgrove\r")  # Ctrl+F, query, Enter
            expect("1 of 16 shown")
            process.write("\r")
            expect("Package details", "Woodgrove Bank")
            process.write("\x1b")
            process.write("\x06\x01\x08\r")  # Ctrl+F, Ctrl+A, Backspace, Enter
            expect("16 packages")
            # Global page navigation and an updates grid exercise a different row shape.
            process.write("\x1b2")  # Alt+2
            expect("Software Updates", "Northwind Terminal", "1.20.1")
            process.write("\x1b7")  # Alt+7
            expect("Evergreen Endpoint | APPS Log")
            process.write("\x11")  # Ctrl+Q
            deadline = time.monotonic() + 15
            while process.isalive() and time.monotonic() < deadline:
                time.sleep(0.1)
            if process.isalive():
                raise AssertionError("Packaged terminal did not shut down after Ctrl+Q")
            if process.exitstatus != 0:
                raise AssertionError(f"Packaged terminal exit code: {process.exitstatus}")
            print("Packaged terminal smoke test passed", flush=True)
        finally:
            process.close(force=True)


if __name__ == "__main__":
    main()
