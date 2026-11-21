// Keep all measured samples and each distinct numerical outcome once.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const crypto = require('node:crypto');
const [source, destination] = process.argv.slice(2);
assert(source && destination, 'summarize.cjs <raw-report.json> <new-summary.json>');
const bytes = fs.readFileSync(source);
const { cases, ...metadata } = JSON.parse(bytes);
assert(metadata.finished, 'Only completed runs can be summarized');
const summary = { ...metadata, rawSha256: crypto.createHash('sha256').update(bytes).digest('hex'),
    cases: cases.map(c => {
        const unique = [];
        const samples = c.samples.map(s => {
            const outcome = {
                type: s.outcome, alert: s.alert ?? null, proof: s.response?.Result?.Contract?.Proof ?? null,
                accuracy: s.response?.Result?.Contract?.Accuracy ?? null, terminal: s.response?.Contract?.Completion ?? null,
                rows: s.rows?.map(({ label, lower, upper }) => [label, lower, upper]) ?? [],
                rendered: s.rendered?.map(r => [r.label, r.value, r.uncertainty]) ?? []
            };
            let index = unique.findIndex(o => JSON.stringify(o) === JSON.stringify(outcome));
            if (index < 0) { index = unique.length; unique.push(outcome); }
            const ms = t => t == null ? null : Number(t.toFixed(3));
            return { phase: s.temperature, uiMs: ms(s.uiMs), workerMs: ms(s.roundTripMs),
                bootstrapMs: ms(s.bootstrapMs), cutoffMs: s.cutoffMs, partial: s.partial, metrics: s.response?.Metrics, outcome: index };
        });
        return { name: c.name, inputHash: c.inputHash, sessionHash: c.sessionHash, wireHash: c.samples[0]?.wireHash, fastWarmup: c.fastWarmup,
            hand: c.hand, reveals: c.reveals, passed: c.passed, error: c.error, samples, outcomes: unique };
    }) };
fs.writeFileSync(destination, JSON.stringify(summary, null, 2) + '\n', { flag: 'wx' });
