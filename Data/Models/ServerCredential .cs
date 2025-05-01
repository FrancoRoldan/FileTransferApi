using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class ServerCredential : BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ServerType { get; set; } = ""; // FTP, SFTP,NETWORK, etc.
        public string Host { get; set; } = "";
        public int Port { get; set; } = 21;
        public string? Username { get; set; } = "";
        public string? EncryptedPassword { get; set; } = ""; // Almacenar contraseña cifrada
        public string? PrivateKeyPath { get; set; } // Para conexiones SFTP con clave privada
        public bool IsActive { get; set; } = true;
    }
}
