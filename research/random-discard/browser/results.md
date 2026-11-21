# Published desktop Fast results

Engine source `22f4e91768cef2238560be40f8d7b6cad5f7f22f`, calculator source `a2c9d5ead93ced00353aaf40ac6f6eb8a213e2b0`, package SHA-256 `ed307672139fa5437596d3a523d83180d0400c36f59662044c6676bf67280906`, published asset digest `99e3ba196e1971e3b4120feece6c0d749ab5b9d0d7473650e587d819fc0a919c`.

Edge 158.0.4370.33 on AMD Ryzen 7 5700X 8-Core Processor. Each first click starts a fresh worker after the calculator session loads. The warm column takes the slowest of 3 subsequent Calculate clicks on that worker. The width column takes the largest certified full interval across every row and sample. The display column takes the largest outward-rounded ± value shown on screen. The Work and Storage columns give the maxima measured by the engine.

## Five- and six-card player hands

Both hands use the same main-deck entries, effect settings and goals. Each player group contains both goals, giving the same union as Any combo.

| Deck | Hand goal | Five cards | Six cards | Worst warm ms, five / six |
| --- | --- | ---: | ---: | ---: |
| Phantom Knights | Any combo | 60.86% | 70.39% | 30.9 / 30.9 |
| Phantom Knights | Level 3 Phantom Knight + Level 3 extender | 36.70% | 46.49% |  |
| Phantom Knights | Tour Guide | 33.74% | 39.41% |  |
| Thunder Dragon | Any combo | 37.24% ±0.25% | 47.52% ±0.48% | 263.0 / 602.9 |
| Thunder Dragon | Solar + Thunder Dragon monster | 21.86% ±0.25% | 29.28% ±0.48% |  |
| Thunder Dragon | Fusion + Thunder Dragon monster | 21.86% ±0.25% | 29.28% ±0.48% |  |
| Lightsworn | Any combo | 40.91% | 48.40% | 81.4 / 86.4 |
| Lightsworn | Charge | 24.70% | 29.08% |  |
| Lightsworn | Recharge + Lightsworn monster | 20.02% | 25.50% |  |

## Controls and ordinary decks

| Input | Hand | Reveal copies | First ms | Worst warm ms | Full width pp | Display ±pp | Work | Peak entries | Peak cells | Result |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| control-40 | 5 | 0 | 446.8 | 30.8 | 0.000000 | 0.00 | 2,804 | 13 | 60 | Exact |
| single-40 | 5 | 1 | 543.0 | 31.1 | 0.000000 | 0.00 | 12,361 | 820 | 1,897 | Exact |
| mixed-40 | 5 | 2 | 516.7 | 31.1 | 0.000000 | 0.00 | 36,726 | 741 | 1,700 | Exact |
| control-60 | 5 | 0 | 438.0 | 31.0 | 0.000000 | 0.00 | 2,804 | 13 | 60 | Exact |
| single-60 | 5 | 1 | 506.4 | 31.0 | 0.000000 | 0.00 | 16,763 | 1,830 | 4,695 | Exact |
| mixed-60 | 5 | 2 | 501.6 | 31.1 | 0.000000 | 0.00 | 40,886 | 1,711 | 4,352 | Exact |
| challenge-mixed-40-direct-exclusion | 5 | 2 | 494.3 | 30.9 | 0.000000 | 0.00 | 36,727 | 741 | 1,700 | Exact |
| phantom-knights-5 | 5 | 2 | 485.9 | 30.9 | 0.000000 | 0.00 | 84,509 | 780 | 2,080 | Exact |
| phantom-knights-6 | 6 | 2 | 506.3 | 30.9 | 0.000000 | 0.00 | 142,378 | 890 | 3,560 | Exact |

## Heavy decks

| Input | Hand | Reveal copies | First ms | Worst warm ms | Full width pp | Display ±pp | Work | Peak entries | Peak cells | Result |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| heavy-40 | 5 | 8 | 782.8 | 264.8 | 1.510925 | 0.77 | 18,066,463 | 6,375 | 57,375 | Bounded |
| heavy-60 | 5 | 8 | 667.6 | 197.2 | 0.293439 | 0.16 | 12,461,025 | 3,484 | 34,604 | Bounded |
| challenge-heavy-40-hand6 | 6 | 8 | 1167.9 | 653.8 | 1.561971 | 0.79 | 52,652,304 | 9,719 | 106,696 | Bounded |
| challenge-heavy-60-priority | 5 | 8 | 680.8 | 180.7 | 0.263456 | 0.14 | 10,428,294 | 2,260 | 35,260 | Bounded |
| challenge-heavy-40-multiplicity | 5 | 6 | 707.8 | 249.8 | 0.578601 | 0.30 | 16,611,578 | 4,345 | 44,695 | Bounded |
| challenge-heavy-60-direct | 5 | 8 | 651.5 | 181.9 | 0.293439 | 0.16 | 12,108,302 | 3,484 | 33,646 | Bounded |
| thunder-dragon-5 | 5 | 10 | 702.5 | 263.0 | 0.492467 | 0.25 | 16,444,659 | 5,261 | 83,336 | Bounded |
| thunder-dragon-6 | 6 | 10 | 1080.8 | 602.9 | 0.947798 | 0.48 | 47,413,811 | 12,102 | 197,912 | Bounded |
| lightsworn-5 | 5 | 4 | 557.2 | 81.4 | 0.000000 | 0.00 | 3,411,844 | 1,507 | 30,028 | Exact |
| lightsworn-6 | 6 | 4 | 545.6 | 86.4 | 0.000000 | 0.00 | 4,664,106 | 1,939 | 40,685 | Exact |

## Optional heavy Exact

Each sample uses the same deck and worker after a Fast warmup. Clicking Calculate in Exact mode keeps Standard Storage and permits unlimited Work. The time limit is 60,000 ms per warm click. Every completed physical fraction was checked against every corresponding Fast interval.

| Input | Warm Exact ms | Work | Peak entries | Peak cells | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| heavy-40 | 3448.4 | 249,719,930 | 29,127 | 262,143 | Exact |
| heavy-60 | 3515.8 | 251,863,861 | 29,127 | 262,143 | Exact |
| challenge-heavy-40-hand6 | 19675.6 | 1,415,320,978 | 29,127 | 262,144 | Exact |
| challenge-heavy-60-priority | 1913.6 | 137,057,816 | 29,127 | 262,143 | Exact |
| challenge-heavy-40-multiplicity | 1348.8 | 94,253,170 | 29,127 | 262,143 | Exact |
| challenge-heavy-60-direct | 3415.2 | 250,449,412 | 29,127 | 262,143 | Exact |
| thunder-dragon-5 | 44131.8 | 3,098,861,311 | 29,127 | 262,144 | Exact |
| thunder-dragon-6 | — | — | — | — | censored |
| lightsworn-5 | 76.8 | 3,411,844 | 1,507 | 30,028 | Exact |
| lightsworn-6 | 81.8 | 4,664,106 | 1,939 | 40,685 | Exact |

[Exact samples and rational rows](edge-final-exact.json)

## Default calculator mode

After opening Help and returning to the calculator, both sessions used the fresh app’s default Progressive mode.

- mixed-40 displayed 6 exact rows.
- heavy-40 displayed 6 certified rows. Cancelling its physical Exact upgrade retained every displayed value and uncertainty.

The compact report retains every distinct rational interval, displayed row, first click and warm sample. The baseline used the same Calculate path and returned a Work limit on ten heavy inputs. [Candidate data](edge-final.json) · [Baseline data](edge-baseline.json) · [Deck sources and reproduction](README.md)
