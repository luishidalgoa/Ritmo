using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Model;

namespace Ritmo_App.Dialogs;

/// <summary>
/// Onboarding del primer arranque (#83): da la bienvenida y deja elegir una plantilla de
/// categorías (Estudio / Trabajo / Genérico). No tiene botón de cancelar: siempre se parte
/// de algo. La elección se aplica con <c>ConfigurationService.SeedTemplate</c>.
/// </summary>
public sealed partial class OnboardingDialog : ContentDialog
{
    public OnboardingDialog()
    {
        InitializeComponent();
    }

    /// <summary>Id de plantilla elegida (por defecto, la neutral "blanco").</summary>
    public string SelectedTemplate =>
        OptStudy.IsChecked == true ? CategoryDefaults.Study :
        OptWork.IsChecked == true ? CategoryDefaults.Work :
        CategoryDefaults.Blank;
}
