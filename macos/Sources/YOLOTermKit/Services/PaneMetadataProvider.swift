import Foundation
import YOLOTermKit

/// Provides metadata about a pane's current state
@MainActor
public class PaneMetadataProvider {
    private let pid: Int32
    private var lastCwd: String?
    private var lastGitBranch: String?
    private var lastShell: String?
    private var lastSshHost: String?
    
    public init(pid: Int32) {
        self.pid = pid
    }
    
    /// Get current pane metadata
    public func getMetadata() -> PaneMetadata {
        // Get current working directory
        let cwd = getCurrentWorkingDirectory()
        
        // Get git branch if in a git repo
        let gitBranch = cwd.flatMap { getGitBranch(in: $0) }
        
        // Get shell name
        let shell = getShellName()
        
        // Check if SSH session
        let sshHost = getSSHHost()
        
        // Cache results
        lastCwd = cwd
        lastGitBranch = gitBranch
        lastShell = shell
        lastSshHost = sshHost
        
        return PaneMetadata(
            cwd: cwd,
            gitBranch: gitBranch,
            shell: shell,
            sshHost: sshHost
        )
    }
    
    // MARK: - Private Helpers
    
    private func getCurrentWorkingDirectory() -> String? {
        // Try to get CWD from the process tree
        // For now, use a simple approach with lsof
        let task = Process()
        task.executableURL = URL(fileURLWithPath: "/usr/sbin/lsof")
        task.arguments = ["-a", "-p", "\(pid)", "-d", "cwd", "-Fn"]
        
        let pipe = Pipe()
        task.standardOutput = pipe
        task.standardError = Pipe()
        
        do {
            try task.run()
            task.waitUntilExit()
            
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            let output = String(data: data, encoding: .utf8) ?? ""
            
            // Parse lsof output - format is "n/path/to/dir"
            for line in output.split(separator: "\n") {
                if line.starts(with: "n") {
                    return String(line.dropFirst())
                }
            }
        } catch {
            // Fallback to home directory
            return nil
        }
        
        return nil
    }
    
    private func getGitBranch(in directory: String) -> String? {
        // Check if we're in a git repository
        let task = Process()
        task.executableURL = URL(fileURLWithPath: "/usr/bin/git")
        task.arguments = ["-C", directory, "rev-parse", "--abbrev-ref", "HEAD"]
        task.currentDirectoryURL = URL(fileURLWithPath: directory)
        
        let pipe = Pipe()
        task.standardOutput = pipe
        task.standardError = Pipe()
        
        do {
            try task.run()
            task.waitUntilExit()
            
            guard task.terminationStatus == 0 else {
                return nil
            }
            
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            let branch = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines)
            return branch?.isEmpty == false ? branch : nil
        } catch {
            return nil
        }
    }
    
    private func getShellName() -> String? {
        // Get process name
        let task = Process()
        task.executableURL = URL(fileURLWithPath: "/bin/ps")
        task.arguments = ["-p", "\(pid)", "-o", "comm="]
        
        let pipe = Pipe()
        task.standardOutput = pipe
        task.standardError = Pipe()
        
        do {
            try task.run()
            task.waitUntilExit()
            
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            let name = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines)
            
            // Extract just the shell name (e.g., "zsh" from "/bin/zsh")
            if let name = name, !name.isEmpty {
                return (name as NSString).lastPathComponent
            }
        } catch {
            return nil
        }
        
        return nil
    }
    
    private func getSSHHost() -> String? {
        // Check if any ancestor process is ssh
        var currentPid = pid
        
        for _ in 0..<10 { // Check up to 10 levels up
            let task = Process()
            task.executableURL = URL(fileURLWithPath: "/bin/ps")
            task.arguments = ["-p", "\(currentPid)", "-o", "ppid=,comm="]
            
            let pipe = Pipe()
            task.standardOutput = pipe
            task.standardError = Pipe()
            
            do {
                try task.run()
                task.waitUntilExit()
                
                let data = pipe.fileHandleForReading.readDataToEndOfFile()
                let output = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
                
                let parts = output.split(separator: " ", maxSplits: 1)
                guard parts.count == 2 else { break }
                
                let parentPid = String(parts[0])
                let command = String(parts[1])
                
                // Check if this is an SSH process
                if command.contains("ssh") || command.contains("mosh") {
                    // Try to extract host from command
                    return extractSSHHost(from: command)
                }
                
                // Move up to parent
                guard let nextPid = Int32(parentPid) else { break }
                currentPid = nextPid
                
                // Stop if we hit init (pid 1)
                if currentPid <= 1 { break }
            } catch {
                break
            }
        }
        
        return nil
    }
    
    private func extractSSHHost(from command: String) -> String? {
        // Simple heuristic: look for user@host pattern or host pattern
        let components = command.split(separator: " ")
        for component in components {
            let str = String(component)
            if str.contains("@") {
                return str
            } else if !str.starts(with: "-") && str != "ssh" && str != "mosh" {
                // Likely a hostname
                return str
            }
        }
        return "remote"
    }
}
