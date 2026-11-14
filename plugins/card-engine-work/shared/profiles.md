# Neutral diagnostic profiles

Inspect `repo:tests/CardProbability.EngineTests/StorageTierProfile.cs` and `OrderedThroughputProfile.cs` for current fixture names, environment controls and explicit test methods before running selected profiles.

Use `dotnet test tests/CardProbability.EngineTests --configuration Release --filter FullyQualifiedName=<selected-method>` with the chosen environment controls. Require emitted records for the intended fixtures; a successful test command with no selected execution proves nothing.

`StorageTierProfile.ProfilePublic` exercises public routing; its instrumented and forced-projection variants answer different questions. `OrderedThroughputProfile` is an intrusive phase/lifetime diagnostic, not a timing benchmark. Keep these explicit fixtures opt-in, serialize measurements, and preserve raw outcome labels.
