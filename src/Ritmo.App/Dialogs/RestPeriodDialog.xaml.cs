using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Ritmo_App.Dialogs;

/// <summary>Diálogo para programar un periodo de descanso (vacaciones…): etiqueta + fechas. #135</summary>
public sealed partial class RestPeriodDialog : ContentDialog
{
    public RestPeriodDialog()
    {
        InitializeComponent();
        var today = new DateTimeOffset(DateTime.Today);
        FromPicker.Date = today;
        ToPicker.Date = today;
        PrimaryButtonClick += (_, args) =>
        {
            if (ToDate < FromDate) { args.Cancel = true; ShowError("La fecha de fin no puede ser anterior a la de inicio."); }
        };
    }

    public string Label => LabelBox.Text?.Trim() ?? "";

    public DateOnly FromDate =>
        FromPicker.Date is { } d ? DateOnly.FromDateTime(d.Date) : DateOnly.FromDateTime(DateTime.Today);

    public DateOnly ToDate =>
        ToPicker.Date is { } d ? DateOnly.FromDateTime(d.Date) : FromDate;

    private void FromPicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        // Mantén «Hasta» ≥ «Desde».
        if (FromPicker.Date is { } f && (ToPicker.Date is null || ToPicker.Date < f))
            ToPicker.Date = f;
    }

    private void ShowError(string msg) { ErrorText.Text = msg; ErrorText.Visibility = Visibility.Visible; }
}
