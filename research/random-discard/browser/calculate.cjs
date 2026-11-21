const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { chromium } = require('playwright');
const { workload, importSession, comparable, hash, sourceCommit } = require('./fixtures.cjs');

// Observe the existing application. All dispatch, routing, policy selection, worker
// lifetime and rendering remain owned by the application. Parse responses after timing.
function observe() {
    const probe = window.browserBenchmark = { workers: [], runs: [] };
    probe.visibleRows = () => [...document.querySelectorAll('.probability-results .combo-probability-row')].map(n => ({
        label: n.querySelector('.combo-probability-name').firstChild.textContent.trim(),
        value: n.querySelector('.combo-probability-value').innerText,
        uncertainty: n.querySelector('.probability-uncertainty').innerText,
        focusDetail: n.querySelector('.probability-with-accuracy').title
    }));
    window.Worker = class extends Worker {
        constructor(...args) {
            super(...args);
            if (!String(args[0]).includes('/calculation-worker.js')) return;
            this.index = probe.workers.length;
            const worker = { created: performance.now() };
            probe.workers.push(worker);
            this.addEventListener('message', ({ data }) => {
                if (data.ready) worker.ready = performance.now();
                else {
                    const run = probe.runs.at(-1);
                    if (!run) return;
                    const request = run.requests.findLast(r => r.id === data.id && r.worker === this.index && !r.received);
                    if (request) Object.assign(request, { received: performance.now(), response: data.response, error: data.error });
                    else run.transportError = data.error ?? 'Unexpected worker response';
                }
            });
        }
        postMessage(data, ...args) {
            if (this.index !== undefined && data.json) probe.runs.at(-1).requests.push({ worker: this.index, id: data.id,
                sent: performance.now(), json: data.json, fastStage: data.fastStage ?? null, execution: data.execution ?? null });
            return super.postMessage(data, ...args);
        }
    };
    document.addEventListener('click', event => {
        const action = event.target.closest('.calculate-action > button');
        if (!action || action.getAttribute('aria-busy') === 'true') return;
        const run = { started: performance.now(), workersBefore: probe.workers.length, requests: [] };
        probe.runs.push(run);
        function check() {
            if (probe.runs.at(-1) !== run) return;
            const button = document.querySelector('.calculate-action > button');
            const failure = document.querySelector('.calculation-error');
            const delivered = run.requests.length > 0 && run.requests.every(r => r.received !== undefined);
            const current = document.querySelector('.probability-results');
            if (button?.getAttribute('aria-busy') === 'false' && (failure ||
                (delivered && current && !current.querySelector('.probability-result-status')))) {
                const nodes = [...(current?.querySelectorAll('.combo-probability-row') ?? [])];
                if (failure || (nodes.length && nodes.every(n => n.querySelector('.combo-probability-value')?.checkVisibility()))) {
                    run.observed = performance.now();
                    run.error = failure?.innerText;
                    run.rows = probe.visibleRows();
                    return;
                }
            }
            requestAnimationFrame(check);
        }
        requestAnimationFrame(check);
    }, true);
}

function fraction(value) {
    assert(value && /^\d+$/.test(value.Numerator) && /^\d+$/.test(value.Denominator));
    const n = BigInt(value.Numerator), d = BigInt(value.Denominator);
    assert(d > 0n && n <= d);
    return [n, d];
}
const rowsOf = item => [item.Total, ...item.Combos, ...item.Groups];
const printed = n => (Number(n) / 100).toFixed(2) + '%';
const round = (n, d) => (2n * n + d) / (2n * d);
const ceil = (n, d) => n > 0n ? (n + d - 1n) / d : 0n;

