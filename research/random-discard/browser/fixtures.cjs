const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const players = require('./player-decks.json');
const neutralCases = require('../../../tests/CardProbability.EngineTests/Fixtures/Data/reveal-cases.json');
const sourceCommit = '64e809da46e4b139949433c95a7caaabe9c222ee';

function canonical(value) {
    if (Array.isArray(value)) return value.map(canonical);
    if (value && typeof value === 'object') {
        return Object.fromEntries(Object.keys(value).sort().map(key => [key, canonical(value[key])]));
    }
    return value;
}
const hash = value => crypto.createHash('sha256').update(JSON.stringify(canonical(value))).digest('hex');

// Session converters and the worker use different enum encodings.
function forSession(value) {
    if (Array.isArray(value)) return value.map(forSession);
    if (!value || typeof value !== 'object') return value;
    const result = Object.fromEntries(Object.entries(value).map(([key, entry]) => [key, forSession(entry)]));
    if ('Identity' in result && 'Source' in result) {
        delete result.Identity;
        result.Source = ['User', 'Metadata'][result.Source];
    }
    if (result.DrawOperation === 1) result.DrawOperation = 'DangerReveal';
    if (result.PostDrawFailureBehavior === 0) result.PostDrawFailureBehavior = 'FailBranch';
    if (result.MaximumMode === 0) result.MaximumMode = 'Fixed';
    if (result.MaximumMode === 1) result.MaximumMode = 'HandSize';
    return result;
}

function importSession(input) {
    const categories = new Map(input.Cards.flatMap(card => card.Categories).map(cat => [cat.Identity, cat]));
    return {
        SchemaVersion: 15, CalculationModel: 'Fast', FastExecutionStrategy: 'FastOnly', HandSize: input.HandSize,
        Categories: forSession([...categories.values()]), Cards: forSession(input.Cards),
        Combos: forSession(input.Combos), ComboGroups: input.Groups,
        DrawActivationPriority: input.Options.DrawActivationPriority ?? { Mode: 'EncounterOrder', CardIds: [] }
    };
}

const category = (Name, MetadataKey = null) => ({ Name, Source: MetadataKey ? 1 : 0, MetadataKey,
    Identity: MetadataKey ? 'metadata:' + MetadataKey : 'user:' + Name });
const dark = category('DARK', 'attribute:dark');
const lowWarrior = category('Level 4 or lower Warrior');

function playerInputs(deck, catalog, heavy) {
    const allNames = new Set(deck.cards.map(([name]) => name));
    assert.equal(allNames.size, deck.cards.length);
    assert.equal(deck.cards.reduce((n, [, count]) => n + count, 0), deck.size);
    for (const names of Object.values(deck.groups)) for (const name of names) assert(allNames.has(name), name);
    const Cards = deck.cards.map(([name, copies]) => {
        const info = catalog.get(name);
        assert(info, 'Missing catalog identity: ' + name);
        const plain = structuredClone(heavy.Cards.find(c => c.Id === 'filler'));
        const card = { ...plain, Id: String(info.id), ExternalCardId: info.id, Name: name, Copies: copies,
            Categories: Object.entries(deck.groups).filter(([, names]) => names.includes(name)).map(([label]) => category(label)) };
        if (deck.name === 'thunder-dragon' && info.attribute === 'DARK') card.Categories.push(dark);
        if (deck.name === 'phantom-knights' && info.race === 'Warrior' && info.level <= 4) card.Categories.push(lowWarrior);
        if (name.startsWith('Danger!')) Object.assign(card, { DrawOperation: 1, DrawOncePerTurn: false });
        if (name === 'Allure of Darkness') Object.assign(card, { DrawCount: 2, DrawOncePerTurn: false,
            PostDrawDiscardCount: 1, PostDrawSelector: { Kind: 'Category', Categories: [dark.Identity] } });
        if (name === 'Reinforcement of the Army') Object.assign(card, { DrawOncePerTurn: false, OncePerTurn: false,
            Search: { Selector: { Kind: 'Category', Categories: [lowWarrior.Identity] }, ObjectiveMetadataOnly: false, Modes: null } });
        if (name === 'Pot of Desires') Object.assign(card, { DrawCount: 2, PreDrawDeckRemovalCount: 10, DrawOncePerTurn: true });
        return card;
    });
    const Combos = deck.goals.map(names => ({ Name: names.join(' + '), GroupId: 'g', Cards: [], AlternativeGroups: [],
        Categories: names.map(name => ({ BaseCategory: category(name), MinCount: 1, MaxCount: 0, MaximumMode: 'HandSize' })) }));
    return [5, 6].map(HandSize => ({ name: deck.name + '-' + HandSize, source: deck.source,
        input: { Cards, Combos, HandSize, Groups: [{ Id: 'g', Name: 'Access lines' }], WorkUnits: 150000000,
            StorageTier: 0, Options: { DrawActivationPriority: null, Mode: 0 }, DrawOperationVersion: 1 }, reference: null }));
}

function workload(host) {
    const frozen = JSON.parse(execFileSync('git', ['-C', host, 'show', sourceCommit + ':research/danger-fast/frozen-fixtures.json'],
        { encoding: 'utf8', maxBuffer: 8 * 1024 * 1024 }));
    assert.equal(frozen.Cases.length, 13);
    const fixtures = frozen.Cases.map(row => {
        assert.equal(hash(row.Input), row.InputSha256, row.Name);
        const name = row.Name.replace(/^danger-/, '');
        const reference = neutralCases.find(entry => entry.Name === name).Reference;
        if (row.ExactReference) assert.deepEqual(reference, row.ExactReference, row.Name);
        return { name, input: row.Input, reference, source: sourceCommit };
    });
    const catalogData = JSON.parse(fs.readFileSync(path.join(host, 'YGOProbabilityCalculatorBlazor/wwwroot/data/card-catalog.v1.json')));
    const catalog = new Map(catalogData.cards.map(card => [card.name, card]));
    return [...fixtures, ...players.flatMap(deck => playerInputs(deck, catalog, fixtures.find(f => f.name === 'heavy-40').input))];
}

function comparable(input) {
    const copy = structuredClone(input);
    // Saved-session catalog reconciliation annotates official entries with these identities.
    // None are referenced by these decks' goals, selectors, or ordering bits.
    for (const card of copy.Cards) card.Categories = card.Categories.filter(c =>
        !/^(archetype|card-name|catalog-id|mentions):/.test(c.MetadataKey ?? ''));
    if (copy.Options.DrawActivationPriority?.Mode === 0 && !copy.Options.DrawActivationPriority.CardIds.length)
        copy.Options.DrawActivationPriority = null;
    for (const combo of copy.Combos) for (const direct of combo.Cards) {
        if (direct.MaximumMode === 'Fixed') direct.MaximumMode = 0;
        if (direct.MaximumMode === 'HandSize') direct.MaximumMode = 1;
    }
    return copy;
}

module.exports = { workload, importSession, comparable, hash, sourceCommit };
if (require.main === module) {
    const [host, destination] = process.argv.slice(2);
    assert(host && destination, 'fixtures.cjs <calculator-repository> <generated-json>');
    fs.writeFileSync(destination, JSON.stringify(workload(host), null, 2) + '\n', { flag: 'wx' });
}
