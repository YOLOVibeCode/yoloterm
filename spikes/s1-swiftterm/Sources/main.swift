import AppKit
import SwiftTerm

@main
class AppDelegate: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var terminalView: LocalProcessTerminalView!
    var useMetal = true
    
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Parse command line args
        let args = CommandLine.arguments
        if args.contains("--coretext") {
            useMetal = false
            print("Using CoreText renderer")
        } else {
            print("Using Metal renderer")
        }
        
        // Create window
        window = NSWindow(
            contentRect: NSRect(x: 100, y: 100, width: 800, height: 600),
            styleMask: [.titled, .closable, .resizable, .miniaturizable],
            backing: .buffered,
            defer: false
        )
        window.title = "SwiftTerm Spike - \(useMetal ? "Metal" : "CoreText")"
        window.center()
        
        // Create terminal view
        terminalView = LocalProcessTerminalView(frame: window.contentView!.bounds)
        terminalView.autoresizingMask = [.width, .height]
        
        // Enable Metal renderer if requested
        if useMetal {
            do {
                try terminalView.setUseMetal(true)
                print("Metal renderer enabled successfully")
            } catch {
                print("Failed to enable Metal renderer: \(error)")
                print("Falling back to CoreText")
            }
        }
        
        // Set font
        if let sfMono = NSFont(name: "SF Mono", size: 13) {
            terminalView.font = sfMono
        }
        
        window.contentView?.addSubview(terminalView)
        window.makeKeyAndOrderFront(nil)
        
        // Start shell
        terminalView.startProcess(executable: "/bin/zsh", args: ["-l"])
        
        NSApp.activate(ignoringOtherApps: true)
    }
    
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return true
    }
}
