import XCTest
@testable import YOLOTermKit

final class PromptMarkParserTests: XCTestCase {
    
    var parser: PromptMarkParser!
    
    override func setUp() {
        parser = PromptMarkParser()
    }
    
    // MARK: - OSC 133 Prompt Marks
    
    func testOSC133_PromptStart() {
        let data = Data("\u{1B}]133;A\u{07}".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 1)
        if case .promptStart = events[0] {
            // Success
        } else {
            XCTFail("Expected promptStart event")
        }
    }
    
    func testOSC133_PromptEnd() {
        let data = Data("\u{1B}]133;B\u{07}".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 1)
        if case .promptEnd = events[0] {
            // Success
        } else {
            XCTFail("Expected promptEnd event")
        }
    }
    
    func testOSC133_CommandStart() {
        // Simulate: prompt end, command typed, command executed
        var events: [PromptMarkParser.Event] = []
        
        // Prompt end (zone B)
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;B\u{07}".utf8)))
        
        // User types command
        events.append(contentsOf: parser.feed(Data("git status".utf8)))
        
        // Command executed (zone C)
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;C\u{07}".utf8)))
        
        // Should have: promptEnd, commandStart
        XCTAssertEqual(events.count, 2)
        
        if case .promptEnd = events[0] {
            // Success
        } else {
            XCTFail("Expected promptEnd")
        }
        
        if case .commandStart(let command) = events[1] {
            XCTAssertEqual(command, "git status", "Should capture typed command")
        } else {
            XCTFail("Expected commandStart with captured command")
        }
    }
    
    func testOSC133_CommandEnd() {
        let data = Data("\u{1B}]133;D;exitCode=0;duration=123\u{07}".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 1)
        if case .commandEnd(let exitCode, let durationMs) = events[0] {
            XCTAssertEqual(exitCode, 0)
            XCTAssertEqual(durationMs, 123)
        } else {
            XCTFail("Expected commandEnd with exit code and duration")
        }
    }
    
    func testOSC133_FullSequence() {
        // Simulate a complete command cycle
        var events: [PromptMarkParser.Event] = []
        
        // 1. Prompt start
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;A\u{07}".utf8)))
        
        // 2. Prompt end / command start
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;B\u{07}".utf8)))
        
        // 3. User types
        events.append(contentsOf: parser.feed(Data("ls -la".utf8)))
        
        // 4. Command executed
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;C\u{07}".utf8)))
        
        // 5. Command finished
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;D;exitCode=0;duration=50\u{07}".utf8)))
        
        XCTAssertEqual(events.count, 4, "Should have 4 events")
        
        // Verify sequence
        if case .promptStart = events[0] {} else { XCTFail() }
        if case .promptEnd = events[1] {} else { XCTFail() }
        if case .commandStart(let cmd) = events[2] {
            XCTAssertEqual(cmd, "ls -la")
        } else { XCTFail() }
        if case .commandEnd(let code, let duration) = events[3] {
            XCTAssertEqual(code, 0)
            XCTAssertEqual(duration, 50)
        } else { XCTFail() }
    }
    
    // MARK: - OSC 7 (CWD)
    
    func testOSC7_CWD() {
        let data = Data("\u{1B}]7;file://hostname/Users/test/project\u{07}".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 1)
        if case .cwdChanged(let path) = events[0] {
            XCTAssertEqual(path, "/Users/test/project")
        } else {
            XCTFail("Expected cwdChanged event")
        }
    }
    
    func testOSC7_URLEncoded() {
        let data = Data("\u{1B}]7;file://hostname/Users/test/my%20folder\u{07}".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 1)
        if case .cwdChanged(let path) = events[0] {
            XCTAssertEqual(path, "/Users/test/my folder", "Should decode URL-encoded path")
        } else {
            XCTFail("Expected cwdChanged event")
        }
    }
    
    // MARK: - String Terminator Variants
    
    func testOSC_BELTerminator() {
        // OSC terminated with BEL (0x07)
        let data = Data("\u{1B}]133;A\u{07}".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 1)
    }
    
