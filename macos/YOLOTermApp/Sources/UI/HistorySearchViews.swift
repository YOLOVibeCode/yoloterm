import SwiftUI
import YOLOTermKit

/// HistorySearchPanel provides pane-scoped command history search (Ctrl+R).
/// Appears inline within a pane, keyboard-navigable, fuzzy search with FTS5.
struct HistorySearchPanel: View {
    @Binding var isPresented: Bool
    let paneID: String
    let historyStore: HistoryStore
    let onSelect: (HistoryStore.Command) -> Void
    
    @State private var query: String = ""
    @State private var results: [HistoryStore.Command] = []
    @State private var selectedIndex: Int = 0
    @FocusState private var isSearchFieldFocused: Bool
    
    var body: some View {
        VStack(spacing: 0) {
            // Search field
            HStack {
                Image(systemName: "magnifyingglass")
                    .foregroundColor(.secondary)
                
                TextField("Search history...", text: $query)
                    .textFieldStyle(.plain)
                    .focused($isSearchFieldFocused)
                    .onSubmit {
                        selectCurrentResult()
                    }
                
                if !query.isEmpty {
                    Button(action: { query = "" }) {
                        Image(systemName: "xmark.circle.fill")
                            .foregroundColor(.secondary)
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(8)
            .background(Color(nsColor: .controlBackgroundColor))
            
            Divider()
            
            // Results list
            ScrollViewReader { proxy in
                List(Array(results.enumerated()), id: \.element.id) { index, command in
                    HistoryResultRow(
                        command: command,
                        isSelected: index == selectedIndex
                    )
                    .id(index)
                    .onTapGesture {
                        selectedIndex = index
                        selectCurrentResult()
                    }
                }
                .listStyle(.plain)
                .onChange(of: selectedIndex) { _, newIndex in
                    proxy.scrollTo(newIndex, anchor: .center)
                }
            }
            .frame(maxHeight: 300)
        }
        .frame(width: 600)
        .background(Color(nsColor: .windowBackgroundColor))
        .cornerRadius(8)
        .shadow(radius: 10)
        .onAppear {
            isSearchFieldFocused = true
            performSearch()
        }
        .onChange(of: query) { _, _ in
            performSearch()
        }
        .onKeyPress(.upArrow) {
            if selectedIndex > 0 {
                selectedIndex -= 1
            }
            return .handled
        }
        .onKeyPress(.downArrow) {
            if selectedIndex < results.count - 1 {
                selectedIndex += 1
            }
            return .handled
        }
        .onKeyPress(.escape) {
            isPresented = false
            return .handled
        }
    }
    
    private func performSearch() {
        Task {
            do {
                let options = HistoryStore.SearchOptions(
                    paneID: paneID,
                    limit: 50
                )
                
                if query.isEmpty {
                    results = try historyStore.recent(options: options)
                } else {
                    results = try historyStore.search(query: query, options: options)
                }
                
                selectedIndex = 0
            } catch {
                print("Search error: \(error)")
                results = []
            }
        }
    }
    
    private func selectCurrentResult() {
        guard selectedIndex < results.count else { return }
        let command = results[selectedIndex]
        onSelect(command)
        isPresented = false
    }
}

struct HistoryResultRow: View {
    let command: HistoryStore.Command
    let isSelected: Bool
    
    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(command.command)
                .font(.system(.body, design: .monospaced))
                .lineLimit(2)
            
            HStack {
                if let cwd = command.cwd {
                    Label(cwd, systemImage: "folder")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                Spacer()
                
                if let exitCode = command.exitCode {
                    Text(exitCode == 0 ? "✓" : "✗ \(exitCode)")
                        .font(.caption)
                        .foregroundColor(exitCode == 0 ? .green : .red)
                }
                
                Text(formatTimestamp(command.timestamp))
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
        }
        .padding(8)
        .background(isSelected ? Color.accentColor.opacity(0.2) : Color.clear)
        .cornerRadius(4)
    }
    
    private func formatTimestamp(_ timestamp: Int64) -> String {
        let date = Date(timeIntervalSince1970: TimeInterval(timestamp) / 1000)
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .abbreviated
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}

// MARK: - Global History Search Window

/// GlobalHistorySearchWindow provides global command history search (Cmd+Shift+R).
/// Searches across all panes and tabs, keyboard-navigable, with filtering options.
struct GlobalHistorySearchWindow: View {
    @Environment(\.dismiss) private var dismiss
    
    let historyStore: HistoryStore
    let onSelect: (HistoryStore.Command) -> Void
    
    @State private var query: String = ""
    @State private var results: [HistoryStore.Command] = []
    @State private var selectedIndex: Int = 0
    @State private var onlyFavorites: Bool = false
    @State private var onlyFailed: Bool = false
    @FocusState private var isSearchFieldFocused: Bool
    
    var body: some View {
        VStack(spacing: 0) {
            // Title bar
            HStack {
                Text("Command History")
                    .font(.headline)
                
                Spacer()
                
                Button("Done") {
                    dismiss()
                }
                .keyboardShortcut(.cancelAction)
            }
            .padding()
            
            Divider()
            
            // Search controls
            VStack(spacing: 12) {
                HStack {
                    Image(systemName: "magnifyingglass")
                        .foregroundColor(.secondary)
                    
                    TextField("Search all history...", text: $query)
                        .textFieldStyle(.plain)
                        .focused($isSearchFieldFocused)
                        .onSubmit {
                            selectCurrentResult()
                        }
                    
                    if !query.isEmpty {
                        Button(action: { query = "" }) {
                            Image(systemName: "xmark.circle.fill")
                                .foregroundColor(.secondary)
                        }
                        .buttonStyle(.plain)
                    }
                }
                
                HStack {
                    Toggle("Favorites only", isOn: $onlyFavorites)
                    Toggle("Failed only", isOn: $onlyFailed)
                    
                    Spacer()
                    
                    Text("\(results.count) results")
                        .foregroundColor(.secondary)
                        .font(.caption)
                }
            }
            .padding()
            
            Divider()
            
            // Results list
            ScrollViewReader { proxy in
                List(Array(results.enumerated()), id: \.element.id) { index, command in
                    GlobalHistoryResultRow(
                        command: command,
                        isSelected: index == selectedIndex,
                        historyStore: historyStore
                    )
                    .id(index)
                    .onTapGesture {
                        selectedIndex = index
                        selectCurrentResult()
                    }
                    .contextMenu {
                        Button("Copy Command") {
                            NSPasteboard.general.clearContents()
                            NSPasteboard.general.setString(command.command, forType: .string)
                        }
                        
                        Button(command.favorite ? "Remove from Favorites" : "Add to Favorites") {
                            Task {
                                try? historyStore.toggleFavorite(commandID: command.id!)
                                performSearch()
                            }
                        }
                        
                        Divider()
                        
                        Button("Execute") {
                            selectCurrentResult()
                        }
                    }
                }
                .listStyle(.plain)
                .onChange(of: selectedIndex) { _, newIndex in
                    proxy.scrollTo(newIndex, anchor: .center)
                }
            }
        }
        .frame(width: 800, height: 600)
        .onAppear {
            isSearchFieldFocused = true
            performSearch()
        }
        .onChange(of: query) { _, _ in
            performSearch()
        }
        .onChange(of: onlyFavorites) { _, _ in
            performSearch()
        }
        .onChange(of: onlyFailed) { _, _ in
            performSearch()
        }
        .onKeyPress(.upArrow) {
            if selectedIndex > 0 {
                selectedIndex -= 1
            }
            return .handled
        }
        .onKeyPress(.downArrow) {
            if selectedIndex < results.count - 1 {
                selectedIndex += 1
            }
            return .handled
        }
    }
    
    private func performSearch() {
        Task {
            do {
                let options = HistoryStore.SearchOptions(
                    limit: 1000,
                    onlyFavorites: onlyFavorites,
                    onlyFailed: onlyFailed
                )
                
                if query.isEmpty {
                    results = try historyStore.recent(options: options)
                } else {
                    results = try historyStore.search(query: query, options: options)
                }
                
                selectedIndex = 0
            } catch {
                print("Search error: \(error)")
                results = []
            }
        }
    }
    
    private func selectCurrentResult() {
        guard selectedIndex < results.count else { return }
        let command = results[selectedIndex]
        onSelect(command)
        dismiss()
    }
}

struct GlobalHistoryResultRow: View {
    let command: HistoryStore.Command
    let isSelected: Bool
    let historyStore: HistoryStore
    
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text(command.command)
                    .font(.system(.body, design: .monospaced))
                    .lineLimit(2)
                
                Spacer()
                
                if command.favorite {
                    Image(systemName: "star.fill")
                        .foregroundColor(.yellow)
                        .font(.caption)
                }
                
                if command.redacted {
                    Image(systemName: "eye.slash.fill")
                        .foregroundColor(.orange)
                        .font(.caption)
                        .help("Contains redacted secrets")
                }
            }
            
            HStack {
                if let cwd = command.cwd {
                    Label(cwd, systemImage: "folder")
                        .font(.caption)
                        .foregroundColor(.secondary)
                        .lineLimit(1)
                }
                
                if let tabName = command.tabName {
                    Label(tabName, systemImage: "rectangle")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                Label(command.shell, systemImage: "terminal")
                    .font(.caption)
                    .foregroundColor(.secondary)
                
                Spacer()
                
                if let durationMs = command.durationMs {
                    Text(formatDuration(durationMs))
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                if let exitCode = command.exitCode {
                    Text(exitCode == 0 ? "✓" : "✗ \(exitCode)")
                        .font(.caption)
                        .foregroundColor(exitCode == 0 ? .green : .red)
                }
                
                Text(formatTimestamp(command.timestamp))
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            
            if let note = command.note, !note.isEmpty {
                Text(note)
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .italic()
            }
        }
        .padding(8)
        .background(isSelected ? Color.accentColor.opacity(0.2) : Color.clear)
        .cornerRadius(4)
    }
    
    private func formatDuration(_ ms: Int) -> String {
        if ms < 1000 {
            return "\(ms)ms"
        } else if ms < 60000 {
            return String(format: "%.1fs", Double(ms) / 1000)
        } else {
            let minutes = ms / 60000
            let seconds = (ms % 60000) / 1000
            return "\(minutes)m \(seconds)s"
        }
    }
    
    private func formatTimestamp(_ timestamp: Int64) -> String {
        let date = Date(timeIntervalSince1970: TimeInterval(timestamp) / 1000)
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .abbreviated
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}
