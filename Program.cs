using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EfCorePoc.Data;
using EfCorePoc.Models;
using EfCorePoc.Services;

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║     Entity Framework Core - POC Demo            ║");
Console.WriteLine("║     In-Memory Provider | .NET 10 | EF Core 10   ║");
Console.WriteLine("║     3-level hierarchy: Author -> Book -> Review  ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝\n");

// ── Configure DbContext with InMemory + Lazy Loading Proxies ──
var options = new DbContextOptionsBuilder<BookStoreContext>()
    .UseInMemoryDatabase("BookStoreDb")
    .UseLazyLoadingProxies()
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
    .LogTo(msg => { }, Microsoft.Extensions.Logging.LogLevel.Warning) // Suppress info logs
    .Options;

using var db = new BookStoreContext(options);

// ── Ensure seed data is loaded ──
db.Database.EnsureCreated();

// Seed M:N relationships (can't be done via HasData for join tables)
SeedManyToMany(db);

Console.WriteLine($"Seed data: {db.Authors.Count()} authors, {db.Books.Count()} books, " +
                  $"{db.Reviews.Count()} reviews, {db.Tags.Count()} tags\n");

// ── Run all demo sections ──
CrudDemos.RunAll(db);
QueryDemos.RunAll(db);
LoadingDemos.RunAll(db);
TransactionDemos.RunAll(db);

// ── Lazy vs Eager: side-by-side comparison (separate DbContexts) ──
LazyVsEagerTests.TestLazyLoading();
LazyVsEagerTests.TestEagerLoading();

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║     All demos completed successfully!            ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");

// ── Helper: seed M:N join table ──
static void SeedManyToMany(BookStoreContext db)
{
    if (db.Books.Include(b => b.Tags).Any(b => b.Tags.Count > 0))
        return; // Already seeded

    var books = db.Books.ToList();
    var tags = db.Tags.ToList();

    var fantasy = tags.First(t => t.Name == "Fantasy");
    var sciFi = tags.First(t => t.Name == "Sci-Fi");
    var classic = tags.First(t => t.Name == "Classic");
    var adventure = tags.First(t => t.Name == "Adventure");
    var robots = tags.First(t => t.Name == "Robots");

    // Lord of the Rings: Fantasy, Classic, Adventure
    var lotr = books.First(b => b.Title.Contains("Lord"));
    lotr.Tags.Add(fantasy); lotr.Tags.Add(classic); lotr.Tags.Add(adventure);

    // The Hobbit: Fantasy, Adventure
    var hobbit = books.First(b => b.Title.Contains("Hobbit"));
    hobbit.Tags.Add(fantasy); hobbit.Tags.Add(adventure);

    // Foundation: Sci-Fi, Classic
    var foundation = books.First(b => b.Title == "Foundation");
    foundation.Tags.Add(sciFi); foundation.Tags.Add(classic);

    // I, Robot: Sci-Fi, Robots
    var iRobot = books.First(b => b.Title == "I, Robot");
    iRobot.Tags.Add(sciFi); iRobot.Tags.Add(robots);

    // Dune: Sci-Fi, Classic, Adventure
    var dune = books.First(b => b.Title == "Dune");
    dune.Tags.Add(sciFi); dune.Tags.Add(classic); dune.Tags.Add(adventure);

    db.SaveChanges();
}
