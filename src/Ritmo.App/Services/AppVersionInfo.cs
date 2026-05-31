using Windows.ApplicationModel;

namespace Ritmo_App.Services;

/// <summary>Versión del paquete MSIX en runtime, para detectar actualizaciones (#updates).</summary>
internal static class AppVersionInfo
{
    /// <summary>Versión actual como "M.m.b.r". "0.0.0.0" si la app corre sin identidad de paquete.</summary>
    public static string Current
    {
        get
        {
            try
            {
                var v = Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
            catch
            {
                return "0.0.0.0";
            }
        }
    }
}
