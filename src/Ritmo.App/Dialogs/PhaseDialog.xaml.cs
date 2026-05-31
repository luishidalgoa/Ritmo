using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ritmo.Core.Model;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para crear o editar una fase del plan (#46).</summary>
public sealed partial class PhaseDialog : ContentDialog
{
    /// <summary>Nombre original (null = fase nueva), para distinguir alta de edición.</summary>
    public string? OriginalName { get; private set; }

    public PhaseDialog()
    {
        InitializeComponent();
        FromPicker.Date = DateTimeOffset.Now;
        // Valida al pulsar Guardar; si no es válido, cancela el cierre y muestra el error.
        PrimaryButtonClick += (_, args) => { var e = Validate(); if (e is not null) { args.Cancel = true; ShowError(e); } };
    }

    /// <summary>Carga una fase existente para editarla.</summary>
    public void LoadFrom(SchedulePhase phase)
    {
        OriginalName = phase.Name;
        Title = "Editar fase";
        NameBox.Text = phase.Name;
        FromPicker.Date = new DateTimeOffset(phase.ValidFrom.ToDateTime(TimeOnly.MinValue));
        if (phase.ValidTo is { } end)
        {
            OpenEndedSwitch.IsOn = false;
            ToPicker.Date = new DateTimeOffset(end.ToDateTime(TimeOnly.MinValue));
        }
        else OpenEndedSwitch.IsOn = true;
        SyncOpenEnded();
    }

    /// <summary>
    /// Pre-rellena el diálogo para DUPLICAR una fase (#38): nombre sugerido «… (copia)» y las
    /// fechas de la fuente, pero como fase NUEVA (OriginalName queda null). El usuario ajusta.
    /// </summary>
    public void LoadForDuplicate(SchedulePhase source)
    {
        Title = "Duplicar fase";
        NameBox.Text = source.Name + " (copia)";
        FromPicker.Date = new DateTimeOffset(source.ValidFrom.ToDateTime(TimeOnly.MinValue));
        if (source.ValidTo is { } end)
        {
            OpenEndedSwitch.IsOn = false;
            ToPicker.Date = new DateTimeOffset(end.ToDateTime(TimeOnly.MinValue));
        }
        else OpenEndedSwitch.IsOn = true;
        SyncOpenEnded();
    }

    private void OpenEndedSwitch_Toggled(object sender, RoutedEventArgs e) => SyncOpenEnded();

    private void SyncOpenEnded()
        => ToPicker.Visibility = OpenEndedSwitch.IsOn ? Visibility.Collapsed : Visibility.Visible;

    public string PhaseName => NameBox.Text.Trim();

    public DateOnly ValidFrom =>
        FromPicker.Date is { } d ? DateOnly.FromDateTime(d.DateTime) : DateOnly.FromDateTime(DateTime.Now);

    public DateOnly? ValidTo =>
        OpenEndedSwitch.IsOn ? null
        : (ToPicker.Date is { } d ? DateOnly.FromDateTime(d.DateTime) : null);

    /// <summary>Valida los campos; devuelve un mensaje de error o null si es válido.</summary>
    public string? Validate()
    {
        if (PhaseName.Length == 0) return "El nombre no puede estar vacío.";
        if (ValidTo is { } end && end < ValidFrom) return "La fecha de fin no puede ser anterior a la de inicio.";
        return null;
    }

    public void ShowError(string msg) { ErrorText.Text = msg; ErrorText.Visibility = Visibility.Visible; }
}
