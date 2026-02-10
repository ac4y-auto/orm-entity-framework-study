using Microsoft.EntityFrameworkCore;
using EfCorePoc.Data;

namespace EfCorePoc.Services;

public static class QueryDemos
{
    public static void RunAll(BookStoreContext db)
    {
        Console.WriteLine("=== LINQ QUERY DEMOS ===\n");

        DemoFiltering(db);
        DemoJoinsAndNavigation(db);
        DemoGrouping(db);
        DemoSubqueries(db);
        DemoPagination(db);
        DemoRawSql(db);
    }

    static void DemoFiltering(BookStoreContext db)
    {
        Console.WriteLine("── Filtering & Ordering ──");

        // Where + OrderBy
        var expensiveBooks = db.Books
            .Where(b => b.Price > 15)
            .OrderByDescending(b => b.Price)
            .Select(b => new { b.Title, b.Price })
            .ToList();

        foreach (var b in expensiveBooks)
            Console.WriteLine($"  {b.Title}: ${b.Price}");

        // Any / All
        var hasHighRated = db.Reviews.Any(r => r.Rating == 5);
        var allAbove2 = db.Reviews.All(r => r.Rating > 2);
        Console.WriteLine($"  Has 5-star review: {hasHighRated}");
        Console.WriteLine($"  All reviews above 2 stars: {allAbove2}\n");
    }

    static void DemoJoinsAndNavigation(BookStoreContext db)
    {
        Console.WriteLine("── Joins & Navigation (3 levels deep) ──");

        // Eager loading: Author -> Books -> Reviews (3 levels!)
        var authorsWithAll = db.Authors
            .Include(a => a.Books)
                .ThenInclude(b => b.Reviews)
            .Include(a => a.Books)
                .ThenInclude(b => b.Tags)
            .ToList();

        foreach (var author in authorsWithAll)
        {
            Console.WriteLine($"  Author: {author.Name}");
            foreach (var book in author.Books)
            {
                var tagStr = book.Tags.Any() ? $" [{string.Join(", ", book.Tags.Select(t => t.Name))}]" : "";
                Console.WriteLine($"    Book: '{book.Title}'{tagStr}");
                foreach (var review in book.Reviews)
                    Console.WriteLine($"      Review: {review.ReviewerName} - {review.Rating}/5 - \"{review.Comment}\"");
            }
        }

        // Explicit join with LINQ query syntax
        var joinResult = (from a in db.Authors
                          join b in db.Books on a.Id equals b.AuthorId
                          join r in db.Reviews on b.Id equals r.BookId
                          where r.Rating >= 4
                          select new { Author = a.Name, Book = b.Title, r.ReviewerName, r.Rating })
                         .ToList();

        Console.WriteLine("\n  Explicit join (4+ star reviews):");
        foreach (var r in joinResult)
            Console.WriteLine($"    {r.Author} - '{r.Book}' - {r.ReviewerName}: {r.Rating}/5");

        Console.WriteLine();
    }

    static void DemoGrouping(BookStoreContext db)
    {
        Console.WriteLine("── Grouping & Aggregation ──");

        // Group by Author, get stats
        var authorStats = db.Books
            .GroupBy(b => b.Author.Name)
            .Select(g => new
            {
                Author = g.Key,
                BookCount = g.Count(),
                AvgPrice = g.Average(b => b.Price),
                TotalReviews = g.Sum(b => b.Reviews.Count)
            })
            .OrderByDescending(x => x.TotalReviews)
            .ToList();

        foreach (var s in authorStats)
            Console.WriteLine($"  {s.Author}: {s.BookCount} books, avg ${s.AvgPrice:F2}, {s.TotalReviews} reviews");

        // Aggregate: average rating per book
        var bookRatings = db.Books
            .Where(b => b.Reviews.Any())
            .Select(b => new
            {
                b.Title,
                AvgRating = b.Reviews.Average(r => r.Rating),
                MinRating = b.Reviews.Min(r => r.Rating),
                MaxRating = b.Reviews.Max(r => r.Rating)
            })
            .OrderByDescending(x => x.AvgRating)
            .ToList();

        Console.WriteLine("\n  Book ratings:");
        foreach (var b in bookRatings)
            Console.WriteLine($"    '{b.Title}': avg={b.AvgRating:F1}, min={b.MinRating}, max={b.MaxRating}");

        Console.WriteLine();
    }

    static void DemoSubqueries(BookStoreContext db)
    {
        Console.WriteLine("── Subqueries ──");

        // Books by authors who have more than 1 book
        var prolificAuthorsBooks = db.Books
            .Where(b => db.Books.Count(b2 => b2.AuthorId == b.AuthorId) > 1)
            .Select(b => new { b.Title, b.Author.Name })
            .ToList();

        Console.WriteLine("  Books by authors with 2+ books:");
        foreach (var b in prolificAuthorsBooks)
            Console.WriteLine($"    {b.Name} - '{b.Title}'");

        // Books with above-average price
        var avgPrice = db.Books.Average(b => b.Price);
        var aboveAvg = db.Books
            .Where(b => b.Price > avgPrice)
            .Select(b => new { b.Title, b.Price })
            .ToList();

        Console.WriteLine($"\n  Books above average price (${avgPrice:F2}):");
        foreach (var b in aboveAvg)
            Console.WriteLine($"    '{b.Title}': ${b.Price}");

        Console.WriteLine();
    }

    static void DemoPagination(BookStoreContext db)
    {
        Console.WriteLine("── Pagination (Skip/Take) ──");

        const int pageSize = 2;
        var totalBooks = db.Books.Count();
        var totalPages = (int)Math.Ceiling((double)totalBooks / pageSize);

        for (int page = 0; page < totalPages; page++)
        {
            var pageBooks = db.Books
                .OrderBy(b => b.Title)
                .Skip(page * pageSize)
                .Take(pageSize)
                .Select(b => b.Title)
                .ToList();

            Console.WriteLine($"  Page {page + 1}: {string.Join(", ", pageBooks)}");
        }

        Console.WriteLine();
    }

    static void DemoRawSql(BookStoreContext db)
    {
        Console.WriteLine("── Raw SQL (concept) ──");
        Console.WriteLine("  FromSql() requires a relational provider (SQL Server, PostgreSQL, etc.).");
        Console.WriteLine("  Pattern:");
        Console.WriteLine("    var minRating = 4;");
        Console.WriteLine("    var highRated = db.Reviews");
        Console.WriteLine("        .FromSql($\"SELECT * FROM Reviews WHERE Rating >= {minRating}\")");
        Console.WriteLine("        .ToList();");
        Console.WriteLine("  [Safely parameterized - no SQL injection risk]\n");

        // Demonstrate equivalent LINQ query instead
        var minR = 4;
        var highRated = db.Reviews.Where(r => r.Rating >= minR).ToList();
        Console.WriteLine($"  LINQ equivalent: found {highRated.Count} reviews with rating >= {minR}\n");
    }
}
