import XCTest
@testable import YOLOTermKit

final class LayoutEngineTests: XCTestCase {
    
    let engine = LayoutEngine()
    
    // MARK: - Fixture Tests
    
    func testSinglePane() throws {
        // Load fixture: contracts/fixtures/layout/single-pane.json
        let fixture = try loadLayoutFixture(named: "single-pane")
        
        let result = engine.calculate(
            paneIds: fixture.input.panes.map { $0.id },
            preset: fixture.input.preset,
            containerSize: fixture.input.containerSize
        )
        
        assertLayoutMatches(result: result, expected: fixture.expected)
    }
    
    func testTwoColumns() throws {
        // Load fixture: contracts/fixtures/layout/two-columns.json
        let fixture = try loadLayoutFixture(named: "two-columns")
        
        let result = engine.calculate(
            paneIds: fixture.input.panes.map { $0.id },
            preset: fixture.input.preset,
            containerSize: fixture.input.containerSize
        )
        
        assertLayoutMatches(result: result, expected: fixture.expected)
    }
    
    func testFourGrid() throws {
        // Load fixture: contracts/fixtures/layout/four-grid.json
        let fixture = try loadLayoutFixture(named: "four-grid")
        
        let result = engine.calculate(
            paneIds: fixture.input.panes.map { $0.id },
            preset: fixture.input.preset,
            containerSize: fixture.input.containerSize
        )
        
        assertLayoutMatches(result: result, expected: fixture.expected)
    }
    
    // MARK: - Preset Tests
    
