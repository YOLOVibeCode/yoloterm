import Cocoa
import SwiftTerm
import YOLOTermKit

/// Manages a single pane's lifecycle
@MainActor
class PaneController {
    let paneId: String
    let paneView: PaneView
    private let terminalView: LocalProcessTerminalView
    private var metadataUpdateTimer: Timer?
    private var metadataProvider: PaneMetadataProvider?
    
    init(paneId: String, shell: String, cwd: String, env: [String: String]) {
        self.paneId = paneId
        
        // Create terminal view
        self.terminalView = LocalProcessTerminalView(frame: .zero)
        self.terminalView.font = NSFont.monospacedSystemFont(ofSize: 13, weight: .regular)
        
        // Enable Metal renderer
        do {
            try self.terminalView.setUseMetal(true)
        } catch {
            print("⚠️  Metal renderer failed for pane \(paneId), using CoreText: \(error)")
        }
        
        // Create pane view
        self.paneView = PaneView(paneId: paneId, terminalView: terminalView)
        
        // Start shell process
        terminalView.startProcess(
            executable: shell,
            args: ["-l"],  // Login shell
            environment: env.map { "\($0.key)=\($0.value)" },
            currentDirectory: cwd
        )
        
        // Note: SwiftTerm's LocalProcessTerminalView doesn't expose the child PID directly
        // For now, we'll create metadata provider without PID
        // In a future enhancement, we could use reflection or fork SwiftTerm
        // to expose the PID for better metadata tracking
        
        // Start metadata updates
        startMetadataUpdates()
    }
    
    func terminate() {
        metadataUpdateTimer?.invalidate()
        metadataUpdateTimer = nil
        terminalView.terminate()
    }
    
    private func startMetadataUpdates() {
        // Update metadata every 3 seconds
        metadataUpdateTimer = Timer.scheduledTimer(withTimeInterval: 3.0, repeats: true) { [weak self] _ in
            Task { @MainActor in
                self?.updateMetadata()
            }
        }
        
        // Initial update
        updateMetadata()
    }
    
    private func updateMetadata() {
        if let metadataProvider = metadataProvider {
            paneView.metadata = metadataProvider.getMetadata()
        } else {
            // Fallback if no PID available
            paneView.metadata = PaneMetadata(
                cwd: NSHomeDirectory(),
                gitBranch: nil,
                shell: nil,
                sshHost: nil
            )
        }
    }
}

/// Manages a tab's pane grid state
@MainActor
class TabController {
    let tabId: String
    let tilingView: TilingView
    private(set) var paneControllers: [PaneController] = []
    
    init(tabId: String, shell: String, cwd: String, env: [String: String]) {
        self.tabId = tabId
        self.tilingView = TilingView(frame: .zero)
        
        // Create initial pane
        let paneId = "\(tabId)-pane-1"
        let paneController = PaneController(paneId: paneId, shell: shell, cwd: cwd, env: env)
        paneControllers.append(paneController)
        tilingView.addPane(paneController.paneView)
    }
    
    func addPane(split: SplitDirection, shell: String, cwd: String, env: [String: String]) {
        let paneId = "\(tabId)-pane-\(paneControllers.count + 1)"
        let paneController = PaneController(paneId: paneId, shell: shell, cwd: cwd, env: env)
        paneControllers.append(paneController)
        tilingView.addPane(paneController.paneView)
        
        // Adjust preset based on pane count
        switch paneControllers.count {
        case 2:
            tilingView.setPreset(split == .horizontal ? .columns : .rows)
        case 3:
            tilingView.setPreset(.mainLeft)
        case 4:
            tilingView.setPreset(.grid)
        default:
            tilingView.setPreset(.auto)
        }
    }
    
    func closeCurrentPane() {
        guard paneControllers.count > 1 else { return }
        guard let focusedPane = tilingView.getFocusedPane() else { return }
        
        if let index = paneControllers.firstIndex(where: { $0.paneId == focusedPane.paneId }) {
            let controller = paneControllers[index]
            controller.terminate()
            paneControllers.remove(at: index)
            tilingView.removePane(focusedPane.paneId)
        }
    }
    
    func terminateAll() {
        for controller in paneControllers {
            controller.terminate()
        }
        paneControllers.removeAll()
    }
    
    enum SplitDirection {
        case horizontal
        case vertical
    }
}

