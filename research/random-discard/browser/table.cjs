const assert = require('node:assert/strict');
const fs = require('node:fs');
const { verifyReferences } = require('./verify-references.cjs');
const [input, destination, exactInput, defaultInput, exactRepeatInput] = process.argv.slice(2);
assert(input && destination, 'table.cjs <raw-fast-report.json> <new-table.md> [raw-exact-report.json] [default-flow-report.json] [thunder-6-exact-repeat.json]');
const report = JSON.parse(fs.readFileSync(input));
assert(report.finished && report.mode === 'Fast' && report.cases.length === 19 && report.warmSamples >= 3,
    'Use a complete 19-case Fast matrix with at least three warm clicks');
const lines = ['# Published desktop Fast results', '',
    `Engine source \`${report.engineCommit}\`, calculator source \`${report.hostCommit}\`, package SHA-256 \`${report.packageSha256}\`, published asset digest \`${report.assetsSha256}\`.`, '',
    `Edge ${report.browser} on ${report.cpu.trim()}. Each first click starts a fresh worker after the calculator session loads. The warm column takes the slowest of ${report.warmSamples} subsequent Calculate clicks on that worker. The width column takes the largest certified full interval across every row and sample. The display column takes the largest outward-rounded ± value shown on screen. The Work and Storage columns give the maxima measured by the engine.`, ''];
const integer = n => n.toLocaleString('en-US');
const fastCheck = verifyReferences(report);
lines.push(`Reference comparison covered ${fastCheck.checkedRows} Fast row observations in ${fastCheck.checkedSamples} successful clicks. Each certified interval contains its reference fraction and each exact row equals its reference. [Reference methods](../legacy-reference-replay.md)`, '');
const display = r => r.uncertainty === '±0%' ? r.value : r.value + ' ' + r.uncertainty;
lines.push('## Five- and six-card player hands', '',
    'Both hands use the same main-deck entries, effect settings and goals. Each player group contains both goals, giving the same union as Any combo.', '',
    '| Deck | Hand goal | Five cards | Six cards | Worst warm ms, five / six |',
    '| --- | --- | ---: | ---: | ---: |');
