using System.ComponentModel.DataAnnotations;

namespace EfCorePoc.Models;

public class Author
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Bio { get; set; }

    public DateTime BirthDate { get; set; }

    // Concurrency token
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    // Navigation: Author has many Books (1:N)
    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
}
