import XCTest
@testable import YOLOTermKit

@MainActor
final class HistoryStoreTests: XCTestCase {
    
    var tempDirectory: URL!
    var store: HistoryStore!
    
    override func setUp() async throws {
        tempDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: tempDirectory, withIntermediateDirectories: true)
        
        // Use test redaction patterns
        let redactionURL = createTestRedactionPatterns()
        store = try HistoryStore(baseDirectory: tempDirectory, redactionPatternsURL: redactionURL)
    }
    
    override func tearDown() async throws {
        try? FileManager.default.removeItem(at: tempDirectory)
    }
    
    // MARK: - Basic Insert and Query
    
    func testInsertAndRetrieve() throws {
        let command = HistoryStore.Command(
            command: "ls -la",
            shell: "zsh",
            cwd: "/Users/test",
            exitCode: 0,
            durationMs: 100
        )
        
        try store.insert(command)
        
        let recent = try store.recent()
        XCTAssertEqual(recent.count, 1)
        XCTAssertEqual(recent.first?.command, "ls -la")
    }
    
    func testMultipleCommands() throws {
        let commands = [
            HistoryStore.Command(command: "git status", shell: "zsh", cwd: "/Users/test/repo"),
            HistoryStore.Command(command: "npm install", shell: "zsh", cwd: "/Users/test/repo"),
            HistoryStore.Command(command: "docker ps", shell: "bash", cwd: "/Users/test")
        ]
        
        for command in commands {
            try store.insert(command)
        }
        
        let recent = try store.recent()
        XCTAssertEqual(recent.count, 3)
    }
    
    // MARK: - Full-Text Search
    
    func testFullTextSearch() throws {
        let commands = [
            HistoryStore.Command(command: "git status", shell: "zsh", cwd: "/Users/test"),
            HistoryStore.Command(command: "git commit -m 'test'", shell: "zsh", cwd: "/Users/test"),
            HistoryStore.Command(command: "npm install", shell: "zsh", cwd: "/Users/test"),
            HistoryStore.Command(command: "git push origin main", shell: "zsh", cwd: "/Users/test")
        ]
        
        for command in commands {
            try store.insert(command)
        }
        
        // Search for "git"
        let gitResults = try store.search(query: "git")
        XCTAssertEqual(gitResults.count, 3, "Should find 3 git commands")
        
        // Search for "npm"
        let npmResults = try store.search(query: "npm")
        XCTAssertEqual(npmResults.count, 1, "Should find 1 npm command")
        
        // Search for "commit"
        let commitResults = try store.search(query: "commit")
        XCTAssertEqual(commitResults.count, 1, "Should find 1 commit command")
    }
    
    func testPaneScopedSearch() throws {
        let pane1 = UUID().uuidString
        let pane2 = UUID().uuidString
        
        try store.insert(HistoryStore.Command(command: "ls", shell: "zsh", paneID: pane1))
        try store.insert(HistoryStore.Command(command: "pwd", shell: "zsh", paneID: pane1))
        try store.insert(HistoryStore.Command(command: "ls", shell: "zsh", paneID: pane2))
        
        let pane1Results = try store.search(query: "ls", options: HistoryStore.SearchOptions(paneID: pane1))
        XCTAssertEqual(pane1Results.count, 1, "Should find only pane1's ls command")
        
        let pane2Results = try store.search(query: "ls", options: HistoryStore.SearchOptions(paneID: pane2))
        XCTAssertEqual(pane2Results.count, 1, "Should find only pane2's ls command")
    }
    
    // MARK: - Redaction
    
    func testRedaction_Password() throws {
        let command = HistoryStore.Command(
            command: "export DATABASE_PASSWORD=supersecret123",
            shell: "zsh"
        )
        
        try store.insert(command)
        
        let recent = try store.recent()
        XCTAssertEqual(recent.count, 1)
        XCTAssertTrue(recent.first?.redacted ?? false, "Command should be marked as redacted")
        XCTAssertTrue(recent.first?.command.contains("***REDACTED***") ?? false, "Password should be redacted")
        XCTAssertFalse(recent.first?.command.contains("supersecret123") ?? true, "Original password should not be in DB")
    }
    
    func testRedaction_BearerToken() throws {
        let command = HistoryStore.Command(
            command: "curl -H 'Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9' https://api.example.com",
            shell: "bash"
        )
        
        try store.insert(command)
        
        let recent = try store.recent()
        XCTAssertTrue(recent.first?.redacted ?? false, "Command with bearer token should be redacted")
        XCTAssertTrue(recent.first?.command.contains("***REDACTED***") ?? false, "Token should be redacted")
    }
    
    func testRedaction_DatabaseURL() throws {
        let command = HistoryStore.Command(
            command: "psql postgres://admin:mypass@localhost/db",
            shell: "zsh"
        )
        
        try store.insert(command)
        
        let recent = try store.recent()
        XCTAssertTrue(recent.first?.redacted ?? false, "DB connection string should be redacted")
        XCTAssertFalse(recent.first?.command.contains("mypass") ?? true, "Password should be redacted from DB URL")
    }
    
    // MARK: - Favorites
    
    func testToggleFavorite() throws {
        var command = HistoryStore.Command(command: "git status", shell: "zsh")
        try store.insert(command)
        
        let inserted = try store.recent().first!
        let commandID = inserted.id!
        
        // Toggle to favorite
        try store.toggleFavorite(commandID: commandID)
        var updated = try store.recent().first!
        XCTAssertTrue(updated.favorite, "Command should be favorited")
        
        // Toggle back
        try store.toggleFavorite(commandID: commandID)
        updated = try store.recent().first!
        XCTAssertFalse(updated.favorite, "Command should be unfavorited")
    }
    
    func testSearchFavoritesOnly() throws {
        var cmd1 = HistoryStore.Command(command: "git status", shell: "zsh")
        var cmd2 = HistoryStore.Command(command: "git log", shell: "zsh")
        var cmd3 = HistoryStore.Command(command: "git diff", shell: "zsh")
        
        try store.insert(cmd1)
        try store.insert(cmd2)
        try store.insert(cmd3)
        
        // Favorite the second command
        let all = try store.recent()
        try store.toggleFavorite(commandID: all[1].id!)
        
        let favorites = try store.recent(options: HistoryStore.SearchOptions(onlyFavorites: true))
        XCTAssertEqual(favorites.count, 1, "Should find only favorited command")
        XCTAssertEqual(favorites.first?.command, "git log")
    }
    
    // MARK: - Performance Benchmark
    
    func testPerformance_100kSearch() throws {
        // M4 Acceptance: "100k-row search < 50 ms benchmark in CI"
        
        // Insert 100k commands
        print("Inserting 100k commands...")
        for i in 0..<100_000 {
            let command = HistoryStore.Command(
                command: "command_\(i % 1000) arg\(i)",
                shell: i % 2 == 0 ? "zsh" : "bash",
                cwd: "/Users/test/dir\(i % 100)",
                exitCode: i % 10 == 0 ? 1 : 0,
                durationMs: Int.random(in: 10...5000)
            )
            try store.insert(command)
        }
        
        let count = try store.count()
        XCTAssertEqual(count, 100_000, "Should have 100k commands")
        
        print("Running search benchmark...")
        
        // Measure search time
        let startTime = Date()
        let results = try store.search(query: "command_5")
        let duration = Date().timeIntervalSince(startTime) * 1000 // milliseconds
        
        print("Search completed in \(duration) ms, found \(results.count) results")
        
        // ACCEPTANCE CRITERIA: < 50 ms
        XCTAssertLessThan(duration, 50.0, "Search on 100k rows must complete in < 50 ms")
        XCTAssertGreaterThan(results.count, 0, "Search should find results")
    }
    
    // MARK: - Retention Policy
    
    func testDeleteOldCommands() throws {
        let now = Date()
        let old = Date(timeIntervalSinceNow: -100 * 24 * 60 * 60) // 100 days ago
        
        let oldCommand = HistoryStore.Command(
            command: "old command",
            shell: "zsh",
            timestamp: Int64(old.timeIntervalSince1970 * 1000)
        )
        
        let recentCommand = HistoryStore.Command(
            command: "recent command",
            shell: "zsh",
            timestamp: Int64(now.timeIntervalSince1970 * 1000)
        )
        
        try store.insert(oldCommand)
        try store.insert(recentCommand)
        
        // Delete commands older than 30 days
        try store.deleteOlderThan(days: 30)
        
        let remaining = try store.recent()
        XCTAssertEqual(remaining.count, 1, "Should have only recent command")
        XCTAssertEqual(remaining.first?.command, "recent command")
    }
    
    // MARK: - Notes
    
    func testUpdateNote() throws {
        let command = HistoryStore.Command(command: "complex_command", shell: "zsh")
        try store.insert(command)
        
        let inserted = try store.recent().first!
        let commandID = inserted.id!
        
        try store.updateNote(commandID: commandID, note: "This is a useful command")
        
        let updated = try store.recent().first!
        XCTAssertEqual(updated.note, "This is a useful command")
    }
    
    // MARK: - Failed Commands Filter
    
    func testSearchFailedOnly() throws {
        try store.insert(HistoryStore.Command(command: "success", shell: "zsh", exitCode: 0))
        try store.insert(HistoryStore.Command(command: "fail1", shell: "zsh", exitCode: 1))
        try store.insert(HistoryStore.Command(command: "fail2", shell: "zsh", exitCode: 127))
        
        let failed = try store.recent(options: HistoryStore.SearchOptions(onlyFailed: true))
        
        XCTAssertEqual(failed.count, 2, "Should find only failed commands")
        XCTAssertTrue(failed.allSatisfy { $0.exitCode != 0 }, "All results should have non-zero exit code")
    }
    
    // MARK: - Helpers
    
    private func createTestRedactionPatterns() -> URL {
        let url = tempDirectory.appendingPathComponent("redaction.json")
        
        let json = """
        {
          "patterns": [
            {
              "name": "Password",
              "regex": "(?i)(password|token|key|secret)=\\\\S+",
              "replacement": "$1=***REDACTED***",
              "reason": "Test"
            },
            {
              "name": "Bearer",
              "regex": "(?i)(bearer|authorization:\\\\s*bearer)\\\\s+[A-Za-z0-9_\\\\-\\\\.]+",
              "replacement": "$1 ***REDACTED***",
              "reason": "Test"
            },
            {
              "name": "DB URL",
              "regex": "(?i)(postgres|mysql|mongodb)://[^@]+:([^@]+)@",
              "replacement": "$1://user:***REDACTED***@",
              "reason": "Test"
            }
          ]
        }
        """
        
        try! json.write(to: url, atomically: true, encoding: .utf8)
        return url
    }
}
