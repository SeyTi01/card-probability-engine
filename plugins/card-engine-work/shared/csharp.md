# C# conventions

Follow the existing C# style: same-line opening braces, braced control flow, explicit local/loop types, target-typed `new()`, and `_camelCase` only for private instance fields. Use one blank line between logical blocks; keep related declarations compact.

Name dense conditions and prefer clear steps or switch expressions to nested ternaries. Wrap substantial declarations/calls one parameter/argument per line with a separate closing line; leave short readable constructs compact.

Respect project language and nullable settings. Preserve defensive validation, collection order, exception meaning and API contracts. Inspect callers before accessibility, static or nullability changes. Do not run automatic formatters, cleanup tools or analyzer fixes; use targeted non-mutating diagnostics and deliberate edits.
