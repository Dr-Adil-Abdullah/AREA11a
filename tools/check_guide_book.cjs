// Optional browser proof for the generated offline book; NOT native WPF/Windows proof.
// Requires Playwright and a Chromium installation outside the application runtime.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

(async () => {
    let count = 0;
    const check = (value, label) => { assert(value, label); count++; console.log('PASS', label); };
    const options = { headless: true };
    if (process.env.CHROMIUM_PATH) options.executablePath = process.env.CHROMIUM_PATH;
    if (process.env.AREA11_SERVERLESS_CHROMIUM === '1') {
        const packaged = require('@sparticuz/chromium').default;
        options.executablePath = await packaged.executablePath();
        options.args = packaged.args;
    }
    const browser = await chromium.launch(options);
    try {
        const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
        const requests = [], errors = [];
        page.on('request', request => requests.push(request.url()));
        page.on('pageerror', error => errors.push(error.message));
        const root = path.resolve(__dirname, '..');
        const bundle = JSON.parse(fs.readFileSync(path.join(root, 'docs/guide/guide-source.json'), 'utf8'));
        await page.goto(pathToFileURL(path.join(root, 'docs/guide-prototype/book.html')).href);
        await page.evaluate(() => document.fonts.ready);
        check(await page.locator('main > ol > li').count() === 6, 'six root links');
        check((await page.locator('h1').innerText()).includes('رہنما کتاب'), 'authored edition label');
        check((await page.locator('main').innerText()).includes(`Docs ${bundle.version} · ${bundle.date}`), 'shared edition metadata');
        check(await page.locator('section').count() === 96, '96 topic anchors');
        for (const topic of bundle.topics) {
            const section = page.locator(`[id="${topic.id}"]`);
            check(await section.locator('table').count() > 0, 'control table ' + topic.id);
            check((await section.innerText()).includes(topic.blocks[0].text), 'purpose rendered ' + topic.id);
        }
        check(!(await page.locator('main').innerText()).includes('معلومات آئے گی'), 'no content placeholders');
        check(await page.locator('script').count() === 0, 'no executable scripts');
        check(requests.every(url => url.startsWith('file:') || url.startsWith('data:')), 'no network requests');
        check(errors.length === 0, 'no page errors');
        check(await page.evaluate(() => document.fonts.check('18px Area11Urdu')), 'embedded font loaded');
        check(await page.evaluate(() => [...document.querySelectorAll('a')].every(a => document.getElementById(decodeURIComponent(a.hash.slice(1))))), 'every offline link resolves');
        await page.locator('main > ol > li a').first().click();
        check(page.url().endsWith('#sales'), 'offline anchor navigation');
        await page.setViewportSize({ width: 390, height: 844 });
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'mobile width has no horizontal overflow');
        await page.setViewportSize({ width: 1280, height: 900 });
        if (process.env.GUIDE_SCREENSHOT) await page.screenshot({ path: process.env.GUIDE_SCREENSHOT });
        await page.emulateMedia({ media: 'print' });
        const pdf = await page.pdf({ format: 'A4', printBackground: true });
        check(pdf.length > 20000, 'browser print PDF nonempty');
        console.log(`TOTAL ${count} offline-book browser assertions; NOT WPF`);
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
