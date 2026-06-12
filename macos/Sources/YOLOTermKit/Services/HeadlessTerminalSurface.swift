import Foundation
import SwiftTerm

/// Headless terminal surface for testing
/// Wraps SwiftTerm's headless Terminal class
public class HeadlessTerminalSurface: TerminalSurface {
    private let terminal: Terminal
    private let delegate: TerminalDelegate
    
    public var cols: Int { terminal.cols }
    public var rows: Int { terminal.rows }
    
    public init(cols: Int = 80, rows: Int = 24) {
        self.delegate = TerminalDelegate()
        var options = TerminalOptions.default
        options.cols = cols
        options.rows = rows
        self.terminal = Terminal(delegate: delegate, options: options)
    }
    
    public func feed(_ data: Data) {
        terminal.feed(byteArray: [UInt8](data))
    }
    
    public func getCellMetrics() -> CellMetrics {
        CellMetrics(width: 10.0, height: 20.0)
    }
    
    public func getCell(col: Int, row: Int) -> Cell? {
        guard col >= 0, col < terminal.cols,
              row >= 0, row < terminal.rows else {
            return nil
        }
        
        // Use getCharData API to access cell data with attributes
        guard let charData = terminal.getCharData(col: col, row: row) else {
            return Cell(char: " ", fg: nil, bg: nil, attrs: [])
        }
        
        // Extract colors (nil means default color)
        let fg = charData.attribute.fg == .defaultColor ? nil : extractColor(from: charData.attribute.fg)
        let bg = charData.attribute.bg == .defaultInvertedColor ? nil : extractColor(from: charData.attribute.bg)
        
        // Extract text attributes
        var attrs: Cell.Attributes = []
        if charData.attribute.style.contains(.bold) { attrs.insert(.bold) }
        if charData.attribute.style.contains(.dim) { attrs.insert(.dim) }
        if charData.attribute.style.contains(.italic) { attrs.insert(.italic) }
        if charData.attribute.style.contains(.underline) { attrs.insert(.underline) }
        if charData.attribute.style.contains(.crossedOut) { attrs.insert(.strikethrough) }
        if charData.attribute.style.contains(.inverse) { attrs.insert(.inverse) }
        
        return Cell(
            char: String(terminal.getCharacter(for: charData)),
            fg: fg,
            bg: bg,
            attrs: attrs
        )
    }
    
    public func serialize() -> Data {
        Data()
    }
    
    private func extractColor(from swiftTermColor: SwiftTerm.Attribute.Color) -> Color? {
        switch swiftTermColor {
        case .defaultColor, .defaultInvertedColor:
            return nil
        case .ansi256(let code):
            return colorFromAnsi256(code)
        case .trueColor(let r, let g, let b):
            return Color(r: r, g: g, b: b)
        }
    }
    
    private func colorFromAnsi256(_ index: UInt8) -> Color {
        // ANSI 256 color palette
        switch index {
        // Standard ANSI 0-15
        case 0: return Color(r: 0, g: 0, b: 0)
        case 1: return Color(r: 205, g: 0, b: 0)
        case 2: return Color(r: 0, g: 205, b: 0)
        case 3: return Color(r: 205, g: 205, b: 0)
        case 4: return Color(r: 0, g: 0, b: 238)
        case 5: return Color(r: 205, g: 0, b: 205)
        case 6: return Color(r: 0, g: 205, b: 205)
        case 7: return Color(r: 229, g: 229, b: 229)
        case 8: return Color(r: 127, g: 127, b: 127)
        case 9: return Color(r: 255, g: 0, b: 0)
        case 10: return Color(r: 0, g: 255, b: 0)
        case 11: return Color(r: 255, g: 255, b: 0)
        case 12: return Color(r: 92, g: 92, b: 255)
        case 13: return Color(r: 255, g: 0, b: 255)
        case 14: return Color(r: 0, g: 255, b: 255)
        case 15: return Color(r: 255, g: 255, b: 255)
        
        // 216 color cube (16-231)
        case 16...231:
            let idx = Int(index) - 16
            let r = UInt8((idx / 36) * 51)
            let g = UInt8(((idx % 36) / 6) * 51)
            let b = UInt8((idx % 6) * 51)
            return Color(r: r, g: g, b: b)
        
        // Grayscale ramp (232-255)
        case 232...255:
            let gray = UInt8(8 + (Int(index) - 232) * 10)
            return Color(r: gray, g: gray, b: gray)
            
        default:
            return Color(r: 0, g: 0, b: 0)
        }
    }
    
    private class TerminalDelegate: SwiftTerm.TerminalDelegate {
        func isProcessTrusted(source: SwiftTerm.Terminal) -> Bool { true }
        func send(source: SwiftTerm.Terminal, data: ArraySlice<UInt8>) {}
        func showCursor(source: SwiftTerm.Terminal) {}
        func hideCursor(source: SwiftTerm.Terminal) {}
        func bell(source: SwiftTerm.Terminal) {}
        func bufferActivated(source: SwiftTerm.Terminal) {}
        func sizeChanged(source: SwiftTerm.Terminal) {}
        func setTerminalTitle(source: SwiftTerm.Terminal, title: String) {}
        func setTerminalIconTitle(source: SwiftTerm.Terminal, title: String) {}
        func windowCommand(source: SwiftTerm.Terminal, command: SwiftTerm.Terminal.WindowManipulationCommand) -> [UInt8]? { nil }
        func scrolled(source: SwiftTerm.Terminal, yDisp: Int) {}
        func linefeed(source: SwiftTerm.Terminal) {}
        func synchronizedOutputChanged(source: SwiftTerm.Terminal, active: Bool) {}
        func selectionChanged(source: SwiftTerm.Terminal) {}
        func cellSizeInPixels(source: SwiftTerm.Terminal) -> (width: Int, height: Int)? { nil }
        func mouseModeChanged(source: SwiftTerm.Terminal) {}
        func cursorStyleChanged(source: SwiftTerm.Terminal, newStyle: SwiftTerm.CursorStyle) {}
        func hostCurrentDirectoryUpdated(source: SwiftTerm.Terminal) {}
        func hostCurrentDocumentUpdated(source: SwiftTerm.Terminal) {}
        func colorChanged(source: SwiftTerm.Terminal, idx: Int?) {}
    }
}
