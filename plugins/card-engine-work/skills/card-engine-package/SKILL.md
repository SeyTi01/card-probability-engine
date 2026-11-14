---
name: card-engine-package
description: "Build, inspect or verify a CardProbability.Engine NuGet candidate, existing artifacts or published-package consumption without implicitly releasing it."
---

# Work with a package

Read [context](../../shared/context.md). Identify the version, source revision, artifact location and requested deliverable.

| Requested operation | Procedure |
| --- | --- |
| Build a new traceable candidate | [Build candidate](../../shared/package-build.md) |
| Verify existing candidate bytes | [Verify artifacts](../../shared/package-verify.md) |
| Verify a version already on NuGet.org | [Verify published consumption](../../shared/package-feed.md) |

Return artifact identity and actual results. Package work alone does not authorize tagging, NuGet publication or a GitHub Release; those use [release](../card-engine-release/SKILL.md).
