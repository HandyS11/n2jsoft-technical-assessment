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

Au premier lancement, la bdd reçoit des données de démonstration datées du jour : 30 utilisateurs (dont 3 inactifs),
leurs notes de frais des douze derniers mois et leurs dépenses.

La bdd est conservée d'un lancement à l'autre dans un volume Docker (`n2jsoft.aspire-<hash>-postgres-data`). Pour
repartir des données de démonstration, supprimer ce volume (`docker volume ls`, puis `docker volume rm`).

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

Toutes les fonctionnalités du sujet sont livrées et testées à quatre niveaux : unitaires (domaine et cas d'usage),
acceptation (API sur une vraie base PostgreSQL), bout en bout (Aspire et Playwright) et front (Vitest). Le tableau
ci-dessous relie chaque exigence à son implémentation et aux tests qui la prouvent. Les tranches qui suivent
détaillent ce qui a été réalisé et les hypothèses retenues là où le sujet ne tranche pas.

Le travail est découpé en tranches verticales décrites dans la [feuille de route](docs/superpowers/roadmap.md).
Chaque tranche a sa spécification (`docs/superpowers/specs/`) et son plan d'implémentation
(`docs/superpowers/plans/`).

### Couverture du sujet

Tests cités : scénarios Gherkin (`backend/tests/AcceptanceTests/Features/`), tests unitaires
(`backend/tests/UnitTests/`), parcours de bout en bout (`backend/tests/EndToEndTests/`) et specs du front.

| Exigence | Implémentation | Tests |
|----------|----------------|-------|
| Un utilisateur est affecté à une note de frais ; une note a un seul utilisateur, un utilisateur plusieurs notes | `ExpenseReport.UserId` ; `POST /v1/expense-reports` | *A user can have reports for several months, and several users for the same month* ; `An_administrator_creates_a_report_for_the_current_month_and_opens_it` |
| Un nombre maximum de dépenses par mois calendaire | `User.MonthlyExpenseQuota` ; règle `expense.quota_exceeded` dans `Expense.Create`, sous verrou | `ExpenseTests.Create_refuses_an_expense_once_the_quota_is_reached` ; *An expense beyond the monthly quota is refused* ; *Two requests adding the last expense the quota allows at the same time add it once* |
| Actif ou inactif ; seuls les actifs sont affectables | `User.Activate` / `Deactivate` ; règle `expense_report.inactive_user` | `ExpenseReportTests.Create_refuses_an_inactive_user` ; *An inactive user cannot be assigned a report* ; *A user deactivated while a report is created for them is not assigned it* ; `An_administrator_deactivates_then_deletes_a_user` |
| Nom, prénom et adresse postale | `User`, `Address` | `UserTests`, `AddressTests` ; `CreateUser.feature` ; `An_administrator_creates_a_user_and_finds_them_in_the_list` |
| Suppression logique d'un utilisateur | `User.Delete` (`DeletedAt`), filtre global des requêtes | `UserTests.Delete_records_when_the_user_was_deleted` ; *A deleted user leaves the list but stays stored* |
| Date d'une dépense au format « mercredi 15 octobre 2025 » | `DateOnly` ISO dans l'API ; format `EEEE d MMMM y` en `fr-FR` dans la page | `app.config.spec.ts` ; `expense-report.page.spec.ts` (*lists the first 5 expenses of the report*) ; `An_administrator_adds_an_expense_and_sees_its_french_date` |
| Description de 50 caractères au plus | `Expense.DescriptionMaxLength` ; formulaire | `ExpenseTests.Create_accepts_a_description_of_50_characters`, `Create_rejects_a_blank_or_too_long_description` ; *counts the characters of the description and refuses more than 50* |
| Montant en euros | `Expense.Amount` (`numeric(9,2)`, jamais arrondi) ; affichage en euros | `ExpenseTests.Create_rejects_an_amount_out_of_range_or_with_more_than_two_decimals` ; `An_administrator_edits_the_amount_of_an_expense` |
| Adresse de facturation (enseigne / rue + code postal + ville) | `BillingAddress` (enseigne facultative + `Address`) | `BillingAddressTests` ; *The store name is optional* |
| Dépense éditable en tout temps | `PUT /v1/expense-reports/{reportId}/expenses/{id}`, `Expense.Update` | *Updating an expense replaces its details, even at the quota* ; `An_administrator_edits_the_amount_of_an_expense` |
| Quota atteint : message d'erreur, création refusée | `422` `expense.quota_exceeded`, traduit par le front | `expense-form.component.spec.ts` (message par code) ; `An_administrator_is_told_when_the_monthly_quota_is_reached` |
| Suppression logique d'une dépense | `Expense.Delete` | *A deleted expense disappears but stays stored* ; `Deleting_an_expense_frees_a_slot_of_the_quota` |
| Une note contient les dépenses d'un utilisateur au mois | `ReportMonth` ; une note par utilisateur et par mois ; règle `expense.date_outside_report_month` | *A second report for the same user and month is refused* ; *A date outside the report's month is refused* |
| Intitulé « Utilisateur - Mois Année » | `ExpenseReport.Create` | `ExpenseReportTests.Create_titles_the_report_with_the_user_name_then_the_month` ; *Creating an expense report answers with the titled report and where to find it* |
| Intitulé non éditable | enregistré à la création ; aucune route de modification | `ExpenseReportTests.The_title_does_not_follow_a_later_rename_of_the_user` ; *Renaming the user does not change the title of their reports* |
| Suppression physique d'une note | `DELETE /v1/expense-reports/{id}`, cascade sur ses dépenses | *A deleted expense report is gone for good* ; *Deleting a report deletes its expenses, deleted ones included* ; `An_administrator_deletes_a_report_from_the_list` |
| Page des notes : ajout en choisissant le mois et l'utilisateur | `expense-report-list.page.ts` | *creates the report for the chosen user and month, then opens it* ; `An_administrator_creates_a_report_for_the_current_month_and_opens_it` |
| Page d'une note : ajout de dépenses | `expense-report.page.ts`, `ExpenseFormComponent` | `expense-form.component.spec.ts` ; `An_administrator_adds_an_expense_and_sees_its_french_date` |
| Page d'une note : dépenses par lot de 5 | pages de 5 (`pagedList`, `app-pagination`) | *pages through the expenses 5 at a time* ; *Listing returns the requested page of 5 and the total number of expenses* |

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

### Tranche 5 — Finitions

Réalisé :

- **Observabilité** : la webapi exporte ses journaux, ses traces et ses métriques en OpenTelemetry (OTLP). Sous
  Aspire, le dashboard les affiche : chaque requête HTTP avec ses requêtes SQL (Npgsql), les métriques ASP.NET Core,
  .NET et Npgsql, et des journaux structurés reliés à leur trace. Les sondes `/health` ne sont pas tracées. L'export
  n'a lieu que si `OTEL_EXPORTER_OTLP_ENDPOINT` est défini (Aspire le fournit) : les tests et un `dotnet run` seul
  n'exportent rien, et un collecteur injoignable ne gêne pas l'API. La configuration tient dans la webapi
  (`Telemetry/TelemetryExtensions.cs`), seul point de composition : un projet `ServiceDefaults` ne se justifierait
  qu'avec plusieurs services.
- **Pagination partagée** : la barre *Précédent* / *Suivant* (« Page X sur Y ») des trois listes est un seul
  composant (`app-pagination`).
- **Démarrage** : le script `prestart: npm install` du front est supprimé, Aspire installe déjà les dépendances
  avant de lancer l'application.
- **Premier lancement vérifié** depuis un clone neuf (sans `node_modules`, `bin` ni `obj`) :
  `dotnet run --project tools/N2JSoft.Aspire` démarre la base, l'API et le front, avec les données de démonstration.
- **Certificat de développement** : sans certificat approuvé, le dashboard ne joint pas l'AppHost (« Lost connection
  to the AppHost ») et ne reçoit pas la télémétrie, qui passe en https. L'approuver avec
  `dotnet dev-certs https --trust` (Windows, macOS). Sous Linux, où l'approbation dépend de la distribution, lancer en
  http : `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true dotnet run --project tools/N2JSoft.Aspire --launch-profile http`.