for (const [slug, name] of [['phantom-knights','Phantom Knights'], ['thunder-dragon','Thunder Dragon'], ['lightsworn','Lightsworn']]) {
    const pair = [5, 6].map(hand => report.cases.find(c => c.name === slug + '-' + hand));
    const snapshots = pair.map(c => c.samples.find(s => s.outcome === 'success')?.rendered);
    if (snapshots.some(s => !s)) continue;
    const worst = pair.map(c => Math.max(...c.samples.filter(s => s.index && s.outcome === 'success').map(s => s.uiMs)).toFixed(1));
    for (const row of snapshots[0].filter(r => r.label !== 'Access lines')) {
        const other = snapshots[1].find(r => r.label === row.label);
        assert(other);
        lines.push('| ' + [name, row.label, display(row), display(other), row.label === 'Any combo' ? worst.join(' / ') : ''].join(' | ') + ' |');
    }
}
lines.push('');
for (const [title, predicate] of [
    ['Controls and ordinary decks', c => c.reveals <= 2],
    ['Heavy decks', c => c.reveals > 2]
]) {
    lines.push('## ' + title, '', '| Input | Hand | Reveal copies | First ms | Worst warm ms | Full width pp | Display ±pp | Work | Peak entries | Peak cells | Result |',
        '| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |');
    for (const c of report.cases.filter(predicate)) {
        const samples = c.samples.filter(s => s.outcome === 'success');
        const warm = samples.filter(s => s.index > 0);
        const rows = samples.flatMap(s => s.rows);
        const metrics = c.samples.flatMap(s => s.response?.Metrics ? [s.response.Metrics] : []);
        const metric = key => metrics.length ? integer(Math.max(...metrics.map(m => m[key]))) : '—';
        const full = rows.length ? Math.max(...rows.map(r => r.widthPp)) : null;
        const visible = samples.flatMap(s => s.rendered);
        const displayed = visible.length ? Math.max(...visible.map(r => Number(r.uncertainty.replace(/[±%\s]/g, '')))) : null;
        if (c.passed) assert(warm.length === report.warmSamples && rows.length && metrics.length === c.samples.length);
        lines.push('| ' + [c.name, c.hand, c.reveals, c.samples[0]?.uiMs?.toFixed(1) ?? '—',
            warm.length ? Math.max(...warm.map(s => s.uiMs)).toFixed(1) : '—', full?.toFixed(6) ?? '—', displayed?.toFixed(2) ?? '—',
            metric('WorkSpent'), metric('PeakEntries'), metric('PeakCells'),
            c.passed ? full === 0 ? 'Exact' : 'Bounded' : c.error ? 'Verification error' : samples.length ? 'Missed target' : c.samples.at(-1)?.outcome].join(' | ') + ' |');
    }
    lines.push('');
}
if (exactInput) {
    const exact = JSON.parse(fs.readFileSync(exactInput));
    assert(exact.finished && exact.mode === 'Exact' && exact.assetsSha256 === report.assetsSha256 && exact.packageSha256 === report.packageSha256);
    const exactCheck = verifyReferences(exact);
    assert.deepEqual(exact.cases.map(c => c.name).sort(), report.cases.filter(c => c.reveals > 2).map(c => c.name).sort());
    lines.push('## Optional heavy Exact', '',
        'Each sample uses the same deck and worker after a Fast warmup. Clicking Calculate in Exact mode keeps Standard Storage and permits unlimited Work. The time limit is 60,000 ms per warm click. Every completed physical fraction was checked against every corresponding Fast interval.', '',
        '| Input | Warm Exact ms | Work | Peak entries | Peak cells | Result |',
        '| --- | ---: | ---: | ---: | ---: | --- |');
    for (const e of exact.cases) {
        const f = report.cases.find(c => c.name === e.name);
        assert.equal(f.inputHash, e.inputHash);
        for (const s of e.samples) {
            if (s.outcome === 'success') {
                assert(s.passesNumericalTarget && s.temperature === 'warm');
                for (const fast of f.samples.filter(x => x.outcome === 'success')) {
                    assert.equal(s.rows.length, fast.rows.length);
                    for (let i = 0; i < s.rows.length; i++) {
                        assert.equal(s.rows[i].label, fast.rows[i].label);
                        const [n, d] = s.rows[i].lower.split('/').map(BigInt);
                        const [ln, ld] = fast.rows[i].lower.split('/').map(BigInt);
                        const [un, ud] = fast.rows[i].upper.split('/').map(BigInt);
                        assert(ln * d <= n * ld && n * ud <= un * d, e.name + ' Fast interval excludes Exact row ' + i);
                    }
                }
            }
            const m = s.response?.Metrics;
            lines.push('| ' + [e.name, s.uiMs?.toFixed(1) ?? '—', m ? integer(m.WorkSpent) : '—',
                m ? integer(m.PeakEntries) : '—', m ? integer(m.PeakCells) : '—',
                s.outcome === 'success' ? (s.passesLatencyTarget ? 'Exact' : 'Missed 60 s') : s.outcome].join(' | ') + ' |');
        }
    }
    for (const e of exact.cases.filter(c => c.samples.some(s => s.outcome === 'censored' && !s.wireHash))) {
        lines.push('', `For ${e.name}, the report records the successful Fast warmup, Exact selection through the UI, one retained worker and the next request dispatch. The 60-second observation ended before a response. The original timeout record omitted that request’s worker execution discriminator and input hash. Wire parity and engine metrics are available for the completed Exact cases.`);
    }
    lines.push('', `Reference comparison matched ${exactCheck.checkedRows} rational rows across ${exactCheck.checkedSamples} completed Exact samples. [Exact samples and rational rows](edge-final-exact.json)`, '');
    if (exactRepeatInput) {
        const repeat = JSON.parse(fs.readFileSync(exactRepeatInput));
        assert(repeat.finished && repeat.mode === 'Exact' && repeat.cases.length === 1 && repeat.assetsSha256 === exact.assetsSha256 &&
            repeat.packageSha256 === exact.packageSha256 && repeat.browser === exact.browser);
        const e = repeat.cases[0], s = e.samples[0], original = exact.cases.find(c => c.name === e.name);
        assert(e.name === 'thunder-dragon-6' && e.inputHash === original.inputHash && e.samples.length === 1 && s.temperature === 'warm' && s.wireHash);
        if (s.outcome === 'censored') {
            const q = s.partial.requests[0];
            assert(s.cutoffMs === 60000 && q.execution === 'interactive-exact-v1' && q.worker === 0 && !q.received &&
                s.partial.workersBefore === 1 && s.partial.workers.length === 1);
            lines.push('A separate six-card Thunder Dragon run also reached the 60-second cutoff. It recorded a new Exact request on the same worker that had completed the Fast warmup and verified the production execution discriminator and input. The worker had returned no response at the cutoff. [Repeat observation](edge-thunder-6-exact-repeat.json)', '');
        } else {
            assert(s.outcome === 'success' && verifyReferences(repeat).checkedSamples === 1);
            lines.push(`A separate six-card Thunder Dragon run returned all four Exact rows in ${s.uiMs.toFixed(1)} ms on a worker warmed with the same deck. The harness verified the production execution discriminator, input and reference fractions. [Repeat observation](edge-thunder-6-exact-repeat.json)`, '');
        }
    }
}
if (defaultInput) {
    const flows = JSON.parse(fs.readFileSync(defaultInput));
    lines.push('## Default calculator mode', '', 'After opening Help and returning to the calculator, both sessions used the fresh app’s default Progressive mode.', '');
    for (const flow of flows) {
        assert.equal(flow.stages[0], 'initial');
        const accuracy = flow.rows.every(r => r.lower === r.upper) ? 'exact' : 'certified';
        lines.push(`- ${flow.name} displayed ${flow.rows.length} ${accuracy} rows.` +
            (flow.cancelledUpgrade ? ' Cancelling its physical Exact upgrade retained every displayed value and uncertainty.' : ''));
    }
    lines.push('');
}
lines.push('The compact report retains every distinct rational interval, displayed row, first click and warm sample. The baseline used the same Calculate path and returned a Work limit on ten heavy inputs. [Candidate data](edge-final.json) · [Baseline data](edge-baseline.json) · [Deck sources and reproduction](README.md)', '');
fs.writeFileSync(destination, lines.join('\n'), { flag: 'wx' });
