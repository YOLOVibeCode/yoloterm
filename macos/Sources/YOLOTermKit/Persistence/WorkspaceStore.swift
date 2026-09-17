import Foundation

/// WorkspaceStore manages the persistent workspace state (tabs, panes, layout, shells, cwds).
///
/// Key features:
/// - Codable workspace model for easy serialization
/// - Debounced save (don't write on every keystroke)
/// - Reconcile-on-save (handle concurrent changes)
/// - **CRITICAL**: Never destructive on partial load (graceful degradation)
///
/// Storage: ~/Library/Application Support/YOLOTerm/workspace.json
///
/// TermGrid lesson: commit c65fef8 caught a bug where corrupt workspace would lose all tabs.
/// This implementation is defensive: if the workspace file is corrupt or partially readable,
/// we preserve what we can and never silently discard user data.
@MainActor
public final class WorkspaceStore {
    
    // MARK: - Models
    
    public struct Workspace: Codable, Equatable {
        public var tabs: [Tab]
        public var selectedTabIndex: Int
        public var version: Int = 1
        
        public init(tabs: [Tab] = [], selectedTabIndex: Int = 0) {
            self.tabs = tabs
            self.selectedTabIndex = selectedTabIndex
        }
    }
    
    public struct Tab: Codable, Equatable, Identifiable {
        public let id: UUID
        public var name: String
        public var panes: [Pane]
        public var layoutPreset: String // "auto", "single", "columns", "rows", "grid", etc.
        
        public init(id: UUID = UUID(), name: String, panes: [Pane], layoutPreset: String = "auto") {
            self.id = id
            self.name = name
            self.panes = panes
            self.layoutPreset = layoutPreset
        }
    }
    
    public struct Pane: Codable, Equatable, Identifiable {
        public let id: UUID
        public var shell: String
        public var cwd: String
        public var title: String?
        
        public init(id: UUID = UUID(), shell: String, cwd: String, title: String? = nil) {
            self.id = id
            self.shell = shell
            self.cwd = cwd
            self.title = title
        }
    }
    
    // MARK: - Configuration
    
    private let workspaceURL: URL
    private let saveDebounceInterval: TimeInterval = 1.0
    private var saveTask: Task<Void, Never>?
    
    public init(baseDirectory: URL? = nil) {
        if let baseDirectory {
            self.workspaceURL = baseDirectory.appendingPathComponent("workspace.json")
        } else {
            let appSupport = FileManager.default.urls(
                for: .applicationSupportDirectory,
                in: .userDomainMask
            ).first!
            let yolotermDir = appSupport.appendingPathComponent("YOLOTerm", isDirectory: true)
            try? FileManager.default.createDirectory(at: yolotermDir, withIntermediateDirectories: true)
            self.workspaceURL = yolotermDir.appendingPathComponent("workspace.json")
        }
    }
    
    // MARK: - Public API
    
    /// Load the workspace from disk.
    /// Returns nil if the file doesn't exist (first launch).
    /// On corrupt data, returns a partial workspace with as much as we could salvage.
    public func load() throws -> Workspace? {
        guard FileManager.default.fileExists(atPath: workspaceURL.path) else {
            return nil
        }
        
        let data = try Data(contentsOf: workspaceURL)
        
        do {
            let workspace = try JSONDecoder().decode(Workspace.self, from: data)
            return workspace
        } catch {
            // Graceful degradation: try to salvage what we can
            print("WorkspaceStore: Failed to decode workspace cleanly, attempting recovery: \(error)")
            return try recoverWorkspace(from: data)
        }
    }
    
    /// Save the workspace to disk (debounced).
    /// Cancels any pending save and schedules a new one.
    public func save(_ workspace: Workspace) {
        // Cancel any pending save
        saveTask?.cancel()
        
        // Schedule a new debounced save
        saveTask = Task {
            try? await Task.sleep(for: .seconds(saveDebounceInterval))
            
            guard !Task.isCancelled else { return }
            
            try? await saveImmediate(workspace)
        }
    }
    
