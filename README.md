# Technical-Assessment-Developer-Dotnet-Angular


## Général

Ce TP technique vise à tester vos compétences en Angular et ASP.NET Core. Ne perdez pas de temps sur le design, il ne sera pas évalué.

### Prérequis techniques

Le TP utilise .NET Aspire pour configurer une bdd PostgreSQL, une webapi ASP.NET Core, et un front Angular.

Il vous faudra donc :
- Docker Desktop (ou similaire)
- .NET 10 SDK avec ASP.NET
- nodeJS >= 22.12

### Lancement

Il suffit de lancer le projet Aspire disponible dans `tools/N2JSoft.Aspire` :

```bash
dotnet run --project tools/N2JSoft.Aspire
```

Aspire lance la bdd, le backend et le frontend. 

La console dotnet affiche le lien vers le dashboard Aspire pour consulter les services applicatifs.

### Critères d'évaluation

Nous fournissons ci-dessous les besoins fonctionnels à développer, ainsi que leurs critères d'acceptation.

Pour faciliter notre évaluation, merci de :

- faire des commits réguliers ;
- compléter la section "Mes Notes" à la fin de ce readme.md pour lister les tâches réalisées et éventuelles pistes d'amélioration.

### Rendu du travail

Créer un repo privé sur GitHub et inviter l'utilisateur suivant : n2jsoft-hr-cr

Ce repo doit inclure l'ensemble des fichiers (backend et frontend).


## Besoins fonctionnels

On souhaite mettre en place une solution de gestion des dépenses utilisable par un administrateur (typiquement secrétaire ou comptable) pour des utilisateurs de la société.

Les utilisateurs ne saisissent donc pas directement leurs dépenses ; c'est l'administrateur qui s'en charge.

### Gestion des Users

Un utilisateur doit être affectable à une note de frais ; on dit de cet utilisateur qu'il est l'"utilisateur affecté" à la note de frais.

Il ne peut y avoir qu'un seul utilisateur affecté à une note de frais, mais un utilisateur peut etre affecté à plusieurs notes de frais.

Chaque utilisateur a droit à un nombre maximum de dépenses par mois calendaire.

Un utilisateur peut être actif ou inactif. Seuls les utilisateurs actifs peuvent être affectés à une note de frais.

Un utilisateur a un nom, prénom et une adresse postale.

Un utilisateur peut etre supprimé (suppression logique).

### Gestion des Expenses

Une dépense est définie par les attributs suivants :
- une date au format exemple suivant : "mercredi 15 octobre 2025" ;
- une description : maximum 50 caractères ;
- un montant en euros ;
- une adresse de facturation (enseigne / rue + code postal + ville).

Ces attributs peuvent être éditables en tout temps.

Dans le cas où l'utilisateur a atteint son quota de dépenses au mois, un message d'erreur doit l'en informer et il ne peut pas créer sa nouvelle dépense.

Une dépense peut etre supprimée (suppression logique).

### Gestion des Expense Reports

Une note de frais contient les dépenses d'un utilisateur au mois.

Le format de l'intitulé d'une note de frais doit respecter le format "Utilisateur - Mois Année", par ex : "Juste Leblanc - Octobre 2025".

L'intitulé n'est pas éditable.

Une note de frais peut être supprimable (suppression physique).

La page de visualisation des notes de frais permet d'ajouter une note de frais en sélectionnant le mois et l'utilisateur.

La page de visualisation d'une note de frais permet : 
- d'y ajouter des dépenses ;
- d'afficher les dépenses existantes par lot de 5.


## Mes Notes

Le travail est découpé en tranches verticales décrites dans la [feuille de route](docs/superpowers/roadmap.md).
Chaque tranche a sa spécification (`docs/superpowers/specs/`) et son plan d'implémentation
(`docs/superpowers/plans/`).

### Tranche 1 — Fondations

Réalisé :

- **Clean Architecture** : le backend est découpé en `Domain`, `Application`, `Infrastructure` et `WebApi` (seul
  point de composition). La règle de dépendance (`Domain ← Application ← Infrastructure`) est vérifiée par des
  tests d'architecture. Les règles métier restent isolées et testables unitairement.
