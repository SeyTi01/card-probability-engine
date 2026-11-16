# Repository instructions

Use **Card Engine Work** for this repository. Its canonical source and build tooling live in [card-engine-work](https://github.com/SeyTi01/work-plugins/tree/main/plugins/card-engine-work). The installed ChatGPT plugin is a standalone build of that source.

If the installed plugin is unavailable, follow the [source fallback procedure](https://github.com/SeyTi01/work-plugins#source-fallback-for-product-agents) in an authenticated `work-plugins` checkout: run `npm ci` and `npm run compile`, then read `.build/plugins/card-engine-work/skills/<skill>/SKILL.md` and its applicable references. The TypeScript profile and Markdown templates are authoring inputs; an uncompiled template or only the remaining handwritten files is not the complete instruction set. Do not recreate plugin instructions in this repository. If neither the installed plugin nor a compiled source is accessible, report the access limitation.

Choose the entry point for the requested task and follow its applicable references:

- `card-engine-diagnose`
- `card-engine-implement`
- `card-engine-issue`
- `card-engine-package`
- `card-engine-plan`
- `card-engine-pr`
- `card-engine-rebase`
- `card-engine-release`
- `card-engine-review`
- `card-engine-verify`
