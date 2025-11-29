namespace Data.Models
{
    /// <summary>
    /// Representa un patrón de archivo procesado con sus componentes separados
    /// </summary>
    public class ProcessedPattern
    {
        /// <summary>
        /// Patrón de expresión regular para filtrar por nombre de archivo
        /// </summary>
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// Indica si se debe aplicar filtro por fecha de última modificación
        /// </summary>
        public bool RequiresDateFilter { get; set; } = false;

        /// <summary>
        /// Número de días hacia atrás para el filtro de fecha (null si no aplica)
        /// </summary>
        public int? DaysBack { get; set; } = null;

        /// <summary>
        /// Patrón original sin procesar (para logging/debugging)
        /// </summary>
        public string OriginalPattern { get; set; } = string.Empty;
    }
}
