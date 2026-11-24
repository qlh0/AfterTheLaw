# MYLIFE Simulation Benchmark

- Date: 2026-07-23 14:48
- Machine: Apple M2 Max, 32768 MB RAM
- Unity: 6000.3.9f1
- Ticks: 30 warmup + 150 measured @ 333 ms/tick budget (target: 3 ticks/sec → every tick must stay well under 333 ms; for 30 fps rendering headroom, ideally under ~10 ms)
- Random seed: 12345, population cap = requested agent count, immortal-agent mode (constant population)

| Agents (requested) | Alive start→end | List size (incl. dead) | Avg ms/tick | p95 ms/tick | Max ms/tick | Alloc/tick | GC gen0 collections |
|---|---|---|---|---|---|---|---|
| 50 | 50→50 | 50 | 0.65 | 0.69 | 0.97 | 0 B | 0 |
| 120 | 120→120 | 120 | 3.74 | 4.09 | 6.65 | 0 B | 0 |
| 250 | 250→250 | 250 | 16.42 | 17.94 | 21.17 | 0 B | 0 |
| 500 | 500→500 | 500 | 67.50 | 74.28 | 94.11 | 0 B | 1 |
| 1000 | 1000→1000 | 1000 | 314.13 | 385.86 | 670.91 | 0 B | 15 |
| 2000 | 2000→2000 | 2000 | 1531.96 | 1935.85 | 2003.03 | 0 B | 96 |

Notes: measures the pure simulation step (no rendering). "Alive" can drop below the requested count because agents age, starve, and fight during the run; the list size column shows how many agents (alive + dead) every O(n²) loop actually iterates.
