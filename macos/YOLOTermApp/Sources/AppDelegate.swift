import Cocoa
import SwiftTerm
import YOLOTermKit

@main
class AppDelegate: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var terminalView: LocalProcessTerminalView!
    
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Create main window
        let contentRect = NSRect(x: 0, y: 0, width: 800, height: 600)
        window = NSWindow(
            contentRect: contentRect,
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.center()
        window.title = "YOLOTerm"
        window.makeKeyAndOrderFront(nil)
        
        // Create terminal view with Metal renderer
        terminalView = LocalProcessTerminalView(frame: contentRect)
        terminalView.font = NSFont.monospacedSystemFont(ofSize: 13, weight: .regular)
        
        // Enable Metal renderer
        do {
            try terminalView.setUseMetal(true)
            print("✅ Metal renderer enabled")
        } catch {
            print("⚠️  Metal renderer failed, using CoreText: \(error)")
        }
        
        window.contentView = terminalView
        
        // Get user's shell
        let shell = getUserShell()
        print("Spawning shell: \(shell)")
        
        // Apply environment policy
        var env = ProcessInfo.processInfo.environment
        let policy = EnvPolicy(
            set: [
                "TERM": "xterm-256color",
                "COLORTERM": "truecolor",
                "CLICOLOR": "1",
                "LSCOLORS": "ExGxBxDxCxEgEdxbxgxcxd",
                "TERM_PROGRAM": "YOLOTerm",
                "TERM_PROGRAM_VERSION": YOLOTermVersion.version
            ],
            remove: ["NO_COLOR", "FORCE_COLOR", "CLICOLOR_FORCE"]
        )
        env = policy.apply(to: env, version: YOLOTermVersion.version)
        
        // Start shell process
        terminalView.startProcess(
            executable: shell,
            args: ["-l"],  // Login shell
            environment: env.map { "\($0.key)=\($0.value)" },
            currentDirectory: NSHomeDirectory()
        )
    }
    
    func applicationWillTerminate(_ notification: Notification) {
        terminalView?.terminate()
    }
    
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return true
    }
    
    private func getUserShell() -> String {
        if let shell = ProcessInfo.processInfo.environment["SHELL"] {
            return shell
        }
        return "/bin/zsh"  // macOS default
    }
}
