using System.ComponentModel.DataAnnotations;

namespace Tokito.Models
{
    public class Tag
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public virtual ICollection<Game> Games { get; set; } = new List<Game>();
    }
}
