# Front Angular (N2F)

Interface d'administration des notes de frais : Angular 22 (composants autonomes, signaux), Nx 23, Tailwind CSS 4 et
tests Vitest.

## Lancement

Le front se lance avec le reste de l'application par Aspire, depuis la racine du dépôt (voir le
[README principal](../README.md)) :

```bash
dotnet run --project tools/N2JSoft.Aspire
```

Aspire installe les dépendances, puis démarre le serveur de développement en lui donnant son port (`PORT`) et l'adresse
de l'API. Le serveur relaie vers l'API les appels en `/api` (`proxy.conf.js`).

## Commandes

| Commande                 | Rôle                                              |
| ------------------------ | ------------------------------------------------- |
| `npm ci`                 | installe les dépendances                          |
| `npx nx lint n2f`        | analyse le code (ESLint)                          |
| `npx nx test n2f`        | lance les tests (Vitest) ; `--coverage` en plus   |
| `npx nx build n2f`       | construit l'application dans `dist/n2f`           |
| `npx prettier --check .` | vérifie le formatage ; `--write` pour l'appliquer |

## Organisation

- `src/app/core/` : les briques partagées, dont la liste paginée (`pagedList`), la barre de pagination
  (`app-pagination`), la lecture des erreurs de l'API (`problem-details.ts`) et les validateurs de formulaire.
- `src/app/features/` : un dossier par fonctionnalité (`users`, `expense-reports`, `expenses`), avec ses pages, ses
  formulaires et son client de l'API.
- `src/styles.css` : les styles communs (Tailwind).
