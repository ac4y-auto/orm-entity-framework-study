using System.ComponentModel.DataAnnotations;

namespace EfCorePoc.Models;

public class Tag
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    // Navigation: Tag belongs to many Books (M:N)
    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
}
