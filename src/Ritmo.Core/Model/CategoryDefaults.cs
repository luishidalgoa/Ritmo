using System.Collections.Generic;

namespace Ritmo.Core.Model;

/// <summary>
/// Sets de categorías por defecto y de plantillas para el Ritmo genérico (#83). El set
/// neutral es el de fábrica; las plantillas del onboarding ("Estudio", "Trabajo", "En
/// blanco") parten de uno de estos. Todas incluyen las categorías de sistema «Por definir»
/// y «Otro». No depende de UI ni de persistencia.
/// </summary>
public static class CategoryDefaults
{
    /// <summary>Ids de plantilla aceptados por el onboarding.</summary>
    public const string Study = "estudio";
    public const string Work = "trabajo";
    public const string Blank = "blanco";

    /// <summary>Set neutral de fábrica: concentración/reunión + descanso/personal + sistema.</summary>
    public static IReadOnlyList<BlockCategory> Neutral()
    {
        var b = new Builder();
        b.Add("Concentración", "#E2EFDA", "#548235", focus: true);
        b.Add("Reunión", "#DCE6F1", "#1F4E79", focus: true);
        b.Add("Descanso", "#FCE9D6", "#595959", focus: false);
        b.Add("Personal", "#FCE4EC", "#AD1457", focus: false);
        b.System();
        return b.Build();
    }

    /// <summary>Plantilla «Estudio»: concentración + lectura/repaso/tests/simulacro.</summary>
    public static IReadOnlyList<BlockCategory> StudySet()
    {
        var b = new Builder();
        b.Add("Concentración", "#E2EFDA", "#548235", focus: true);
        b.Add("Lectura", "#FDE2C8", "#C55A11", focus: true);
        b.Add("Repaso", "#E4DFEC", "#7030A0", focus: true);
        b.Add("Tests", "#DCE6F1", "#1F4E79", focus: true);
        b.Add("Simulacro", "#F8CBAD", "#C0392B", focus: true);
        b.Add("Descanso", "#FCE9D6", "#595959", focus: false);
        b.Add("Personal", "#FCE4EC", "#AD1457", focus: false);
        b.System();
        return b.Build();
    }

    /// <summary>Plantilla «Trabajo»: concentración + reuniones/email/gestión.</summary>
    public static IReadOnlyList<BlockCategory> WorkSet()
    {
        var b = new Builder();
        b.Add("Concentración", "#E2EFDA", "#548235", focus: true);
        b.Add("Reuniones", "#DCE6F1", "#1F4E79", focus: true);
        b.Add("Email", "#FFF2CC", "#806000", focus: true);
        b.Add("Gestión", "#FDE2C8", "#C55A11", focus: true);
        b.Add("Descanso", "#FCE9D6", "#595959", focus: false);
        b.Add("Personal", "#FCE4EC", "#AD1457", focus: false);
        b.System();
        return b.Build();
    }

    /// <summary>Categorías para una plantilla dada (por defecto, neutral).</summary>
    public static IReadOnlyList<BlockCategory> ForTemplate(string? templateId) => templateId switch
    {
        Study => StudySet(),
        Work => WorkSet(),
        _ => Neutral()   // "blanco" y desconocido → neutral
    };

    private sealed class Builder
    {
        private readonly List<BlockCategory> _items = [];
        public void Add(string name, string color, string text, bool focus)
            => _items.Add(new BlockCategory
            {
                Id = CategorySlug.From(name, Ids()),
                Name = name, ColorHex = color, TextColorHex = text,
                IsFocus = focus, Order = _items.Count
            });
        /// <summary>Añade las categorías de sistema «Por definir» y «Otro».</summary>
        public void System()
        {
            _items.Add(new BlockCategory { Id = CategoryIds.Undecided, Name = "Por definir", ColorHex = "#F2F2F2", TextColorHex = "#595959", IsFocus = false, Order = _items.Count, IsSystem = true });
            _items.Add(new BlockCategory { Id = CategoryIds.Other, Name = "Otro", ColorHex = "#EDEDED", TextColorHex = "#595959", IsFocus = false, Order = _items.Count, IsSystem = true });
        }
        private IEnumerable<string> Ids() { foreach (var c in _items) yield return c.Id; }
        public IReadOnlyList<BlockCategory> Build() => _items;
    }
}
