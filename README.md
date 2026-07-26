# Autonomous Arena

An automated large-map combat sandbox where the player watches autonomous
factions fight. The first visual representation is simple colored dots.

## Initial direction

- Single-player spectator sandbox
- Large logical maps with pan and zoom
- Fully autonomous movement, targeting, fighting, retreat, and battle outcomes
- Dot-based people as the first entity representation
- Deterministic scenarios that can be replayed from a seed
- Detailed combat systems added in deliberate layers

## First design decisions

Before implementation, define:

1. Whether battles are disposable arena matches or part of a persistent world
2. The first target scale: 50, 200, 500, or 1,000 simultaneous combatants
3. Whether combat prioritizes readability or detailed physiology
4. How much the player configures before starting a battle
5. Whether dots are placeholders or the long-term visual identity

## Recommended first milestone

Build a deterministic arena proof with two factions, colored dots, autonomous
enemy detection, movement, ranged attacks, health, death, camera controls,
pause, speed controls, reset, and a visible winner.
