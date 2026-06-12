# Restore Fixtures

**Status:** TODO - to be added in M4 (Track A Phase 3: Persistence)

This directory will contain workspace restore conformance fixtures testing:
- Tab/pane/layout round-trip persistence
- Partial load reconciliation (non-destructive on incomplete restore)
- Output journal replay
- Orphan journal purge

These fixtures gate the `WorkspaceStore` and `OutputJournal` interface implementations.

See `IMPLEMENTATION_PLAN.md` M4 (A3.2).
