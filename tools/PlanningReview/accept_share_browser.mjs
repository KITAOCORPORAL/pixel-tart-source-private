// Single synthetic-room browser check, driven through a private headless Chromium session.
// Reads the one-time member link from stdin, so no credential appears in process arguments.
import {spawn} from 'node:child_process';
import {readFile} from 'node:fs/promises';
import {readFileSync} from 'node:fs';
import {join} from 'node:path';
import {setTimeout as wait} from 'node:timers/promises';

const {browser, profile, spki, memberLink, roomPath, timelineName} = JSON.parse(readFileSync(0, 'utf8'));
if (!spki || !/^https:\/\/.+\/r\/[0-9a-f]{32}\/workbench\.html#code=invite\./.test(memberLink))
    throw Error('Synthetic member link and QA certificate SPKI pin required');
const launched = spawn(browser, ['--headless=new', '--no-first-run', '--no-proxy-server', '--no-default-browser-check',
    '--disable-background-networking', '--remote-debugging-port=0', '--remote-debugging-address=127.0.0.1',
    '--ignore-certificate-errors-spki-list=' + spki, '--user-data-dir=' + profile, 'about:blank'],
    {stdio:'ignore', windowsHide:true});
let socket;
const pending = new Map();
let counter = 0, outcome;
try {
    let port;
    for (let attempt = 0; attempt < 150; attempt++) {
        if (launched.exitCode != null) throw Error('QA browser exited before configuring CDP');
        try { port = (await readFile(join(profile, 'DevToolsActivePort'), 'utf8')).split(/\r?\n/)[0]; break; }
        catch (err) { if (err.code !== 'ENOENT') throw err; }
        await wait(100);
    }
    if (!port) throw Error('QA browser did not expose a private CDP port');
    const tabs = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
    const page = tabs.find(t => t.type === 'page');
    if (!page) throw Error('QA browser has no test page');
    socket = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => { socket.addEventListener('open', resolve); socket.addEventListener('error', reject); });
    socket.addEventListener('message', event => {
        const result = JSON.parse(event.data), reply = pending.get(result.id);
        if (reply) { pending.delete(result.id); reply(result); }
    });
    const cdp = async (method, params = {}) => {
        const id = ++counter;
        const answer = new Promise(resolve => pending.set(id, resolve));
        socket.send(JSON.stringify({id, method, params}));
        const result = await Promise.race([answer, wait(7000, null, {ref:false})]);
        if (!result || result.error) throw Error('QA browser command failed: ' + method);
        return result.result;
    };
    await cdp('Page.enable');
    await cdp('Runtime.enable');
    await cdp('Page.navigate', {url:memberLink});
    for (let i = 0; i < 120; i++) {
        const check = await cdp('Runtime.evaluate', {returnByValue:true, expression:`(() => {
            if (location.pathname !== ${JSON.stringify(roomPath)} || location.hash !== '#timeline' || document.querySelector('#work')?.hidden !== false) return null;
            const rendered = Array.from(document.querySelectorAll('#canvasHost input, #canvasHost textarea')).map(el=>el.value).join(' ') + ' ' + (document.querySelector('#canvasHost')?.innerText || '');
            const sessionKey = 'workbench-session' + location.pathname.replace(/workbench\.html$/, '');
            const identity = JSON.parse(localStorage.getItem(sessionKey) || 'null');
            return { section: document.querySelector('#pageHead h1')?.innerText, timelineFound: rendered.includes(${JSON.stringify(timelineName)}),
                cleanedHash: location.hash === '#timeline', scopeMatches: !!identity && !localStorage.getItem('workbench-session'), distinctMember: identity?.name === '真实浏览器成员' };
        })()`});
        if (check.result?.value?.timelineFound) { outcome = check.result.value; break; }
        await wait(125);
    }
    if (!outcome?.scopeMatches || !outcome?.distinctMember || !outcome?.cleanedHash || outcome?.section !== '当天时间表')
        throw Error('Candidate browser did not render the member timeline with the scoped session');
    console.log(JSON.stringify(outcome));
} finally {
    socket?.close();
    if (launched.exitCode == null) launched.kill();
}