- **CQRS léger, sans médiateur** : un handler par cas d'usage en écriture (à partir de la tranche 2) ; en
  lecture, des projections EF Core `AsNoTracking`, servies depuis la tranche 2 par un « store » par
  fonctionnalité (voir plus bas) plutôt que par des interfaces de requêtes. MediatR est devenu payant et
  n'apporte rien à cette taille de projet.
- **Listes paginées** côté API : `GET /v1/users?page=1&pageSize=20` (100 au plus) renvoie
  `{ items, page, pageSize, totalCount }`.
- **Versionnement d'API** par segment d'URL (`/v1/users`) avec `Asp.Versioning` : visible dans les URL et dans
  la documentation. Une version inconnue répond `404`.
- **OpenAPI + Scalar** en développement uniquement : un document par version, et un lien « Scalar » sur la
  ressource `webapi` du dashboard Aspire.
- **Contrat d'erreur unique** (RFC 9457 ProblemDetails) :
  - `400` pour la validation, celle de la requête comme celle du domaine (`errors` indexé par champ) ;
  - `404` pour une ressource introuvable ;
  - `422` pour une règle métier violée, avec un `code` stable que le front traduit en français ;
  - `500` sans détail technique hors développement.
- **Redis supprimé** : il n'était pas utilisé et aurait imposé un conteneur de plus à chaque niveau de tests.
- **Front** : la page d'accueil Nx est remplacée par une coquille en français (locale `fr-FR`), avec la liste
  des utilisateurs (message d'erreur si l'API ne répond pas). Une URL inconnue ramène à cette liste.
- **CI** : build et tests unitaires sur Ubuntu, Windows et macOS ; les tests qui ont besoin de Docker
  (acceptation, bout en bout) tournent sur Ubuntu uniquement.

### Tranche 2 — Utilisateurs

Réalisé :

- **Modèle complet** : nom, prénom, adresse postale (rue, code postal à 5 chiffres, ville), quota mensuel de
  dépenses, statut actif/inactif et suppression logique. Les règles sont portées par l'entité `User` et l'objet
  valeur `Address`, testés unitairement.
- **API** sous `/v1/users` : liste paginée, détail, création (`201` + `Location`), modification (`PUT`),
  activation et désactivation (`POST …/activate`, `…/deactivate`, sans erreur si le statut est déjà le bon),
  suppression logique (`DELETE`). Un utilisateur supprimé disparaît partout : toute requête le concernant répond
  `404`.
- **Concurrence optimiste** : le détail d'un utilisateur porte sa `version` (la colonne système `xmin` de
  Postgres), aussi renvoyée en `ETag`. Un `PUT` avec `If-Match` est refusé en `412` si l'utilisateur a changé
  depuis sa lecture ; le formulaire l'envoie toujours. Toute écriture enregistrée par-dessus une modification
  concurrente répond `409`.
- **Migration des données existantes** : les utilisateurs créés avant la tranche reçoivent une adresse à
  compléter, le statut actif et un quota de 10, au lieu de valeurs que le domaine refuse.
- **Accès aux données** : un « store » par fonctionnalité (`IUserStore` dans `Application`, implémenté sur le
  `DbContext` dans `Infrastructure`) sert les lectures et les écritures. Pas de repository ni d'`IUnitOfWork` :
  le `DbContext` est déjà une unité de travail. Un handler par cas d'usage en écriture.
- **Front** : liste paginée (statut, activation, suppression avec confirmation) et formulaire de création et de
  modification, avec des messages de validation en français.

Hypothèses (le sujet ne précise pas) :

- Le quota mensuel est un entier supérieur ou égal à 1, sans plafond. Il reste modifiable ; le baisser sous le
  nombre de dépenses déjà saisies dans le mois ne touche pas l'existant et ne bloque que les nouvelles dépenses.
- Longueurs maximales : 100 caractères pour le nom et le prénom, 200 pour la rue, 100 pour la ville. Elles sont
  comptées avant le retrait des espaces en début et en fin, qui est ensuite appliqué.
- Un utilisateur inactif reste visible et modifiable ; seul son rattachement à une nouvelle note de frais sera
  refusé (tranche 3).
