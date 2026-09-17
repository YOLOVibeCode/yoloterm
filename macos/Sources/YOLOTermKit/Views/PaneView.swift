import Cocoa
import SwiftTerm

/// PaneView: wraps a terminal view with chrome (label bar)
public class PaneView: NSView {
    public let paneId: String
    private let terminalView: LocalProcessTerminalView
    private let labelBar: NSView
    private let labelText: NSTextField
    
    public var metadata: PaneMetadata? {
        didSet {
            updateLabel()
        }
    }
    
    public init(paneId: String, terminalView: LocalProcessTerminalView) {
        self.paneId = paneId
        self.terminalView = terminalView
        
        // Create label bar
        self.labelBar = NSView()
        self.labelBar.wantsLayer = true
        self.labelBar.layer?.backgroundColor = NSColor.controlBackgroundColor.cgColor
        
        // Create label text
        self.labelText = NSTextField()
        self.labelText.isEditable = false
        self.labelText.isBordered = false
        self.labelText.backgroundColor = .clear
        self.labelText.font = NSFont.systemFont(ofSize: 11)
        self.labelText.textColor = .secondaryLabelColor
        self.labelText.stringValue = "~"
        
        super.init(frame: .zero)
        
        // Add subviews
        addSubview(labelBar)
        labelBar.addSubview(labelText)
        addSubview(terminalView)
        
        // Disable autoresizing masks
        labelBar.translatesAutoresizingMaskIntoConstraints = false
        labelText.translatesAutoresizingMaskIntoConstraints = false
        terminalView.translatesAutoresizingMaskIntoConstraints = false
        
        setupConstraints()
        setupDragAndDrop()
    }
    
    private func setupDragAndDrop() {
        // Register for drag types
        registerForDraggedTypes([
            .fileURL,
            .string
        ])
    }
    
    public override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        // Accept files and text
        let pasteboard = sender.draggingPasteboard
        
        if pasteboard.availableType(from: [.fileURL]) != nil {
            return .copy
        }
        
        if pasteboard.availableType(from: [.string]) != nil {
            return .copy
        }
        
        return []
    }
    
    public override func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        let pasteboard = sender.draggingPasteboard
        
        // Handle file/folder drops
        if let fileURLs = pasteboard.readObjects(forClasses: [NSURL.self], options: nil) as? [URL] {
            let paths = fileURLs.map { $0.path }
            let escapedPaths = PathEscaping.quoteShellPaths(paths)
            terminalView.send(txt: escapedPaths)
            return true
        }
        
        // Handle text drops
        if let text = pasteboard.string(forType: .string) {
            let preparedText = PathEscaping.preparePasteText(text)
            terminalView.send(txt: preparedText)
            return true
        }
        
        return false
    }
    
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }
    
    private func setupConstraints() {
        NSLayoutConstraint.activate([
            // Label bar at top
            labelBar.topAnchor.constraint(equalTo: topAnchor),
            labelBar.leadingAnchor.constraint(equalTo: leadingAnchor),
            labelBar.trailingAnchor.constraint(equalTo: trailingAnchor),
            labelBar.heightAnchor.constraint(equalToConstant: 20),
            
            // Label text inside label bar
            labelText.leadingAnchor.constraint(equalTo: labelBar.leadingAnchor, constant: 8),
            labelText.trailingAnchor.constraint(equalTo: labelBar.trailingAnchor, constant: -8),
            labelText.centerYAnchor.constraint(equalTo: labelBar.centerYAnchor),
            
            // Terminal view below label bar
            terminalView.topAnchor.constraint(equalTo: labelBar.bottomAnchor),
            terminalView.leadingAnchor.constraint(equalTo: leadingAnchor),
            terminalView.trailingAnchor.constraint(equalTo: trailingAnchor),
            terminalView.bottomAnchor.constraint(equalTo: bottomAnchor)
        ])
    }
    
    private func updateLabel() {
        guard let metadata = metadata else {
            labelText.stringValue = "~"
            return
        }
        
        var parts: [String] = []
        
        // CWD
        if let cwd = metadata.cwd {
            let cwdPath = (cwd as NSString).lastPathComponent
            parts.append(cwdPath)
        }
        
        // Git branch
        if let branch = metadata.gitBranch {
            parts.append("[\(branch)]")
        }
        
        // Shell
        if let shell = metadata.shell {
            parts.append(shell)
        }
        
        // SSH host
        if let sshHost = metadata.sshHost {
            parts.append("ssh → \(sshHost)")
        }
        
        labelText.stringValue = parts.isEmpty ? "~" : parts.joined(separator: " · ")
    }
    
    public func getTerminalView() -> LocalProcessTerminalView {
        return terminalView
    }
}

/// Pane metadata for display
public struct PaneMetadata {
    public let cwd: String?
    public let gitBranch: String?
    public let shell: String?
    public let sshHost: String?
    
    public init(cwd: String?, gitBranch: String?, shell: String?, sshHost: String?) {
        self.cwd = cwd
        self.gitBranch = gitBranch
        self.shell = shell
        self.sshHost = sshHost
    }
}
