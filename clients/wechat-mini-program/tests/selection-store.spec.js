const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const app = JSON.parse(read('app.json'));
const api = read('services/api.ts');
const store = read('services/selection-store.ts');
const appTs = read('app.ts');
const localConfig = read('localdev.config.example.ts');
const pages = fs.readdirSync(path.join(root, 'pages')).flatMap(folder =>
  fs.readdirSync(path.join(root, 'pages', folder)).filter(file => file.endsWith('.ts')).map(file => read(`pages/${folder}/${file}`)));

if (app.pages.length !== 5) throw new Error('Exactly five pages are required.');
if (!api.includes("'X-PixelTart-Dev-Token'")) throw new Error('LocalDev token header is missing.');
for (const token of ['expectedSelectionVersion', 'expectedRevision', 'operationId', 'mediaSession', 'confirmationSelectionVersion'])
  if (!api.includes(token) && !store.includes(token)) throw new Error(`Missing ${token}.`);
for (const token of ['wx.setStorageSync', 'refreshProject', 'initializeLocalDev', 'mediaUrl', 'syncPromise'])
  if (!store.includes(token) && !appTs.includes(token)) throw new Error(`Missing ${token}.`);
if (store.includes("ProviderNone') this.confirmed = true")) throw new Error('ProviderNone must never confirm.');
if (!localConfig.includes('enabled: false') || !localConfig.includes("devAccessToken: ''")) throw new Error('Committed LocalDev config must stay disabled and credential-free.');
if (!appTs.includes("wx.getStorageSync('pixel-tart-localdev-config/v1')")) throw new Error('Runtime LocalDev config must come from DevTools local storage.');
if (pages.some(source => source.includes('wx.request'))) throw new Error('Pages must not call wx.request directly.');
if (!pages.filter(source => source.includes('mediaUrl')).length) throw new Error('Image pages must use media sessions.');
console.log('mini-program contract tests: PASS');
