# Changelog

## 1.6.1

- Remove exact game-version rejection while retaining executable identity, native hook checks, and offline campaign restrictions.
- Record the running game version in hook metadata.
- Update compatibility documentation and remove the campaign testing overview.

## 1.6.0

Release preparation for StarCraft II 5.0.16.97563.

- Detect the game in any installation directory; retain exact executable-name and build validation.
- Add an MIT license, source project, documentation, test runner, Windows CI, and release packaging.
- Remove obsolete development scripts, backups, generated logs, editor caches, and old executables from the source tree.
- Allow isolated self-tests and layout tests while the normal helper is open.

Includes the existing automatic campaign resolution fix, centered HUD, 50–125% scale selector, cargo and production queue corrections, Recheck game, and stable dropdown/status layout.

## 1.5.4

- Keep open dropdowns undisturbed by background updates.
- Update status text only when it changes and keep the layout stable.
- Shorten the mission-ready status to Ready.

## 1.5.3

- Handle incomplete status updates without a launcher exception.
- Automatically detect an already-running game and add Recheck game.

## 1.5.1

- Scale native cargo cells and preserve the cargo panel's placement.
- Keep production queues centered after selection changes.

## 1.5.0

- Add saved bottom HUD sizes from 50% to 125% in 5% increments.
