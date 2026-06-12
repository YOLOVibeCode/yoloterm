import Foundation
import XCTest
@testable import YOLOTermKit

/// Color fixtures test case
struct ColorFixture: Codable {
    let name: String
    let input_b64: String
    let grid: [[CellExpectation]]
    
    struct CellExpectation: Codable {
        let ch: String
        let fg: String?
        let bg: String?
        let attrs: [String]?
    }
}

/// Golden test runner for contracts/fixtures/colors/
/// This is GATE 1 — must be completely green
final class ColorGoldenTests: XCTestCase {
    func testColorFixturesCorpus() async throws {
        // Load fixtures
        let fixturesURL = try getFixturesURL()
        let fixtureFile = fixturesURL
            .appendingPathComponent("colors")
            .appendingPathComponent("color-fixtures.json")
        
        let data = try Data(contentsOf: fixtureFile)
        let fixtures = try JSONDecoder().decode([ColorFixture].self, from: data)
        
        print("Running \(fixtures.count) color golden tests...")
        
        var failures: [(String, String)] = []
        
        for fixture in fixtures {
            do {
                try await runFixture(fixture)
                print("✅ \(fixture.name)")
            } catch {
                let message = "❌ \(fixture.name): \(error)"
                print(message)
                failures.append((fixture.name, message))
            }
        }
        
        if !failures.isEmpty {
            XCTFail("""
                GATE 1 FAILURE: \(failures.count)/\(fixtures.count) color fixtures failed
                
                Failed tests:
                \(failures.map { $0.1 }.joined(separator: "\n"))
                
                Color rendering must be 100% correct to proceed past GATE 1.
                """)
        }
        
        print("\n✅ GATE 1: All \(fixtures.count) color fixtures passed")
    }
    
    private func runFixture(_ fixture: ColorFixture) async throws {
        // Decode base64 input
        guard let decodedString = Data(base64Encoded: fixture.input_b64),
              let inputString = String(data: decodedString, encoding: .utf8) else {
            throw FixtureError.invalidBase64
        }
        
        // Convert literal escape sequences (\x1b) to actual bytes
        let inputData = convertEscapeSequences(inputString)
        
        // Create headless terminal (80x24 per fixture format)
        let surface = HeadlessTerminalSurface(cols: 80, rows: 24)
        
        // Feed the VT sequence
        await surface.feed(inputData)
        
        // Validate grid
        for (rowIdx, expectedRow) in fixture.grid.enumerated() {
            for (colIdx, expectedCell) in expectedRow.enumerated() {
                let actualCell = await surface.getCell(col: colIdx, row: rowIdx)
                
                guard let actualCell = actualCell else {
                    throw FixtureError.cellNotFound(col: colIdx, row: rowIdx)
                }
                
                try validateCell(
                    actual: actualCell,
                    expected: expectedCell,
                    col: colIdx,
                    row: rowIdx,
                    fixtureName: fixture.name
                )
            }
        }
    }
    
    /// Convert literal escape sequences like \x1b to actual bytes
    private func convertEscapeSequences(_ input: String) -> Data {
        var result = Data()
        var index = input.startIndex
        
        while index < input.endIndex {
            if input[index] == "\\" && input.index(after: index) < input.endIndex {
                let nextIndex = input.index(after: index)
                let nextChar = input[nextIndex]
                
                if nextChar == "x" {
                    // Handle \xHH hex sequences
                    let hexStart = input.index(after: nextIndex)
                    let hexEnd = input.index(hexStart, offsetBy: 2, limitedBy: input.endIndex) ?? input.endIndex
                    if hexEnd <= input.endIndex {
                        let hexString = String(input[hexStart..<hexEnd])
                        if let byte = UInt8(hexString, radix: 16) {
                            result.append(byte)
                            index = hexEnd
                            continue
                        }
                    }
                }
            }
            
            // Default: add the character as-is
            if let scalar = input[index].unicodeScalars.first {
                let utf8 = String(scalar).utf8
                result.append(contentsOf: utf8)
            }
            index = input.index(after: index)
        }
        
        return result
    }
    
