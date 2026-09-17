import XCTest
@testable import YOLOTermKit

@MainActor
final class OutputJournalTests: XCTestCase {
    
    var tempDirectory: URL!
    var journal: OutputJournal!
    
    override func setUp() async throws {
        tempDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: tempDirectory, withIntermediateDirectories: true)
        journal = OutputJournal(baseDirectory: tempDirectory)
    }
    
    override func tearDown() async throws {
        try? FileManager.default.removeItem(at: tempDirectory)
    }
    
    // MARK: - Basic Append and Replay
    
    func testAppendAndReplay() throws {
        let paneID = UUID()
        let data1 = Data("Hello, world!\n".utf8)
        let data2 = Data("More output\n".utf8)
        
        try journal.append(paneID: paneID, data: data1)
        try journal.append(paneID: paneID, data: data2)
        
        let replayed = try journal.replay(paneID: paneID)
        let expected = data1 + data2
        
        XCTAssertEqual(replayed, expected, "Replayed data should match appended data")
    }
    
    func testReplayNonExistentPane() throws {
        let paneID = UUID()
        let replayed = try journal.replay(paneID: paneID)
        
        XCTAssertEqual(replayed, Data(), "Replaying non-existent pane should return empty data")
    }
    
    // MARK: - Journal Rotation
    
    func testJournalRotation() throws {
        let paneID = UUID()
        
        // Create a large chunk of data to force rotation
        let chunkSize = OutputJournal.maxJournalSize / 2
        let chunk1 = Data(repeating: 0x41, count: chunkSize) // 'A' repeated
        let chunk2 = Data(repeating: 0x42, count: chunkSize) // 'B' repeated
        let chunk3 = Data(repeating: 0x43, count: chunkSize) // 'C' repeated
        
        try journal.append(paneID: paneID, data: chunk1)
        try journal.append(paneID: paneID, data: chunk2) // Should trigger rotation
        try journal.append(paneID: paneID, data: chunk3)
        
        // Replay should contain all data in order
        let replayed = try journal.replay(paneID: paneID)
        
        XCTAssertTrue(replayed.starts(with: chunk1), "Replayed data should start with chunk1")
        XCTAssertTrue(replayed.contains(chunk2), "Replayed data should contain chunk2")
        XCTAssertTrue(replayed.contains(chunk3), "Replayed data should contain chunk3")
    }
    
    // MARK: - Deletion
    
    func testDelete() throws {
        let paneID = UUID()
        let data = Data("Test data".utf8)
        
        try journal.append(paneID: paneID, data: data)
        XCTAssertNoThrow(try journal.delete(paneID: paneID))
        
        let replayed = try journal.replay(paneID: paneID)
        XCTAssertEqual(replayed, Data(), "After deletion, replay should return empty data")
    }
    
    func testDeleteNonExistentPane() throws {
        let paneID = UUID()
        XCTAssertNoThrow(try journal.delete(paneID: paneID), "Deleting non-existent pane should not throw")
    }
    
    // MARK: - Orphan Purging
    
    func testPurgeOrphans() throws {
        let pane1 = UUID()
        let pane2 = UUID()
        let pane3 = UUID()
        
        // Create journals for three panes
        try journal.append(paneID: pane1, data: Data("Pane 1".utf8))
        try journal.append(paneID: pane2, data: Data("Pane 2".utf8))
        try journal.append(paneID: pane3, data: Data("Pane 3".utf8))
        
        // Purge orphans, keeping only pane1 and pane2
        try journal.purgeOrphans(activePaneIDs: [pane1, pane2])
        
        // Pane 3 should be gone
        let replayed1 = try journal.replay(paneID: pane1)
        let replayed2 = try journal.replay(paneID: pane2)
        let replayed3 = try journal.replay(paneID: pane3)
        
        XCTAssertEqual(replayed1, Data("Pane 1".utf8), "Pane 1 should still exist")
        XCTAssertEqual(replayed2, Data("Pane 2".utf8), "Pane 2 should still exist")
        XCTAssertEqual(replayed3, Data(), "Pane 3 should be purged")
    }
    
    // MARK: - Acceptance Criteria
    
    func testAcceptanceCriteria_IdenticalScrollback() throws {
        // M4 Acceptance: "Restore shows identical scrollback after restart"
        
        let paneID = UUID()
        let originalData = Data("""
        Line 1
        Line 2
        Line 3 with ANSI \u{1B}[31mred\u{1B}[0m text
        Line 4
        """.utf8)
        
        // Write data
        try journal.append(paneID: paneID, data: originalData)
        
        // Simulate restart by creating a new journal instance
        let journal2 = OutputJournal(baseDirectory: tempDirectory)
        
        // Replay should return identical data
        let replayed = try journal2.replay(paneID: paneID)
        XCTAssertEqual(replayed, originalData, "Scrollback after restart must be identical")
    }
    
    func testAcceptanceCriteria_OrphanTest() throws {
        // M4 Acceptance: "orphan test green"
        
        let activePanes: Set<UUID> = [UUID(), UUID(), UUID()]
        let orphanPanes: Set<UUID> = [UUID(), UUID()]
        
        // Create journals for both active and orphan panes
        for paneID in activePanes {
            try journal.append(paneID: paneID, data: Data("Active pane \(paneID)".utf8))
        }
        for paneID in orphanPanes {
            try journal.append(paneID: paneID, data: Data("Orphan pane \(paneID)".utf8))
        }
        
        // Purge orphans
        try journal.purgeOrphans(activePaneIDs: activePanes)
        
        // Verify active panes still exist
        for paneID in activePanes {
            let replayed = try journal.replay(paneID: paneID)
            XCTAssertFalse(replayed.isEmpty, "Active pane \(paneID) should not be purged")
        }
        
        // Verify orphan panes are gone
        for paneID in orphanPanes {
            let replayed = try journal.replay(paneID: paneID)
            XCTAssertTrue(replayed.isEmpty, "Orphan pane \(paneID) should be purged")
        }
    }
}
