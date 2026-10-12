using System.Diagnostics.CodeAnalysis;
using Domain.Entities;
using Domain.ValueObjects;

namespace Infrastructure.Persistence;

/// <summary>
/// What a fresh development database starts with: users, their expense reports over the twelve months up to today, and
/// the expenses of those reports. Built through the domain's factories, so it follows every rule; the same day always
/// gives the same data.
/// </summary>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = RandomJustification)]
[SuppressMessage("Security", "S2245:Using pseudorandom number generators is security-sensitive",
    Justification = RandomJustification)]
internal sealed record DemoData(
    IReadOnlyList<User> Users,
    IReadOnlyList<ExpenseReport> ExpenseReports,
    IReadOnlyList<Expense> Expenses)
{
    private const string RandomJustification =
        "The random numbers only spread demo data; a fixed seed makes it the same from one database to the next.";

    /// <summary>Fixed, so that two fresh databases created the same day hold the same data.</summary>
    private const int RandomSeed = 2026;

    /// <summary>The chance, in percent, that a user has no report for a past month.</summary>
    private const int SkippedMonthChance = 15;

    /// <summary>An inactive user's reports stop that many months before the current one.</summary>
    private const int MonthsSinceDeactivation = 2;

    private static readonly DemoUser[] DemoUsers =
    [
        new("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 10, 12, ThisMonth.FullReport),
        new("Marc", "Assin", new Address("3 place Bellecour", "69002", "Lyon"), 10, 12, ThisMonth.Report),
        new("Camille", "Martin", new Address("8 rue Sainte-Catherine", "33000", "Bordeaux"), 5, 12, ThisMonth.Report),
        new("Léa", "Bernard", new Address("21 rue Nationale", "59000", "Lille"), 8, 11, ThisMonth.Report),
        new("Hugo", "Dubois", new Address("4 place Kléber", "67000", "Strasbourg"), 3, 10, ThisMonth.FullReport),
        new("Chloé", "Thomas", new Address("15 rue Crébillon", "44000", "Nantes"), 5, 12, ThisMonth.NoReport),
        new("Lucas", "Robert", new Address("30 allée Jean Jaurès", "31000", "Toulouse"), 15, 9, ThisMonth.Report),
        new("Manon", "Richard", new Address("9 place de la Comédie", "34000", "Montpellier"), 10, 12,
            ThisMonth.EmptyReport),
        new("Nathan", "Petit", new Address("52 avenue Jean Médecin", "06000", "Nice"), 5, 8, ThisMonth.Report),
        new("Inès", "Durand", new Address("11 rue Félix Poulat", "38000", "Grenoble"), 8, 12, ThisMonth.Inactive),
        new("Théo", "Moreau", new Address("6 rue de la Liberté", "21000", "Dijon"), 3, 7, ThisMonth.Report),
        new("Jade", "Laurent", new Address("25 rue d'Antrain", "35000", "Rennes"), 10, 12, ThisMonth.NoReport),
        new("Louis", "Simon", new Address("40 rue de la République", "13001", "Marseille"), 20, 12, ThisMonth.Report),
        new("Emma", "Michel", new Address("2 boulevard du Roi René", "49100", "Angers"), 5, 6, ThisMonth.Report),
        new("Gabriel", "Lefebvre", new Address("14 rue Nationale", "37000", "Tours"), 8, 12, ThisMonth.NoReport),
        new("Louise", "Leroy", new Address("7 place de Jaude", "63000", "Clermont-Ferrand"), 10, 5,
            ThisMonth.NoReport),
        new("Raphaël", "Roux", new Address("19 rue de Vesle", "51100", "Reims"), 5, 12, ThisMonth.Inactive),
        new("Alice", "David", new Address("33 rue du Faubourg Saint-Antoine", "75011", "Paris"), 15, 12,
            ThisMonth.Report),
        new("Arthur", "Bertrand", new Address("5 cours Mirabeau", "13100", "Aix-en-Provence"), 3, 4,
            ThisMonth.Report),
        new("Lina", "Morel", new Address("28 rue Saint-Ferréol", "13001", "Marseille"), 8, 12, ThisMonth.NoReport),
        new("Jules", "Fournier", new Address("16 quai de la Fosse", "44000", "Nantes"), 10, 10, ThisMonth.Report),
        new("Rose", "Girard", new Address("45 rue Paradis", "13006", "Marseille"), 5, 3, ThisMonth.EmptyReport),
        new("Adam", "Bonnet", new Address("10 rue Mercière", "69002", "Lyon"), 10, 12, ThisMonth.Report),
        new("Zoé", "Dupont", new Address("22 rue des Carmes", "31000", "Toulouse"), 8, 9, ThisMonth.NoReport),
        new("Paul", "Lambert", new Address("13 rue Esquermoise", "59800", "Lille"), 5, 12, ThisMonth.Inactive),
        new("Ambre", "Fontaine", new Address("3 rue du Vieux-Marché-aux-Poissons", "67000", "Strasbourg"), 10, 2,
            ThisMonth.Report),
        new("Noah", "Rousseau", new Address("60 rue de Sèze", "69006", "Lyon"), 20, 12, ThisMonth.Report),
        new("Mila", "Vincent", new Address("8 rue Sainte-Croix", "33800", "Bordeaux"), 3, 1, ThisMonth.Report),
        new("Victor", "Muller", new Address("18 Grand'Rue", "67000", "Strasbourg"), 8, 12, ThisMonth.NoReport),
        new("Sarah", "Faure", new Address("27 rue de Metz", "31000", "Toulouse"), 10, 11, ThisMonth.Report),
    ];

    private static readonly ExpenseKind[] ExpenseKinds =
    [
        new("Billet de train Paris - Lyon", "SNCF", new Address("Place Louis-Armand", "75012", "Paris"), 45m, 140m),
        new("Billet de train Lyon - Marseille", "SNCF", new Address("Place Charles Béraudier", "69003", "Lyon"), 35m,
            95m),
        new("Billet d'avion Paris - Toulouse", "Air France", new Address("Aéroport d'Orly", "94390", "Orly"), 90m,
            260m),
        new("Taxi depuis la gare", "G7", new Address("Place Napoléon III", "75010", "Paris"), 15m, 45m),
        new("Location de voiture", "Europcar", new Address("Place Charles Béraudier", "69003", "Lyon"), 60m, 210m),
        new("Plein de carburant", "TotalEnergies", new Address("120 avenue Jean Jaurès", "69007", "Lyon"), 55m, 95m),
        new("Péage autoroute A7", "Vinci Autoroutes", new Address("Barrière de péage de Vienne", "38200", "Vienne"),
            8.5m, 32m),
        new("Parking de l'aéroport", "Parking Orly P1", new Address("Aéroport d'Orly", "94390", "Orly"), 18m, 75m),
        new("Tickets de métro", "RATP", new Address("54 quai de la Rapée", "75012", "Paris"), 2.15m, 17.35m),
        new("Nuit d'hôtel", "Ibis Lyon Part-Dieu", new Address("78 rue de la Villette", "69003", "Lyon"), 75m, 140m),
        new("Nuit d'hôtel", "Novotel Bordeaux Centre",
            new Address("45 cours du Maréchal Juin", "33000", "Bordeaux"), 95m, 180m),
        new("Déjeuner client", "Brasserie Georges", new Address("30 cours de Verdun", "69002", "Lyon"), 35m, 120m),
        new("Dîner d'équipe", "Bouillon Chartier", new Address("7 rue du Faubourg Montmartre", "75009", "Paris"),
            60m, 240m),
        new("Repas sur la route", null, new Address("Aire de Montélimar Est", "26200", "Montélimar"), 12m, 28m),
        new("Café avec un partenaire", null, new Address("2 place de la Comédie", "34000", "Montpellier"), 4.5m,
            14m),
        new("Fournitures de bureau", "Bureau Vallée", new Address("23 avenue de Lyon", "31500", "Toulouse"), 8m, 60m),
        new("Câble HDMI et adaptateur", "Fnac", new Address("Forum des Halles", "75001", "Paris"), 15m, 45m),
        new("Livre technique", "Fnac", new Address("Forum des Halles", "75001", "Paris"), 25m, 55m),
        new("Inscription à une conférence", "Devoxx France",
            new Address("2 place de la Porte Maillot", "75017", "Paris"), 150m, 450m),
    ];

    public static DemoData Create(DateOnly today)
    {
        var random = new Random(RandomSeed);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        List<User> users = [];
        List<ExpenseReport> reports = [];
        List<Expense> expenses = [];

        foreach (var demoUser in DemoUsers)
        {
            var user = User.Create(demoUser.FirstName, demoUser.LastName, demoUser.Address,
                demoUser.MonthlyExpenseQuota);
            users.Add(user);

            for (var monthsAgo = demoUser.MonthsOfReports - 1; monthsAgo >= 0; monthsAgo--)
            {
                if (demoUser.ExpenseCount(monthsAgo, random) is not { } expenseCount)
                {
                    continue;
                }

                var firstDay = currentMonth.AddMonths(-monthsAgo);
                var report = ExpenseReport.Create(user, new ReportMonth(firstDay.Year, firstDay.Month));
                reports.Add(report);

                // Nothing after today: the current month's expenses all happened already.
                var dayCount = monthsAgo == 0 ? today.Day : DateTime.DaysInMonth(firstDay.Year, firstDay.Month);
                for (var liveExpenseCount = 0; liveExpenseCount < expenseCount; liveExpenseCount++)
                {
                    var kind = ExpenseKinds[random.Next(ExpenseKinds.Length)];
                    expenses.Add(Expense.Create(report, user, liveExpenseCount,
                        firstDay.AddDays(random.Next(dayCount)), kind.Description, kind.Amount(random),
                        kind.BillingAddress));
                }
            }

            // After the reports: an inactive user cannot get one.
            if (demoUser.ThisMonth == ThisMonth.Inactive)
            {
                user.Deactivate();
            }
        }

        return new DemoData(users, reports, expenses);
    }

    /// <summary>What a demo user has for the current month.</summary>
    private enum ThisMonth
    {
        /// <summary>A report with some expenses, up to the quota.</summary>
        Report = 0,

        /// <summary>No report yet: the user can be picked to create one.</summary>
        NoReport = 1,

        EmptyReport = 2,

        /// <summary>A report holding the user's whole quota: no expense can be added.</summary>
        FullReport = 3,

        /// <summary>No report since <see cref="MonthsSinceDeactivation" /> months, then deactivated.</summary>
        Inactive = 4,
    }

    /// <param name="MonthsOfReports">How many months, up to the current one, the user has had reports for.</param>
    private sealed record DemoUser(
        string FirstName,
        string LastName,
        Address Address,
        int MonthlyExpenseQuota,
        int MonthsOfReports,
        ThisMonth ThisMonth)
    {
        /// <summary>The expenses of the user's report for that month, or null for no report.</summary>
        public int? ExpenseCount(int monthsAgo, Random random)
        {
            if (monthsAgo == 0)
            {
                return ThisMonth switch
                {
                    ThisMonth.Report => random.Next(1, MonthlyExpenseQuota + 1),
                    ThisMonth.EmptyReport => 0,
                    ThisMonth.FullReport => MonthlyExpenseQuota,
                    _ => null,
                };
            }

            var isFirstMonth = monthsAgo == MonthsOfReports - 1;
            if ((ThisMonth == ThisMonth.Inactive && monthsAgo < MonthsSinceDeactivation)
                || (!isFirstMonth && random.Next(100) < SkippedMonthChance))
            {
                return null;
            }

            return random.Next(1, MonthlyExpenseQuota + 1);
        }
    }

    /// <summary>An expense a demo user may have, at an amount drawn between two bounds.</summary>
    private sealed record ExpenseKind(
        string Description,
        string? StoreName,
        Address Address,
        decimal MinAmount,
        decimal MaxAmount)
    {
        public BillingAddress BillingAddress { get; } = new(StoreName, Address);

        public decimal Amount(Random random)
        {
            return decimal.Round(MinAmount + ((MaxAmount - MinAmount) * (decimal)random.NextDouble()), 2);
        }
    }
}