function verifyRows(fixture, response, visible, exactRequired, mode) {
    const { Result: result } = response, contract = result.Contract;
    assert.equal(contract.RequestedMode, mode);
    assert.equal(contract.ExecutedMode, mode);
    assert.equal(contract.Scope, 1);
    assert.equal(contract.Completion, 0);
    assert([0, 1].includes(contract.Accuracy), 'Result needs exact or certified bounded accuracy');
    const labels = ['Any combo', ...fixture.input.Combos.map(c => c.Name), ...fixture.input.Groups.map(g => g.Name)];
    const raw = rowsOf(contract.Fractions);
    const certificate = result.SearchAvailability;
    const hasIntervals = [0, 1, 4].includes(certificate?.Status) && certificate?.Total;
    const intervals = hasIntervals ? rowsOf(certificate) : [];
    if (contract.Accuracy === 0 && !hasIntervals) assert.equal(contract.Proof, 'CertifiedPhysical', 'Exact primary proof');
    assert.equal(visible.length, labels.length, 'Visible result-row count');
    assert.equal(raw.length, labels.length, 'Primary fraction count');
    if (contract.Accuracy === 1 || hasIntervals) assert.equal(intervals.length, labels.length, 'Certificate row count');
    if (fixture.reference) assert.equal(fixture.reference.length, labels.length, 'Reference row count');
    return labels.map((label, i) => {
        const row = visible.find(r => r.label === label);
        assert(row, 'Missing visible row ' + label);
        const primary = fraction(raw[i]);
        const [ln, ld] = intervals[i] ? fraction(intervals[i].Lower) : primary;
        const [un, ud] = intervals[i] ? fraction(intervals[i].Upper) : primary;
        const width = un * ld - ln * ud, denom = ld * ud;
        assert(width >= 0n, label + ' unordered interval');
        // The host permits a collapsed physical certificate below an optimistic primary.
        if (contract.Accuracy === 0) assert.equal(width, 0n, label + ' exact proof has nonzero width');
        const reference = fixture.reference?.[i];
        if (reference) {
            const [n, d] = reference.split('/').map(BigInt);
            assert(ln * d <= n * ld && n * ud <= un * d, label + ' excludes reference ' + reference);
        }
        const center = round((ln * ud + un * ld) * 10000n, 2n * denom);
        const lowerRadius = ceil(center * ld - ln * 10000n, ld);
        const upperRadius = ceil(un * 10000n - center * ud, ud);
        const radius = lowerRadius > upperRadius ? lowerRadius : upperRadius;
        const value = Number(row.value.replace(/[%\s,]/g, ''));
        // The exact-only UI may use a double-formatting midpoint convention.
        if (width === 0n) assert(Math.abs(value - Number(ln * 10000000000n / ld) / 1e8) <= 0.00500002, label + ' printed primary');
        else assert.equal(row.value, printed(center), label + ' printed center');
        assert.equal(row.uncertainty, width === 0n ? '±0%' : '±' + printed(radius), label + ' printed uncertainty');
        assert(row.focusDetail.includes(raw[i].Numerator + '/' + raw[i].Denominator), label + ' bound to wrong primary fraction');
        return { label, lower: ln + '/' + ld, upper: un + '/' + ud, reference: reference ?? null,
            widthPp: Number(width * 10000000000n / denom) / 1e8, displayedRadiusPp: width === 0n ? 0 : Number(radius) / 100,
            passesNumericalTarget: exactRequired ? contract.Accuracy === 0 && width === 0n :
                width * 100n <= 2n * denom && radius <= 100n };
    });
}

async function scanAssetHash(root) {
    const files = (await fs.readdir(path.join(root, '_framework'))).filter(name => /\.(js|json|wasm)$/.test(name)).sort()
        .map(name => '_framework/' + name);
    assert(files.some(name => name.endsWith('.wasm')), 'Expected published WASM assets');
    files.push('js/calculation-worker.js', 'js/background-calculation.js');
    const manifest = [];
    for (const name of files) {
        const bytes = await fs.readFile(path.join(root, name));
        manifest.push([name, bytes.length, crypto.createHash('sha256').update(bytes).digest('hex')]);
    }
    return hash(manifest);
}

