using System.ComponentModel.DataAnnotations;

namespace EfCorePoc.Models;

public class Book
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(13)]
    public string? Isbn { get; set; }

    public decimal Price { get; set; }

    public DateTime PublishedDate { get; set; }

    // FK to Author (1:N - many Books belong to one Author)
    public int AuthorId { get; set; }
    public virtual Author Author { get; set; } = null!;

    // Navigation: Book has many Reviews (1:N) - this is the 3rd level
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    // Navigation: Book has many Tags (M:N)
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
