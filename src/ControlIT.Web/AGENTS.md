<!-- BEGIN:nextjs-agent-rules -->
# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` before writing any code. Heed deprecation notices.
<!-- END:nextjs-agent-rules -->

Also follow the root `AGENTS.md`.

Frontend security rules:
- No browser secrets. `NEXT_PUBLIC_*` is public.
- Access token stays in memory only; never use `localStorage` or `sessionStorage` for auth secrets.
- Keep security headers/CSP in production config or documented edge layer.
- Avoid dangerous DOM sinks: `dangerouslySetInnerHTML`, `innerHTML`, `eval`, `new Function`, unsafe redirects.
- Dependency changes require `npm audit --audit-level=moderate`, `npm run build`, `npm run test -- --runInBand`, and `npm run typecheck`.
