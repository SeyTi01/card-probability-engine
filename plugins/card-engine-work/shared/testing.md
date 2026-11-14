# Meaningful tests

Tests must catch consequential defects at the simplest reliable layer. Neutral regressions belong in `repo:tests/CardProbability.EngineTests/`; existing independent oracles include `Fixtures/DrawSequenceOracle.cs` and `SearchCertificateOracle.cs`. Their modeled rules must match the question before reuse.

Derive expected fractions/counts from independent physical enumeration or combinatorics, never from production output or intuition. Cover affected distinct-copy collisions, bounds/exclusions, unions, effect order, failed branches and proof intervals. Keep fictional neutral inputs.

Use `repo:tests/PackageConsumer/Program.cs` for public-boundary regressions and `PackageInspection` for package structure. Preserve public-only consumption; a project-reference test does not establish packed-package behavior.

Coordinate concurrency and cancellation through explicit barriers and observable completion. Wall-clock limits are deadlock guards, not success conditions. Keep opt-in diagnostics and benchmarks separate from correctness gates. Do not add tests that mirror implementation, freeze incidental prose, or merely inventory files.
