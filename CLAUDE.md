# Neon Breaker: session guide

This file is loaded automatically at the start of every session. The full project brief, systems and changelog live in
`Assets/CLAUDE.md`, imported here:

@Assets/CLAUDE.md

## How we work

- **One task per session.** Sessions start fresh; the context comes from this file and `Assets/CLAUDE.md`,
  not from older conversations. Do not ask the user to paste previous chats.
- **Gitflow.** Branch from `develop` (`feature/...`, `fix/...`, `docs/...`). Commit there, then merge into `develop`
  with `--no-ff`, push, and delete the branch only when the user asks. `main` is only updated for releases.
- **End of task.** When a feature is done, add a short entry to the changelog in `Assets/CLAUDE.md` (what changed,
  open issues, next steps) so the next session starts from the latest state.

## Model per task

The user switches models to save tokens. If a task clearly fits the other model better, say so in one line at the start.

| Claude Sonnet 5.5 | Claude Opus 5.5 |
|---|---|
| New gameplay features and systems (power-ups, bricks, balancing) | Art direction, visual effects, shaders, audio and music |
| Small fixes, UI tweaks, refactors | Hard debugging (editor freezes, "it doesn't work and nobody knows why") |
| Docs, README, CLAUDE.md, Git operations | Planning a large system before building it |

## Saving tokens

- **Write scripts with the file tools (Write/Edit), not through the Unity MCP `RunCommand` tool.** `RunCommand` echoes
  the whole submitted code back in its result, so writing a script through it costs about twice. Use `RunCommand`
  only for short editor actions (creating objects, wiring references, refreshing, small checks).
- **Screenshots and renders only when a visual change needs judging.** Images are expensive; prefer logs and
  numbers to verify behaviour.
- **Read only what is needed.** Use the file paths given by the user, grep before reading whole files, and read
  line ranges of large scripts.
- **Unity can stall between commands** (frames stop advancing, or the MCP bridge disconnects during script reloads).
  Do not poll in a loop: ask the user to confirm when Unity is ready, or retry once.
