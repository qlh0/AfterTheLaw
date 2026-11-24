# MYLIFE Simulation Benchmark

- Date: 2026-07-23 17:25
- Machine: Apple M2 Max, 32768 MB RAM
- Unity: 6000.3.9f1
- Ticks: 30 warmup + 150 measured @ 333 ms/tick budget (target: 3 ticks/sec → every tick must stay well under 333 ms; for 30 fps rendering headroom, ideally under ~10 ms)
- Random seed: 12345, population cap = requested agent count, immortal-agent mode (constant population)

## Fixed world (one screen — the real installation)

| Agents (requested) | Alive start→end | List size (incl. dead) | Avg ms/tick | p95 ms/tick | Max ms/tick | Alloc/tick | GC gen0 collections |
|---|---|---|---|---|---|---|---|
| 50 | 50→50 | 50 | 0.39 | 0.44 | 0.49 | 0 B | 0 |
| 120 | 120→120 | 120 | 1.80 | 2.23 | 2.36 | 0 B | 0 |
| 250 | 250→250 | 250 | 7.27 | 8.91 | 9.28 | 0 B | 0 |
| 500 | 500→500 | 500 | 35.32 | 45.53 | 54.73 | 0 B | 0 |
| 1000 | 1000→1000 | 1000 | 172.37 | 262.63 | 286.61 | 0 B | 2 |
| 2000 | 2000→2000 | 2000 | 1169.87 | 1619.99 | 1824.56 | 0 B | 16 |

## Constant density (world area scales with population; density pinned to 120 agents/screen)

| Agents (requested) | Alive start→end | List size (incl. dead) | Avg ms/tick | p95 ms/tick | Max ms/tick | Alloc/tick | GC gen0 collections |
|---|---|---|---|---|---|---|---|
| 50 | 50→50 | 50 | 0.44 | 0.49 | 0.52 | 0 B | 0 |
| 120 | 120→120 | 120 | 1.80 | 2.27 | 2.38 | 0 B | 0 |
| 250 | 250→250 | 250 | 5.79 | 7.40 | 8.23 | 0 B | 0 |
| 500 | 500→500 | 500 | 19.61 | 26.42 | 28.33 | 0 B | 0 |
| 1000 | 1000→1000 | 1000 | 66.35 | 92.23 | 99.41 | 0 B | 0 |
| 2000 | 2000→2000 | 2000 | 204.66 | 265.12 | 290.48 | 0 B | 0 |

Notes: measures the pure simulation step (no rendering). The fixed-world mode packs every population onto one screen, so interaction radii cover ever-larger fractions of the population — costs there reflect the DESIGN's density ceiling. The constant-density mode scales world area with population and isolates the engine's scaling behavior.
