import SwiftUI
import YOLOTermKit

/// SettingsWindow provides a comprehensive settings UI per SPEC §9.
/// Sections: Appearance, Behavior, Keybindings, History, Advanced
struct SettingsWindow: View {
    @StateObject private var settings = AppSettings.shared
    
    var body: some View {
        TabView {
            AppearanceSettings()
                .tabItem {
                    Label("Appearance", systemImage: "paintbrush")
                }
            
            BehaviorSettings()
                .tabItem {
                    Label("Behavior", systemImage: "gearshape")
                }
            
            KeybindingsSettings()
                .tabItem {
                    Label("Keybindings", systemImage: "keyboard")
                }
            
            HistorySettings()
                .tabItem {
                    Label("History", systemImage: "clock")
                }
            
            ShellPluginSettings()
                .tabItem {
                    Label("Shell Integration", systemImage: "terminal")
                }
            
            AdvancedSettings()
                .tabItem {
                    Label("Advanced", systemImage: "wrench")
                }
        }
        .frame(width: 600, height: 500)
    }
}

// MARK: - Appearance Settings

struct AppearanceSettings: View {
    @StateObject private var settings = AppSettings.shared
    @State private var availableThemes: [String] = []
    @State private var showingImportError = false
    @State private var importErrorMessage = ""
    @State private var showingImportSuccess = false
    @State private var importedThemeName = ""
    
    var body: some View {
        Form {
            Section("Theme") {
                Picker("Theme", selection: $settings.themeName) {
                    ForEach(availableThemes, id: \.self) { theme in
                        Text(theme).tag(theme)
                    }
                }
                .pickerStyle(.menu)
                
                Button("Import Theme...") {
                    importTheme()
                }
                .help("Import themes from iTerm2, Windows Terminal, or Ghostty")
            }
            
            Section("Font") {
                HStack {
                    Text("Font")
                    Spacer()
                    Text(settings.fontName)
                        .foregroundColor(.secondary)
                    Button("Change...") {
                        showFontPanel()
                    }
                }
                
                Stepper("Size: \(Int(settings.fontSize))", value: $settings.fontSize, in: 8...36)
            }
            
            Section("Cursor") {
                Picker("Style", selection: $settings.cursorStyle) {
                    Text("Block").tag(CursorStyle.block)
                    Text("Underline").tag(CursorStyle.underline)
                    Text("Vertical Bar").tag(CursorStyle.verticalBar)
                }
                
                Toggle("Blink", isOn: $settings.cursorBlink)
            }
            
            Section("Window") {
                Slider(value: $settings.windowOpacity, in: 0.5...1.0) {
                    Text("Window Opacity")
                }
                
                Toggle("Show tabs bar", isOn: $settings.showTabsBar)
            }
        }
        .formStyle(.grouped)
        .padding()
        .onAppear {
            loadAvailableThemes()
        }
        .alert("Import Error", isPresented: $showingImportError) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(importErrorMessage)
        }
        .alert("Theme Imported", isPresented: $showingImportSuccess) {
            Button("OK", role: .cancel) {}
        } message: {
            Text("Successfully imported theme: \(importedThemeName)")
        }
    }
    
    private func loadAvailableThemes() {
        // Load themes from contracts/themes/
        let themesURL = Bundle.main.resourceURL?.deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("contracts/themes")
        
        if let themesURL = themesURL,
           let enumerator = FileManager.default.enumerator(at: themesURL, includingPropertiesForKeys: nil) {
            availableThemes = enumerator.compactMap { url in
                guard let fileURL = url as? URL,
                      fileURL.pathExtension == "json",
                      fileURL.lastPathComponent != "theme-schema.json" else {
                    return nil
                }
                return fileURL.deletingPathExtension().lastPathComponent
            }.sorted()
        }
        
        if availableThemes.isEmpty {
            availableThemes = ["terminal-default", "dracula", "nord", "gruvbox-dark", "tokyo-night", "catppuccin-mocha"]
        }
    }
    
    private func showFontPanel() {
        let fontManager = NSFontManager.shared
        let currentFont = NSFont(name: settings.fontName, size: settings.fontSize) ?? NSFont.monospacedSystemFont(ofSize: settings.fontSize, weight: .regular)
        fontManager.setSelectedFont(currentFont, isMultiple: false)
        fontManager.orderFrontFontPanel(nil)
    }
    
    private func importTheme() {
        let panel = NSOpenPanel()
        panel.title = "Import Theme"
        panel.message = "Select a theme file to import"
        panel.allowedContentTypes = [.json, .propertyList]
        panel.allowsOtherFileTypes = true
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        
        panel.begin { response in
            guard response == .OK, let url = panel.url else { return }
            
            let importer = ThemeImporter()
            do {
                let theme = try importer.importTheme(from: url)
                
                // Save to contracts/themes/
                let themesURL = Bundle.main.resourceURL?.deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("contracts/themes")
                
                if let themesURL = themesURL {
                    try importer.saveTheme(theme, to: themesURL)
                    importedThemeName = theme.name
                    showingImportSuccess = true
                    loadAvailableThemes()
                    settings.themeName = theme.id
                } else {
                    importErrorMessage = "Could not find themes directory"
                    showingImportError = true
                }
            } catch {
                importErrorMessage = error.localizedDescription
                showingImportError = true
            }
        }
    }
}

