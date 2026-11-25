/* Service worker — Orçamento comprometido (usado só quando o app é servido por http/https, ex.: PWA hospedado).
   O aplicativo do Windows (Orcamento.exe) roda direto do arquivo e não usa este arquivo.
   • Todos os arquivos do app (inclusive Tailwind, Lucide e fontes, que agora são locais) ficam em cache → abre offline.
   • Páginas: rede primeiro (pega atualizações), cache como reserva.
   Para publicar uma nova versão, aumente o número em VERSAO. */
const VERSAO = 'v7';
const CACHE_APP = `orcamento-app-${VERSAO}`;
const FONTES = [
  'geist-latin-2','geist-latin-4','geist-latin-6','geist-latin-8',
  'geist-latin-ext-1','geist-latin-ext-3','geist-latin-ext-5','geist-latin-ext-7',
  'geistmono-latin-10','geistmono-latin-12','geistmono-latin-14',
  'geistmono-latin-ext-9','geistmono-latin-ext-11','geistmono-latin-ext-13'
].map(n => `./vendor/fonts/${n}.woff2`);
const CASCA = [
  './', './index.html', './seed.js', './manifest.webmanifest',
  './vendor/tailwind-3.4.17.js', './vendor/lucide-0.469.0.min.js', './vendor/fonts.css',
  './icons/icon-192.png', './icons/icon-512.png', './icons/icon-maskable-512.png', './icons/apple-touch-icon.png', './icons/favicon-32.png',
  ...FONTES
];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE_APP).then(c => c.addAll(CASCA)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const nomes = await caches.keys();
    await Promise.all(nomes.filter(n => n !== CACHE_APP).map(n => caches.delete(n)));
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', event => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;

  // Abrir o app: rede primeiro, cache como reserva
  if (req.mode === 'navigate') {
    event.respondWith((async () => {
      const cache = await caches.open(CACHE_APP);
      try {
        const resp = await fetch(req);
        if (resp.ok) cache.put('./index.html', resp.clone());
        return resp;
      } catch (e) {
        return (await cache.match('./index.html')) || (await cache.match('./')) || Response.error();
      }
    })());
    return;
  }

  // Demais arquivos: cache primeiro
  event.respondWith((async () => {
    const cache = await caches.open(CACHE_APP);
    const guardado = await cache.match(req, { ignoreSearch: true });
    if (guardado) return guardado;
    const resp = await fetch(req);
    if (resp.ok) cache.put(req, resp.clone());
    return resp;
  })());
});
