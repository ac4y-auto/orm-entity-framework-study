using Microsoft.EntityFrameworkCore;
using EfCorePoc.Models;

namespace EfCorePoc.Data;

public class BookStoreContext : DbContext
{
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Tag> Tags => Set<Tag>();

    public BookStoreContext(DbContextOptions<BookStoreContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Author configuration
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(100);

            // 1:N Author -> Books
            entity.HasMany(a => a.Books)
                  .WithOne(b => b.Author)
                  .HasForeignKey(b => b.AuthorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Book configuration
        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Title).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Price).HasPrecision(10, 2);

            // 1:N Book -> Reviews (3rd level!)
            entity.HasMany(b => b.Reviews)
                  .WithOne(r => r.Book)
                  .HasForeignKey(r => r.BookId)
                  .OnDelete(DeleteBehavior.Cascade);

            // M:N Book <-> Tag (EF Core auto join table)
            entity.HasMany(b => b.Tags)
                  .WithMany(t => t.Books);
        });

        // Review configuration
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.ReviewerName).IsRequired().HasMaxLength(100);
        });

        // Tag configuration
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(t => t.Name).IsUnique();
        });

        // Seed data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>().HasData(
            new Author { Id = 1, Name = "Tolkien", Bio = "English writer and philologist", BirthDate = new DateTime(1892, 1, 3) },
            new Author { Id = 2, Name = "Asimov", Bio = "American writer and professor of biochemistry", BirthDate = new DateTime(1920, 1, 2) },
            new Author { Id = 3, Name = "Herbert", Bio = "American science fiction author", BirthDate = new DateTime(1920, 10, 8) }
        );

        modelBuilder.Entity<Book>().HasData(
            new Book { Id = 1, Title = "The Lord of the Rings", Isbn = "9780544003415", Price = 29.99m, PublishedDate = new DateTime(1954, 7, 29), AuthorId = 1 },
            new Book { Id = 2, Title = "The Hobbit", Isbn = "9780547928227", Price = 14.99m, PublishedDate = new DateTime(1937, 9, 21), AuthorId = 1 },
            new Book { Id = 3, Title = "Foundation", Isbn = "9780553293357", Price = 17.99m, PublishedDate = new DateTime(1951, 6, 1), AuthorId = 2 },
            new Book { Id = 4, Title = "I, Robot", Isbn = "9780553294385", Price = 15.99m, PublishedDate = new DateTime(1950, 12, 2), AuthorId = 2 },
            new Book { Id = 5, Title = "Dune", Isbn = "9780441013593", Price = 18.99m, PublishedDate = new DateTime(1965, 8, 1), AuthorId = 3 }
        );

        modelBuilder.Entity<Review>().HasData(
            new Review { Id = 1, ReviewerName = "Alice", Rating = 5, Comment = "A masterpiece of fantasy literature!", BookId = 1, CreatedAt = new DateTime(2024, 1, 15) },
            new Review { Id = 2, ReviewerName = "Bob", Rating = 4, Comment = "Epic but long", BookId = 1, CreatedAt = new DateTime(2024, 2, 20) },
            new Review { Id = 3, ReviewerName = "Charlie", Rating = 5, Comment = "The book that started it all", BookId = 2, CreatedAt = new DateTime(2024, 3, 10) },
            new Review { Id = 4, ReviewerName = "Diana", Rating = 5, Comment = "Mind-blowing sci-fi", BookId = 3, CreatedAt = new DateTime(2024, 4, 5) },
            new Review { Id = 5, ReviewerName = "Eve", Rating = 4, Comment = "Thought-provoking robot stories", BookId = 4, CreatedAt = new DateTime(2024, 5, 1) },
            new Review { Id = 6, ReviewerName = "Frank", Rating = 5, Comment = "The greatest sci-fi novel ever written", BookId = 5, CreatedAt = new DateTime(2024, 6, 15) },
            new Review { Id = 7, ReviewerName = "Grace", Rating = 3, Comment = "Dense but rewarding", BookId = 5, CreatedAt = new DateTime(2024, 7, 20) }
        );

        modelBuilder.Entity<Tag>().HasData(
            new Tag { Id = 1, Name = "Fantasy" },
            new Tag { Id = 2, Name = "Sci-Fi" },
            new Tag { Id = 3, Name = "Classic" },
            new Tag { Id = 4, Name = "Adventure" },
            new Tag { Id = 5, Name = "Robots" }
        );
    }
}
