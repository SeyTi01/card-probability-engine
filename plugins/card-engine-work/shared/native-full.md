# Full solution verification

Run from the repository root:

```sh
dotnet build CardProbability.slnx --configuration Release
dotnet test tests/CardProbability.EngineTests --configuration Release
```

Confirm the complete neutral suite ran. The solution build also compiles samples and package tooling; explicit diagnostic profiles remain opt-in.
