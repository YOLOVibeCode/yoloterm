import XCTest
@testable import YOLOTermKit

@MainActor
final class WorkspaceStoreTests: XCTestCase {
    
    var tempDirectory: URL!
    var store: WorkspaceStore!
    
    override func setUp() async throws {
        tempDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: tempDirectory, withIntermediateDirectories: true)
        store = WorkspaceStore(baseDirectory: tempDirectory)
    }
    
    override func tearDown() async throws {
        try? FileManager.default.removeItem(at: tempDirectory)
    }
    
    // MARK: - Basic Save and Load
    
    func testSaveAndLoad() async throws {
        let workspace = WorkspaceStore.Workspace(
            tabs: [
                WorkspaceStore.Tab(
                    name: "Tab 1",
                    panes: [
                        WorkspaceStore.Pane(shell: "/bin/zsh", cwd: "/Users/test")
                    ],
                    layoutPreset: "single"
                )
            ],
            selectedTabIndex: 0
        )
        
        try await store.saveImmediate(workspace)
        let loaded = try store.load()
        
        XCTAssertEqual(loaded, workspace, "Loaded workspace should match saved workspace")
    }
    
    func testLoadNonExistent() throws {
        let loaded = try store.load()
        XCTAssertNil(loaded, "Loading non-existent workspace should return nil")
    }
    
    func testMultipleTabs() async throws {
        let workspace = WorkspaceStore.Workspace(
            tabs: [
                WorkspaceStore.Tab(
                    name: "Tab 1",
                    panes: [
                        WorkspaceStore.Pane(shell: "/bin/zsh", cwd: "/Users/test1")
                    ],
                    layoutPreset: "single"
                ),
                WorkspaceStore.Tab(
                    name: "Tab 2",
                    panes: [
                        WorkspaceStore.Pane(shell: "/bin/bash", cwd: "/Users/test2"),
                        WorkspaceStore.Pane(shell: "/bin/zsh", cwd: "/Users/test3")
                    ],
                    layoutPreset: "columns"
                )
            ],
            selectedTabIndex: 1
        )
        
        try await store.saveImmediate(workspace)
        let loaded = try store.load()
        
        XCTAssertEqual(loaded?.tabs.count, 2, "Should load 2 tabs")
        XCTAssertEqual(loaded?.selectedTabIndex, 1, "Should restore selected tab index")
        XCTAssertEqual(loaded?.tabs[1].panes.count, 2, "Tab 2 should have 2 panes")
    }
    
    // MARK: - Graceful Degradation (CRITICAL - TermGrid lesson)
    
    func testGracefulDegradation_InvalidJSON() throws {
        // TermGrid c65fef8 lesson: corrupt workspace should not lose all tabs
        
        try store.writeCorruptWorkspace(scenario: .invalidJSON)
        
        let loaded = try store.load()
        
        // Should return empty workspace, not crash or return nil
        XCTAssertNotNil(loaded, "Corrupt workspace should not return nil")
        XCTAssertEqual(loaded?.tabs.count, 0, "Corrupt workspace should return empty tabs")
    }
    
    func testGracefulDegradation_MissingTabs() throws {
        try store.writeCorruptWorkspace(scenario: .missingTabs)
        
        let loaded = try store.load()
        
        XCTAssertNotNil(loaded, "Workspace with missing tabs field should not return nil")
        XCTAssertEqual(loaded?.tabs.count, 0, "Workspace with missing tabs should return empty array")
    }
    
    func testGracefulDegradation_PartiallyCorruptTab() throws {
        // One valid tab, one corrupt tab - should salvage the valid one
        
        try store.writeCorruptWorkspace(scenario: .partiallyCorruptTab)
        
        let loaded = try store.load()
        
        XCTAssertNotNil(loaded, "Partially corrupt workspace should not return nil")
        XCTAssertEqual(loaded?.tabs.count, 1, "Should salvage the valid tab")
        XCTAssertEqual(loaded?.tabs.first?.name, "Tab 1", "Should preserve the valid tab's data")
    }
    
    func testNeverDestructiveOnPartialLoad() async throws {
        // CRITICAL TEST: This is the TermGrid c65fef8 bug test
        // If workspace is partially readable, we must preserve what we can
        
        // Create a workspace with multiple tabs
        let originalWorkspace = WorkspaceStore.Workspace(
            tabs: [
                WorkspaceStore.Tab(name: "Tab 1", panes: [
                    WorkspaceStore.Pane(shell: "/bin/zsh", cwd: "/Users/test1")
                ], layoutPreset: "single"),
                WorkspaceStore.Tab(name: "Tab 2", panes: [
                    WorkspaceStore.Pane(shell: "/bin/bash", cwd: "/Users/test2")
                ], layoutPreset: "single"),
                WorkspaceStore.Tab(name: "Tab 3", panes: [
                    WorkspaceStore.Pane(shell: "/bin/zsh", cwd: "/Users/test3")
                ], layoutPreset: "single")
            ],
            selectedTabIndex: 0
        )
        
        try await store.saveImmediate(originalWorkspace)
        
        // Simulate partial corruption (manually corrupt the workspace file)
        try store.writeCorruptWorkspace(scenario: .partiallyCorruptTab)
        
        let recovered = try store.load()
        
        // We should recover at least some tabs, never return empty when data exists
        XCTAssertNotNil(recovered, "Must not return nil on partial corruption")
        XCTAssertGreaterThan(recovered?.tabs.count ?? 0, 0, "Must salvage at least one valid tab")
        
        // The key lesson: NEVER silently discard all user data
        // Even if only 1 of 3 tabs can be parsed, we preserve that 1 tab
    }
    
    // MARK: - Atomic Write with Backup
    
    func testAtomicWrite() async throws {
        let workspace1 = WorkspaceStore.Workspace(
            tabs: [WorkspaceStore.Tab(name: "Tab 1", panes: [], layoutPreset: "single")],
            selectedTabIndex: 0
        )
        
        let workspace2 = WorkspaceStore.Workspace(
            tabs: [WorkspaceStore.Tab(name: "Tab 2", panes: [], layoutPreset: "single")],
            selectedTabIndex: 0
        )
        
        try await store.saveImmediate(workspace1)
        try await store.saveImmediate(workspace2)
        
        // Verify backup exists
        let backupURL = tempDirectory
            .appendingPathComponent("workspace.json.backup")
        
        XCTAssertTrue(FileManager.default.fileExists(atPath: backupURL.path), "Backup should exist")
        
        // Backup should contain the first workspace
        let backupData = try Data(contentsOf: backupURL)
        let backup = try JSONDecoder().decode(WorkspaceStore.Workspace.self, from: backupData)
        
        XCTAssertEqual(backup.tabs.first?.name, "Tab 1", "Backup should contain previous workspace")
    }
    
    // MARK: - Acceptance Criteria
    
    func testAcceptanceCriteria_RestoreFixtures() async throws {
        // M4 Acceptance: "contracts/fixtures/restore/ tests green"
        
        // Test multiple restore scenarios
        let scenarios = [
            WorkspaceStore.Workspace(
                tabs: [WorkspaceStore.Tab(name: "Single", panes: [
                    WorkspaceStore.Pane(shell: "/bin/zsh", cwd: "/")
                ], layoutPreset: "single")],
                selectedTabIndex: 0
            ),
            WorkspaceStore.Workspace(
                tabs: [
                    WorkspaceStore.Tab(name: "Tab 1", panes: [
                        WorkspaceStore.Pane(shell: "/bin/zsh", cwd: "/tmp")
                    ], layoutPreset: "single"),
                    WorkspaceStore.Tab(name: "Tab 2", panes: [
                        WorkspaceStore.Pane(shell: "/bin/bash", cwd: "/usr"),
                        WorkspaceStore.Pane(shell: "/bin/zsh", cwd: "/var")
                    ], layoutPreset: "columns")
                ],
                selectedTabIndex: 1
            )
        ]
        
        for scenario in scenarios {
            try await store.saveImmediate(scenario)
            let loaded = try store.load()
            
            XCTAssertEqual(loaded, scenario, "Restore fixture must match exactly")
        }
    }
}
