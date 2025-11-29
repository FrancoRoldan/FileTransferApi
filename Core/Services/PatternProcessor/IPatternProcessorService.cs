using Data.Models;

namespace Core.Services.PatternProcessor
{
    /// <summary>
    /// Servicio para procesar patrones dinámicos en FilePattern
    /// </summary>
    public interface IPatternProcessorService
    {
        /// <summary>
        /// Procesa un patrón de archivo, expandiendo patrones dinámicos y
        /// extrayendo filtros de fecha
        /// </summary>
        /// <param name="filePattern">Patrón original del FileTransferTask</param>
        /// <returns>ProcessedPattern con componentes separados</returns>
        ProcessedPattern ParseAndExpandPattern(string? filePattern);

        /// <summary>
        /// Valida si un archivo cumple con los criterios de fecha de modificación
        /// </summary>
        /// <param name="lastModified">Fecha de última modificación del archivo</param>
        /// <param name="daysBack">Días hacia atrás desde hoy</param>
        /// <returns>True si el archivo está dentro del rango</returns>
        bool MatchesDateFilter(DateTime lastModified, int daysBack);
    }
}