enum CursorStyle: String, Codable {
    case block
    case underline
    case verticalBar
}

// MARK: - Behavior Settings

struct BehaviorSettings: View {
    @StateObject private var settings = AppSettings.shared
    
    var body: some View {
        Form {
            Section("Shell") {
                Picker("Default shell", selection: $settings.defaultShell) {
                    Text("/bin/zsh").tag("/bin/zsh")
                    Text("/bin/bash").tag("/bin/bash")
                    Text("/opt/homebrew/bin/fish").tag("/opt/homebrew/bin/fish")
                    Text("Custom...").tag("custom")
                }
                
                if settings.defaultShell == "custom" {
                    TextField("Shell path", text: $settings.customShellPath)
                        .font(.system(.body, design: .monospaced))
                }
                
                Toggle("Login shell", isOn: $settings.loginShell)
            }
            
            Section("Scrollback") {
                Stepper("Lines: \(settings.scrollbackLines)", value: $settings.scrollbackLines, in: 1000...100000, step: 1000)
            }
            
            Section("Behavior") {
                Toggle("Copy on select", isOn: $settings.copyOnSelect)
                Toggle("Paste on right-click", isOn: $settings.pasteOnRightClick)
                Toggle("Confirm quit", isOn: $settings.confirmQuit)
            }
            
            Section("Bell") {
                Toggle("Visual bell", isOn: $settings.visualBell)
                Toggle("Audible bell", isOn: $settings.audibleBell)
            }
            
            Section("Startup") {
                Picker("On startup", selection: $settings.startupBehavior) {
                    Text("Restore previous session").tag(StartupBehavior.restore)
                    Text("New window").tag(StartupBehavior.newWindow)
                    Text("Nothing").tag(StartupBehavior.nothing)
                }
            }
        }
        .formStyle(.grouped)
        .padding()
    }
}

enum StartupBehavior: String, Codable {
    case restore
    case newWindow
    case nothing
}

// MARK: - Keybindings Settings

struct KeybindingsSettings: View {
    @State private var keybindings: [Keybinding] = []
    @State private var searchQuery: String = ""
    
