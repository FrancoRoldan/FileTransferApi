using Core.Utils;
using Data.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Core.Services.PatternProcessor
{
    public class PatternProcessorService : IPatternProcessorService
    {
        private readonly ILogger<PatternProcessorService> _logger;

        public PatternProcessorService(ILogger<PatternProcessorService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Punto de entrada principal: procesa el patrón completo
        /// </summary>
        public ProcessedPattern ParseAndExpandPattern(string? filePattern)
        {
            var result = new ProcessedPattern
            {
                OriginalPattern = filePattern ?? string.Empty
            };

            // Si el patrón es nulo o vacío, coincidir con todo
            if (string.IsNullOrWhiteSpace(filePattern))
            {
                result.RegexPattern = ".*";
                return result;
            }

            try
            {
                string processedPattern = filePattern;

                // PASO 1: Extraer filtro de fecha de última modificación
                var (patternWithoutDateFilter, daysBack) = ExtractModificationDateFilter(processedPattern);

                if (daysBack.HasValue)
                {
                    result.RequiresDateFilter = true;
                    result.DaysBack = daysBack.Value;
                    processedPattern = patternWithoutDateFilter;
                }

                // PASO 2: Expandir patrones de fecha fija ([fechaAyerddmmaaaa])
                processedPattern = ExpandDatePatterns(processedPattern);

                // PASO 3: Convertir wildcards a regex válido
                result.RegexPattern = ConvertWildcardsToRegex(processedPattern);

                _logger.LogDebug(
                    "Pattern processed: '{Original}' -> Regex: '{Regex}', DateFilter: {HasFilter}, DaysBack: {Days}",
                    filePattern, result.RegexPattern, result.RequiresDateFilter, result.DaysBack);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error processing pattern '{Pattern}'. Using original as literal match.",
                    filePattern);

                // Fallback: usar el patrón original como match literal
                result.RegexPattern = Regex.Escape(filePattern);
                return result;
            }
        }

        /// <summary>
        /// Expande patrones de fecha como [fechaAyerddmmaaaa] a valores reales
        /// </summary>
        private string ExpandDatePatterns(string pattern)
        {
            // Calcular fecha de ayer
            DateTime yesterday = DateTime.Now.AddDays(-1);
            string yesterdayFormatted = yesterday.ToString("ddMMyyyy");

            // Reemplazar todas las ocurrencias de [fechaAyerddmmaaaa]
            string expanded = Regex.Replace(
                pattern,
                PatternConstants.FECHA_AYER_PATTERN,
                yesterdayFormatted,
                RegexOptions.IgnoreCase);

            return expanded;
        }

        /// <summary>
        /// Extrae y elimina el patrón [fechaUltMod_X] del pattern
        /// </summary>
        /// <returns>Tupla con (patrón sin filtro de fecha, días hacia atrás)</returns>
        private (string patternWithoutDateFilter, int? daysBack) ExtractModificationDateFilter(string pattern)
        {
            var match = Regex.Match(pattern, PatternConstants.FECHA_ULTMOD_PATTERN, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return (pattern, null);
            }

            // Extraer el número de días (grupo 1)
            int daysBack = PatternConstants.DEFAULT_DAYS_BACK;

            if (match.Groups.Count > 1 && match.Groups[1].Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int parsedDays))
                {
                    daysBack = Math.Abs(parsedDays); // Asegurar valor positivo
                }
            }

            // Remover el patrón de fecha del pattern original
            string cleanedPattern = Regex.Replace(
                pattern,
                PatternConstants.FECHA_ULTMOD_PATTERN,
                string.Empty,
                RegexOptions.IgnoreCase);

            return (cleanedPattern, daysBack);
        }

        /// <summary>
        /// Convierte un patrón con wildcards (* y ?) a una expresión regular válida
        /// </summary>
        private string ConvertWildcardsToRegex(string pattern)
        {
            // PASO 1: Escapar todos los caracteres especiales de regex EXCEPTO * y ?
            // Caracteres especiales: . $ ^ { [ ( | ) ] } + \
            string escaped = Regex.Escape(pattern);

            // PASO 2: Reemplazar los wildcards escapados por sus equivalentes regex
            // Regex.Escape convierte * en \* y ? en \?
            escaped = escaped.Replace(@"\*", ".*");  // * -> cualquier cantidad de caracteres
            escaped = escaped.Replace(@"\?", ".");   // ? -> exactamente un carácter

            // PASO 3: Agregar anclas para match completo
            return $"^{escaped}$";
        }

        /// <summary>
        /// Valida si una fecha de modificación cumple con el filtro
        /// </summary>
        public bool MatchesDateFilter(DateTime lastModified, int daysBack)
        {
            if (daysBack <= 0)
            {
                _logger.LogWarning("Invalid daysBack value: {DaysBack}. Using absolute value.", daysBack);
                daysBack = Math.Abs(daysBack);
            }

            DateTime now = DateTime.Now;
            DateTime cutoffDate = now.AddDays(-daysBack);

            // El archivo debe estar modificado entre (now - daysBack) y now
            return lastModified.Date >= cutoffDate.Date && lastModified.Date <= now.Date;
        }
    }
}
