namespace Ritmo.Core.Timing;

/// <summary>
/// Programa una acción para que se ejecute tras un retardo, o la cancela.
/// Abstrae los timers del SO para que el servicio en segundo plano se pueda
/// testear con un scheduler falso de ejecución inmediata/controlada.
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Programa <paramref name="callback"/> para dispararse una vez tras
    /// <paramref name="delay"/>. Devuelve un handle desechable para cancelarlo.
    /// Un delay &lt;= 0 debe disparar lo antes posible.
    /// </summary>
    IDisposable Schedule(TimeSpan delay, Action callback);
}
