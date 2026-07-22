declare global {
  interface Window {
    __LIVE_SYNC_CONFIG__?: {
      apiBaseUrl?: string;
      signalRBaseUrl?: string;
    };
  }
}

const isLocalhost = ['localhost', '127.0.0.1'].includes(window.location.hostname);
const runtimeConfig = window.__LIVE_SYNC_CONFIG__ ?? {};
const normalize = (url: string): string => url.replace(/\/$/, '');

export const appEndpoints = {
  apiBaseUrl: normalize(runtimeConfig.apiBaseUrl ?? (isLocalhost ? 'https://localhost:7001' : '')),
  signalRBaseUrl: normalize(
    runtimeConfig.signalRBaseUrl ?? (isLocalhost ? 'https://localhost:7000' : '')
  ),
};

