namespace Core.Utils
{
    /// <summary>
    /// Constantes para los patrones dinámicos soportados en FilePattern
    /// </summary>
    public static class PatternConstants
    {
        /// <summary>
        /// Patrón regex para detectar [fechaAyerddmmaaaa]
        /// </summary>
        public const string FECHA_AYER_PATTERN = @"\[fechaAyerddmmaaaa\]";

        /// <summary>
        /// Patrón regex para detectar [fechaUltMod] o [fechaUltMod_X]
        /// Grupo 1 captura el número X (opcional)
        /// </summary>
        public const string FECHA_ULTMOD_PATTERN = @"\[fechaUltMod(?:_(\d+))?\]";

        /// <summary>
        /// Valor por defecto de días hacia atrás cuando se usa [fechaUltMod] sin número
        /// </summary>
        public const int DEFAULT_DAYS_BACK = 1;
    }
}
