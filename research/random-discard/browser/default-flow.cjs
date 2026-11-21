const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('playwright');
const { observe, verifyRows } = require('./calculate.cjs');
const { workload, importSession, comparable } = require('./fixtures.cjs');

async function main() {
    const [hostArg, root, output] = process.argv.slice(2);
    assert(hostArg && root && output, 'default-flow.cjs <calculator-checkout> <published-wwwroot> <new-report.json>');
    const host = path.resolve(hostArg);
    const { serve, load, launchOptions } = require(path.join(host, 'scripts/background-calculation/browser-support.cjs'));
    const { server, url } = await serve(root);
    let browser;
    const results = [];
    try {
        browser = await chromium.launch(launchOptions());
        for (const fixture of workload(host).filter(f => ['mixed-40', 'heavy-40'].includes(f.name))) {
            const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, locale: 'en-US' });
            try {
                const page = await context.newPage(), errors = [];
                page.on('pageerror', e => errors.push(e.message));
                await page.route('https://**/*', route => route.abort());
                await page.addInitScript(observe);
                await page.goto(url);
                const session = importSession(fixture.input);
                // Schema 15 requires a strategy even when saving the app defaults.
                await page.getByRole('button', { name: 'Calculation settings', exact: true }).click();
                assert.equal(await page.locator('#calculationModel').inputValue(), 'Fast', 'Fresh app default is Progressive');
                await page.locator('#calculationModel').press('Escape');
                session.FastExecutionStrategy = 'Progressive';
                await load(page, session);
                await page.getByRole('link', { name: 'Help', exact: true }).click();
                await page.getByRole('link', { name: 'Back', exact: true }).click();
                await page.getByRole('button', { name: 'Calculate', exact: true }).click();
                const handle = await page.waitForFunction(() => {
                    if (document.querySelector('.calculation-error')) throw Error(document.querySelector('.calculation-error').innerText);
                    const run = browserBenchmark.runs[0];
                    if (!run?.requests[0]?.response || !document.querySelector('.probability-total-value')) return false;
                    const button = document.querySelector('.calculate-action > button');
                    const preliminary = !!document.querySelector('.probability-result-status-preliminary');
                    if (!preliminary && button.getAttribute('aria-busy') !== 'false') return false;
                    const accepted = preliminary ? run.requests[0] : run.requests.at(-1);
                    if (!accepted.response) return false;
                    const data = { response: JSON.parse(accepted.response), requests: run.requests.map(r => ({
                        input: JSON.parse(r.json), stage: r.fastStage, execution: r.execution })),
                        preliminary, rows: browserBenchmark.visibleRows() };
                    if (preliminary) {
                        if (button.getAttribute('aria-label') !== 'Cancel Exact upgrade') throw Error('Missing accessible cancellation');
                        button.click();
                    }
                    return data;
                }, undefined, { timeout: 60000 });
                const observed = await handle.jsonValue();
                assert.equal(observed.requests[0].stage, 'initial', 'Default mode uses the production Progressive owner');
                for (const request of observed.requests) assert.deepEqual(comparable(request.input), comparable(fixture.input));
                if (observed.requests.length > 1) assert.equal(observed.requests[1].stage, 'progressive-exact-v1');
                if (observed.preliminary) assert.equal(observed.requests.length, 2);
                const rows = verifyRows(fixture, observed.response, observed.rows, fixture.name === 'mixed-40', 0);
                assert(rows.every(row => row.passesNumericalTarget));
                await page.getByRole('button', { name: 'Calculate', exact: true }).waitFor();
                assert.deepEqual(await page.evaluate(() => browserBenchmark.visibleRows()), observed.rows);
                assert.deepEqual(errors, []);
                results.push({ name: fixture.name, stages: observed.requests.map(r => r.stage), cancelledUpgrade: observed.preliminary, rows });
            } finally { await context.close(); }
        }
    } finally {
        await browser?.close();
        await new Promise(resolve => server.close(resolve));
    }
    await fs.writeFile(output, JSON.stringify(results, null, 2) + '\n', { flag: 'wx' });
}
main().catch(e => { console.error(e); process.exitCode = 1; });
