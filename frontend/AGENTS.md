# AyShort frontend — working agreement

Place this file at frontend/AGENTS.md in the AyShort repository.
Scope: the React frontend. Preserve existing repository-wide guidance.

## Purpose and first scope
Build a clear, responsive interface for the existing .NET URL shortener.
First: static URL form and sample result; then local interaction; then API integration.
Deliver URL creation, loading/error feedback and copying before statistics lookup.
Do not add a dashboard requiring endpoints that do not exist.

## Read first
- Read ./frontend-design.md before UI changes.
- Inspect package.json, configuration, existing components and relevant API contracts.
- Use local guidance for routine work. Consult Notion when the task needs current
  decisions or an explicit refresh; report access gaps rather than inventing content.
- These files are project defaults. Follow the user's current task and flag material
  conflicts between documentation and the actual application.

## Working with Ayoub
Default to learning mode for React feature practice unless the task asks for building.
- Learning: explain one concept, give a small task and let Ayoub attempt the core
  behaviour before providing the full solution. Offer hints and minimal examples.
- Building: implement the agreed bounded outcome and verify it without repeatedly
  requesting confirmation for routine decisions.
- Debugging: compare expected and actual behaviour, inspect evidence, test a hypothesis.
- Review: identify correctness problems, missing cases and unnecessary complexity.
Routine setup can be automated. Use plain English and explain unfamiliar syntax.
Distinguish delivered features from independently demonstrated understanding.
End meaningful learning steps with one small variation or explanation checkpoint.

## Stack and boundaries
React, TypeScript, Vite, ESLint; Tailwind is the agreed styling direction.
Inspect whether Tailwind is installed before using its classes; install/configure it
only within an authorised setup task. Keep package versions compatible and preserve
package-lock.json. Do not bypass peer conflicts with force or legacy-peer-deps.
Native fetch behind named API functions; local React state and props initially.
Add routing, global state, query libraries or abstractions for a concrete need.

## Structure (create only as needed)
- src/App.tsx: assemble the screen.
- src/features/create-link/: creation UI and behaviour.
- src/features/link-stats/: later statistics feature.
- src/api/links.ts: HTTP functions and API types.
- src/components/: UI genuinely shared across features.
- src/index.css: Tailwind setup, theme tokens and global defaults.
Keep backend business rules on the backend. Inspect endpoints and DTOs before
implementing calls; do not guess field names, response shapes or API base URLs.

## Code
Use function components, clear names and readable control flow.
Keep rendering pure; update state immutably and give each state value one owner.
Derive values instead of duplicating state. Handle submissions in event handlers.
Use effects for external synchronisation, with dependencies and cleanup as needed.
Type props and API contracts; use inference for obvious locals. Avoid unexplained
any and type assertions. Types do not validate external data at runtime.
Check response.ok/status and handle network and HTTP failures explicitly.
Prevent unintended duplicate submissions; handle stale responses when relevant.
Keep secrets on the backend; frontend environment variables are public.
Use semantic HTML, labels, explicit button types, keyboard access and visible focus.

## Verification and commits
From frontend/, inspect the current scripts, then use:
- npm run dev — development server.
- npm run lint — lint checks.
- npm run build — TypeScript build checks and production bundle.
Use npm ci for a clean install when a valid lockfile exists; use npm install when
intentionally changing dependencies and review the lockfile diff.
Verify relevant interactions in the browser at narrow and wide widths, including
keyboard use, long URLs, loading and failure states. Report unavailable checks.
Add focused tests for meaningful behaviour; do not add tests solely for trivial markup.
Review diffs and preserve unrelated work. Keep node_modules/ and dist/ ignored.
Suggest coherent commit messages; commit/push/close issues only when requested or
already authorised. Never claim verification, implementation or learning not observed.

## Living references
- [Atlas React — Development Principles](https://app.notion.com/p/40674d05d2f18118b7fbe94163520779)
- [AyShort — Learning React by Building](https://app.notion.com/p/40674d05d2f1812a9a18f61a01ff6bea)
- [Roadmap 4.0](https://app.notion.com/p/40374d05d2f181f18f74f42e823199c3)
- [Browser, React and connected interfaces](https://app.notion.com/p/40574d05d2f1819895a5e7c70bb9035a)

Update these local conventions when an agreed decision changes. Do not silently
treat a Notion edit as an automatic update to repository files.

