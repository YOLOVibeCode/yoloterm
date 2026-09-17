import Foundation

/// ShellPluginInstaller manages the installation of YOLOTerm shell integration plugins
/// into user's shell RC files.
///
/// Supports: zsh, bash, fish, pwsh
/// Provides: one-click install, uninstall, and status detection
@MainActor
public final class ShellPluginInstaller {
    
    // MARK: - Models
    
    public enum Shell: String, CaseIterable, Sendable {
        case zsh
        case bash
        case fish
        case pwsh
        
        public var displayName: String {
            switch self {
            case .zsh: return "Zsh"
            case .bash: return "Bash"
            case .fish: return "Fish"
            case .pwsh: return "PowerShell"
            }
        }
        
        public var rcFile: String {
            switch self {
            case .zsh: return ".zshrc"
            case .bash: return ".bashrc"
            case .fish: return ".config/fish/config.fish"
            case .pwsh: return ".config/powershell/profile.ps1"
            }
        }
        
        public var pluginFilename: String {
            return "yoloterm.\(rawValue)"
        }
        
        public var executable: String {
            rawValue
        }
    }
    
    public struct InstallStatus {
        public let shell: Shell
        public let isInstalled: Bool
        public let shellExists: Bool
        public let pluginFileExists: Bool
        
        public var canInstall: Bool {
            shellExists && pluginFileExists && !isInstalled
        }
        
        public var canUninstall: Bool {
            isInstalled
        }
    }
    
    // MARK: - Configuration
    
    private let pluginsSourceDirectory: URL
    
