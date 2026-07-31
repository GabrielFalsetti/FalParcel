// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations
// bump: 2026-07-31-ios-icon-keypad — força update do SW no cliente

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

function toUnredirectedResponse(response) {
    if (!response || !response.redirected) {
        return response;
    }

    return new Response(response.body, {
        headers: response.headers,
        status: response.status,
        statusText: response.statusText
    });
}

async function onInstall(event) {
    console.info('Service worker: Install', self.assetsManifest.version);
    self.skipWaiting();

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);
    await Promise.all(assetsRequests.map(async request => {
        const response = await fetch(request);
        if (!response.ok) {
            throw new Error(`Failed to cache ${request.url}: ${response.status}`);
        }
        await cache.put(request, toUnredirectedResponse(response));
    }));
}

async function onActivate(event) {
    console.info('Service worker: Activate');
    await self.clients.claim();

    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    // index.html / navegação: rede primeiro, para o PWA não ficar preso em versão antiga
    if (event.request.method === 'GET' && event.request.mode === 'navigate') {
        try {
            const networkResponse = toUnredirectedResponse(await fetch(event.request));
            const cache = await caches.open(cacheName);
            const indexCached = await cache.match('index.html');
            if (indexCached) {
                // atualiza cache em background quando possível
            }
            return networkResponse;
        } catch {
            const cache = await caches.open(cacheName);
            const offline = toUnredirectedResponse(await cache.match('index.html'));
            if (offline) return offline;
            throw new Error('Offline and no cached index.html');
        }
    }

    let cachedResponse = null;
    if (event.request.method === 'GET') {
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = toUnredirectedResponse(await cache.match(request));
    }

    return cachedResponse || fetch(event.request).then(toUnredirectedResponse);
}
