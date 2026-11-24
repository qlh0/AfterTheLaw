# MYLIFE Simulation Benchmark

- Date: 2026-07-23 17:08
- Machine: Apple M2 Max, 32768 MB RAM
- Unity: 6000.3.9f1
- Ticks: 30 warmup + 150 measured @ 333 ms/tick budget (target: 3 ticks/sec → every tick must stay well under 333 ms; for 30 fps rendering headroom, ideally under ~10 ms)
- Random seed: 12345, population cap = requested agent count, immortal-agent mode (constant population)

| Agents (requested) | Alive start→end | List size (incl. dead) | Avg ms/tick | p95 ms/tick | Max ms/tick | Alloc/tick | GC gen0 collections |
|---|---|---|---|---|---|---|---|
| 50 | 50→50 | 50 | 0.41 | 0.48 | 0.49 | 0 B | 0 |
| 120 | 120→120 | 120 | 1.85 | 2.32 | 2.43 | 0 B | 0 |
| 250 | 250→250 | 250 | 7.81 | 10.17 | 11.61 | 0 B | 0 |
| 500 | 500→500 | 500 | 36.14 | 46.68 | 54.05 | 0 B | 0 |
| 1000 | 1000→1000 | 1000 | 177.00 | 282.14 | 316.89 | 0 B | 2 |
| 2000 | 2000→2000 | 2000 | 1197.51 | 1654.42 | 1795.54 | 0 B | 16 |

Notes: measures the pure simulation step (no rendering). "Alive" can drop below the requested count because agents age, starve, and fight during the run; the list size column shows how many agents (alive + dead) every O(n²) loop actually iterates.
