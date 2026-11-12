# Chatex web app

Angular 22 + Tailwind CSS v4 frontend for Chatex. See the [Frontend docs](../Docs/Frontend.md) for the architecture and how it talks to the API.

```bash
npm install
```

```bash
npm start
```

The dev server runs at http://localhost:4200 and proxies `/api` and `/hubs` to the API at http://localhost:5214, so start the API first.

```bash
npm run lint
```

```bash
npm test -- --watch=false
```

```bash
npm run build
```
