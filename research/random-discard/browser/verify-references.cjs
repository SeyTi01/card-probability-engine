// Check newly established baseline Exact values against the recorded browser evidence.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const [reportPath, referencePath] = process.argv.slice(2);
assert(reportPath && referencePath, 'verify-references.cjs <browser-result.json> <legacy-exact.json>');
const report = JSON.parse(fs.readFileSync(reportPath));
const reference = JSON.parse(fs.readFileSync(referencePath));
const byName = new Map([...reference.Cases, ...reference.PlayerCases].map(r => [r.Name, r]));
let checkedRows = 0, checkedSamples = 0, missing = [], noResult = [];
for (const entry of report.cases) {
    const vector = byName.get(entry.name);
    const values = vector?.Rows ?? entry.reference;
    if (!values) { missing.push(entry.name); continue; }
    if (vector) assert.equal(vector.InputHash, entry.inputHash, entry.name + ' exact reference input');
    const success = entry.samples.filter(s => s.outcome === 'success');
    if (!success.length) noResult.push(entry.name);
    for (const sample of success) {
        assert.equal(sample.rows.length, values.length);
        for (let i = 0; i < values.length; i++) {
            const [n, d] = values[i].split('/').map(BigInt);
            const [ln, ld] = sample.rows[i].lower.split('/').map(BigInt);
            const [un, ud] = sample.rows[i].upper.split('/').map(BigInt);
            assert(d > 0n && n >= 0n && n <= d && ln * d <= n * ld && n * ud <= un * d,
                entry.name + ': ' + sample.rows[i].label + ' excludes baseline Exact');
            checkedRows++;
        }
        checkedSamples++;
    }
}
console.log(JSON.stringify({ checkedRows, checkedSamples, missing, noResult }));
if (missing.length || noResult.length) process.exitCode = 1;
