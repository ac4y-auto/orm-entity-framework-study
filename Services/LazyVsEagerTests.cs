using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EfCorePoc.Data;
using EfCorePoc.Models;

namespace EfCorePoc.Services;

public static class LazyVsEagerTests
{
    /// <summary>
    /// Test 1: LAZY LOADING
    /// - UseLazyLoadingProxies() enabled
    /// - No Include() calls
    /// - Navigation properties load automatically when accessed
    /// - Each access = separate DB round-trip (N+1 problem)
    /// </summary>
    public static void TestLazyLoading()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║  TEST 1: LAZY LOADING                           ║");
        Console.WriteLine("║  Navigation properties load on first access     ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝\n");

        // Fresh DbContext WITH lazy loading proxies
        var options = new DbContextOptionsBuilder<BookStoreContext>()
            .UseInMemoryDatabase("LazyTestDb")
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var db = new BookStoreContext(options);
        db.Database.EnsureCreated();
        SeedTestData(db);

        int queryCount = 0;

        // ── Step 1: Load only Authors (no Include!) ──
        queryCount++;
        var authors = db.Authors.ToList();
        Console.WriteLine($"  Query #{queryCount}: db.Authors.ToList()");
        Console.WriteLine($"  Result: {authors.Count} authors loaded");
        Console.WriteLine($"  Books loaded? Let's check...\n");

        // ── Step 2: Access Books -> triggers lazy load per author ──
        foreach (var author in authors)
        {
            queryCount++;
            // This ACCESS triggers a lazy load query behind the scenes!
            var bookCount = author.Books.Count;
            Console.WriteLine($"  Query #{queryCount}: author.Books.Count (LAZY LOAD triggered for '{author.Name}')");
            Console.WriteLine($"  Result: {bookCount} books\n");

            // ── Step 3: Access Reviews per book -> another lazy load ──
            foreach (var book in author.Books)
            {
                queryCount++;
                // This ACCESS triggers yet another lazy load!
                var reviewCount = book.Reviews.Count;
                Console.WriteLine($"  Query #{queryCount}: book.Reviews.Count (LAZY LOAD for '{book.Title}')");
                Console.WriteLine($"  Result: {reviewCount} reviews");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"  TOTAL queries: {queryCount}");
        Console.WriteLine("  PROBLEM: N+1 queries! 1 for authors + N for books + M for reviews");
        Console.WriteLine("  With a real DB, each would be a network round-trip!\n");
    }

    /// <summary>
    /// Test 2: EAGER LOADING
    /// - NO lazy loading proxies
    /// - Include() / ThenInclude() loads everything in one query
    /// - Without Include(), navigation properties stay EMPTY
    /// </summary>
    public static void TestEagerLoading()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║  TEST 2: EAGER LOADING                          ║");
        Console.WriteLine("║  Include() loads all data upfront in one query  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝\n");

        // Fresh DbContext WITHOUT lazy loading proxies!
        var options = new DbContextOptionsBuilder<BookStoreContext>()
            .UseInMemoryDatabase("EagerTestDb")
            // NO .UseLazyLoadingProxies() !!
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var db = new BookStoreContext(options);
        db.Database.EnsureCreated();
        SeedTestData(db);

        // ── Part A: WITHOUT Include -> navigation properties are EMPTY ──
        Console.WriteLine("  ── Part A: WITHOUT Include() ──\n");

        var authorsNoInclude = db.Authors.ToList();
        Console.WriteLine($"  Query: db.Authors.ToList()");
        Console.WriteLine($"  Result: {authorsNoInclude.Count} authors loaded");

        foreach (var author in authorsNoInclude)
        {
            Console.WriteLine($"  '{author.Name}'.Books.Count = {author.Books.Count}  <-- EMPTY! Not loaded!");
        }

        Console.WriteLine("\n  Without lazy loading, navigation properties stay empty");
        Console.WriteLine("  unless you explicitly Include() them.\n");

        // ── Part B: WITH Include -> everything loaded in ONE query ──
        Console.WriteLine("  ── Part B: WITH Include() + ThenInclude() ──\n");

        int queryCount = 0;

        queryCount++;
        var authorsWithAll = db.Authors
            .Include(a => a.Books)                  // 2nd level
                .ThenInclude(b => b.Reviews)        // 3rd level
            .ToList();
        Console.WriteLine($"  Query #{queryCount}: db.Authors");
        Console.WriteLine($"           .Include(a => a.Books)");
        Console.WriteLine($"               .ThenInclude(b => b.Reviews)");
        Console.WriteLine($"           .ToList()");
        Console.WriteLine($"  Result: ALL 3 levels loaded in ONE query!\n");

        // No more queries needed - everything is already in memory
        foreach (var author in authorsWithAll)
        {
            Console.WriteLine($"  Author: {author.Name}");
            foreach (var book in author.Books)
            {
                Console.WriteLine($"    Book: '{book.Title}' ({book.Reviews.Count} reviews)");
                foreach (var review in book.Reviews)
                    Console.WriteLine($"      Review: {review.ReviewerName} - {review.Rating}/5");
            }
        }

        Console.WriteLine($"\n  TOTAL queries: {queryCount}");
        Console.WriteLine("  BENEFIT: Single query loads the entire 3-level tree!");
        Console.WriteLine("  TRADEOFF: May load more data than you actually need.\n");
    }

    private static void SeedTestData(BookStoreContext db)
    {
        if (db.Authors.Any()) return;

        var tolkien = new Author { Name = "Tolkien", Bio = "Fantasy master", BirthDate = new DateTime(1892, 1, 3) };
        var asimov = new Author { Name = "Asimov", Bio = "Sci-fi legend", BirthDate = new DateTime(1920, 1, 2) };

        db.Authors.AddRange(tolkien, asimov);
        db.SaveChanges();

        var lotr = new Book { Title = "The Lord of the Rings", Price = 29.99m, PublishedDate = new DateTime(1954, 7, 29), AuthorId = tolkien.Id };
        var hobbit = new Book { Title = "The Hobbit", Price = 14.99m, PublishedDate = new DateTime(1937, 9, 21), AuthorId = tolkien.Id };
        var foundation = new Book { Title = "Foundation", Price = 17.99m, PublishedDate = new DateTime(1951, 6, 1), AuthorId = asimov.Id };

        db.Books.AddRange(lotr, hobbit, foundation);
        db.SaveChanges();

        db.Reviews.AddRange(
            new Review { ReviewerName = "Alice", Rating = 5, Comment = "Masterpiece!", BookId = lotr.Id },
            new Review { ReviewerName = "Bob", Rating = 4, Comment = "Epic but long", BookId = lotr.Id },
            new Review { ReviewerName = "Charlie", Rating = 5, Comment = "Charming", BookId = hobbit.Id },
            new Review { ReviewerName = "Diana", Rating = 5, Comment = "Mind-blowing", BookId = foundation.Id }
        );
        db.SaveChanges();
    }
}
