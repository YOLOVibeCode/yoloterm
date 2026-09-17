import Foundation

/// OutputJournal manages append-only capped raw-byte files for terminal output capture.
/// Each pane gets its own journal file for scrollback restoration.
///
/// - Capped size with atomic rotation (prevents unbounded growth)
/// - Replay-before-attach for restoring scrollback on pane reopen
/// - Orphan purge on startup (cleans up journals for deleted panes)
///
/// Storage: ~/Library/Application Support/YOLOTerm/journals/{pane-uuid}.bytes
@MainActor
public final class OutputJournal {
    
    // MARK: - Configuration
    
    /// Maximum journal file size before rotation (default: 10 MB)
    public static let maxJournalSize: Int = 10 * 1024 * 1024
    
    /// Number of rotated journals to keep (default: 2)
    public static let maxRotatedFiles: Int = 2
    
    // MARK: - Paths
    
    private let journalsDirectory: URL
    
    public init(baseDirectory: URL? = nil) {
        if let baseDirectory {
            self.journalsDirectory = baseDirectory.appendingPathComponent("journals", isDirectory: true)
        } else {
            let appSupport = FileManager.default.urls(
                for: .applicationSupportDirectory,
                in: .userDomainMask
            ).first!
            self.journalsDirectory = appSupport
                .appendingPathComponent("YOLOTerm", isDirectory: true)
                .appendingPathComponent("journals", isDirectory: true)
        }
        
        // Ensure journals directory exists
        try? FileManager.default.createDirectory(
            at: journalsDirectory,
            withIntermediateDirectories: true
        )
    }
    
    // MARK: - Public API
    
    /// Append raw terminal output bytes to the journal for a given pane.
    public func append(paneID: UUID, data: Data) throws {
        let journalURL = journalURL(for: paneID)
        let fileHandle: FileHandle
        
        // Open or create the journal file
        if FileManager.default.fileExists(atPath: journalURL.path) {
            fileHandle = try FileHandle(forWritingTo: journalURL)
            try fileHandle.seekToEnd()
        } else {
            FileManager.default.createFile(atPath: journalURL.path, contents: nil)
            fileHandle = try FileHandle(forWritingTo: journalURL)
        }
        
        defer { try? fileHandle.close() }
        
        // Write the data
        try fileHandle.write(contentsOf: data)
        
        // Check if rotation is needed
        let attrs = try FileManager.default.attributesOfItem(atPath: journalURL.path)
        if let fileSize = attrs[.size] as? Int, fileSize >= Self.maxJournalSize {
            try rotateJournal(for: paneID)
        }
    }
    
    /// Replay the journal for a pane, returning all captured output bytes.
    /// Used to restore scrollback when reopening a pane.
    public func replay(paneID: UUID) throws -> Data {
        let journalURL = journalURL(for: paneID)
        
        guard FileManager.default.fileExists(atPath: journalURL.path) else {
            return Data()
        }
        
        // Read all rotated journals in order, then the main journal
        var allData = Data()
        
        for i in (0..<Self.maxRotatedFiles).reversed() {
            let rotatedURL = rotatedJournalURL(for: paneID, index: i)
            if FileManager.default.fileExists(atPath: rotatedURL.path) {
                let rotatedData = try Data(contentsOf: rotatedURL)
                allData.append(rotatedData)
            }
        }
        
        // Append the main journal
        let mainData = try Data(contentsOf: journalURL)
        allData.append(mainData)
        
        return allData
    }
    
    /// Delete the journal for a pane (called when pane is closed).
    public func delete(paneID: UUID) throws {
        let journalURL = journalURL(for: paneID)
        
        // Delete main journal
        if FileManager.default.fileExists(atPath: journalURL.path) {
            try FileManager.default.removeItem(at: journalURL)
        }
        
        // Delete rotated journals
        for i in 0..<Self.maxRotatedFiles {
            let rotatedURL = rotatedJournalURL(for: paneID, index: i)
            if FileManager.default.fileExists(atPath: rotatedURL.path) {
                try FileManager.default.removeItem(at: rotatedURL)
            }
        }
    }
    
    /// Purge orphaned journals (journals for panes that no longer exist).
    /// Should be called on app startup with the set of active pane IDs.
    public func purgeOrphans(activePaneIDs: Set<UUID>) throws {
        let contents = try FileManager.default.contentsOfDirectory(
            at: journalsDirectory,
            includingPropertiesForKeys: nil
        )
        
        for fileURL in contents {
            let filename = fileURL.lastPathComponent
            
            // Extract pane UUID from filename (format: {uuid}.bytes or {uuid}.bytes.{n})
            let components = filename.split(separator: ".")
            guard let uuidString = components.first,
                  let paneID = UUID(uuidString: String(uuidString)) else {
                continue
            }
            
            // If this pane ID is not active, delete the journal
            if !activePaneIDs.contains(paneID) {
                try FileManager.default.removeItem(at: fileURL)
            }
        }
    }
    
    // MARK: - Private Helpers
    
    private func journalURL(for paneID: UUID) -> URL {
        journalsDirectory.appendingPathComponent("\(paneID.uuidString).bytes")
    }
    
    private func rotatedJournalURL(for paneID: UUID, index: Int) -> URL {
        journalsDirectory.appendingPathComponent("\(paneID.uuidString).bytes.\(index)")
    }
    
    /// Rotate the journal: rename current to .0, shift existing rotated files up, delete oldest.
    private func rotateJournal(for paneID: UUID) throws {
        let fileManager = FileManager.default
        let mainURL = journalURL(for: paneID)
        
        // Delete the oldest rotated file if it exists
        let oldestURL = rotatedJournalURL(for: paneID, index: Self.maxRotatedFiles - 1)
        if fileManager.fileExists(atPath: oldestURL.path) {
            try fileManager.removeItem(at: oldestURL)
        }
        
        // Shift all rotated files up by one
        for i in (0..<(Self.maxRotatedFiles - 1)).reversed() {
            let fromURL = rotatedJournalURL(for: paneID, index: i)
            let toURL = rotatedJournalURL(for: paneID, index: i + 1)
            
            if fileManager.fileExists(atPath: fromURL.path) {
                try fileManager.moveItem(at: fromURL, to: toURL)
            }
        }
        
        // Move main journal to .0
        let newRotatedURL = rotatedJournalURL(for: paneID, index: 0)
        try fileManager.moveItem(at: mainURL, to: newRotatedURL)
        
        // Create new empty main journal
        fileManager.createFile(atPath: mainURL.path, contents: nil)
    }
}
