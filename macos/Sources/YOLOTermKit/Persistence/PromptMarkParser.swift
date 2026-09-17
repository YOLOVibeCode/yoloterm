import Foundation

/// PromptMarkParser is a state machine for parsing OSC 133 (prompt marks) and OSC 7 (CWD)
/// escape sequences from terminal output.
///
/// OSC 133 zones:
/// - A: prompt start
/// - B: prompt end / command start
/// - C: command executed (pre-exec)
/// - D: command finished (with exit code and optional duration)
///
/// OSC 7: file://hostname/path (current working directory)
///
/// This parser is used by both:
/// - HistoryStore: to capture command boundaries and metadata
/// - PaneMetadataProvider: to update pane labels with current CWD
///
/// Heuristic fallback: If shell plugins are not installed, we fall back to
/// pattern matching on common shell prompts (limited accuracy).
public struct PromptMarkParser {
    
    // MARK: - Public Models
    
    public enum Event {
        case promptStart
        case promptEnd
        case commandStart(command: String)
        case commandEnd(exitCode: Int?, durationMs: Int?)
        case cwdChanged(path: String)
    }
    
    // MARK: - State
    
    private enum State {
        case normal
        case escape
        case osc
        case oscPayload(Int) // OSC number
        case collectingCommand // Between zone B and C
    }
    
    private var state: State = .normal
    private var buffer: [UInt8] = []
    
    /// Accumulated command text between zones B (command start) and C (command exec)
    private var commandBuffer: String = ""
    
    /// Whether we've seen any OSC 133 marks (indicates shell plugin is active)
    public private(set) var hasShellPlugin: Bool = false
    
    // MARK: - Public API
    
    public init() {}
    
    /// Feed raw terminal output bytes to the parser.
    /// Returns events as they are recognized.
    public mutating func feed(_ data: Data) -> [Event] {
        var events: [Event] = []
        
        for byte in data {
            events.append(contentsOf: processByte(byte))
        }
        
        return events
    }
    
    /// Reset the parser state (e.g., when switching panes).
    public mutating func reset() {
        state = .normal
        buffer.removeAll()
        commandBuffer = ""
        // Note: Don't reset hasShellPlugin - it's a detection flag that persists
    }
    
    // MARK: - State Machine
    
    private mutating func processByte(_ byte: UInt8) -> [Event] {
        switch state {
        case .normal, .collectingCommand:
            return processNormalByte(byte)
        case .escape:
            return processEscapeByte(byte)
        case .osc:
            return processOSCByte(byte)
        case .oscPayload(let oscNumber):
            return processOSCPayloadByte(byte, oscNumber: oscNumber)
        }
    }
    
    private mutating func processNormalByte(_ byte: UInt8) -> [Event] {
        if byte == 0x1B { // ESC
            state = .escape
            buffer = [byte]
            return []
        }
        
        // Accumulate command characters if we're in command collection mode
        if case .collectingCommand = state {
            // Collect printable characters
            if byte >= 0x20 && byte <= 0x7E {
                commandBuffer.append(Character(UnicodeScalar(byte)))
            }
        }
        
        return []
    }
    
    private mutating func processEscapeByte(_ byte: UInt8) -> [Event] {
        buffer.append(byte)
        
        if byte == 0x5D { // ESC ]  → OSC
            state = .osc
            return []
        }
        
        // Not an OSC sequence, reset
        state = .normal
        buffer.removeAll()
        return []
    }
    
    private mutating func processOSCByte(_ byte: UInt8) -> [Event] {
        // OSC format: ESC ] <number> ; <payload> ST
        // ST can be BEL (0x07) or ESC \ (0x1B 0x5C)
        
        if byte == 0x3B { // semicolon
            // Extract OSC number (before adding semicolon to buffer)
            let numberBytes = buffer.dropFirst(2) // skip ESC ]
            if let numberString = String(bytes: numberBytes, encoding: .ascii),
               let oscNumber = Int(numberString) {
                state = .oscPayload(oscNumber)
                buffer.removeAll()
                return []
            }
        }
        
        buffer.append(byte)
        
        // Continue accumulating OSC number
        return []
    }
    
