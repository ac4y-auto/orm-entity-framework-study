using System.ComponentModel.DataAnnotations;

namespace EfCorePoc.Models;

public class Review
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ReviewerName { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FK to Book (1:N - many Reviews belong to one Book)
    public int BookId { get; set; }
    public virtual Book Book { get; set; } = null!;
}
