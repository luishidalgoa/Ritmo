namespace Ritmo.Core.Timing;

/// <summary>
/// Fuente de tiempo. Abstrae "qué hora es" para que la lógica que depende del
/// reloj sea testeable (en tests se usa una implementación falsa controlable).
/// </summary>
public interface IClock
{
    /// <summary>Hora local actual.</summary>
    DateTime Now { get; }
}

/// <summary>Reloj real del sistema. Úsese en producción.</summary>
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
