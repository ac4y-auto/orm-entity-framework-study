using Microsoft.EntityFrameworkCore;
using EfCorePoc.Data;

namespace EfCorePoc.Services;

public static class LoadingDemos
{
    public static void RunAll(BookStoreContext db)
    {
        Console.WriteLine("=== LOADING STRATEGY DEMOS ===\n");

        DemoEagerLoading(db);
        DemoExplicitLoading(db);
        DemoLazyLoading(db);
        DemoSplitQuery(db);
        DemoFilteredInclude(db);
    }

    static void DemoEagerLoading(BookStoreContext db)
    {
        Console.WriteLine("── Eager Loading (Include/ThenInclude) ──");
        Console.WriteLine("  Loads all related data in a single query.\n");

        // Single level Include
        var authorsWithBooks = db.Authors
            .Include(a => a.Books)
            .ToList();
        Console.WriteLine($"  Single Include: {authorsWithBooks.Count} authors loaded with their books");

        // Multi-level Include (3 levels: Author -> Book -> Review)
        var full = db.Authors
            .Include(a => a.Books)
                .ThenInclude(b => b.Reviews)
            .ToList();
        var totalReviews = full.SelectMany(a => a.Books).SelectMany(b => b.Reviews).Count();
        Console.WriteLine($"  ThenInclude (3 levels): {full.Count} authors, {totalReviews} reviews loaded");

        // Multiple Include chains
        var withTags = db.Authors
            .Include(a => a.Books)
                .ThenInclude(b => b.Reviews)
            .Include(a => a.Books)
                .ThenInclude(b => b.Tags)
            .ToList();
        var totalTags = withTags.SelectMany(a => a.Books).SelectMany(b => b.Tags).Count();
        Console.WriteLine($"  Multiple chains: also loaded {totalTags} book-tag associations\n");
    }

    static void DemoExplicitLoading(BookStoreContext db)
    {
        Console.WriteLine("── Explicit Loading ──");
        Console.WriteLine("  Loads related data on demand using Entry().Collection/Reference.\n");

        // Load author WITHOUT books
        var author = db.Authors.First(a => a.Name == "Tolkien");
        Console.WriteLine($"  Loaded Author: {author.Name} (books not loaded yet: {author.Books.Count})");

        // Explicitly load books
        db.Entry(author).Collection(a => a.Books).Load();
        Console.WriteLine($"  After explicit load: {author.Books.Count} books");

        // Explicitly load reviews for first book
        var firstBook = author.Books.First();
        db.Entry(firstBook).Collection(b => b.Reviews).Load();
        Console.WriteLine($"  Explicitly loaded {firstBook.Reviews.Count} reviews for '{firstBook.Title}'");

        // Explicit load with query (filtered)
        var secondBook = author.Books.Skip(1).First();
        var highRatedReviews = db.Entry(secondBook)
            .Collection(b => b.Reviews)
            .Query()
            .Where(r => r.Rating >= 4)
            .ToList();
        Console.WriteLine($"  Filtered explicit load: {highRatedReviews.Count} high-rated reviews for '{secondBook.Title}'\n");
    }

    static void DemoLazyLoading(BookStoreContext db)
    {
        Console.WriteLine("── Lazy Loading ──");
        Console.WriteLine("  Requires virtual navigation properties + Proxies package.");
        Console.WriteLine("  When enabled, accessing a navigation property triggers a DB query.\n");

        // Lazy loading is enabled via UseLazyLoadingProxies() in DbContext config
        // With InMemory + Proxies, it works if entities have virtual nav properties
        var author = db.Authors.First();
        Console.WriteLine($"  Accessing author.Books (lazy load triggers here)...");
        Console.WriteLine($"  {author.Name} has {author.Books.Count} books");

        if (author.Books.Any())
        {
            var book = author.Books.First();
            Console.WriteLine($"  Accessing book.Reviews (another lazy load)...");
            Console.WriteLine($"  '{book.Title}' has {book.Reviews.Count} reviews");
        }

        Console.WriteLine("  WARNING: Lazy loading can cause N+1 query problem!\n");
    }

    static void DemoSplitQuery(BookStoreContext db)
    {
        Console.WriteLine("── Split Query (concept) ──");
        Console.WriteLine("  AsSplitQuery() splits complex includes into separate SQL queries.");
        Console.WriteLine("  Only available with relational providers (SQL Server, PostgreSQL, etc.).\n");

        // With a real relational DB, you would write:
        //   var singleQuery = db.Authors
        //       .AsSingleQuery()
        //       .Include(a => a.Books).ThenInclude(b => b.Reviews)
        //       .ToList();
        //
        //   var splitQuery = db.Authors
        //       .AsSplitQuery()
        //       .Include(a => a.Books).ThenInclude(b => b.Reviews)
        //       .ToList();

        // Demonstrate the equivalent with InMemory:
        var authors = db.Authors
            .Include(a => a.Books)
                .ThenInclude(b => b.Reviews)
            .ToList();
        Console.WriteLine($"  Loaded {authors.Count} authors with all nested data");
        Console.WriteLine("  AsSingleQuery: one big result set (can cause cartesian explosion)");
        Console.WriteLine("  AsSplitQuery:  separate query per Include level (avoids explosion)\n");
    }

    static void DemoFilteredInclude(BookStoreContext db)
    {
        Console.WriteLine("── Filtered Include ──");
        Console.WriteLine("  Include() supports inline Where, OrderBy, Take.\n");

        // Only include 5-star reviews
        var booksWithTopReviews = db.Books
            .Include(b => b.Reviews.Where(r => r.Rating == 5))
            .ToList();

        foreach (var book in booksWithTopReviews.Where(b => b.Reviews.Any()))
            Console.WriteLine($"  '{book.Title}': {book.Reviews.Count} five-star reviews");

        // Include with ordering and limiting
        var booksWithLatestReview = db.Books
            .Include(b => b.Reviews
                .OrderByDescending(r => r.CreatedAt)
                .Take(1))
            .Where(b => b.Reviews.Any())
            .ToList();

        Console.WriteLine("\n  Latest review per book:");
        foreach (var book in booksWithLatestReview)
        {
            var latest = book.Reviews.FirstOrDefault();
            if (latest != null)
                Console.WriteLine($"    '{book.Title}': {latest.ReviewerName} ({latest.Rating}/5)");
        }

        Console.WriteLine();
    }
}
