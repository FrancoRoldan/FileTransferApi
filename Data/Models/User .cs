using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class User: BaseEntity
    {
        [Column("IdUsuario")]
        [Key]
        public int Id { get; set; }
        public string Nombre { get; set; }  = "";
        public required string Email { get; set; } 
        public string Password { get; set; } = "";

    }
}
