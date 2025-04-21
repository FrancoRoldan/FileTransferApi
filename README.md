# Servicio de Programación de Transferencia de Archivos

## Descripción General

El Programador de Transferencia de Archivos es un servicio en segundo plano que permite transferencias automáticas de archivos según diversos patrones de programación. Este servicio monitorea y ejecuta continuamente tareas de transferencia de archivos basadas en sus horarios definidos, admitiendo transferencias únicas, operaciones diarias recurrentes, patrones semanales, ejecuciones mensuales y programaciones personalizadas mediante expresiones cron.

## Historias de Usuario

### Como administrador de sistemas
Quiero configurar transferencias automáticas de archivos con una programación recurrente  
Para garantizar que los datos se muevan de manera confiable sin intervención manual

### Como usuario de negocio
Quiero que mis transferencias de archivos se ejecuten automáticamente en momentos específicos  
Para que los sistemas posteriores reciban datos actualizados cuando sea necesario

### Como gerente de operaciones de TI
Quiero un sistema de programación unificado para todas las transferencias de archivos  
Para poder gestionar y monitorear el movimiento de datos en toda la organización

### Como especialista en integración de datos
Quiero opciones de programación flexibles para las transferencias de archivos  
Para poder satisfacer diversos requisitos comerciales y acuerdos de nivel de servicio

## Características

### Tipos de Programación

- **Una sola vez**: Ejecuta una transferencia de archivos una vez en una fecha y hora específicas
- **Diario**: Ejecuta transferencias todos los días a una o más horas específicas
- **Semanal**: Ejecuta transferencias en días seleccionados de la semana a horas específicas
- **Mensual**: Ejecuta transferencias en un día específico de cada mes a horas configurables
- **Personalizado**: Define programaciones complejas utilizando expresiones cron

### Múltiples Tiempos de Ejecución

Cada tarea puede configurarse con múltiples tiempos de ejecución, permitiendo una programación flexible:

- **Tareas diarias**: Se ejecutan en cada hora especificada todos los días
- **Tareas semanales**: Se ejecutan en cada hora especificada, pero solo en los días habilitados
- **Tareas mensuales**: Se ejecutan en cada hora especificada en el día programado del mes

### Características de Confiabilidad

- Ejecuta tareas en hilos separados para evitar bloqueos
- Utiliza ámbitos de servicio para garantizar una gestión adecuada de los recursos
- Incluye registro de errores completo
- Maneja pequeños errores de sincronización (dentro de la ventana de intervalo de verificación)

## Ejemplos de Escenarios de Uso

1. **Carga de Almacén de Datos**:  
   Programar transferencias diarias a las 2:00 AM para mover datos de transacciones al almacén de datos

2. **Sincronización entre Sistemas**:  
   Configurar transferencias por hora durante el horario laboral (9:00 AM, 10:00 AM, ..., 5:00 PM) en días laborables

3. **Informes Mensuales**:  
   Configurar una transferencia para que se ejecute el primer día de cada mes para recopilar y distribuir informes de fin de mes

4. **Programación Comercial Compleja**:  
   Utilizar una expresión cron personalizada para manejar requisitos comerciales especiales (por ejemplo, "Ejecutar a las 10:45 AM el segundo martes de cada mes")

5. **Respaldo Incremental de Bases de Datos**:  
   Configurar transferencias que se ejecuten cada 6 horas para mover archivos de respaldo incremental a un almacenamiento seguro, asegurando la posibilidad de recuperación en caso de fallos del sistema

6. **Intercambio de Archivos con Socios Comerciales**:  
   Programar transferencias para ejecutarse al final del día laboral (5:30 PM) solo en días hábiles (lunes a viernes) para intercambiar datos de pedidos y facturas con socios comerciales, respetando los horarios internacionales y zonas horarias

## Requisitos del Sistema

- El servicio se ejecuta como parte de una aplicación .NET
- Requiere configuración de inyección de dependencias para IFileTransferService y acceso al repositorio
- Las tareas se persisten en un almacén de datos accesible a través de IRepository<FileTransferTask>

## Monitoreo y Gestión

El programador registra eventos importantes que incluyen:
- Inicio y detención del servicio
- Inicio de ejecución de tareas
- Errores durante la programación o ejecución

Estos registros pueden monitorearse para garantizar que el sistema funcione correctamente y para solucionar cualquier problema.

## Building the project

change the connection string to match your postgres credentials in the appsettings.json
``` code
"ConnectionStrings": {
  "DbConnString": "Server=localhost;Database=Board;User Id=user;Password=password;"
}
```

Build an .NET Core api with .NET Core CLI, which is installed with [the .NET Core SDK](https://www.microsoft.com/net/download). Then run
these commands from the CLI in the directory:

```console
dotnet build
dotnet run
```