async function main() {
    const [hostArg, webArg, outputArg, packageArg] = process.argv.slice(2);
    assert(hostArg && webArg && outputArg && packageArg, 'calculate.cjs <calculator-checkout> <published-wwwroot> <new-output.json> <restored.nupkg>');
    const host = path.resolve(hostArg), root = path.resolve(webArg), output = path.resolve(outputArg);
    const { serve, load, mode: selectMode, launchOptions } = require(path.join(host, 'scripts/background-calculation/browser-support.cjs'));
    const selected = new Set((process.env.REVEAL_CASES || '').split(',').filter(Boolean));
    const fixtures = workload(host).filter(f => !selected.size || selected.has(f.name));
    assert(fixtures.length && (!selected.size || fixtures.length === selected.size), 'Unknown selected case');
    const supplemental = path.join(__dirname, '../legacy-exact.json');
    const exactEvidence = await fs.readFile(supplemental, 'utf8').catch(error => {
        if (error.code !== 'ENOENT') throw error;
        return null;
    });
    if (exactEvidence) {
        const evidence = JSON.parse(exactEvidence);
        const rows = new Map([...evidence.Cases, ...evidence.PlayerCases].map(v => [v.Name, v]));
        for (const fixture of fixtures) {
            const v = rows.get(fixture.name);
            if (!v) continue;
            assert.equal(v.InputHash, hash(fixture.input), fixture.name + ' legacy Exact input');
            if (fixture.reference) assert.deepEqual(v.Rows, fixture.reference);
            else fixture.reference = v.Rows;
        }
    }
    const modeName = process.env.REVEAL_MODE ?? 'Fast';
    assert(['Fast', 'Exact'].includes(modeName));
    const exact = modeName === 'Exact', mode = exact ? 2 : 0;
    const metricsSetting = process.env.REVEAL_REQUIRE_METRICS ?? '1';
    assert(['0', '1'].includes(metricsSetting));
    const requireMetrics = metricsSetting === '1';
    const warmSamples = Number(process.env.REVEAL_WARM ?? (exact ? 1 : 3)), cutoff = Number(process.env.REVEAL_CUTOFF_MS ?? 60000);
    assert(Number.isInteger(warmSamples) && warmSamples >= 1 && warmSamples <= 15);
    assert(Number.isInteger(cutoff) && cutoff >= 1000 && cutoff <= 600000);
    const packageBytes = await fs.readFile(packageArg);
    const project = JSON.parse(await fs.readFile(path.join(host, 'YGOProbabilityCalculatorBlazor/obj/project.assets.json')));
    const restored = Object.entries(project.libraries).filter(([id]) => id.startsWith('CardProbability.Engine/'));
    assert.equal(restored.length, 1, 'Expected one restored engine implementation');
    const [packageId, info] = restored[0];
    assert.equal(crypto.createHash('sha512').update(packageBytes).digest('base64'), info.sha512, 'Package must match restored hash');
    const manifests = await Promise.all(Object.keys(project.packageFolders).map(folder =>
        fs.readFile(path.join(folder, info.path, 'cardprobability.engine.nuspec'), 'utf8').catch(error => {
            if (error.code !== 'ENOENT') throw error;
            return null;
        })));
    const engineCommit = manifests.find(Boolean)?.match(/<repository\b[^>]*\bcommit="([a-f0-9]{40})"/i)?.[1];
    assert(engineCommit, 'Package manifest must identify the measured source commit');
    const report = { sourceCommit, assetsSha256: await scanAssetHash(root), mode: modeName, requireMetrics, warmSamples, cutoff, viewport: [1440, 1000],
        supplementalReferenceSha256: exactEvidence ? crypto.createHash('sha256').update(exactEvidence).digest('hex') : null,
        hostCommit: execFileSync('git', ['-C', host, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim(),
        engineCommit, packageId, packageSha256: crypto.createHash('sha256').update(packageBytes).digest('hex'),
        platform: os.platform(), cpu: os.cpus()[0].model, logicalProcessors: os.cpus().length,
        node: process.version, browser: null, cases: [] };
    await fs.mkdir(path.dirname(output), { recursive: true });
    await fs.writeFile(output, JSON.stringify(report), { flag: 'wx' });
    const save = async () => {
        await fs.writeFile(output + '.pending', JSON.stringify(report, null, 2) + '\n');
        await fs.rename(output + '.pending', output);
    };
    const { server, url } = await serve(root);
    let browser;
    try {
        browser = await chromium.launch(launchOptions());
        report.browser = browser.version();
        for (const fixture of fixtures) {
            const reveals = fixture.input.Cards.filter(c => c.DrawOperation === 1).reduce((sum, c) => sum + c.Copies, 0);
            const ordinary = reveals <= 2, session = importSession(fixture.input);
            const expectedWire = structuredClone(fixture.input);
            expectedWire.Options.Mode = mode;
            const entry = { name: fixture.name, source: fixture.source, inputHash: hash(fixture.input), sessionHash: hash(session),
                reveals, hand: session.HandSize, reference: fixture.reference, samples: [], passed: false };
            report.cases.push(entry);
            const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, locale: 'en-US', serviceWorkers: 'block' });
            try {
                const page = await context.newPage(), pageErrors = [];
                page.on('pageerror', e => pageErrors.push(e.message));
                await page.route('https://**/*', route => route.abort());
                await page.addInitScript(observe);
                await page.goto(url);
                await load(page, session);
                if (exact) {
                    // Prime the actual worker on this same deck before measuring interactive Exact.
                    await page.getByRole('button', { name: 'Calculate', exact: true }).click();
                    await page.waitForFunction(() => browserBenchmark.runs[0]?.observed !== undefined, undefined, { timeout: cutoff });
                    const prime = await page.evaluate(() => ({ run: browserBenchmark.runs[0], workers: browserBenchmark.workers }));
                    assert.equal(prime.workers.length, 1);
                    assert.equal(prime.run.requests.length, 1);
                    const request = prime.run.requests[0];
                    assert.equal(request.worker, 0);
                    assert.equal(request.execution, null);
                    assert.equal(request.fastStage, null);
                    assert.deepEqual(comparable(JSON.parse(request.json)), comparable(fixture.input));
                    assert(!prime.run.error && JSON.parse(request.response).Result, 'Warmup Fast calculation must succeed');
                    entry.fastWarmup = { uiMs: prime.run.observed - prime.run.started, bootstrapMs: prime.workers[0].ready - prime.workers[0].created };
                    await selectMode(page, 'Exact');
                }
                for (let index = exact ? 1 : 0; index <= warmSamples; index++) {
                    const sample = { temperature: index === 0 ? 'first' : 'warm', index, outcome: 'started' };
                    entry.samples.push(sample);
                    await save();
                    await page.getByRole('button', { name: 'Calculate', exact: true }).click();
                    try {
                        await page.waitForFunction(i => window.browserBenchmark.runs[i]?.observed !== undefined, index, { timeout: cutoff });
                    } catch (e) {
                        if (e.name !== 'TimeoutError') throw e;
                        sample.outcome = 'censored';
                        sample.cutoffMs = cutoff;
                        const partial = await page.evaluate(i => {
                            const r = window.browserBenchmark.runs[i];
                            return { started: r?.started, elapsedMs: r ? performance.now() - r.started : null,
                                workersBefore: r?.workersBefore, workers: window.browserBenchmark.workers, requests: r?.requests.map(q => ({
                                id: q.id, worker: q.worker, sent: q.sent, received: q.received,
                                execution: q.execution, fastStage: q.fastStage, json: q.json })) ?? [] };
                        }, index);
                        sample.partial = { ...partial, requests: partial.requests.map(({ json, ...q }) => q) };
                        if (partial.requests.length) {
                            assert.equal(partial.requests.length, 1);
                            const q = partial.requests[0], wire = JSON.parse(q.json);
                            assert.equal(q.execution, exact ? 'interactive-exact-v1' : null);
                            assert.equal(q.fastStage, null);
                            assert.equal(q.worker, 0);
                            assert.equal(partial.workers.length, 1, 'Measurement must retain the original worker');
                            assert.equal(partial.workersBefore, index === 0 ? 0 : 1);
                            assert.deepEqual(comparable(wire), comparable(expectedWire));
                            sample.wireHash = hash(wire);
                        }
                        break;
                    }
                    const { run, workers } = await page.evaluate(i => ({ run: window.browserBenchmark.runs[i], workers: window.browserBenchmark.workers }), index);
                    sample.uiMs = run.observed - run.started;
                    sample.rendered = run.rows;
                    sample.alert = run.error;
                    assert.equal(run.requests.length, 1, 'Expected one production worker request');
                    const q = run.requests[0], input = JSON.parse(q.json);
                    sample.wireHash = hash(input);
                    assert.equal(q.execution, exact ? 'interactive-exact-v1' : null);
                    assert.equal(q.fastStage, null);
                    assert.deepEqual(comparable(input), comparable(expectedWire), fixture.name + ' input parity');
                    assert.equal(workers.length, 1, 'Fresh first request then same physical worker');
                    assert.equal(q.worker, 0);
                    assert.equal(run.workersBefore, index === 0 ? 0 : 1);
                    sample.bootstrapMs = index === 0 ? workers[0].ready - workers[0].created : null;
                    sample.roundTripMs = q.received - q.sent;
                    assert(!q.error, q.error);
                    const response = sample.response = JSON.parse(q.response);
                    assert.deepEqual(pageErrors, []);
                    if (requireMetrics && response.Result) assert(response.Metrics, 'Candidate success must expose actual engine metrics');
                    if (response.Metrics) {
                        const { WorkSpent, RemainingWork, PeakEntries, PeakCells } = response.Metrics;
                        assert([WorkSpent, PeakEntries, PeakCells].every(n => Number.isSafeInteger(n) && n >= 0), 'Invalid engine metrics');
                        if (exact) assert.equal(RemainingWork, null, 'Interactive Exact Work policy');
                        else assert(Number.isSafeInteger(RemainingWork) && RemainingWork >= 0);
                        if (response.Result) {
                            if (!exact) assert(WorkSpent <= 150000000 && RemainingWork <= 150000000, 'Exceeded Fast Work');
                            assert(PeakEntries <= 32768 && PeakCells <= 262144, 'Exceeded Standard Storage');
                        }
                    }
                    if (!response.Result) {
                        assert(run.error, 'Terminal failure must be shown');
                        sample.outcome = response.LimitReason === 1 ? 'work-limit' : response.LimitReason === 2 ? 'storage-limit' : 'failure';
                        break;
                    }
                    assert(!run.error, run.error);
                    sample.rows = verifyRows(fixture, response, run.rows, ordinary || exact, mode);
                    sample.outcome = 'success';
                    sample.passesNumericalTarget = sample.rows.every(row => row.passesNumericalTarget);
                    if (index) sample.passesLatencyTarget = sample.uiMs <= (exact ? 60000 : ordinary ? 1000 : 5000);
                }
                entry.passed = entry.samples.length === warmSamples + (exact ? 0 : 1) && entry.samples.every(s =>
                    s.outcome === 'success' && s.passesNumericalTarget && s.passesLatencyTarget !== false);
            } catch (error) {
                entry.error = error.stack;
                console.error(fixture.name, String(error.message).slice(0, 2048));
            } finally {
                await context.close();
                await save();
                console.log(fixture.name, entry.samples.map(s => s.outcome + (s.uiMs ? ' ' + Math.round(s.uiMs) + 'ms' : '')).join(', '));
            }
        }
        assert.equal(await scanAssetHash(root), report.assetsSha256, 'Published assets changed during rehearsal');
        report.finished = true;
        report.passed = report.cases.every(c => c.passed);
    } finally {
        await browser?.close();
        await new Promise(resolve => server.close(resolve));
        await save();
    }
    process.exitCode = report.passed ? 0 : 1;
}
module.exports = { observe, verifyRows };
if (require.main === module) main().catch(error => { console.error(error); process.exitCode = 1; });
