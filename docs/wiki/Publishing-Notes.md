# Publishing Notes

## Goal

This folder is designed to be copied into a GitHub Wiki with minimal cleanup.

## Recommended Publish Steps

1. Copy every file from `docs/wiki/` into the root of the GitHub Wiki repository.
2. Keep the filenames `Home.md`, `_Sidebar.md`, and `_Footer.md` unchanged so GitHub Wiki picks them up automatically.
3. If you want richer wiki pages, expand the summary pages from these repo docs:
   - `docs/README.md`
   - `docs/TECHNICAL_SPEC.md`
   - `docs/CODE_FLOW.md`
   - `docs/FEATURE_FLOW.md`
4. Refresh any code references before publishing if the host wiring or module map has changed.

## Maintenance Rule

Treat the repo docs as canonical and this folder as the publication layout. Update the canonical docs first, then refresh the wiki pages.