# MYLIFE Simulation Benchmark

- Date: 2026-07-23 14:33
- Machine: Apple M2 Max, 32768 MB RAM
- Unity: 6000.3.9f1
- Ticks: 30 warmup + 150 measured @ 333 ms/tick budget (target: 3 ticks/sec → every tick must stay well under 333 ms; for 30 fps rendering headroom, ideally under ~10 ms)
- Random seed: 12345, population cap = requested agent count

| Agents (requested) | Alive start→end | List size (incl. dead) | Avg ms/tick | p95 ms/tick | Max ms/tick | Alloc/tick | GC gen0 collections |
|---|---|---|---|---|---|---|---|
| 50 | 50→0 | 50 | 0.23 | 0.65 | 0.91 | 0 B | 0 |
| 120 | 120→0 | 120 | 1.21 | 3.65 | 4.10 | 0 B | 0 |
| 250 | 250→0 | 250 | 5.17 | 17.38 | 18.56 | 0 B | 0 |
| 500 | 500→0 | 500 | 20.21 | 68.92 | 70.47 | 0 B | 0 |
| 1000 | 1000→0 | 1000 | 80.25 | 270.47 | 278.89 | 0 B | 1 |
| 2000 | 2000→0 | 2000 | 312.38 | 1056.55 | 1085.95 | 0 B | 1 |

Notes: measures the pure simulation step (no rendering). "Alive" can drop below the requested count because agents age, starve, and fight during the run; the list size column shows how many agents (alive + dead) every O(n²) loop actually iterates.
