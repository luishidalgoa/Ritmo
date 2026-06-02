using System;
using System.Linq;

namespace Ritmo_App.Services;

/// <summary>
/// Idioma efectivo de la app (#48 i18n) para el contenido generado por código/datos (Novedades,
/// glosario de ayuda…): "es" o "en". Resuelve la preferencia guardada (<c>AppSettings.Language</c>),
/// y para "system" mira el idioma de Windows. Los textos estáticos de XAML los resuelve x:Uid/.resw.
/// </summary>
public static class Loc
{
    public static string Lang
    {
        get
        {
            var l = AppState.Load().Language;
            if (l == "en") return "en";
            if (l == "es") return "es";
            try
            {
                var top = Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault() ?? "";
                return top.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
            }
            catch { return "es"; }
        }
    }

    public static bool IsEnglish => Lang == "en";

    /// <summary>Atajo: elige entre dos textos según el idioma efectivo.</summary>
    public static string Pick(string es, string en) => IsEnglish ? en : es;
}