    func testRowsPreset() {
        let result = engine.calculate(
            paneIds: ["pane-1", "pane-2"],
            preset: .rows,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        XCTAssertEqual(result.count, 2)
        
        // First pane on top
        XCTAssertEqual(result[0].id, "pane-1")
        XCTAssertEqual(result[0].x, 0)
        XCTAssertEqual(result[0].y, 0)
        XCTAssertEqual(result[0].width, 800)
        XCTAssertEqual(result[0].height, 300)
        
        // Second pane below
        XCTAssertEqual(result[1].id, "pane-2")
        XCTAssertEqual(result[1].x, 0)
        XCTAssertEqual(result[1].y, 300)
        XCTAssertEqual(result[1].width, 800)
        XCTAssertEqual(result[1].height, 300)
    }
    
    func testMainLeftPreset() {
        let result = engine.calculate(
            paneIds: ["main", "side-1", "side-2"],
            preset: .mainLeft,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        XCTAssertEqual(result.count, 3)
        
        // Main pane on left
        XCTAssertEqual(result[0].id, "main")
        XCTAssertEqual(result[0].x, 0)
        XCTAssertEqual(result[0].y, 0)
        XCTAssertEqual(result[0].width, 400)
        XCTAssertEqual(result[0].height, 600)
        
        // Side panes stacked on right
        XCTAssertEqual(result[1].id, "side-1")
        XCTAssertEqual(result[1].x, 400)
        XCTAssertEqual(result[1].y, 0)
        XCTAssertEqual(result[1].width, 400)
        XCTAssertEqual(result[1].height, 300)
        
        XCTAssertEqual(result[2].id, "side-2")
        XCTAssertEqual(result[2].x, 400)
        XCTAssertEqual(result[2].y, 300)
        XCTAssertEqual(result[2].width, 400)
        XCTAssertEqual(result[2].height, 300)
    }
    
    func testMainRightPreset() {
        let result = engine.calculate(
            paneIds: ["main", "side-1", "side-2"],
            preset: .mainRight,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        XCTAssertEqual(result.count, 3)
        
        // Side panes stacked on left
        XCTAssertEqual(result[0].id, "side-1")
        XCTAssertEqual(result[0].x, 0)
        XCTAssertEqual(result[0].y, 0)
        XCTAssertEqual(result[0].width, 400)
        XCTAssertEqual(result[0].height, 300)
        
        XCTAssertEqual(result[1].id, "side-2")
        XCTAssertEqual(result[1].x, 0)
        XCTAssertEqual(result[1].y, 300)
        XCTAssertEqual(result[1].width, 400)
        XCTAssertEqual(result[1].height, 300)
        
        // Main pane on right
        XCTAssertEqual(result[2].id, "main")
        XCTAssertEqual(result[2].x, 400)
        XCTAssertEqual(result[2].y, 0)
        XCTAssertEqual(result[2].width, 400)
        XCTAssertEqual(result[2].height, 600)
    }
    
    func testAutoPresetWithOnePane() {
        let result = engine.calculate(
            paneIds: ["pane-1"],
            preset: .auto,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        XCTAssertEqual(result.count, 1)
        XCTAssertEqual(result[0].width, 800)
        XCTAssertEqual(result[0].height, 600)
    }
    
    func testAutoPresetWithTwoPanes() {
        let result = engine.calculate(
            paneIds: ["pane-1", "pane-2"],
            preset: .auto,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        // Auto with 2 panes = columns
        XCTAssertEqual(result.count, 2)
        XCTAssertEqual(result[0].width, 400)
        XCTAssertEqual(result[1].width, 400)
    }
    
    func testAutoPresetWithThreePanes() {
        let result = engine.calculate(
            paneIds: ["pane-1", "pane-2", "pane-3"],
            preset: .auto,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        // Auto with 3 panes = main-left
        XCTAssertEqual(result.count, 3)
        XCTAssertEqual(result[0].width, 400) // main
        XCTAssertEqual(result[1].width, 400) // side
        XCTAssertEqual(result[2].width, 400) // side
    }
    
    func testAutoPresetWithFourPanes() {
        let result = engine.calculate(
            paneIds: ["pane-1", "pane-2", "pane-3", "pane-4"],
            preset: .auto,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        // Auto with 4 panes = grid (2x2)
        XCTAssertEqual(result.count, 4)
        for pane in result {
            XCTAssertEqual(pane.width, 400)
            XCTAssertEqual(pane.height, 300)
        }
    }
    
    // MARK: - Zoom Tests
    
    func testZoomPane() {
        let result = engine.calculate(
            paneIds: ["pane-1", "pane-2", "pane-3"],
            preset: .columns,
            containerSize: ContainerSize(width: 800, height: 600),
            zoomedPane: "pane-2"
        )
        
        // When zoomed, only the zoomed pane is returned filling container
        XCTAssertEqual(result.count, 1)
        XCTAssertEqual(result[0].id, "pane-2")
        XCTAssertEqual(result[0].x, 0)
        XCTAssertEqual(result[0].y, 0)
        XCTAssertEqual(result[0].width, 800)
        XCTAssertEqual(result[0].height, 600)
    }
    
    // MARK: - Equalize Tests
    
    func testEqualize() {
        let result = engine.equalize(
            paneIds: ["pane-1", "pane-2"],
            preset: .columns,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        // Equalize should reset to default sizing
        XCTAssertEqual(result.count, 2)
        XCTAssertEqual(result[0].width, 400)
        XCTAssertEqual(result[1].width, 400)
    }
    
    // MARK: - Edge Cases
    
    func testEmptyPaneList() {
        let result = engine.calculate(
            paneIds: [],
            preset: .single,
            containerSize: ContainerSize(width: 800, height: 600)
        )
        
        XCTAssertEqual(result.count, 0)
    }
    
    func testGridWithFivePanes() {
        let result = engine.calculate(
            paneIds: ["p1", "p2", "p3", "p4", "p5"],
            preset: .grid,
            containerSize: ContainerSize(width: 900, height: 600)
        )
        
        // 5 panes in grid = 3 cols × 2 rows
        XCTAssertEqual(result.count, 5)
        
        let colWidth = 900.0 / 3.0
        let rowHeight = 600.0 / 2.0
        
        // Verify first row
        XCTAssertEqual(result[0].x, 0)
        XCTAssertEqual(result[0].y, 0)
        XCTAssertEqual(result[0].width, colWidth, accuracy: 0.01)
        XCTAssertEqual(result[0].height, rowHeight, accuracy: 0.01)
        
        XCTAssertEqual(result[1].x, colWidth, accuracy: 0.01)
        XCTAssertEqual(result[1].y, 0)
        
        XCTAssertEqual(result[2].x, colWidth * 2, accuracy: 0.01)
        XCTAssertEqual(result[2].y, 0)
        
        // Verify second row
        XCTAssertEqual(result[3].x, 0)
        XCTAssertEqual(result[3].y, rowHeight, accuracy: 0.01)
        
        XCTAssertEqual(result[4].x, colWidth, accuracy: 0.01)
        XCTAssertEqual(result[4].y, rowHeight, accuracy: 0.01)
    }
    
    // MARK: - Helpers
    
    private func loadLayoutFixture(named name: String) throws -> LayoutFixture {
        // Get the contracts directory (go up from macos/ to project root)
        let testBundle = Bundle(for: type(of: self))
        let projectRoot = testBundle.bundleURL
            .deletingLastPathComponent()  // remove test bundle name
            .deletingLastPathComponent()  // remove .build
            .deletingLastPathComponent()  // remove debug-macos
            .deletingLastPathComponent()  // remove .build
            .deletingLastPathComponent()  // remove macos/
        
        let fixturePath = projectRoot
            .appendingPathComponent("contracts")
            .appendingPathComponent("fixtures")
            .appendingPathComponent("layout")
            .appendingPathComponent("\(name).json")
        
        let data = try Data(contentsOf: fixturePath)
        let decoder = JSONDecoder()
        return try decoder.decode(LayoutFixture.self, from: data)
    }
    
    private func assertLayoutMatches(result: [PaneRect], expected: [ExpectedPaneRect], file: StaticString = #file, line: UInt = #line) {
        XCTAssertEqual(result.count, expected.count, "Pane count mismatch", file: file, line: line)
        
        for expectedPane in expected {
            guard let actualPane = result.first(where: { $0.id == expectedPane.id }) else {
                XCTFail("Missing pane: \(expectedPane.id)", file: file, line: line)
                continue
            }
            
            XCTAssertEqual(actualPane.x, Double(expectedPane.x), accuracy: 0.01, "x mismatch for \(expectedPane.id)", file: file, line: line)
            XCTAssertEqual(actualPane.y, Double(expectedPane.y), accuracy: 0.01, "y mismatch for \(expectedPane.id)", file: file, line: line)
            XCTAssertEqual(actualPane.width, Double(expectedPane.width), accuracy: 0.01, "width mismatch for \(expectedPane.id)", file: file, line: line)
            XCTAssertEqual(actualPane.height, Double(expectedPane.height), accuracy: 0.01, "height mismatch for \(expectedPane.id)", file: file, line: line)
        }
    }
}

// MARK: - Fixture Types

struct LayoutFixture: Codable {
    let name: String
    let input: LayoutInput
    let expected: [ExpectedPaneRect]
}

struct LayoutInput: Codable {
    let containerSize: ContainerSize
    let panes: [PaneId]
    let preset: LayoutPreset
}

struct PaneId: Codable {
    let id: String
}

struct ExpectedPaneRect: Codable {
    let id: String
    let x: Int
    let y: Int
    let width: Int
    let height: Int
}

// Codable conformance for LayoutPreset
extension LayoutPreset: Codable {
    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        let rawValue = try container.decode(String.self)
        guard let preset = LayoutPreset(rawValue: rawValue) else {
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Unknown preset: \(rawValue)")
        }
        self = preset
    }
}

// Codable conformance for ContainerSize
extension ContainerSize: Codable {
    enum CodingKeys: String, CodingKey {
        case width, height
    }
    
    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let width = try container.decode(Double.self, forKey: .width)
        let height = try container.decode(Double.self, forKey: .height)
        self.init(width: width, height: height)
    }
    
    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(width, forKey: .width)
        try container.encode(height, forKey: .height)
    }
}