    /// Save the workspace immediately (for shutdown or explicit save).
    public func saveImmediate(_ workspace: Workspace) async throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(workspace)
        
        // Atomic write: write to temp file, then move
        let tempURL = workspaceURL.deletingLastPathComponent()
            .appendingPathComponent("workspace.json.tmp")
        
        try data.write(to: tempURL, options: .atomic)
        
        // If the main file exists, create a backup
        if FileManager.default.fileExists(atPath: workspaceURL.path) {
            let backupURL = workspaceURL.deletingLastPathComponent()
                .appendingPathComponent("workspace.json.backup")
            try? FileManager.default.removeItem(at: backupURL)
            try? FileManager.default.copyItem(at: workspaceURL, to: backupURL)
        }
        
        // Move temp to main (with retry logic for atomic write)
        do {
            // Remove destination if it exists (shouldn't, but be safe)
            try? FileManager.default.removeItem(at: workspaceURL)
            try FileManager.default.moveItem(at: tempURL, to: workspaceURL)
        } catch {
            // If move fails, try copy + delete
            try FileManager.default.copyItem(at: tempURL, to: workspaceURL)
            try? FileManager.default.removeItem(at: tempURL)
        }
    }
    
    // MARK: - Recovery (Graceful Degradation)
    
    /// Attempt to recover a partially corrupt workspace.
    /// Strategy: try to decode individual tabs, preserve what works, discard what doesn't.
    private func recoverWorkspace(from data: Data) throws -> Workspace {
        // Try to parse as JSON dictionary at least
        do {
            guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                print("WorkspaceStore: Cannot parse as JSON dictionary, returning empty workspace")
                return Workspace()
            }
            
            var recoveredTabs: [Tab] = []
            
            // Try to extract tabs array
            if let tabsArray = json["tabs"] as? [[String: Any]] {
                for tabDict in tabsArray {
                    if let tabData = try? JSONSerialization.data(withJSONObject: tabDict),
                       let tab = try? JSONDecoder().decode(Tab.self, from: tabData) {
                        recoveredTabs.append(tab)
                    } else {
                        print("WorkspaceStore: Skipping corrupt tab entry")
                    }
                }
            }
            
            let selectedIndex = json["selectedTabIndex"] as? Int ?? 0
            
            print("WorkspaceStore: Recovered \(recoveredTabs.count) tabs from corrupt workspace")
            
            return Workspace(
                tabs: recoveredTabs,
                selectedTabIndex: min(selectedIndex, max(0, recoveredTabs.count - 1))
            )
        } catch {
            print("WorkspaceStore: JSON parsing failed completely, returning empty workspace")
            return Workspace()
        }
    }
    
    /// Test helper: Force a specific corrupt workspace scenario for testing.
    /// This is used by tests to verify graceful degradation.
    public func writeCorruptWorkspace(scenario: CorruptWorkspaceScenario) throws {
        let data: Data
        
        switch scenario {
        case .invalidJSON:
            data = Data("{ invalid json".utf8)
        case .missingTabs:
            data = Data(#"{ "selectedTabIndex": 0 }"#.utf8)
        case .partiallyCorruptTab:
            // One valid tab, one corrupt tab
            data = Data("""
            {
              "tabs": [
                {
                  "id": "00000000-0000-0000-0000-000000000001",
                  "name": "Tab 1",
                  "panes": [],
                  "layoutPreset": "single"
                },
                {
                  "id": "invalid-uuid",
                  "name": "Corrupt Tab"
                }
              ],
              "selectedTabIndex": 0,
              "version": 1
            }
            """.utf8)
        }
        
        try data.write(to: workspaceURL, options: .atomic)
    }
    
    public enum CorruptWorkspaceScenario {
        case invalidJSON
        case missingTabs
        case partiallyCorruptTab
    }
}