### Tranche 6 — Interface

Le sujet n'évalue pas le design ; cette tranche rend seulement l'interface lisible et confortable, sans bibliothèque de
composants.

Réalisé :

- **Tailwind compilé** : le builder Angular (`@angular/build:application`) ne lit que `postcss.config.json` ou
  `.postcssrc.json`. Le `postcss.config.js` initial était ignoré : aucune classe Tailwind ne s'appliquait. Il est
  remplacé par `.postcssrc.json` ; `autoprefixer` est retiré, Tailwind v4 et Angular préfixent déjà le CSS.
- **Styles communs** dans `styles.css` : champs, boutons (principal, secondaire, suppression), tableaux, alertes et
  pastilles de statut. Les pages posent leurs listes et formulaires dans des cartes, alignent les montants à droite et
  regroupent les champs d'adresse sur une ligne.
- **Deux possibilités de l'API enfin utilisées** : la page des utilisateurs filtre par statut (`GET /v1/users?isActive=`,
  que le front n'appelait plus depuis la lecture des utilisateurs actifs par curseur), et la page d'une note de frais
  mène à son utilisateur (`userId` du détail), sauf s'il a été supprimé.

### Lancer les tests

| Niveau                                      | Commande                                                      | Prérequis       |
|---------------------------------------------|---------------------------------------------------------------|-----------------|
| Unitaires (xUnit v3, Shouldly, NSubstitute) | `dotnet test --project backend/tests/UnitTests`               | —               |
| Acceptation (Reqnroll, Testcontainers)      | `dotnet test --project backend/tests/AcceptanceTests`         | Docker          |
| Bout en bout (Aspire, Playwright)           | `dotnet test --project backend/tests/EndToEndTests`           | Docker, Node.js |
| Front (Vitest)                              | `cd frontend && npm ci && npx nx run-many -t lint test build` | Node.js         |

