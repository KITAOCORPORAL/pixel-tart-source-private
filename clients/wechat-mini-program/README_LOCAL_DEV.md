# WeChat Mini Program LocalDev Preview

This native mock has exactly five pages. Set `enabled`, `publicId`, and the temporary LocalDev token in `localdev.config.ts` only for a local WeChat DevTools session. Never commit a real token. The default committed configuration remains disabled.

The server binds `127.0.0.1`, so this works only in the simulator on the same computer. It does not support a real phone. Production requires HTTPS, a permitted domain, server-side `code2Session`, and deployment credentials that are not part of this repository.

Run the lightweight contract check with `node tests/selection-store.spec.js`.