    var body: some View {
        VStack {
            HStack {
                Image(systemName: "magnifyingglass")
                    .foregroundColor(.secondary)
                TextField("Search keybindings...", text: $searchQuery)
                    .textFieldStyle(.roundedBorder)
            }
            .padding()
            
            List(filteredKeybindings) { keybinding in
                HStack {
                    Text(keybinding.action)
                        .frame(width: 200, alignment: .leading)
                    
                    Spacer()
                    
                    Text(keybinding.shortcut)
                        .font(.system(.body, design: .monospaced))
                        .foregroundColor(.secondary)
                    
                    Button("Edit") {
                        // TODO: Show keybinding editor
                    }
                    .buttonStyle(.borderless)
                }
            }
            
            HStack {
                Button("Reset to Defaults") {
                    resetKeybindings()
                }
                
                Spacer()
                
                Button("Export...") {
                    exportKeybindings()
                }
                
                Button("Import...") {
                    importKeybindings()
                }
            }
            .padding()
        }
    }
    
    private var filteredKeybindings: [Keybinding] {
        if searchQuery.isEmpty {
            return keybindings
        }
        return keybindings.filter { $0.action.localizedCaseInsensitiveContains(searchQuery) }
    }
    
    private func resetKeybindings() {
        // Load default keybindings from contracts/keymap.json
    }
    
    private func exportKeybindings() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.json]
        panel.nameFieldStringValue = "yoloterm-keybindings.json"
        panel.begin { response in
            if response == .OK, let url = panel.url {
                // Export keybindings
            }
        }
    }
    
    private func importKeybindings() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.json]
        panel.begin { response in
            if response == .OK, let url = panel.url {
                // Import keybindings
            }
        }
    }
}

struct Keybinding: Identifiable {
    let id = UUID()
    let action: String
    let shortcut: String
}

// MARK: - History Settings

struct HistorySettings: View {
    @StateObject private var settings = AppSettings.shared
    @State private var historyCount: Int = 0
    @State private var historySize: String = "Unknown"
    
    var body: some View {
        Form {
            Section("History Storage") {
                Toggle("Enable command history", isOn: $settings.historyEnabled)
                
                if settings.historyEnabled {
                    Picker("Retention period", selection: $settings.historyRetentionDays) {
                        Text("7 days").tag(7)
                        Text("30 days").tag(30)
                        Text("90 days").tag(90)
                        Text("1 year").tag(365)
                        Text("Forever").tag(0)
                    }
                    
                    HStack {
                        Text("History count")
                        Spacer()
                        Text("\(historyCount) commands")
                            .foregroundColor(.secondary)
                    }
                    
                    HStack {
                        Text("Database size")
                        Spacer()
                        Text(historySize)
                            .foregroundColor(.secondary)
                    }
                    
                    Button("Clear History...") {
                        showClearHistoryConfirmation()
                    }
                }
            }
            
            Section("Privacy") {
                Toggle("Redact sensitive data", isOn: $settings.historyRedactionEnabled)
                
                if settings.historyRedactionEnabled {
                    Text("Patterns like passwords, API keys, and tokens will be redacted before storage")
                        .font(.caption)
                        .foregroundColor(.secondary)
                    
                    Button("View Redaction Patterns...") {
                        showRedactionPatterns()
                    }
                }
            }
        }
        .formStyle(.grouped)
        .padding()
        .onAppear {
            updateHistoryStats()
        }
    }
    
    private func updateHistoryStats() {
        // TODO: Query HistoryStore for count and size
        historyCount = 0
        historySize = "0 KB"
    }
    
    private func showClearHistoryConfirmation() {
        let alert = NSAlert()
        alert.messageText = "Clear Command History?"
        alert.informativeText = "This will permanently delete all command history. This cannot be undone."
        alert.alertStyle = .warning
        alert.addButton(withTitle: "Cancel")
        alert.addButton(withTitle: "Clear History")
        
        if alert.runModal() == .alertSecondButtonReturn {
            // Clear history
            updateHistoryStats()
        }
    }
    
    private func showRedactionPatterns() {
        // TODO: Show redaction patterns viewer
    }
}

// MARK: - Shell Plugin Settings

struct ShellPluginSettings: View {
    @StateObject private var installer = ShellPluginInstallerViewModel()
    
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Shell Integration")
                .font(.headline)
            
