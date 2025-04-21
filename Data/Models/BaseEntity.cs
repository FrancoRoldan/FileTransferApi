
namespace Data.Models
{
    public class BaseEntity
    {
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CreatedUser { get; set; }
        public string? UpdatedUser { get; set; }
    }
}
