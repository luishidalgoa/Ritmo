using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Ritmo_App.Services;

/// <summary>
/// Navegación entre páginas desde cualquier control, sincronizando el
/// NavigationView (para que la pestaña activa quede coherente). Centraliza el
/// "ir al temporizador y, si procede, empezar ya" que usan Hoy (#68) y Horario (#69).
/// </summary>
public static class Navigator
{
    /// <summary>Lleva al temporizador; si <paramref name="autoStart"/>, lo arranca al cargar.</summary>
    public static void GoToTimer(DependencyObject from, bool autoStart = false)
    {
        if (autoStart) TimerPage.AutoStartPending = true;

        var nav = FindAncestor<NavigationView>(from);
        if (nav is not null)
        {
            foreach (var mi in nav.MenuItems.OfType<NavigationViewItem>())
                if ((string?)mi.Tag == "timer") { nav.SelectedItem = mi; return; }
        }
        // Fallback: navegar el Frame directamente.
        FindAncestor<Frame>(from)?.Navigate(typeof(TimerPage));
    }

    /// <summary>Lleva a «Tareas», opcionalmente con un bloque ya seleccionado (#145).</summary>
    public static void GoToTasks(DependencyObject from, string? blockId = null)
    {
        TasksPage.PendingBlockId = blockId;

        var nav = FindAncestor<NavigationView>(from);
        if (nav is not null)
        {
            foreach (var mi in nav.MenuItems.OfType<NavigationViewItem>())
                if ((string?)mi.Tag == "tasks") { nav.SelectedItem = mi; return; }
        }
        FindAncestor<Frame>(from)?.Navigate(typeof(TasksPage), blockId);
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : class
    {
        var cur = VisualTreeHelper.GetParent(start);
        while (cur is not null)
        {
            if (cur is T t) return t;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return null;
    }
}
