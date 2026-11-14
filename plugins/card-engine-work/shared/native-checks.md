# Choose native checks

Use [execution setup](execution.md). Select every matching route:

| Trigger | Procedure |
| --- | --- |
| A specific regression or affected fixture needs execution | [Focused tests](native-focused.md) |
| Substantial engine changes or full-suite verification requested | [Full solution checks](native-full.md) |
| Sample behavior changed or examples need execution | [Run samples](sample-checks.md) |

Source-project tests do not establish packed-package consumption. Public API/package changes also route to [package verification](../skills/card-engine-package/SKILL.md).
