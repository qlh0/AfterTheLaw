# MYLIFE Simulation Benchmark

- Date: 2026-07-23 19:35
- Machine: Apple M2 Max, 32768 MB RAM
- Unity: 6000.3.9f1
- Ticks: 30 warmup + 150 measured @ 333 ms/tick budget (target: 3 ticks/sec → every tick must stay well under 333 ms; for 30 fps rendering headroom, ideally under ~10 ms)
- Random seed: 12345, population cap = requested agent count, immortal-agent mode (constant population)

## Fixed world (one screen — the real installation)

| Agents (requested) | Alive start→end | List size (incl. dead) | Avg ms/tick | p95 ms/tick | Max ms/tick | Alloc/tick | GC gen0 collections |
|---|---|---|---|---|---|---|---|
| 50 | 50→50 | 50 | 0.41 | 0.46 | 1.47 | 0 B | 0 |
| 120 | 120→120 | 120 | 1.77 | 2.16 | 2.22 | 0 B | 0 |
| 250 | 250→250 | 250 | 7.33 | 9.38 | 11.98 | 0 B | 0 |
| 500 | 500→500 | 500 | 34.29 | 43.53 | 47.99 | 0 B | 0 |
| 1000 | 1000→1000 | 1000 | 163.99 | 247.66 | 295.46 | 0 B | 2 |
| 2000 | 2000→2000 | 2000 | 1369.30 | 1931.94 | 1988.39 | 0 B | 17 |

## Constant density (world area scales with population; density pinned to 120 agents/screen)

| Agents (requested) | Alive start→end | List size (incl. dead) | Avg ms/tick | p95 ms/tick | Max ms/tick | Alloc/tick | GC gen0 collections |
|---|---|---|---|---|---|---|---|
| 50 | 50→50 | 50 | 0.44 | 0.49 | 0.50 | 0 B | 0 |
| 120 | 120→120 | 120 | 1.81 | 2.27 | 2.34 | 0 B | 0 |
| 250 | 250→250 | 250 | 6.00 | 7.84 | 8.45 | 0 B | 0 |
| 500 | 500→500 | 500 | 19.50 | 25.99 | 30.53 | 0 B | 0 |
| 1000 | 1000→1000 | 1000 | 60.58 | 84.21 | 93.61 | 0 B | 0 |
| 2000 | 2000→2000 | 2000 | 176.16 | 247.19 | 277.04 | 0 B | 0 |

Notes: measures the pure simulation step (no rendering). The fixed-world mode packs every population onto one screen, so interaction radii cover ever-larger fractions of the population — costs there reflect the DESIGN's density ceiling. The constant-density mode scales world area with population and isolates the engine's scaling behavior.
