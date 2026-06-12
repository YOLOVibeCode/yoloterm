import Foundation

// MARK: - Layout Types (Pure Swift, No AppKit)

/// A rectangle representing a pane's position and size
public struct PaneRect: Equatable, Sendable {
    public let id: String
    public let x: Double
    public let y: Double
    public let width: Double
    public let height: Double
    
    public init(id: String, x: Double, y: Double, width: Double, height: Double) {
        self.id = id
        self.x = x
        self.y = y
        self.width = width
        self.height = height
    }
}

/// Container size
public struct ContainerSize: Equatable, Sendable {
    public let width: Double
    public let height: Double
    
    public init(width: Double, height: Double) {
        self.width = width
        self.height = height
    }
}

/// Layout preset types
public enum LayoutPreset: String, Sendable {
    case auto
    case single
    case columns
    case rows
    case grid
    case mainLeft = "main-left"
    case mainRight = "main-right"
}

/// Drag delta for border adjustments
public struct DragDelta: Sendable {
    public enum Orientation: Sendable {
        case horizontal
        case vertical
    }
    
    public let orientation: Orientation
    public let position: Double // normalized 0-1
    public let delta: Double // in pixels
    
    public init(orientation: Orientation, position: Double, delta: Double) {
        self.orientation = orientation
        self.position = position
        self.delta = delta
    }
}

// MARK: - Layout Engine

/// Pure function: (pane set, preset, container size, drag deltas) → pane rects
/// No UI types. Fixture-tested behavior.
public struct LayoutEngine: Sendable {
    
    public init() {}
    
    /// Calculate layout for given panes, preset, and container size
    public func calculate(
        paneIds: [String],
        preset: LayoutPreset,
        containerSize: ContainerSize,
        dragDeltas: [DragDelta] = [],
        zoomedPane: String? = nil
    ) -> [PaneRect] {
        // If a pane is zoomed, it fills the entire container
        if let zoomedId = zoomedPane {
            return [PaneRect(
                id: zoomedId,
                x: 0,
                y: 0,
                width: containerSize.width,
                height: containerSize.height
            )]
        }
        
        let count = paneIds.count
        guard count > 0 else { return [] }
        
        // Single pane always fills container
        if count == 1 {
            return [PaneRect(
                id: paneIds[0],
                x: 0,
                y: 0,
                width: containerSize.width,
                height: containerSize.height
            )]
        }
        
        // Apply preset
        let actualPreset = preset == .auto ? autoPreset(for: count) : preset
        
        switch actualPreset {
        case .single:
            return layoutSingle(paneIds: paneIds, containerSize: containerSize)
        case .columns:
            return layoutColumns(paneIds: paneIds, containerSize: containerSize)
        case .rows:
            return layoutRows(paneIds: paneIds, containerSize: containerSize)
        case .grid:
            return layoutGrid(paneIds: paneIds, containerSize: containerSize)
        case .mainLeft:
            return layoutMainLeft(paneIds: paneIds, containerSize: containerSize)
        case .mainRight:
            return layoutMainRight(paneIds: paneIds, containerSize: containerSize)
        case .auto:
            // Shouldn't reach here as auto is resolved above
            return layoutAuto(paneIds: paneIds, containerSize: containerSize)
        }
    }
    
    /// Equalize all panes (reset custom sizing)
    public func equalize(
        paneIds: [String],
        preset: LayoutPreset,
        containerSize: ContainerSize
    ) -> [PaneRect] {
        // Equalize just recalculates without drag deltas
        return calculate(
            paneIds: paneIds,
            preset: preset,
            containerSize: containerSize,
            dragDeltas: []
        )
    }
    
    // MARK: - Private Layout Algorithms
    
    private func autoPreset(for count: Int) -> LayoutPreset {
        switch count {
        case 1:
            return .single
        case 2:
            return .columns
        case 3:
            return .mainLeft
        case 4:
            return .grid
        default:
            // 5+ panes: use grid
            return .grid
        }
    }
    
