# YOLOTerm Contracts — Interface Definitions

**Version:** 0.1.0-draft (pre-freeze)
**Status:** Draft — interfaces will be ratified at contracts v1 freeze (end of Track A Phase 2)

---

## Overview

This document normatively defines the 13 small, role-specific interfaces (ISP) that every YOLOTerm track implements 1:1 in its native language. These contracts are the **only** coupling between tracks.

Each interface specifies:
- **Name** (used verbatim in Swift protocols, C# interfaces, etc.)
- **Methods** with signatures, semantics, and error contracts
- **Threading model** (main-thread-only, thread-safe, actor-isolated)
- **Lifecycle** (who owns instances, when they're created/destroyed)

Tracks mirror these definitions exactly — same names, same method semantics, same conformance tests from `contracts/fixtures/`.

---

## Interface catalog

*Interfaces will be populated here as Track A Phase 1–2 discover the working shapes. Current status: skeleton only.*

### PTY Management

#### `PtySpawning`
- **Role:** Spawn a shell with cwd, size, and env policy applied
- **Methods:** TBD
- **Conformance:** `fixtures/env-policy.json` (hostile vars scrubbed)

#### `PtyWriting`
- **Role:** Write user input bytes to a session
- **Methods:** TBD

#### `PtyResizing`
- **Role:** Propagate row/col changes to PTY
- **Methods:** TBD

#### `PtyLifecycle`
- **Role:** Kill, observe natural exit (fires exactly once), introspect liveness
- **Methods:** TBD
- **Conformance:** Track-specific lifecycle tests (no `/bin/sh` flake)

### Terminal Emulation

#### `TerminalSurface`
- **Role:** Feed bytes, report cell metrics, select/search, serialize visible state — wraps SwiftTerm / WT control / VTE
- **Methods:** TBD
- **Conformance:** `fixtures/colors/` golden corpus

### Theming

#### `ThemeSource`
- **Role:** Resolve current theme for every surface; emit change events
- **Methods:** TBD
- **Conformance:** `themes/*.json` loaded; Vivid has no ANSI overlay

### Layout

#### `LayoutEngine`
- **Role:** Pure function: (panes, preset, container size, drag deltas) → rects
- **Methods:** TBD
- **Conformance:** `fixtures/layout/*.json`

### Persistence

#### `WorkspaceStore`
- **Role:** Persist/restore tabs, panes, layout, CWDs; non-destructive on partial load
- **Methods:** TBD
- **Conformance:** `fixtures/restore/` (reconcile-on-save, never destructive)

#### `OutputJournal`
- **Role:** Append-only capped raw-byte journal per pane; replay on restore
- **Methods:** TBD

#### `HistoryStore`
- **Role:** Insert/search commands (FTS5), redaction before insert
- **Methods:** TBD
- **Conformance:** `schema/history.sql`, `fixtures/redaction.json`

### Metadata

#### `PromptMarkParser`
- **Role:** OSC 133 / OSC 7 sniffing shared semantics
- **Methods:** TBD

#### `PaneMetadataProvider`
- **Role:** cwd, git branch, shell name, SSH-descendant detection
- **Methods:** TBD

#### `DeepLinkHandler`
- **Role:** `yoloterm://` URL → workspace action
- **Methods:** TBD

---

## Ratification process

1. Track A Phase 1–2 prove the interface shapes in working Swift code
2. Extract normative definitions into this file (with signatures, semantics, errors)
3. Tag as `contracts-v1` and freeze
4. CI rule activates: `contracts/` changes require all active tracks conformant in-PR

Until ratification, Track A may reshape freely; Track B waits.