    private func validateCell(
        actual: Cell,
        expected: ColorFixture.CellExpectation,
        col: Int,
        row: Int,
        fixtureName: String
    ) throws {
        // Validate character
        if actual.char != expected.ch {
            throw FixtureError.charMismatch(
                col: col, row: row,
                expected: expected.ch,
                actual: actual.char
            )
        }
        
        // Validate foreground color
        if let expectedFg = expected.fg {
            guard let expectedColor = Color(hex: expectedFg) else {
                throw FixtureError.invalidColorFormat(expectedFg)
            }
            
            guard let actualFg = actual.fg else {
                throw FixtureError.colorMismatch(
                    col: col, row: row,
                    type: "fg",
                    expected: expectedFg,
                    actual: "nil"
                )
            }
            
            if !colorsMatch(actualFg, expectedColor) {
                throw FixtureError.colorMismatch(
                    col: col, row: row,
                    type: "fg",
                    expected: expectedFg,
                    actual: actualFg.hex
                )
            }
        }
        
        // Validate background color
        if let expectedBg = expected.bg {
            guard let expectedColor = Color(hex: expectedBg) else {
                throw FixtureError.invalidColorFormat(expectedBg)
            }
            
            guard let actualBg = actual.bg else {
                throw FixtureError.colorMismatch(
                    col: col, row: row,
                    type: "bg",
                    expected: expectedBg,
                    actual: "nil"
                )
            }
            
            if !colorsMatch(actualBg, expectedColor) {
                throw FixtureError.colorMismatch(
                    col: col, row: row,
                    type: "bg",
                    expected: expectedBg,
                    actual: actualBg.hex
                )
            }
        }
        
        // Validate attributes
        if let expectedAttrs = expected.attrs {
            var expectedAttrSet: Cell.Attributes = []
            for attr in expectedAttrs {
                switch attr {
                case "bold": expectedAttrSet.insert(.bold)
                case "italic": expectedAttrSet.insert(.italic)
                case "underline": expectedAttrSet.insert(.underline)
                case "strikethrough": expectedAttrSet.insert(.strikethrough)
                case "dim": expectedAttrSet.insert(.dim)
                case "inverse": expectedAttrSet.insert(.inverse)
                default: break
                }
            }
            
            if actual.attrs != expectedAttrSet {
                throw FixtureError.attrMismatch(
                    col: col, row: row,
                    expected: expectedAttrs,
                    actual: Array(actual.attrs.stringRepresentation())
                )
            }
        }
    }
    
    private func colorsMatch(_ a: Color, _ b: Color) -> Bool {
        // Allow 1-bit tolerance for quantization
        abs(Int(a.r) - Int(b.r)) <= 1 &&
        abs(Int(a.g) - Int(b.g)) <= 1 &&
        abs(Int(a.b) - Int(b.b)) <= 1
    }
    
    private func getFixturesURL() throws -> URL {
        // #file in SPM is relative, working directory is macos/
        // Navigate from macos/ -> ../contracts/fixtures
        let fileManager = FileManager.default
        let currentDir = URL(fileURLWithPath: fileManager.currentDirectoryPath)
        
        return currentDir
            .deletingLastPathComponent()  // yoloterm/
            .appendingPathComponent("contracts")
            .appendingPathComponent("fixtures")
    }
}

enum FixtureError: Error, CustomStringConvertible {
    case invalidBase64
    case cellNotFound(col: Int, row: Int)
    case charMismatch(col: Int, row: Int, expected: String, actual: String)
    case colorMismatch(col: Int, row: Int, type: String, expected: String, actual: String)
    case attrMismatch(col: Int, row: Int, expected: [String], actual: [String])
    case invalidColorFormat(String)
    
    var description: String {
        switch self {
        case .invalidBase64:
            return "Invalid base64 input"
        case .cellNotFound(let col, let row):
            return "Cell not found at (\(col),\(row))"
        case .charMismatch(let col, let row, let expected, let actual):
            return "Char mismatch at (\(col),\(row)): expected '\(expected)', got '\(actual)'"
        case .colorMismatch(let col, let row, let type, let expected, let actual):
            return "Color \(type) mismatch at (\(col),\(row)): expected \(expected), got \(actual)"
        case .attrMismatch(let col, let row, let expected, let actual):
            return "Attrs mismatch at (\(col),\(row)): expected \(expected), got \(actual)"
        case .invalidColorFormat(let color):
            return "Invalid color format: \(color)"
        }
    }
}

extension Cell.Attributes {
    func stringRepresentation() -> [String] {
        var result: [String] = []
        if contains(.bold) { result.append("bold") }
        if contains(.italic) { result.append("italic") }
        if contains(.underline) { result.append("underline") }
        if contains(.strikethrough) { result.append("strikethrough") }
        if contains(.dim) { result.append("dim") }
        if contains(.inverse) { result.append("inverse") }
        return result
    }
}
