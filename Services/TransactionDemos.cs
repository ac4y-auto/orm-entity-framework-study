using Microsoft.EntityFrameworkCore;
using EfCorePoc.Data;
using EfCorePoc.Models;

namespace EfCorePoc.Services;

public static class TransactionDemos
{
    public static void RunAll(BookStoreContext db)
    {
        Console.WriteLine("=== TRANSACTION & CONCURRENCY DEMOS ===\n");

        DemoImplicitTransaction(db);
        DemoExplicitTransaction(db);
        DemoTransactionRollback(db);
        DemoConcurrencyConflict(db);
        DemoChangeTracker(db);
    }

    static void DemoImplicitTransaction(BookStoreContext db)
    {
        Console.WriteLine("── Implicit Transaction (SaveChanges) ──");
        Console.WriteLine("  Every SaveChanges() call is wrapped in an implicit transaction.\n");

        var author = db.Authors.First();
        author.Bio = "Updated in implicit transaction";

        var newReview = new Review
        {
            ReviewerName = "TransactionTest",
            Rating = 3,
            Comment = "Testing implicit transaction",
            BookId = db.Books.First().Id
        };
        db.Reviews.Add(newReview);

        // Both changes are committed atomically
        db.SaveChanges();
        Console.WriteLine("  Both author update and review insert committed atomically.\n");

        // Clean up
        db.Reviews.Remove(newReview);
        db.SaveChanges();
    }

    static void DemoExplicitTransaction(BookStoreContext db)
    {
        Console.WriteLine("── Explicit Transaction ──");
        Console.WriteLine("  Use db.Database.BeginTransaction() for multi-SaveChanges scenarios.\n");

        using var transaction = db.Database.BeginTransaction();
        try
        {
            // Step 1: Create a new author
            var author = new Author
            {
                Name = "TransactionAuthor",
                Bio = "Created in explicit transaction",
                BirthDate = new DateTime(1990, 1, 1)
            };
            db.Authors.Add(author);
            db.SaveChanges(); // First savepoint
            Console.WriteLine($"  Step 1: Created author (Id={author.Id})");

            // Step 2: Create a book for that author
            var book = new Book
            {
                Title = "Transaction Book",
                Price = 9.99m,
                PublishedDate = DateTime.UtcNow,
                AuthorId = author.Id
            };
            db.Books.Add(book);
            db.SaveChanges(); // Second savepoint
            Console.WriteLine($"  Step 2: Created book (Id={book.Id})");

            // Step 3: Add a review
            db.Reviews.Add(new Review
            {
                ReviewerName = "TxReviewer",
                Rating = 5,
                Comment = "Great transaction!",
                BookId = book.Id
            });
            db.SaveChanges(); // Third savepoint
            Console.WriteLine("  Step 3: Created review");

            // Commit all three operations
            transaction.Commit();
            Console.WriteLine("  Transaction COMMITTED successfully!\n");

            // Clean up
            var createdAuthor = db.Authors
                .Include(a => a.Books).ThenInclude(b => b.Reviews)
                .First(a => a.Name == "TransactionAuthor");
            db.Authors.Remove(createdAuthor);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"  Transaction ROLLED BACK: {ex.Message}\n");
        }
    }

    static void DemoTransactionRollback(BookStoreContext db)
    {
        Console.WriteLine("── Transaction Rollback ──");

        var originalCount = db.Authors.Count();

        using var transaction = db.Database.BeginTransaction();
        try
        {
            db.Authors.Add(new Author
            {
                Name = "WillBeRolledBack",
                BirthDate = DateTime.UtcNow
            });
            db.SaveChanges();
            Console.WriteLine("  Added author (not yet committed)...");

            // Simulate an error
            throw new InvalidOperationException("Simulated business logic error!");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"  Rolled back: {ex.Message}");
        }

        // Verify nothing was persisted (in-memory may behave differently)
        // With a real DB, the count would be unchanged
        Console.WriteLine($"  Author count before: {originalCount}, after rollback: {db.Authors.Count()}");
        Console.WriteLine("  [Note: InMemory provider doesn't support true transactions,");
        Console.WriteLine("   but the pattern is correct for real databases.]\n");

        // Manual cleanup for InMemory
        var rolledBack = db.Authors.FirstOrDefault(a => a.Name == "WillBeRolledBack");
        if (rolledBack != null)
        {
            db.Authors.Remove(rolledBack);
            db.SaveChanges();
        }
    }

    static void DemoConcurrencyConflict(BookStoreContext db)
    {
        Console.WriteLine("── Concurrency Conflict Handling ──");
        Console.WriteLine("  Uses [Timestamp]/RowVersion to detect conflicts.\n");

        // Note: InMemory provider doesn't enforce concurrency tokens,
        // but we demonstrate the pattern.

        // Simulate two "users" loading the same entity
        var book1 = db.Books.First();
        var book2 = db.Books.First();

        // "User 1" changes the price
        book1.Price = 99.99m;
        db.SaveChanges();
        Console.WriteLine($"  User 1 updated price to ${book1.Price}");

        // "User 2" tries to change the same book (stale data)
        book2.Price = 1.99m;
        try
        {
            db.SaveChanges();
            Console.WriteLine($"  User 2 also saved (InMemory doesn't enforce concurrency tokens)");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Console.WriteLine($"  User 2 got CONFLICT: {ex.Message}");

            // Resolution strategies:
            Console.WriteLine("  Resolution strategies:");
            Console.WriteLine("    1. Client Wins: entry.OriginalValues.SetValues(databaseValues)");
            Console.WriteLine("    2. Database Wins: entry.Reload()");
            Console.WriteLine("    3. Custom merge: compare and merge field by field");

            foreach (var entry in ex.Entries)
            {
                var dbValues = entry.GetDatabaseValues();
                if (dbValues != null)
                {
                    // Database wins strategy
                    entry.OriginalValues.SetValues(dbValues);
                    db.SaveChanges();
                }
            }
        }

        Console.WriteLine("  [With a real DB + [Timestamp], DbUpdateConcurrencyException is thrown]\n");
    }

    static void DemoChangeTracker(BookStoreContext db)
    {
        Console.WriteLine("── Change Tracker ──");

        // Show current tracking state
        var author = db.Authors.First();
        author.Bio = "Modified bio for change tracker demo";

        var newTag = new Tag { Name = "ChangeTrackerDemo" };
        db.Tags.Add(newTag);

        Console.WriteLine("  Tracked entities:");
        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Unchanged)
                Console.WriteLine($"    {entry.Entity.GetType().Name}: {entry.State}");
        }

        // HasChanges
        Console.WriteLine($"  HasChanges: {db.ChangeTracker.HasChanges()}");

        // Undo changes
        foreach (var entry in db.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Modified:
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                    break;
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
            }
        }
        Console.WriteLine($"  After undo - HasChanges: {db.ChangeTracker.HasChanges()}");

        // No-tracking query for read-only scenarios
        var readOnly = db.Books.AsNoTracking().ToList();
        Console.WriteLine($"  AsNoTracking: loaded {readOnly.Count} books (not tracked, better performance)\n");
    }
}
