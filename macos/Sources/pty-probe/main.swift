import Foundation

/// pty-probe — Test binary for PTY lifecycle tests
///
/// Replaces TermGrid's flaky /bin/sh harness with a controlled test program.
/// 
/// Behavior:
/// - Prints "PROBE_READY\n" to stdout on startup
/// - Echoes stdin lines back to stdout
/// - Exits cleanly on "exit\n" input
/// - Exits on SIGTERM/SIGINT
///
/// This enables deterministic tests for:
/// - Natural exit (exit\n) fires exactly once
/// - Kill (SIGTERM) fires exactly once
/// - Output echo round-trip

import Darwin

// Set up signal handlers
signal(SIGTERM) { _ in
    print("PROBE_SIGNAL_TERM", terminator: "\n")
    fflush(stdout)
    exit(0)
}

signal(SIGINT) { _ in
    print("PROBE_SIGNAL_INT", terminator: "\n")
    fflush(stdout)
    exit(0)
}

// Print ready marker
print("PROBE_READY", terminator: "\n")
fflush(stdout)

// Echo loop
while let line = readLine(strippingNewline: false) {
    let trimmed = line.trimmingCharacters(in: .newlines)
    
    if trimmed == "exit" {
        print("PROBE_EXIT", terminator: "\n")
        fflush(stdout)
        break
    }
    
    // Echo back
    print("ECHO: \(trimmed)", terminator: "\n")
    fflush(stdout)
}

exit(0)
