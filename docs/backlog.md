# Backlog

Planned work that is agreed but not started yet. When an item is done, move what was decided to
[`decisions.md`](decisions.md) and any problems met to [`issues-log.md`](issues-log.md), then remove it here.

## UI-001: Three responsive layout modes and switchable themes

- **Added:** 2026-11-08, requested by the project owner.
- **What:** rework the browser UI in `src/GameDataAggregator.Api/wwwroot`:
  - **Three layout modes:** wide, medium and narrow. The page switches between them by itself as the window
    changes size, at two breakpoints. A breakpoint is the window width where the layout changes. The planned
    breakpoints are 640 px (wide to medium) and 480 px (medium to narrow).
  - **A layout setting** with four choices: Auto, Wide, Medium and Narrow. Auto follows the window size. The other
    three keep one layout whatever the window size.
  - **Themes** the user can switch between, each kept in its own CSS file (for example `theme-dark.css`). The
    colours change with a short animation, not all at once. Today this UI only follows the system's light or dark
    setting.
- **Design rules:** follow Joe Natoli's UX principles: clarity before decoration, consistency, visible feedback
  for every action, user control, accessibility, and as little for the user to think about as possible.
- **Notes:** keep the rules already in [`patterns.md`](patterns.md) (text-only rendering, stable page frame, URL
  as state). Remember the theme choice in `localStorage`, and default to the system setting.
