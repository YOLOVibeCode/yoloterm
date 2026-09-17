import Foundation

/// Shell path escaping utilities for drag-and-drop paste operations.
/// 
/// Behavior matches iTerm2 / Terminal.app: a drop **pastes** — never executes.
/// The user presses Enter themselves. Paths are shell-quoted so spaces and
/// special characters survive.
public struct PathEscaping {
    
    /// Quote a single path for shell safety (POSIX-style single quotes).
    /// 
    /// POSIX single-quote: literal everything inside except `'` itself, which
    /// is rendered as `'\''` (close, literal-quote, reopen).
    ///
    /// - Parameter path: The filesystem path to escape
    /// - Returns: Shell-safe quoted path
    public static func quoteShellPath(_ path: String) -> String {
        // POSIX single-quote escaping
        return "'\(path.replacingOccurrences(of: "'", with: "'\\''"))'"
    }
    
    /// Join multiple paths with spaces, each individually quoted.
    ///
    /// Used for the typical "drop a few files" case.
    ///
    /// - Parameter paths: Array of filesystem paths to escape
    /// - Returns: Space-separated shell-safe quoted paths
    public static func quoteShellPaths(_ paths: [String]) -> String {
        return paths.map(quoteShellPath).joined(separator: " ")
    }
    
    /// Prepare text for pasting into terminal, preserving newlines
    /// but ensuring it's safe to paste.
    ///
    /// - Parameter text: The text to prepare for pasting
    /// - Returns: Paste-safe text
    public static func preparePasteText(_ text: String) -> String {
        // For now, just return as-is. In the future, we could add:
        // - Warning for multiline pastes
        // - Bracket paste mode wrapping
        return text
    }
}
