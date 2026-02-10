using Microsoft.EntityFrameworkCore;
using EfCorePoc.Data;
using EfCorePoc.Models;

namespace EfCorePoc.Services;

public static class CrudDemos
{
    public static void RunAll(BookStoreContext db)
    {
        Console.WriteLine("=== CRUD DEMOS ===\n");

        DemoCreate(db);
        DemoRead(db);
        DemoUpdate(db);
        DemoDelete(db);
    }

    // ── CREATE ──────────────────────────────────────────────
    static void DemoCreate(BookStoreContext db)
    {
        Console.WriteLine("── CREATE ──");

        // 1. Simple insert
        var newAuthor = new Author
        {
            Name = "Bradbury",
            Bio = "American author and screenwriter",
            BirthDate = new DateTime(1920, 8, 22)
        };
        db.Authors.Add(newAuthor);
        db.SaveChanges();
        Console.WriteLine($"  Created Author: {newAuthor.Name} (Id={newAuthor.Id})");

        // 2. Insert with nested child (Book + Reviews in one go)
        var newBook = new Book
        {
            Title = "Fahrenheit 451",
            Isbn = "9781451673319",
            Price = 13.99m,
            PublishedDate = new DateTime(1953, 10, 19),
            AuthorId = newAuthor.Id,
            Reviews = new List<Review>
            {
                new() { ReviewerName = "Henry", Rating = 5, Comment = "A dystopian classic!" },
                new() { ReviewerName = "Irene", Rating = 4, Comment = "Thought-provoking" }
            }
        };
        db.Books.Add(newBook);
        db.SaveChanges();
        Console.WriteLine($"  Created Book: '{newBook.Title}' with {newBook.Reviews.Count} reviews");

        // 3. Many-to-many: assign tags
        var sciFiTag = db.Tags.First(t => t.Name == "Sci-Fi");
        var classicTag = db.Tags.First(t => t.Name == "Classic");
        newBook.Tags.Add(sciFiTag);
        newBook.Tags.Add(classicTag);
        db.SaveChanges();
        Console.WriteLine($"  Assigned tags [{string.Join(", ", newBook.Tags.Select(t => t.Name))}] to '{newBook.Title}'");

        // 4. Bulk insert
        var bulkReviews = Enumerable.Range(1, 5).Select(i => new Review
        {
            ReviewerName = $"BulkReviewer_{i}",
            Rating = (i % 5) + 1,
            Comment = $"Bulk review #{i}",
            BookId = newBook.Id
        }).ToList();
        db.Reviews.AddRange(bulkReviews);
        db.SaveChanges();
        Console.WriteLine($"  Bulk inserted {bulkReviews.Count} reviews\n");
    }

    // ── READ ────────────────────────────────────────────────
    static void DemoRead(BookStoreContext db)
    {
        Console.WriteLine("── READ ──");

        // 1. Simple read by Id
        var author = db.Authors.Find(1);
        Console.WriteLine($"  Find by Id: Author #{author?.Id} = {author?.Name}");

        // 2. First/Single
        var firstBook = db.Books.FirstOrDefault(b => b.Price > 20);
        Console.WriteLine($"  First book over $20: '{firstBook?.Title}' (${firstBook?.Price})");

        // 3. Read all with count
        var totalReviews = db.Reviews.Count();
        Console.WriteLine($"  Total reviews in DB: {totalReviews}");

        // 4. Projection with anonymous type
        var bookSummaries = db.Books
            .Select(b => new { b.Title, b.Price, ReviewCount = b.Reviews.Count })
            .OrderByDescending(b => b.ReviewCount)
            .ToList();
        Console.WriteLine("  Book summaries (by review count):");
        foreach (var s in bookSummaries)
            Console.WriteLine($"    '{s.Title}' - ${s.Price} - {s.ReviewCount} reviews");

        Console.WriteLine();
    }

    // ── UPDATE ──────────────────────────────────────────────
    static void DemoUpdate(BookStoreContext db)
    {
        Console.WriteLine("── UPDATE ──");

        // 1. Simple property update (tracked entity)
        var book = db.Books.First(b => b.Title == "Dune");
        var oldPrice = book.Price;
        book.Price = 21.99m;
        db.SaveChanges();
        Console.WriteLine($"  Updated Dune price: ${oldPrice} -> ${book.Price}");

        // 2. Batch update (loop-based; with a real DB you'd use ExecuteUpdate)
        //    Real DB pattern: db.Reviews.Where(r => r.Rating < 3)
        //        .ExecuteUpdate(s => s.SetProperty(r => r.Comment, r => r.Comment + " [edited]"));
        var lowRated = db.Reviews.Where(r => r.Rating < 3).ToList();
        foreach (var r in lowRated)
            r.Comment += " [edited]";
        db.SaveChanges();
        Console.WriteLine($"  Batch updated {lowRated.Count} low-rating reviews");

        // 3. Disconnected entity update
        var detachedAuthor = new Author { Id = 2, Name = "Isaac Asimov", Bio = "Updated bio - prolific writer", BirthDate = new DateTime(1920, 1, 2) };
        db.Authors.Update(detachedAuthor);
        db.SaveChanges();
        Console.WriteLine($"  Disconnected update: Author #{detachedAuthor.Id} -> '{detachedAuthor.Name}'");

        Console.WriteLine();
    }

    // ── DELETE ──────────────────────────────────────────────
    static void DemoDelete(BookStoreContext db)
    {
        Console.WriteLine("── DELETE ──");

        // 1. Simple delete
        var reviewToDelete = db.Reviews.First(r => r.ReviewerName == "BulkReviewer_1");
        db.Reviews.Remove(reviewToDelete);
        db.SaveChanges();
        Console.WriteLine($"  Deleted review by '{reviewToDelete.ReviewerName}'");

        // 2. Batch delete (loop-based; with a real DB you'd use ExecuteDelete)
        //    Real DB pattern: db.Reviews.Where(r => r.ReviewerName.StartsWith("BulkReviewer_")).ExecuteDelete();
        var bulkToDelete = db.Reviews.Where(r => r.ReviewerName.StartsWith("BulkReviewer_")).ToList();
        db.Reviews.RemoveRange(bulkToDelete);
        db.SaveChanges();
        Console.WriteLine($"  Batch deleted {bulkToDelete.Count} bulk reviews");

        // 3. Cascade delete demo (delete author -> deletes their books -> deletes reviews)
        var authorToDelete = db.Authors
            .Include(a => a.Books)
                .ThenInclude(b => b.Reviews)
            .First(a => a.Name == "Bradbury");
        var bookCount = authorToDelete.Books.Count;
        var revCount = authorToDelete.Books.SelectMany(b => b.Reviews).Count();
        db.Authors.Remove(authorToDelete);
        db.SaveChanges();
        Console.WriteLine($"  Cascade delete: removed Author '{authorToDelete.Name}' + {bookCount} books + {revCount} reviews\n");
    }
}
