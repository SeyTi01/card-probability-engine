// Verify either a raw measurement or a checked-in compact report without a browser.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const frozen = require('../../../tests/CardProbability.EngineTests/Fixtures/Data/reveal-cases.json');

function verifyReferences(report, referencePath) {
    assert(report.finished && report.cases.length, 'Expected a completed measurement');
    const reference = JSON.parse(fs.readFileSync(referencePath ?? path.join(__dirname, '../legacy-exact.json')));
    const byName = new Map([...reference.Cases, ...reference.PlayerCases].map(r => [r.Name, r]));
    const original = new Map(frozen.filter(row => row.Reference).map(row => [row.Name, row.Reference]));
    const baselineHashes = new Map(require('./edge-baseline.json').cases.map(row => [row.name, row.inputHash]));
    let checkedRows = 0, checkedSamples = 0, missing = [], noResult = [];
    for (const entry of report.cases) {
        const vector = byName.get(entry.name);
        assert.equal(entry.inputHash, baselineHashes.get(entry.name), entry.name + ' source fixture');
        const values = vector?.Rows ?? original.get(entry.name);
        if (!values) { missing.push(entry.name); continue; }
        if (vector) assert.equal(vector.InputHash, entry.inputHash, entry.name + ' exact reference input');
        if (entry.reference) assert.deepEqual(values, entry.reference, entry.name + ' captured reference');
        const success = entry.samples.flatMap(sample => {
            if (!entry.outcomes) return sample.outcome === 'success' ? [sample.rows] : [];
            const outcome = entry.outcomes[sample.outcome];
            assert(outcome, entry.name + ' invalid compact outcome index');
            if (outcome.type !== 'success') return [];
            return [outcome.rows.map(([label, lower, upper]) => ({ label, lower, upper }))];
        });
        if (!success.length) noResult.push(entry.name);
        for (const rows of success) {
            assert.equal(rows.length, values.length);
            for (let i = 0; i < values.length; i++) {
                const [n, d] = values[i].split('/').map(BigInt);
                const [ln, ld] = rows[i].lower.split('/').map(BigInt);
                const [un, ud] = rows[i].upper.split('/').map(BigInt);
                assert(d > 0n && n >= 0n && n <= d && ld > 0n && ud > 0n && ln >= 0n && un <= ud
                    && ln * d <= n * ld && n * ud <= un * d, entry.name + ': ' + rows[i].label + ' excludes exact reference');
                if (report.mode === 'Exact') assert.equal(ln * ud, un * ld, entry.name + ' nonexact Exact row');
                checkedRows++;
            }
            checkedSamples++;
        }
    }
    return { checkedRows, checkedSamples, missing, noResult };
}

module.exports = { verifyReferences };
if (require.main === module) {
    const [reportPath, referencePath] = process.argv.slice(2);
    assert(reportPath, 'verify-references.cjs <raw-or-compact-report.json> [legacy-exact.json]');
    const result = verifyReferences(JSON.parse(fs.readFileSync(reportPath)), referencePath);
    console.log(JSON.stringify(result));
    if (result.missing.length || result.noResult.length) process.exitCode = 1;
}