- Les tests de bout en bout installent Chromium automatiquement au premier lancement (dans
  `~/.cache/ms-playwright`). Sous Linux, s'il manque des bibliothèques système :
  `pwsh backend/tests/EndToEndTests/bin/Debug/net10.0/playwright.ps1 install-deps chromium`.
- En cas d'échec, une trace et une capture d'écran sont écrites dans
  `backend/tests/EndToEndTests/bin/<Configuration>/net10.0/playwright-artifacts/`.
- Couverture de code : ajouter `--coverage --coverage-output-format cobertura --results-directory artifacts/coverage`.

### Pistes d'amélioration

- Ajouter l'authentification et l'autorisation : l'API et l'interface sont ouvertes, sans connexion. Le sujet décrit
  un administrateur unique et ne prévoit pas de connexion. Le faire proprement passerait par un fournisseur OpenID
  Connect (par exemple un conteneur Keycloak dans l'AppHost) dont l'API validerait les jetons JWT, plutôt que par des
  jetons émis par l'API elle-même.
- Générer le client TypeScript à partir du document OpenAPI, au lieu de maintenir les interfaces à la main.
- Passer le front en détection de changements sans zone.js (zoneless).
- Ajouter un health check Aspire sur `webapi`. Il sonde l'endpoint https, et échoue donc tant que le certificat
  de développement ASP.NET Core n'est pas approuvé (c'est le cas en CI) : le front attendrait indéfiniment.
- Internationaliser le front : l'interface est en français, comme le sujet. L'API renvoie déjà des codes de règle
  métier stables (`expense.quota_exceeded`) que le front traduit : une seconde langue n'aurait qu'à fournir ses
  messages.
- Envoyer la télémétrie du navigateur (chargements de page, appels à l'API) au dashboard Aspire, pour suivre une
  action de l'interface jusqu'à la base.
- Ajouter des métriques métier (dépenses ajoutées, refus pour quota atteint) à côté des métriques techniques.