    public init(pluginsSourceDirectory: URL? = nil) {
        if let pluginsSourceDirectory {
            self.pluginsSourceDirectory = pluginsSourceDirectory
        } else {
            // Default to shared/shell-plugins/ relative to project root
            let currentFile = URL(fileURLWithPath: #file)
            let projectRoot = currentFile
                .deletingLastPathComponent() // Persistence
                .deletingLastPathComponent() // YOLOTermKit
                .deletingLastPathComponent() // Sources
                .deletingLastPathComponent() // macos
            self.pluginsSourceDirectory = projectRoot
                .appendingPathComponent("shared", isDirectory: true)
                .appendingPathComponent("shell-plugins", isDirectory: true)
        }
    }
    
    // MARK: - Public API
    
    /// Get installation status for all supported shells.
    public func getStatus() -> [Shell: InstallStatus] {
        var result: [Shell: InstallStatus] = [:]
        
        for shell in Shell.allCases {
            result[shell] = getStatus(for: shell)
        }
        
        return result
    }
    
    /// Get installation status for a specific shell.
    public func getStatus(for shell: Shell) -> InstallStatus {
        let shellExists = isShellInstalled(shell)
        let pluginFileExists = pluginSourceFile(for: shell) != nil
        let isInstalled = isPluginInstalled(for: shell)
        
        return InstallStatus(
            shell: shell,
            isInstalled: isInstalled,
            shellExists: shellExists,
            pluginFileExists: pluginFileExists
        )
    }
    
    /// Install the plugin for a specific shell.
    public func install(shell: Shell) throws {
        guard let pluginSource = pluginSourceFile(for: shell) else {
            throw InstallerError.pluginFileNotFound(shell: shell)
        }
        
        let rcFileURL = rcFileURL(for: shell)
        
        // Ensure the RC file's directory exists
        let rcDirectory = rcFileURL.deletingLastPathComponent()
        try FileManager.default.createDirectory(at: rcDirectory, withIntermediateDirectories: true)
        
        // Create RC file if it doesn't exist
        if !FileManager.default.fileExists(atPath: rcFileURL.path) {
            FileManager.default.createFile(atPath: rcFileURL.path, contents: Data())
        }
        
        // Read current RC file content
        let currentContent = try String(contentsOf: rcFileURL, encoding: .utf8)
        
        // Check if already installed
        if currentContent.contains(markerComment(for: shell)) {
            throw InstallerError.alreadyInstalled(shell: shell)
        }
        
        // Generate the source line
        let sourceLine = generateSourceLine(for: shell, pluginPath: pluginSource.path)
        
        // Append to RC file
        let marker = markerComment(for: shell)
        let newContent = currentContent + "\n" + marker + "\n" + sourceLine + "\n"
        
        // Create backup
        let backupURL = rcFileURL.appendingPathExtension("yoloterm-backup")
        try? FileManager.default.removeItem(at: backupURL)
        try FileManager.default.copyItem(at: rcFileURL, to: backupURL)
        
        // Write new content
        try newContent.write(to: rcFileURL, atomically: true, encoding: .utf8)
    }
    
    /// Uninstall the plugin for a specific shell.
    public func uninstall(shell: Shell) throws {
        let rcFileURL = rcFileURL(for: shell)
        
        guard FileManager.default.fileExists(atPath: rcFileURL.path) else {
            throw InstallerError.rcFileNotFound(shell: shell)
        }
        
        // Read current RC file content
        var currentContent = try String(contentsOf: rcFileURL, encoding: .utf8)
        
        // Find and remove the YOLOTerm section
        let marker = markerComment(for: shell)
        
        guard currentContent.contains(marker) else {
            throw InstallerError.notInstalled(shell: shell)
        }
        
        // Remove lines between marker and next blank line or EOF
        let lines = currentContent.components(separatedBy: .newlines)
        var newLines: [String] = []
        var skipMode = false
        
        for line in lines {
            if line.contains(marker) {
                skipMode = true
                continue
            }
            
            if skipMode {
                // Stop skipping on blank line or another comment/command
                if line.trimmingCharacters(in: .whitespaces).isEmpty {
                    skipMode = false
                } else if !line.hasPrefix(commentPrefix(for: shell)) && !line.contains("source") && !line.contains("yoloterm") {
                    skipMode = false
                }
            }
            
            if !skipMode {
                newLines.append(line)
            }
        }
        
        // Create backup
        let backupURL = rcFileURL.appendingPathExtension("yoloterm-backup")
        try? FileManager.default.removeItem(at: backupURL)
        try FileManager.default.copyItem(at: rcFileURL, to: backupURL)
        
        // Write cleaned content
        let newContent = newLines.joined(separator: "\n")
        try newContent.write(to: rcFileURL, atomically: true, encoding: .utf8)
    }
    
    // MARK: - Private Helpers
    
    private func isShellInstalled(_ shell: Shell) -> Bool {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/which")
        process.arguments = [shell.executable]
        process.standardOutput = Pipe()
        process.standardError = Pipe()
        
        do {
            try process.run()
            process.waitUntilExit()
            return process.terminationStatus == 0
        } catch {
            return false
        }
    }
    
    private func pluginSourceFile(for shell: Shell) -> URL? {
        let url = pluginsSourceDirectory.appendingPathComponent(shell.pluginFilename)
        return FileManager.default.fileExists(atPath: url.path) ? url : nil
    }
    
    private func rcFileURL(for shell: Shell) -> URL {
        let home = FileManager.default.homeDirectoryForCurrentUser
        return home.appendingPathComponent(shell.rcFile)
    }
    
    private func isPluginInstalled(for shell: Shell) -> Bool {
        let rcFileURL = rcFileURL(for: shell)
        
        guard FileManager.default.fileExists(atPath: rcFileURL.path),
              let content = try? String(contentsOf: rcFileURL, encoding: .utf8) else {
            return false
        }
        
        return content.contains(markerComment(for: shell))
    }
    
    private func markerComment(for shell: Shell) -> String {
        let prefix = commentPrefix(for: shell)
        return "\(prefix) YOLOTerm shell integration"
    }
    
    private func commentPrefix(for shell: Shell) -> String {
        switch shell {
        case .zsh, .bash, .fish:
            return "#"
        case .pwsh:
            return "#"
        }
    }
    
    private func generateSourceLine(for shell: Shell, pluginPath: String) -> String {
        switch shell {
        case .zsh, .bash:
            return "source \"\(pluginPath)\""
        case .fish:
            return "source \"\(pluginPath)\""
        case .pwsh:
            return ". \"\(pluginPath)\""
        }
    }
    
    // MARK: - Errors
    
    public enum InstallerError: LocalizedError {
        case pluginFileNotFound(shell: Shell)
        case rcFileNotFound(shell: Shell)
        case alreadyInstalled(shell: Shell)
        case notInstalled(shell: Shell)
        
        public var errorDescription: String? {
            switch self {
            case .pluginFileNotFound(let shell):
                return "Plugin file for \(shell.displayName) not found"
            case .rcFileNotFound(let shell):
                return "RC file for \(shell.displayName) not found"
            case .alreadyInstalled(let shell):
                return "YOLOTerm plugin is already installed for \(shell.displayName)"
            case .notInstalled(let shell):
                return "YOLOTerm plugin is not installed for \(shell.displayName)"
            }
        }
    }
}
