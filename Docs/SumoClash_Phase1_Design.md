# Sumo Clash - Phase 1: Gameplay Design

## Game scope

- Windows PC, top-down 2D local multiplayer.
- Two human players; no AI and no online multiplayer.
- Both players use the same prefab and identical gameplay values.
- A championship has four rounds. Environmental difficulty changes between rounds, never player abilities.

## Arena and camera

- World view: 16 units wide by 9 units high.
- Circular arena centred at `(0, 0)` with visual radius `3.75` units.
- Safe player-centre radius: `3.30` units.
- Fixed orthographic camera at `(0, 0, -10)`, Orthographic Size `5`.
- First prototype: a plain arena, ring outline, and coloured circle placeholders only.

## Players and controls

| Player | Start position | Controls |
|---|---|---|
| Player 1 | `(-1.5, 0)` | W, A, S, D |
| Player 2 | `(1.5, 0)` | Arrow keys |

- Recommended starting movement speed: `5 units/second`.
- Diagonal movement is normalized, so it is not faster than straight movement.
- Circular colliders are used for smooth, fair contact.
- Both players have identical mass, drag, collider size, movement speed, and pushing behaviour.
- Pushing comes from 2D physics contact while moving into the other player. There is no attack button in the first version.

## Elimination and round flow

- A player is eliminated when their centre passes the safe radius of `3.30` units.
- There are no solid outer walls; leaving the ring immediately ends the round.
- Before every round, both players reset to their start positions.
- Countdown: `3`, `2`, `1`, `CLASH!`; movement is disabled during the countdown.
- Result display: 3 seconds; movement is disabled.
- A player leaving the arena ends the round immediately; the remaining player wins.
- If the timer ends with both players inside, the round is a draw.
- If both players are eliminated in the same physics update, the round is a draw.

## Rounds and championship

| Round | Timer | Planned environment |
|---|---:|---|
| 1 | 90 seconds | Normal full-size arena |
| 2 | 75 seconds | Slightly smaller arena |
| 3 | 60 seconds | Neutral obstacles |
| 4 | 45 seconds | Smaller arena plus obstacles / visual pressure |

First-prototype rule: use the normal, plain Round 1 arena for all four rounds. Obstacles, shrinking, hazards, and advanced visual effects are deferred.

- A round win gives 1 point.
- A round draw gives 0 points to both players.
- After Round 4, the higher total score wins the championship.
- Equal final scores produce a championship draw. There is no tiebreaker round in the first version.

## Team interfaces

### Person A — Gameplay

- Owns movement values, player prefab gameplay requirements, collider radius, boundary checks, round states, score handling, and integration.

### Person B — Arena, art, animation, effects

- Keeps the arena centred at `(0, 0)` and matches art to the `3.75` visual radius and `0.45` player-radius target.
- Supplies future obstacle dimensions, positions, visuals, animations, and effects.

### Person C — UI, audio, database

- Defines the score display, countdown and result screens, audio cues, match-history fields, and leaderboard presentation.

Shared gameplay events to agree before implementation:

- `RoundStarted`
- `PlayerEliminated`
- `RoundEnded`
- `ScoreChanged`
- `ChampionshipEnded`