            Text("Install YOLOTerm shell plugins to enable advanced features like command history with exit codes, durations, and working directory tracking.")
                .foregroundColor(.secondary)
            
            Divider()
            
            ForEach(Array(installer.status.keys.sorted(by: { $0.displayName < $1.displayName })), id: \.self) { shell in
                if let status = installer.status[shell] {
                    ShellPluginRow(
                        shell: shell,
                        status: status,
                        installer: installer
                    )
                }
            }
            
            Spacer()
        }
        .padding()
        .onAppear {
            installer.refreshStatus()
        }
    }
}

struct ShellPluginRow: View {
    let shell: ShellPluginInstaller.Shell
    let status: ShellPluginInstaller.InstallStatus
    @ObservedObject var installer: ShellPluginInstallerViewModel
    
    var body: some View {
        HStack {
            VStack(alignment: .leading) {
                Text(shell.displayName)
                    .font(.headline)
                
                Text(statusText)
                    .font(.caption)
                    .foregroundColor(statusColor)
            }
            
            Spacer()
            
            if status.canInstall {
                Button("Install") {
                    installer.install(shell: shell)
                }
            } else if status.canUninstall {
                Button("Uninstall") {
                    installer.uninstall(shell: shell)
                }
            } else if !status.shellExists {
                Text("Not installed")
                    .foregroundColor(.secondary)
            }
        }
        .padding()
        .background(Color(nsColor: .controlBackgroundColor))
        .cornerRadius(8)
    }
    
    private var statusText: String {
        if !status.shellExists {
            return "Shell not found on system"
        } else if status.isInstalled {
            return "Installed and active"
        } else {
            return "Not installed"
        }
    }
    
    private var statusColor: SwiftUI.Color {
        if !status.shellExists {
            return .secondary
        } else if status.isInstalled {
            return .green
        } else {
            return .orange
        }
    }
}

@MainActor
class ShellPluginInstallerViewModel: ObservableObject {
    @Published var status: [ShellPluginInstaller.Shell: ShellPluginInstaller.InstallStatus] = [:]
    
    private let installer = ShellPluginInstaller()
    
    func refreshStatus() {
        status = installer.getStatus()
    }
    
    func install(shell: ShellPluginInstaller.Shell) {
        do {
            try installer.install(shell: shell)
            refreshStatus()
        } catch {
            showError(error)
        }
    }
    
    func uninstall(shell: ShellPluginInstaller.Shell) {
        do {
            try installer.uninstall(shell: shell)
            refreshStatus()
        } catch {
            showError(error)
        }
    }
    
    private func showError(_ error: Error) {
        let alert = NSAlert()
        alert.messageText = "Shell Plugin Error"
        alert.informativeText = error.localizedDescription
        alert.alertStyle = .warning
        alert.runModal()
    }
}

// MARK: - Advanced Settings

struct AdvancedSettings: View {
    @StateObject private var settings = AppSettings.shared
    
