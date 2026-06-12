import Cocoa
import YOLOTermKit

/// TilingView: manages multiple PaneViews using LayoutEngine
public class TilingView: NSView {
    private let engine = LayoutEngine()
    private var paneViews: [PaneView] = []
    private var currentPreset: LayoutPreset = .auto
    private var zoomedPaneId: String?
    private var focusedPaneIndex: Int = 0
    
    public override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
    }
    
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }
    
    // MARK: - Pane Management
    
    public func addPane(_ paneView: PaneView) {
        paneViews.append(paneView)
        addSubview(paneView)
        relayout(animated: false)
        focusedPaneIndex = paneViews.count - 1
    }
    
    public func removePane(_ paneId: String) {
        guard let index = paneViews.firstIndex(where: { $0.paneId == paneId }) else {
            return
        }
        
        let paneView = paneViews[index]
        paneView.removeFromSuperview()
        paneViews.remove(at: index)
        
        // Adjust focus if needed
        if focusedPaneIndex >= paneViews.count {
            focusedPaneIndex = max(0, paneViews.count - 1)
        }
        
        // Clear zoom if zoomed pane was removed
        if zoomedPaneId == paneId {
            zoomedPaneId = nil
        }
        
        relayout(animated: true)
    }
    
    public func getFocusedPane() -> PaneView? {
        guard !paneViews.isEmpty && focusedPaneIndex < paneViews.count else {
            return nil
        }
        return paneViews[focusedPaneIndex]
    }
    
    public func focusPane(at index: Int) {
        guard index >= 0 && index < paneViews.count else { return }
        focusedPaneIndex = index
        window?.makeFirstResponder(paneViews[index].getTerminalView())
    }
    
    public func focusPaneById(_ paneId: String) {
        guard let index = paneViews.firstIndex(where: { $0.paneId == paneId }) else {
            return
        }
        focusPane(at: index)
    }
    
    // MARK: - Layout Management
    
    public func setPreset(_ preset: LayoutPreset) {
        currentPreset = preset
        relayout(animated: true)
    }
    
    public func toggleZoom() {
        if let focusedPane = getFocusedPane() {
            if zoomedPaneId == focusedPane.paneId {
                // Un-zoom
                zoomedPaneId = nil
            } else {
                // Zoom focused pane
                zoomedPaneId = focusedPane.paneId
            }
            relayout(animated: true)
        }
    }
    
    public func equalize() {
        let rects = engine.equalize(
            paneIds: paneViews.map { $0.paneId },
            preset: currentPreset,
            containerSize: ContainerSize(width: bounds.width, height: bounds.height)
        )
        applyLayout(rects, animated: true)
    }
    
    private func relayout(animated: Bool) {
        let containerSize = ContainerSize(width: bounds.width, height: bounds.height)
        let paneIds = paneViews.map { $0.paneId }
        
        let rects = engine.calculate(
            paneIds: paneIds,
            preset: currentPreset,
            containerSize: containerSize,
            dragDeltas: [],
            zoomedPane: zoomedPaneId
        )
        
        applyLayout(rects, animated: animated)
    }
    
    private func applyLayout(_ rects: [PaneRect], animated: Bool) {
        let animations: () -> Void = {
            for rect in rects {
                guard let paneView = self.paneViews.first(where: { $0.paneId == rect.id }) else {
                    continue
                }
                
                let frame = NSRect(
                    x: rect.x,
                    y: rect.y,
                    width: rect.width,
                    height: rect.height
                )
                paneView.frame = frame
            }
        }
        
        if animated {
            NSAnimationContext.runAnimationGroup { context in
                context.duration = 0.25
                context.allowsImplicitAnimation = true
                animations()
            }
        } else {
            animations()
        }
    }
    
    // MARK: - Resize Handling
    
    public override func resizeSubviews(withOldSize oldSize: NSSize) {
        super.resizeSubviews(withOldSize: oldSize)
        relayout(animated: false)
    }
    
    // MARK: - Navigation
    
    public func focusPaneInDirection(_ direction: Direction) {
        guard !paneViews.isEmpty else { return }
        
        let currentPane = paneViews[focusedPaneIndex]
        let currentCenter = NSPoint(
            x: currentPane.frame.midX,
            y: currentPane.frame.midY
        )
        
        // Find the closest pane in the given direction
        var candidates: [(index: Int, distance: Double)] = []
        
        for (index, pane) in paneViews.enumerated() {
            guard index != focusedPaneIndex else { continue }
            
            let paneCenter = NSPoint(x: pane.frame.midX, y: pane.frame.midY)
            let dx = paneCenter.x - currentCenter.x
            let dy = paneCenter.y - currentCenter.y
            
            let isInDirection: Bool
            switch direction {
            case .up:
                isInDirection = dy > 0 && abs(dx) < abs(dy)
            case .down:
                isInDirection = dy < 0 && abs(dx) < abs(dy)
            case .left:
                isInDirection = dx < 0 && abs(dy) < abs(dx)
            case .right:
                isInDirection = dx > 0 && abs(dy) < abs(dx)
            }
            
            if isInDirection {
                let distance = sqrt(dx * dx + dy * dy)
                candidates.append((index: index, distance: distance))
            }
        }
        
        // Focus the closest candidate
        if let closest = candidates.min(by: { $0.distance < $1.distance }) {
            focusPane(at: closest.index)
        }
    }
    
    public enum Direction {
        case up, down, left, right
    }
}