- Un utilisateur supprimé ne peut pas être restauré.

### Tranche 3 — Notes de frais

Réalisé :

- **Modèle** : une note de frais (`ExpenseReport`) appartient à un utilisateur et couvre un mois (`ReportMonth`, de
  janvier 2000 à décembre 2100). Son intitulé, « Prénom Nom - Mois Année » (par ex. « Juste Leblanc - Octobre
  2025 »), est composé et enregistré à la création, puis ne change plus. Les noms de mois sont écrits en dur, avec
  leur majuscule, pour ne dépendre ni de la culture `fr-FR` ni de la présence d'ICU sur le serveur.
- **Règles métier**, premières réponses `422` (avec un `code` que le front traduit) : seul un utilisateur actif peut
  être affecté à une note de frais (`expense_report.inactive_user`) ; un utilisateur a au plus une note de frais par
  mois (`expense_report.already_exists`). Cette seconde règle est vérifiée avant l'enregistrement et garantie par un
  index unique : deux créations simultanées donnent une note et un `422`, jamais une erreur `500`. La première règle
  tient aussi face à une désactivation ou une suppression simultanée : la création verrouille la ligne de
  l'utilisateur (`SELECT … FOR SHARE`, dans la transaction de l'enregistrement) ; la modification concurrente attend
  la fin de la création, ou la création voit la modification déjà enregistrée.
- **Erreurs d'enregistrement** : `AppDbContext.SaveChangesAsync` traduit seul les erreurs de la base (`409` pour un
  utilisateur modifié entre-temps, `404` pour une note déjà supprimée, `422` pour une note en double), quel que soit
  le store par lequel le handler enregistre.
- **API** sous `/v1/expense-reports` : liste paginée (le mois le plus récent d'abord), détail, création (`201` +
  `Location`) et suppression physique (`DELETE` ; `404` si la note a déjà été supprimée, y compris par une requête
  simultanée). `GET /v1/users` accepte un filtre `isActive`. `GET /v1/users/active` lit les utilisateurs actifs
  tranche par tranche avec un curseur (`after`, le `next` de la tranche précédente) : contrairement aux pages, une
  activation ou une désactivation entre deux lectures ne fait ni sauter ni répéter un utilisateur.
- **Front** : la page des notes de frais, devenue la page d'accueil, permet d'en créer une en choisissant un
  utilisateur actif et un mois (le mois courant par défaut), puis ouvre la note créée. Elle liste les notes par pages
  de 20, avec suppression après confirmation. La page d'une note affiche son intitulé et son mois et permet de la
  supprimer ; ses dépenses arrivent avec la tranche 4.

Hypothèses (le sujet ne précise pas) :

- Un utilisateur a au plus une note de frais par mois.
- L'intitulé est figé à la création : renommer l'utilisateur ne le change pas.
- Les mois acceptés vont de janvier 2000 à décembre 2100, passés comme à venir.
- Supprimer une note de frais supprimera aussi ses dépenses (tranche 4).
- Les notes de frais d'un utilisateur supprimé restent visibles, avec leur intitulé d'origine.

### Tranche 4 — Dépenses

Réalisé :

- **Modèle** : une dépense (`Expense`) appartient à une note de frais. Elle a une date, une description (50
  caractères au plus), un montant en euros (de 0,01 à 999 999,99 €, deux décimales au plus, jamais arrondi) et une
  adresse de facturation (`BillingAddress` : une enseigne facultative, puis la rue, le code postal et la ville, avec
  les règles de l'adresse d'un utilisateur). Elle reste modifiable et se supprime logiquement.
- **Règles métier** (`422` avec un `code`) : la date tombe dans le mois de la note
  (`expense.date_outside_report_month`) ; une dépense au-delà du quota mensuel de l'utilisateur est refusée
  (`expense.quota_exceeded`) ; la note d'un utilisateur supprimé passe en lecture seule
  (`expense_report.deleted_user`).
- **Concurrence** : chaque écriture d'une dépense verrouille la note (`SELECT … FOR UPDATE`), puis l'utilisateur
  (`FOR SHARE`), jusqu'à l'enregistrement. Deux ajouts simultanés pour la dernière place du quota donnent une
  dépense et un `422`, jamais deux dépenses ; un ajout voit la suppression de l'utilisateur faite en même temps. Une
  modification envoie la version lue (`If-Match`) : `412` si la dépense a changé entre-temps.
