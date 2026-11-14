# Focused native tests

Run from the repository root:

```sh
dotnet test tests/CardProbability.EngineTests --configuration Release --filter FullyQualifiedName~SelectedFixture
```

Replace the filter with the actual affected fixture and confirm matching tests executed. Check the targeted regression and relevant neighboring boundaries. Return the outcome to the caller.