@main
class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    var window: NSWindow!
    private var tabControllers: [NSWindow: TabController] = [:]
    private var env: [String: String] = [:]
    private var shell: String = "/bin/zsh"
    
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Setup environment
        setupEnvironment()
        
        // Create first window with first tab
        createNewWindow()
        
        // Setup menu
        setupMenu()
    }
    
    func applicationWillTerminate(_ notification: Notification) {
        // Terminate all tabs
        for (_, tabController) in tabControllers {
            tabController.terminateAll()
        }
    }
    
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return true
    }
    
    // MARK: - Window Management
    
    @MainActor
    private func createNewWindow() {
        let contentRect = NSRect(x: 0, y: 0, width: 1000, height: 700)
        let window = NSWindow(
            contentRect: contentRect,
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.center()
        window.title = "YOLOTerm"
        window.delegate = self
        window.tabbingMode = .preferred  // Enable native tabs
        window.makeKeyAndOrderFront(nil)
        
        // Create tab controller
        let tabId = "tab-\(UUID().uuidString)"
        let tabController = TabController(tabId: tabId, shell: shell, cwd: NSHomeDirectory(), env: env)
        tabControllers[window] = tabController
        
        window.contentView = tabController.tilingView
    }
    
    func windowWillClose(_ notification: Notification) {
        guard let window = notification.object as? NSWindow else { return }
        
        // Terminate tab
        if let tabController = tabControllers[window] {
            tabController.terminateAll()
            tabControllers.removeValue(forKey: window)
        }
    }
    
    // MARK: - Environment Setup
    
    private func setupEnvironment() {
        self.shell = getUserShell()
        
        let env = ProcessInfo.processInfo.environment
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
        self.env = policy.apply(to: env, version: YOLOTermVersion.version)
    }
    
    private func getUserShell() -> String {
        if let shell = ProcessInfo.processInfo.environment["SHELL"] {
            return shell
        }
        return "/bin/zsh"
    }
    
    // MARK: - Actions
    
    @MainActor
    @objc private func newTab(_ sender: Any?) {
        guard let mainWindow = NSApp.keyWindow ?? NSApp.windows.first else {
            createNewWindow()
            return
        }
        
        // Create new window in same tab group
        let newWindow = NSWindow(
            contentRect: mainWindow.frame,
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        newWindow.delegate = self
        newWindow.tabbingMode = .preferred
        
        // Create tab controller
        let tabId = "tab-\(UUID().uuidString)"
        let tabController = TabController(tabId: tabId, shell: shell, cwd: NSHomeDirectory(), env: env)
        tabControllers[newWindow] = tabController
        
        newWindow.contentView = tabController.tilingView
        mainWindow.addTabbedWindow(newWindow, ordered: .above)
        newWindow.makeKeyAndOrderFront(nil)
    }
    
    @MainActor
    @objc private func newPaneRight(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        tabController.addPane(split: .horizontal, shell: shell, cwd: NSHomeDirectory(), env: env)
    }
    
    @MainActor
    @objc private func newPaneDown(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        tabController.addPane(split: .vertical, shell: shell, cwd: NSHomeDirectory(), env: env)
    }
    
    @MainActor
    @objc private func closePane(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        if tabController.paneControllers.count > 1 {
            tabController.closeCurrentPane()
        } else {
            window.performClose(sender)
        }
    }
    
    @MainActor
    @objc private func findInTerminal(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window],
              let focusedPane = tabController.tilingView.getFocusedPane() else { return }
        
        // Get the terminal view and show find UI
        let terminalView = focusedPane.getTerminalView()
        terminalView.performFindPanelAction(sender)
    }
    
    @MainActor
    @objc private func zoomPane(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        tabController.tilingView.toggleZoom()
    }
    
    @MainActor
    @objc private func equalizeAllPanes(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        tabController.tilingView.equalize()
    }
    
    @MainActor
    @objc private func focusPaneUp(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        tabController.tilingView.focusPaneInDirection(.up)
    }
    
    @MainActor
    @objc private func focusPaneDown(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        tabController.tilingView.focusPaneInDirection(.down)
    }
    
    @MainActor
    @objc private func focusPaneLeft(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        tabController.tilingView.focusPaneInDirection(.left)
    }
    
    @MainActor
    @objc private func focusPaneRight(_ sender: Any?) {
        guard let window = NSApp.keyWindow,
              let tabController = tabControllers[window] else { return }
        
        tabController.tilingView.focusPaneInDirection(.right)
    }
    
    // MARK: - Menu Setup
    
    @MainActor
    private func setupMenu() {
        let mainMenu = NSMenu()
        
        // App menu
        let appMenu = NSMenu()
        appMenu.addItem(withTitle: "About YOLOTerm", action: nil, keyEquivalent: "")
        appMenu.addItem(NSMenuItem.separator())
        appMenu.addItem(withTitle: "Quit YOLOTerm", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        
        let appMenuItem = NSMenuItem()
        appMenuItem.submenu = appMenu
        mainMenu.addItem(appMenuItem)
        
        // File menu
        let fileMenu = NSMenu(title: "File")
        fileMenu.addItem(withTitle: "New Tab", action: #selector(newTab(_:)), keyEquivalent: "t")
        fileMenu.addItem(withTitle: "Close Tab", action: #selector(NSWindow.performClose(_:)), keyEquivalent: "w")
        fileMenu.addItem(NSMenuItem.separator())
        fileMenu.addItem(withTitle: "Split Right", action: #selector(newPaneRight(_:)), keyEquivalent: "d")
        fileMenu.addItem(withTitle: "Split Down", action: #selector(newPaneDown(_:)), keyEquivalent: "D")
        fileMenu.addItem(withTitle: "Close Pane", action: #selector(closePane(_:)), keyEquivalent: "w")
        
        let fileMenuItem = NSMenuItem()
        fileMenuItem.submenu = fileMenu
        mainMenu.addItem(fileMenuItem)
        
        // Edit menu
        let editMenu = NSMenu(title: "Edit")
        editMenu.addItem(withTitle: "Copy", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: "Paste", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: "Select All", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        
        let editMenuItem = NSMenuItem()
        editMenuItem.submenu = editMenu
        mainMenu.addItem(editMenuItem)
        
        // View menu
        let viewMenu = NSMenu(title: "View")
        viewMenu.addItem(withTitle: "Find...", action: #selector(findInTerminal(_:)), keyEquivalent: "f")
        viewMenu.addItem(NSMenuItem.separator())
        viewMenu.addItem(withTitle: "Zoom Pane", action: #selector(zoomPane(_:)), keyEquivalent: "\r")
        viewMenu.addItem(withTitle: "Equalize Panes", action: #selector(equalizeAllPanes(_:)), keyEquivalent: "=")
        viewMenu.addItem(NSMenuItem.separator())
        viewMenu.addItem(withTitle: "Increase Font Size", action: nil, keyEquivalent: "+")
        viewMenu.addItem(withTitle: "Decrease Font Size", action: nil, keyEquivalent: "-")
        viewMenu.addItem(withTitle: "Reset Font Size", action: nil, keyEquivalent: "0")
        
        let viewMenuItem = NSMenuItem()
        viewMenuItem.submenu = viewMenu
        mainMenu.addItem(viewMenuItem)
        
        // Window menu
        let windowMenu = NSMenu(title: "Window")
        windowMenu.addItem(withTitle: "Minimize", action: #selector(NSWindow.miniaturize(_:)), keyEquivalent: "m")
        windowMenu.addItem(withTitle: "Zoom", action: #selector(NSWindow.zoom(_:)), keyEquivalent: "")
        windowMenu.addItem(NSMenuItem.separator())
        windowMenu.addItem(withTitle: "Focus Pane Up", action: #selector(focusPaneUp(_:)), keyEquivalent: "")
        windowMenu.addItem(withTitle: "Focus Pane Down", action: #selector(focusPaneDown(_:)), keyEquivalent: "")
        windowMenu.addItem(withTitle: "Focus Pane Left", action: #selector(focusPaneLeft(_:)), keyEquivalent: "")
        windowMenu.addItem(withTitle: "Focus Pane Right", action: #selector(focusPaneRight(_:)), keyEquivalent: "")
        windowMenu.addItem(NSMenuItem.separator())
        windowMenu.addItem(withTitle: "Show Previous Tab", action: #selector(NSWindow.selectPreviousTab(_:)), keyEquivalent: "")
        windowMenu.addItem(withTitle: "Show Next Tab", action: #selector(NSWindow.selectNextTab(_:)), keyEquivalent: "")
        
        let windowMenuItem = NSMenuItem()
        windowMenuItem.submenu = windowMenu
        mainMenu.addItem(windowMenuItem)
        
        // Help menu
        let helpMenu = NSMenu(title: "Help")
        helpMenu.addItem(withTitle: "YOLOTerm Help", action: nil, keyEquivalent: "")
        
        let helpMenuItem = NSMenuItem()
        helpMenuItem.submenu = helpMenu
        mainMenu.addItem(helpMenuItem)
        
        NSApp.mainMenu = mainMenu
        NSApp.windowsMenu = windowMenu
    }
}