- **API** sous `/v1/expense-reports/{reportId}/expenses` : liste paginée (par date), détail (avec `ETag`), ajout
  (`201` + `Location`), modification (`PUT`) et suppression logique (`DELETE`). Une dépense n'est trouvée que par sa
  propre note. La liste et le détail des notes donnent le nombre de dépenses et leur total ; le détail donne aussi
  le quota de l'utilisateur et indique si la note est en lecture seule. Supprimer une note supprime ses dépenses
  (cascade en base).
- **Front** : la page d'une note affiche le nombre de dépenses au regard du quota (« 3 dépenses sur 10 ce
  mois-ci ») et leur total, un formulaire d'ajout qui sert aussi à la modification, et les dépenses par lots de 5,
  datées au format « mercredi 15 octobre 2025 ». Une note en lecture seule n'affiche ni formulaire ni actions sur
  ses dépenses. La liste des notes affiche le nombre de dépenses et le total de chacune.

Hypothèses (le sujet ne précise pas) :

- La date d'une dépense tombe dans le mois de sa note : le quota mensuel se compte donc sur la note.
- Le quota compte les dépenses non supprimées : supprimer une dépense libère une place. Une modification ne le
  vérifie pas : elle ne peut pas changer de mois. Le quota appliqué est celui de l'utilisateur au moment de l'ajout.
- La note d'un utilisateur inactif reste modifiable ; celle d'un utilisateur supprimé passe en lecture seule (elle
  peut toujours être supprimée).
- Le montant va de 0,01 à 999 999,99 €, avec deux décimales au plus ; l'enseigne est facultative (100 caractères
  au plus).
- « Par lot de 5 » : des pages numérotées de 5 dépenses, de la plus ancienne à la plus récente.

### Lancer les tests

| Niveau                                      | Commande                                                   | Prérequis       |
|---------------------------------------------|------------------------------------------------------------|-----------------|
| Unitaires (xUnit v3, Shouldly, NSubstitute) | `dotnet test --project backend/tests/UnitTests`            | —               |
| Acceptation (Reqnroll, Testcontainers)      | `dotnet test --project backend/tests/AcceptanceTests`      | Docker          |
| Bout en bout (Aspire, Playwright)           | `dotnet test --project backend/tests/EndToEndTests`        | Docker, Node.js |
| Front (Vitest)                              | `cd frontend && npx nx run-many -t lint test build`        | Node.js         |

- Les tests de bout en bout installent Chromium automatiquement au premier lancement (dans
  `~/.cache/ms-playwright`). Sous Linux, s'il manque des bibliothèques système :
  `pwsh backend/tests/EndToEndTests/bin/Debug/net10.0/playwright.ps1 install-deps chromium`.
- En cas d'échec, une trace et une capture d'écran sont écrites dans
  `backend/tests/EndToEndTests/bin/<Configuration>/net10.0/playwright-artifacts/`.
- Couverture de code : ajouter `--coverage --coverage-output-format cobertura --results-directory artifacts/coverage`.

### Pistes d'amélioration

- Ajouter l'authentification et l'autorisation : l'API et l'interface sont ouvertes, sans connexion. Le sujet décrit un
  administrateur unique et ne prévoit pas de connexion.
- Générer le client TypeScript à partir du document OpenAPI, au lieu de maintenir les interfaces à la main.
- Passer le front en détection de changements sans zone.js (zoneless).
- Supprimer le script `prestart: npm install` du front : Aspire 13.6 installe déjà les dépendances avant de
  lancer l'application.
- Ajouter un health check Aspire sur `webapi`. Il sonde l'endpoint https, et échoue donc tant que le certificat
  de développement ASP.NET Core n'est pas approuvé (c'est le cas en CI) : le front attendrait indéfiniment.
- Extraire la barre de pagination (*Précédent* / *Suivant*, « Page X sur Y ») dans un composant partagé : elle est
  répétée sur les trois listes (utilisateurs, notes de frais, dépenses).