    func testOSC_STTerminator() {
        // OSC terminated with ESC \ (0x1B 0x5C)
        let data = Data("\u{1B}]133;A\u{1B}\\".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 1)
    }
    
    // MARK: - Shell Plugin Detection
    
    func testShellPluginDetection() {
        XCTAssertFalse(parser.hasShellPlugin, "Should start with no plugin detected")
        
        // Feed an OSC 133 sequence
        _ = parser.feed(Data("\u{1B}]133;A\u{07}".utf8))
        
        XCTAssertTrue(parser.hasShellPlugin, "Should detect shell plugin after OSC 133")
    }
    
    // MARK: - Reset
    
    func testReset() {
        _ = parser.feed(Data("\u{1B}]133;A\u{07}".utf8))
        XCTAssertTrue(parser.hasShellPlugin)
        
        parser.reset()
        
        // After reset, hasShellPlugin persists but state is cleared
        XCTAssertTrue(parser.hasShellPlugin, "hasShellPlugin is a detection flag that persists across resets")
    }
    
    // MARK: - Heuristic Fallback
    
    func testHeuristicCommandExtraction() {
        let testCases: [(input: String, expected: String?)] = [
            ("user@host:~/projects$ git status", "git status"),
            ("❯ npm install", "npm install"),
            ("➜ docker ps", "docker ps"),
            ("▶ ls -la", "ls -la"),
            ("git status", "git status"), // No prompt
            ("   ", nil) // Empty/whitespace only
        ]
        
        for (input, expected) in testCases {
            let result = PromptMarkParser.extractCommandHeuristic(from: input)
            XCTAssertEqual(result, expected, "Failed for input: \(input)")
        }
    }
    
    // MARK: - Edge Cases
    
    func testMultipleSequencesInOneBuffer() {
        // Multiple OSC sequences in a single data buffer
        let data = Data("\u{1B}]133;A\u{07}\u{1B}]133;B\u{07}".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 2)
        if case .promptStart = events[0] {} else { XCTFail() }
        if case .promptEnd = events[1] {} else { XCTFail() }
    }
    
    func testInterleavedNormalAndOSC() {
        // Normal output interleaved with OSC sequences
        let data = Data("Hello\u{1B}]133;A\u{07}World".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 1)
        if case .promptStart = events[0] {} else { XCTFail() }
    }
    
    func testUnrecognizedOSC() {
        // OSC sequence we don't handle (should be ignored gracefully)
        let data = Data("\u{1B}]999;unknown\u{07}".utf8)
        let events = parser.feed(data)
        
        XCTAssertEqual(events.count, 0, "Unrecognized OSC should be ignored")
    }
    
    // MARK: - Real-world Fixture Tests
    
    func testZshPluginOutput() {
        // Simulate output from yoloterm.zsh plugin
        var events: [PromptMarkParser.Event] = []
        
        // Initial prompt start
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;A\u{07}".utf8)))
        
        // CWD update
        events.append(contentsOf: parser.feed(Data("\u{1B}]7;file://hostname/Users/test\u{07}".utf8)))
        
        // Prompt rendered, user input starts
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;B\u{07}".utf8)))
        
        // Command typed
        events.append(contentsOf: parser.feed(Data("git status".utf8)))
        
        // Command executed
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;C\u{07}".utf8)))
        
        // Command output (normal text)
        events.append(contentsOf: parser.feed(Data("On branch main\n".utf8)))
        
        // Command finished
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;D;exitCode=0;duration=123\u{07}".utf8)))
        
        // Next prompt
        events.append(contentsOf: parser.feed(Data("\u{1B}]133;A\u{07}".utf8)))
        
        // Verify we captured all the important events
        let eventTypes = events.map { event -> String in
            switch event {
            case .promptStart: return "promptStart"
            case .promptEnd: return "promptEnd"
            case .commandStart: return "commandStart"
            case .commandEnd: return "commandEnd"
            case .cwdChanged: return "cwdChanged"
            }
        }
        
        XCTAssertEqual(eventTypes, [
            "promptStart",
            "cwdChanged",
            "promptEnd",
            "commandStart",
            "commandEnd",
            "promptStart"
        ])
    }
}