    private func layoutSingle(paneIds: [String], containerSize: ContainerSize) -> [PaneRect] {
        guard let firstId = paneIds.first else { return [] }
        return [PaneRect(
            id: firstId,
            x: 0,
            y: 0,
            width: containerSize.width,
            height: containerSize.height
        )]
    }
    
    private func layoutColumns(paneIds: [String], containerSize: ContainerSize) -> [PaneRect] {
        let count = paneIds.count
        let colWidth = containerSize.width / Double(count)
        
        return paneIds.enumerated().map { index, id in
            PaneRect(
                id: id,
                x: Double(index) * colWidth,
                y: 0,
                width: colWidth,
                height: containerSize.height
            )
        }
    }
    
    private func layoutRows(paneIds: [String], containerSize: ContainerSize) -> [PaneRect] {
        let count = paneIds.count
        let rowHeight = containerSize.height / Double(count)
        
        return paneIds.enumerated().map { index, id in
            PaneRect(
                id: id,
                x: 0,
                y: Double(index) * rowHeight,
                width: containerSize.width,
                height: rowHeight
            )
        }
    }
    
    private func layoutGrid(paneIds: [String], containerSize: ContainerSize) -> [PaneRect] {
        let count = paneIds.count
        
        // Calculate grid dimensions
        let cols = Int(ceil(sqrt(Double(count))))
        let rows = Int(ceil(Double(count) / Double(cols)))
        
        let colWidth = containerSize.width / Double(cols)
        let rowHeight = containerSize.height / Double(rows)
        
        return paneIds.enumerated().map { index, id in
            let col = index % cols
            let row = index / cols
            
            return PaneRect(
                id: id,
                x: Double(col) * colWidth,
                y: Double(row) * rowHeight,
                width: colWidth,
                height: rowHeight
            )
        }
    }
    
    private func layoutMainLeft(paneIds: [String], containerSize: ContainerSize) -> [PaneRect] {
        guard paneIds.count >= 2 else {
            return layoutSingle(paneIds: paneIds, containerSize: containerSize)
        }
        
        // Main pane on left takes 50%, rest stacked on right
        let mainWidth = containerSize.width * 0.5
        let sideWidth = containerSize.width * 0.5
        
        let mainPane = PaneRect(
            id: paneIds[0],
            x: 0,
            y: 0,
            width: mainWidth,
            height: containerSize.height
        )
        
        let sideCount = paneIds.count - 1
        let sideHeight = containerSize.height / Double(sideCount)
        
        let sidePanes = paneIds.dropFirst().enumerated().map { index, id in
            PaneRect(
                id: id,
                x: mainWidth,
                y: Double(index) * sideHeight,
                width: sideWidth,
                height: sideHeight
            )
        }
        
        return [mainPane] + sidePanes
    }
    
    private func layoutMainRight(paneIds: [String], containerSize: ContainerSize) -> [PaneRect] {
        guard paneIds.count >= 2 else {
            return layoutSingle(paneIds: paneIds, containerSize: containerSize)
        }
        
        // Main pane on right takes 50%, rest stacked on left
        let sideWidth = containerSize.width * 0.5
        let mainWidth = containerSize.width * 0.5
        
        let sideCount = paneIds.count - 1
        let sideHeight = containerSize.height / Double(sideCount)
        
        let sidePanes = paneIds.dropFirst().enumerated().map { index, id in
            PaneRect(
                id: id,
                x: 0,
                y: Double(index) * sideHeight,
                width: sideWidth,
                height: sideHeight
            )
        }
        
        let mainPane = PaneRect(
            id: paneIds[0],
            x: sideWidth,
            y: 0,
            width: mainWidth,
            height: containerSize.height
        )
        
        return sidePanes + [mainPane]
    }
    
    private func layoutAuto(paneIds: [String], containerSize: ContainerSize) -> [PaneRect] {
        let actualPreset = autoPreset(for: paneIds.count)
        return calculate(
            paneIds: paneIds,
            preset: actualPreset,
            containerSize: containerSize
        )
    }
}