    var body: some View {
        Form {
            Section("Renderer") {
                Toggle("Use Metal renderer", isOn: $settings.useMetalRenderer)
                
                Text("Metal provides better performance. Disable if you experience rendering issues.")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            
            Section("Logging") {
                Toggle("Enable debug logging", isOn: $settings.debugLoggingEnabled)
                
                if settings.debugLoggingEnabled {
                    Button("Open Logs Folder") {
                        openLogsFolder()
                    }
                }
            }
            
            Section("Environment Policy") {
                Button("View Environment Variables...") {
                    showEnvironmentPolicy()
                }
                
                Text("Control which environment variables are passed to shells")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            
            Section("Data") {
                HStack {
                    VStack(alignment: .leading) {
                        Text("Storage location")
                        Text(storageLocation)
                            .font(.caption)
                            .foregroundColor(.secondary)
                    }
                    
                    Spacer()
                    
                    Button("Open in Finder") {
                        openStorageFolder()
                    }
                }
                
                Button("Reset All Settings...") {
                    showResetConfirmation()
                }
                .foregroundColor(.red)
            }
        }
        .formStyle(.grouped)
        .padding()
    }
    
    private var storageLocation: String {
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let yolotermDir = appSupport.appendingPathComponent("YOLOTerm")
        return yolotermDir.path
    }
    
    private func openLogsFolder() {
        // Open logs directory
        NSWorkspace.shared.open(URL(fileURLWithPath: storageLocation).appendingPathComponent("logs"))
    }
    
    private func openStorageFolder() {
        NSWorkspace.shared.open(URL(fileURLWithPath: storageLocation))
    }
    
    private func showEnvironmentPolicy() {
        // TODO: Show environment policy viewer
    }
    
    private func showResetConfirmation() {
        let alert = NSAlert()
        alert.messageText = "Reset All Settings?"
        alert.informativeText = "This will restore all settings to their default values. Your history and workspace will not be affected."
        alert.alertStyle = .warning
        alert.addButton(withTitle: "Cancel")
        alert.addButton(withTitle: "Reset")
        
        if alert.runModal() == .alertSecondButtonReturn {
            settings.resetToDefaults()
        }
    }
}

// MARK: - AppSettings (UserDefaults wrapper)

@MainActor
class AppSettings: ObservableObject {
    static let shared = AppSettings()
    
    private let defaults = UserDefaults.standard
    
    // Appearance
    @Published var themeName: String {
        didSet { defaults.set(themeName, forKey: "themeName") }
    }
    @Published var fontName: String {
        didSet { defaults.set(fontName, forKey: "fontName") }
    }
    @Published var fontSize: Double {
        didSet { defaults.set(fontSize, forKey: "fontSize") }
    }
    @Published var cursorStyle: CursorStyle {
        didSet { defaults.set(cursorStyle.rawValue, forKey: "cursorStyle") }
    }
    @Published var cursorBlink: Bool {
        didSet { defaults.set(cursorBlink, forKey: "cursorBlink") }
    }
    @Published var windowOpacity: Double {
        didSet { defaults.set(windowOpacity, forKey: "windowOpacity") }
    }
    @Published var showTabsBar: Bool {
        didSet { defaults.set(showTabsBar, forKey: "showTabsBar") }
    }
    
    // Behavior
    @Published var defaultShell: String {
        didSet { defaults.set(defaultShell, forKey: "defaultShell") }
    }
    @Published var customShellPath: String {
        didSet { defaults.set(customShellPath, forKey: "customShellPath") }
    }
    @Published var loginShell: Bool {
        didSet { defaults.set(loginShell, forKey: "loginShell") }
    }
    @Published var scrollbackLines: Int {
        didSet { defaults.set(scrollbackLines, forKey: "scrollbackLines") }
    }
    @Published var copyOnSelect: Bool {
        didSet { defaults.set(copyOnSelect, forKey: "copyOnSelect") }
    }
    @Published var pasteOnRightClick: Bool {
        didSet { defaults.set(pasteOnRightClick, forKey: "pasteOnRightClick") }
    }
    @Published var confirmQuit: Bool {
        didSet { defaults.set(confirmQuit, forKey: "confirmQuit") }
    }
    @Published var visualBell: Bool {
        didSet { defaults.set(visualBell, forKey: "visualBell") }
    }
    @Published var audibleBell: Bool {
        didSet { defaults.set(audibleBell, forKey: "audibleBell") }
    }
    @Published var startupBehavior: StartupBehavior {
        didSet { defaults.set(startupBehavior.rawValue, forKey: "startupBehavior") }
    }
    
    // History
    @Published var historyEnabled: Bool {
        didSet { defaults.set(historyEnabled, forKey: "historyEnabled") }
    }
    @Published var historyRetentionDays: Int {
        didSet { defaults.set(historyRetentionDays, forKey: "historyRetentionDays") }
    }
    @Published var historyRedactionEnabled: Bool {
        didSet { defaults.set(historyRedactionEnabled, forKey: "historyRedactionEnabled") }
    }
    
    // Advanced
    @Published var useMetalRenderer: Bool {
        didSet { defaults.set(useMetalRenderer, forKey: "useMetalRenderer") }
    }
    @Published var debugLoggingEnabled: Bool {
        didSet { defaults.set(debugLoggingEnabled, forKey: "debugLoggingEnabled") }
    }
    
    private init() {
        // Load from UserDefaults
        self.themeName = defaults.string(forKey: "themeName") ?? "terminal-default"
        self.fontName = defaults.string(forKey: "fontName") ?? "SF Mono"
        self.fontSize = defaults.double(forKey: "fontSize") == 0 ? 13.0 : defaults.double(forKey: "fontSize")
        self.cursorStyle = CursorStyle(rawValue: defaults.string(forKey: "cursorStyle") ?? "block") ?? .block
        self.cursorBlink = defaults.bool(forKey: "cursorBlink")
        self.windowOpacity = defaults.double(forKey: "windowOpacity") == 0 ? 1.0 : defaults.double(forKey: "windowOpacity")
        self.showTabsBar = defaults.object(forKey: "showTabsBar") as? Bool ?? true
        
        self.defaultShell = defaults.string(forKey: "defaultShell") ?? "/bin/zsh"
        self.customShellPath = defaults.string(forKey: "customShellPath") ?? ""
        self.loginShell = defaults.object(forKey: "loginShell") as? Bool ?? true
        self.scrollbackLines = defaults.integer(forKey: "scrollbackLines") == 0 ? 10000 : defaults.integer(forKey: "scrollbackLines")
        self.copyOnSelect = defaults.bool(forKey: "copyOnSelect")
        self.pasteOnRightClick = defaults.object(forKey: "pasteOnRightClick") as? Bool ?? true
        self.confirmQuit = defaults.bool(forKey: "confirmQuit")
        self.visualBell = defaults.object(forKey: "visualBell") as? Bool ?? true
        self.audibleBell = defaults.bool(forKey: "audibleBell")
        self.startupBehavior = StartupBehavior(rawValue: defaults.string(forKey: "startupBehavior") ?? "restore") ?? .restore
        
        self.historyEnabled = defaults.object(forKey: "historyEnabled") as? Bool ?? true
        self.historyRetentionDays = defaults.integer(forKey: "historyRetentionDays") == 0 ? 90 : defaults.integer(forKey: "historyRetentionDays")
        self.historyRedactionEnabled = defaults.object(forKey: "historyRedactionEnabled") as? Bool ?? true
        
        self.useMetalRenderer = defaults.object(forKey: "useMetalRenderer") as? Bool ?? true
        self.debugLoggingEnabled = defaults.bool(forKey: "debugLoggingEnabled")
    }
    
    func resetToDefaults() {
        // Reset all settings to defaults
        defaults.removePersistentDomain(forName: Bundle.main.bundleIdentifier!)
        
        // Reinitialize
        self.themeName = "terminal-default"
        self.fontName = "SF Mono"
        self.fontSize = 13.0
        self.cursorStyle = .block
        self.cursorBlink = false
        self.windowOpacity = 1.0
        self.showTabsBar = true
        
        self.defaultShell = "/bin/zsh"
        self.customShellPath = ""
        self.loginShell = true
        self.scrollbackLines = 10000
        self.copyOnSelect = false
        self.pasteOnRightClick = true
        self.confirmQuit = false
        self.visualBell = true
        self.audibleBell = false
        self.startupBehavior = .restore
        
        self.historyEnabled = true
        self.historyRetentionDays = 90
        self.historyRedactionEnabled = true
        
        self.useMetalRenderer = true
        self.debugLoggingEnabled = false
    }
}