    private mutating func processOSCPayloadByte(_ byte: UInt8, oscNumber: Int) -> [Event] {
        // Check for string terminator
        if byte == 0x07 { // BEL
            return finalizeOSC(oscNumber)
        }
        
        if byte == 0x5C && buffer.last == 0x1B { // ESC \
            buffer.removeLast() // remove ESC
            return finalizeOSC(oscNumber)
        }
        
        buffer.append(byte)
        return []
    }
    
    private mutating func finalizeOSC(_ oscNumber: Int) -> [Event] {
        defer {
            state = .normal
            buffer.removeAll()
        }
        
        let payload = String(bytes: buffer, encoding: .utf8) ?? ""
        
        switch oscNumber {
        case 7:
            return parseOSC7(payload)
        case 133:
            return parseOSC133(payload)
        default:
            return []
        }
    }
    
    // MARK: - OSC Parsers
    
    private mutating func parseOSC7(_ payload: String) -> [Event] {
        // OSC 7: file://hostname/path
        guard payload.hasPrefix("file://") else { return [] }
        
        // Extract path (skip hostname)
        let pathStart = payload.firstIndex(of: "/", offsetBy: 7) ?? payload.endIndex
        let path = String(payload[pathStart...])
        
        // URL decode
        let decoded = path.removingPercentEncoding ?? path
        
        return [.cwdChanged(path: decoded)]
    }
    
    private mutating func parseOSC133(_ payload: String) -> [Event] {
        hasShellPlugin = true
        
        // OSC 133 format: <zone>[;<key>=<value>]*
        let parts = payload.split(separator: ";", maxSplits: 1)
        guard let zone = parts.first else { return [] }
        
        let params = parts.count > 1 ? parseKeyValuePairs(String(parts[1])) : [:]
        
        switch zone {
        case "A":
            return [.promptStart]
            
        case "B":
            commandBuffer = ""
            state = .collectingCommand
            return [.promptEnd]
            
        case "C":
            let command = commandBuffer.trimmingCharacters(in: .whitespacesAndNewlines)
            commandBuffer = ""
            state = .normal
            return [.commandStart(command: command)]
            
        case "D":
            state = .normal
            let exitCode = params["exitCode"].flatMap(Int.init)
            let durationMs = params["duration"].flatMap(Int.init)
            return [.commandEnd(exitCode: exitCode, durationMs: durationMs)]
            
        default:
            return []
        }
    }
    
    private func parseKeyValuePairs(_ string: String) -> [String: String] {
        var result: [String: String] = [:]
        
        for pair in string.split(separator: ";") {
            let kv = pair.split(separator: "=", maxSplits: 1)
            if kv.count == 2 {
                result[String(kv[0])] = String(kv[1])
            }
        }
        
        return result
    }
}

// MARK: - Heuristic Fallback

extension PromptMarkParser {
    /// Heuristic command detection for shells without plugin support.
    /// This is much less accurate but provides some functionality.
    public static func extractCommandHeuristic(from line: String) -> String? {
        // Strip common prompt patterns
        let patterns = [
            #"^\w+@[\w\-]+:~?[^\$#]*[\$#]\s*"#, // user@host:path$
            #"^❯\s*"#, // Starship prompt
            #"^➜\s*"#, // Oh My Zsh prompt
            #"^[▶►]\s*"#, // Various custom prompts
        ]
        
        var cleaned = line
        for pattern in patterns {
            if let regex = try? NSRegularExpression(pattern: pattern) {
                let range = NSRange(cleaned.startIndex..<cleaned.endIndex, in: cleaned)
                if let match = regex.firstMatch(in: cleaned, range: range) {
                    cleaned = String(cleaned[Range(match.range, in: cleaned)!.upperBound...])
                }
            }
        }
        
        let trimmed = cleaned.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}

// MARK: - Helper

private extension String {
    func firstIndex(of character: Character, offsetBy: Int) -> String.Index? {
        let startIndex = index(self.startIndex, offsetBy: offsetBy, limitedBy: endIndex) ?? endIndex
        return self[startIndex...].firstIndex(of: character)
    }
}
