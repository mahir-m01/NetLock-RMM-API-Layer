# ControlIT Documentation

This folder contains submission-ready project documentation.

| Document | Purpose |
|---|---|
| [Final Technical Report](final-technical-report.md) | University final report covering architecture, SDLC, SOLID, design patterns, diagrams, API layer, security, deployment, and release branch strategy. |

## PDF Export

Markdown remains searchable and link-friendly when exported through tools such as Pandoc, VS Code Markdown PDF, Obsidian, Typora, or GitHub-rendered print to PDF.

Recommended Pandoc command:

```bash
pandoc docs/final-technical-report.md \
  --from gfm \
  --toc \
  --number-sections \
  --metadata title="ControlIT Final Technical Report" \
  -o docs/controlit-final-technical-report.pdf
```

## Obsidian Copy

Keep a current copy in:

`/Users/mahir/Documents/Obsidian-Vault/6 - Main Notes/ControlIT/`

Whenever `docs/final-technical-report.md` changes, copy the updated report and this README there.
