using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DispatcherARM.Domain.Entities;

namespace DispatcherARM.UI.Views.Dialogs;

/// <summary>
/// Путевой лист — официальная форма согласно Приказу Минтранса РФ №152
/// </summary>
public partial class WaybillDialog : Window
{
    private readonly Order   _order;
    private readonly Driver? _driver;
    private readonly string  _waybillNumber;
    private readonly string  _series;
    private readonly int     _number;

    public WaybillDialog(Order order, Driver? driver)
    {
        InitializeComponent();
        _order  = order;
        _driver = driver;

        // Серия и номер путевого листа
        _series = $"АРМ{DateTime.Now:yy}";
        _number = order.Id + 1000;
        _waybillNumber = $"{_series} № {_number:D6}";

        FillForm();
    }

    private void FillForm()
    {
        // Заголовок
        WaybillNumberText.Text = _waybillNumber;
        IssueDateText.Text     = DateTime.Now.ToString("«dd» MMMM yyyy г.", new System.Globalization.CultureInfo("ru-RU"));
        OrganizationText.Text  = "ООО «Транспортно-логистическая компания»";
        InnText.Text           = "7701000001 / КПП 770101001";
        OgrnText.Text          = "1027700000001";
        OrgAddressText.Text    = "г. Москва, ул. Складская, д. 1, стр. 2";
        OrgPhoneText.Text      = "+7 (495) 000-00-00";

        // Водитель
        if (_driver != null)
        {
            DriverNameText.Text     = _driver.FullName;
            LicenseSeriesText.Text  = _driver.LicenseNumber?.Length > 4
                ? _driver.LicenseNumber[..^6] : "—";
            LicenseNumberText.Text  = _driver.LicenseNumber ?? "—";
            LicenseCatText.Text     = _driver.LicenseCategory ?? "B, C";
            DriverPhoneText.Text    = _driver.Phone;
            MedCheckText.Text       = $"Допущен  {DateTime.Now:dd.MM.yyyy}";
            TechCheckText.Text      = $"Технически исправен  {DateTime.Now:dd.MM.yyyy}";

            // ТС
            if (_driver.AssignedVehicle != null)
            {
                var v = _driver.AssignedVehicle;
                VehicleBrandText.Text  = v.Model;
                VehiclePlateText.Text  = v.LicensePlate;
                VehicleVinText.Text    = v.Vin ?? "—";
                VehicleTypeText.Text   = v.Type.ToString();
                OdometerStartText.Text = "—";
                OdometerEndText.Text   = "—";
                FuelTypeText.Text      = "Дизельное";
                FuelStartText.Text     = "—";
                FuelGivenText.Text     = "—";
                FuelEndText.Text       = "—";
            }
            else
            {
                VehicleBrandText.Text = VehiclePlateText.Text =
                VehicleVinText.Text   = VehicleTypeText.Text  = "не назначено";
            }
        }
        else
        {
            DriverNameText.Text    = "— не назначен —";
            LicenseSeriesText.Text = LicenseNumberText.Text =
            LicenseCatText.Text    = DriverPhoneText.Text    = "—";
            MedCheckText.Text      = TechCheckText.Text      = "—";
            VehicleBrandText.Text  = VehiclePlateText.Text   = "—";
            VehicleVinText.Text    = VehicleTypeText.Text    = "—";
            OdometerStartText.Text = OdometerEndText.Text    = "—";
            FuelTypeText.Text      = FuelStartText.Text      = FuelGivenText.Text = FuelEndText.Text = "—";
        }

        // Маршрут
        PointFromText.Text   = "г. Москва, ул. Складская, д. 1 (Склад ТЛК)";
        PointToText.Text     = _order.Address;
        DeparturePlanText.Text = _order.DeliveryDate.HasValue
            ? _order.DeliveryDate.Value.ToString("dd.MM.yyyy") + "  08:00"
            : DateTime.Today.ToString("dd.MM.yyyy") + "  08:00";
        ArrivalPlanText.Text   = _order.DeliveryDate.HasValue
            ? _order.DeliveryDate.Value.ToString("dd.MM.yyyy") + "  18:00"
            : DateTime.Today.ToString("dd.MM.yyyy") + "  18:00";
        DeparturFactText.Text  = "____.__.____ ____:____";
        ArrivalFactText.Text   = "____.__.____ ____:____";

        // Задание
        CargoText.Text    = string.IsNullOrWhiteSpace(_order.Notes) ? "Сборный груз" : _order.Notes;
        WeightText.Text   = $"{_order.Weight:F1} кг";
        VolumeText.Text   = $"{_order.Volume:F2} м³";
        OrderNumText.Text = _order.OrderNumber;
        ClientText.Text   = $"{_order.ClientName}  тел.: {_order.ClientPhone ?? "—"}";
    }

    // ─── Экспорт в Word ─────────────────────────────────────────────────────────
    private async void SaveToWord_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Сохранить путевой лист",
            Filter     = "Word документ (*.docx)|*.docx|Текстовый файл (*.txt)|*.txt",
            FileName   = $"Путевой_лист_{_waybillNumber.Replace(":", "-").Replace("/", "-").Replace(" ", "_")}"
        };
        if (saveDialog.ShowDialog() != true) return;

        try
        {
            if (saveDialog.FilterIndex == 1)
                await SaveAsWordAsync(saveDialog.FileName);
            else
                await SaveAsTxtAsync(saveDialog.FileName);

            var result = MessageBox.Show(
                $"Путевой лист сохранён:\n{saveDialog.FileName}\n\nОткрыть файл?",
                "Сохранено", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка сохранения: {ex.Message}"); }
    }

    private Task SaveAsWordAsync(string path)
    {
        // Формируем Word XML (Office Open XML format - нативный .docx без COM)
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>");
        sb.AppendLine("<w:wordDocument xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">");
        // Используем RTF для совместимости с Word без зависимостей
        var rtf = BuildRtf();
        File.WriteAllText(path.Replace(".docx", ".rtf"), rtf, Encoding.Default);
        // Переименовываем под нужное расширение
        if (File.Exists(path)) File.Delete(path);
        File.Move(path.Replace(".docx", ".rtf"), path.Replace(".docx", ".rtf"));
        // Сохраняем RTF и указываем верное имя
        var rtfPath = Path.ChangeExtension(path, ".rtf");
        File.WriteAllText(rtfPath, rtf, Encoding.Default);
        if (path != rtfPath)
        {
            if (File.Exists(path)) File.Delete(path);
            File.Copy(rtfPath, path);
        }
        return Task.CompletedTask;
    }

    private string BuildRtf()
    {
        var sb = new StringBuilder();
        var ru = new System.Globalization.CultureInfo("ru-RU");

        sb.AppendLine(@"{\rtf1\ansi\ansicpg1251\deff0");
        sb.AppendLine(@"{\fonttbl{\f0\froman\fcharset204 Times New Roman;}{\f1\fswiss\fcharset204 Arial;}}");
        sb.AppendLine(@"{\colortbl;\red30\green58\blue95;\red0\green0\blue0;}");
        sb.AppendLine(@"\paperw12240\paperh15840\margl1800\margr1440\margt1440\margb1440");

        void Line(string text, int size = 20, bool bold = false, bool center = false)
        {
            sb.Append(@"\pard");
            if (center) sb.Append(@"\qc");
            else sb.Append(@"\ql");
            sb.Append($@"\f1\fs{size}");
            if (bold) sb.Append(@"\b");
            sb.Append($" {EscapeRtf(text)}");
            if (bold) sb.Append(@"\b0");
            sb.AppendLine(@"\par");
        }



        void Field(string label, string value)
        {
            sb.Append(@"\pard\ql\f1\fs18 ");
            sb.Append($@"\b {EscapeRtf(label)}\b0   {EscapeRtf(value)}");
            sb.AppendLine(@"\par");
        }

        // Заголовок
        Line("ПУТЕВОЙ ЛИСТ", 28, bold: true, center: true);
        Line($"Серия {_series}   №  {_number:D6}", 22, bold: true, center: true);
        Line($"Дата выдачи: {IssueDateText.Text}", 18, center: true);
        sb.AppendLine(@"\pard\par");

        // Организация
        Line("1. ОРГАНИЗАЦИЯ-ВЛАДЕЛЕЦ ТРАНСПОРТНОГО СРЕДСТВА", 18, bold: true);
        Field("Наименование:", OrganizationText.Text);
        Field("ИНН/КПП:", InnText.Text);
        Field("ОГРН:", OgrnText.Text);
        Field("Адрес:", OrgAddressText.Text);
        sb.AppendLine(@"\pard\par");

        // ТС
        Line("2. ТРАНСПОРТНОЕ СРЕДСТВО", 18, bold: true);
        Field("Марка, модель:", VehicleBrandText.Text);
        Field("Государственный номер:", VehiclePlateText.Text);
        Field("VIN:", VehicleVinText.Text);
        Field("Вид ТС:", VehicleTypeText.Text);
        Field("Показания одометра (выезд):", OdometerStartText.Text);
        Field("Показания одометра (возврат):", OdometerEndText.Text);
        Field("Вид топлива:", FuelTypeText.Text);
        Field("Горючее при выезде (л):", FuelStartText.Text);
        Field("Выдано горючего (л):", FuelGivenText.Text);
        Field("Остаток при возврате (л):", FuelEndText.Text);
        sb.AppendLine(@"\pard\par");

        // Водитель
        Line("3. ВОДИТЕЛЬ", 18, bold: true);
        Field("ФИО:", DriverNameText.Text);
        Field("Водительское удостоверение:", $"Серия {LicenseSeriesText.Text}  №  {LicenseNumberText.Text}");
        Field("Категории:", LicenseCatText.Text);
        Field("Телефон:", DriverPhoneText.Text);
        sb.AppendLine(@"\pard\par");
        Field("Медицинский допуск:", MedCheckText.Text);
        Field("Техническая проверка ТС:", TechCheckText.Text);
        sb.AppendLine(@"\pard\par");

        // Маршрут
        Line("4. МАРШРУТ ДВИЖЕНИЯ", 18, bold: true);
        Field("Пункт отправления:", PointFromText.Text);
        Field("Пункт назначения:", PointToText.Text);
        Field("Плановое время выезда:", DeparturePlanText.Text);
        Field("Плановое время прибытия:", ArrivalPlanText.Text);
        Field("Фактическое время выезда:", "________________");
        Field("Фактическое время прибытия:", "________________");
        sb.AppendLine(@"\pard\par");

        // Задание
        Line("5. ЗАДАНИЕ ВОДИТЕЛЮ", 18, bold: true);
        Field("№ Заказа:", OrderNumText.Text);
        Field("Грузополучатель:", ClientText.Text);
        Field("Адрес доставки:", PointToText.Text);
        Field("Наименование груза:", CargoText.Text);
        Field("Масса груза:", WeightText.Text);
        Field("Объём груза:", VolumeText.Text);
        sb.AppendLine(@"\pard\par");

        // Подписи
        Line("6. ПОДПИСИ", 18, bold: true);
        sb.AppendLine(@"\pard\par");
        sb.Append(@"\pard\ql\f1\fs18 ");
        sb.AppendLine($@"Диспетчер: ___________________________    Дата: {DateTime.Now:dd.MM.yyyy}\par");
        sb.AppendLine(@"\pard\par");
        sb.Append(@"\pard\ql\f1\fs18 ");
        sb.AppendLine(@"Водитель (подпись об ознакомлении): _________________________\par");
        sb.AppendLine(@"\pard\par");
        sb.Append(@"\pard\qc\f1\fs16\i ");
        sb.AppendLine(@"М.П.\i0\par");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EscapeRtf(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "—";
        var result = new StringBuilder();
        foreach (var c in s)
        {
            if (c > 127)
            {
                result.Append($@"\u{(int)c}?");
            }
            else if (c == '\\') result.Append(@"\\");
            else if (c == '{')  result.Append(@"\{");
            else if (c == '}')  result.Append(@"\}");
            else result.Append(c);
        }
        return result.ToString();
    }

    private Task SaveAsTxtAsync(string path)
    {
        var sb = new StringBuilder();
        var sep = new string('═', 60);
        var sep2 = new string('─', 60);
        sb.AppendLine(sep);
        sb.AppendLine($"                П У Т Е В О Й   Л И С Т");
        sb.AppendLine($"           Серия {_series}  №  {_number:D6}");
        sb.AppendLine($"  Дата выдачи: {IssueDateText.Text}");
        sb.AppendLine(sep);
        sb.AppendLine($"Организация: {OrganizationText.Text}");
        sb.AppendLine($"ИНН/КПП:     {InnText.Text}");
        sb.AppendLine($"ОГРН:        {OgrnText.Text}");
        sb.AppendLine($"Адрес:       {OrgAddressText.Text}");
        sb.AppendLine(sep2);
        sb.AppendLine("ТРАНСПОРТНОЕ СРЕДСТВО");
        sb.AppendLine($"Марка/Модель:      {VehicleBrandText.Text}");
        sb.AppendLine($"Гос. номер:        {VehiclePlateText.Text}");
        sb.AppendLine($"VIN:               {VehicleVinText.Text}");
        sb.AppendLine(sep2);
        sb.AppendLine("ВОДИТЕЛЬ");
        sb.AppendLine($"ФИО:                    {DriverNameText.Text}");
        sb.AppendLine($"Вод. удостоверение:     {LicenseSeriesText.Text} {LicenseNumberText.Text}");
        sb.AppendLine($"Категории:              {LicenseCatText.Text}");
        sb.AppendLine($"Телефон:                {DriverPhoneText.Text}");
        sb.AppendLine($"Медосмотр:              {MedCheckText.Text}");
        sb.AppendLine($"Тех. осмотр ТС:         {TechCheckText.Text}");
        sb.AppendLine(sep2);
        sb.AppendLine("МАРШРУТ");
        sb.AppendLine($"Откуда: {PointFromText.Text}");
        sb.AppendLine($"Куда:   {PointToText.Text}");
        sb.AppendLine($"Выезд (план):   {DeparturePlanText.Text}");
        sb.AppendLine($"Прибытие (план):{ArrivalPlanText.Text}");
        sb.AppendLine(sep2);
        sb.AppendLine("ЗАДАНИЕ");
        sb.AppendLine($"№ Заказа:      {OrderNumText.Text}");
        sb.AppendLine($"Клиент:        {ClientText.Text}");
        sb.AppendLine($"Адрес:         {PointToText.Text}");
        sb.AppendLine($"Груз:          {CargoText.Text}");
        sb.AppendLine($"Вес / Объём:   {WeightText.Text} / {VolumeText.Text}");
        sb.AppendLine(sep2);
        sb.AppendLine($"Диспетчер: ________________________   Дата: {DateTime.Now:dd.MM.yyyy}");
        sb.AppendLine();
        sb.AppendLine("Водитель (подпись): ________________________   М.П.");
        sb.AppendLine(sep);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return Task.CompletedTask;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var pd  = new PrintDialog();
        if (pd.ShowDialog() != true) return;

        var doc = BuildPrintDocument();
        var paginatorSource = (IDocumentPaginatorSource)doc;
        pd.PrintDocument(paginatorSource.DocumentPaginator, $"Путевой лист {_waybillNumber}");
    }

    private FlowDocument BuildPrintDocument()
    {
        var doc = new FlowDocument
        {
            FontFamily  = new FontFamily("Times New Roman"),
            FontSize    = 12,
            PagePadding = new Thickness(72, 50, 72, 50)  // ~2.5cm margins
        };

        Paragraph H(string text, double size = 14, bool bold = false, TextAlignment align = TextAlignment.Left)
        {
            var p = new Paragraph(new Run(text))
            {
                FontSize      = size,
                FontWeight    = bold ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = align,
                Margin        = new Thickness(0, 4, 0, 4)
            };
            return p;
        }

        Paragraph Row(string label, string value)
        {
            var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
            p.Inlines.Add(new Run(label) { FontWeight = FontWeights.Bold });
            p.Inlines.Add(new Run("  " + value));
            return p;
        }

        doc.Blocks.Add(H("ПУТЕВОЙ ЛИСТ", 18, bold: true, align: TextAlignment.Center));
        doc.Blocks.Add(H($"Серия {_series}  №  {_number:D6}", 14, bold: true, align: TextAlignment.Center));
        doc.Blocks.Add(H($"Дата выдачи: {IssueDateText.Text}", align: TextAlignment.Center));
        doc.Blocks.Add(new Paragraph(new Run(new string('_', 80))) { Margin = new Thickness(0, 4, 0, 8) });

        doc.Blocks.Add(H("1. ОРГАНИЗАЦИЯ", 13, bold: true));
        doc.Blocks.Add(Row("Наименование:", OrganizationText.Text));
        doc.Blocks.Add(Row("ИНН/КПП:", InnText.Text));
        doc.Blocks.Add(Row("ОГРН:", OgrnText.Text));
        doc.Blocks.Add(Row("Адрес:", OrgAddressText.Text));

        doc.Blocks.Add(H("2. ТРАНСПОРТНОЕ СРЕДСТВО", 13, bold: true));
        doc.Blocks.Add(Row("Марка, модель:", VehicleBrandText.Text));
        doc.Blocks.Add(Row("Государственный номер:", VehiclePlateText.Text));
        doc.Blocks.Add(Row("VIN:", VehicleVinText.Text));
        doc.Blocks.Add(Row("Вид топлива:", FuelTypeText.Text));
        doc.Blocks.Add(Row("Показания одометра (выезд/возврат):", $"{OdometerStartText.Text} / {OdometerEndText.Text}"));

        doc.Blocks.Add(H("3. ВОДИТЕЛЬ", 13, bold: true));
        doc.Blocks.Add(Row("ФИО:", DriverNameText.Text));
        doc.Blocks.Add(Row("Вод. удостоверение:", $"Серия {LicenseSeriesText.Text}  №  {LicenseNumberText.Text}"));
        doc.Blocks.Add(Row("Категории:", LicenseCatText.Text));
        doc.Blocks.Add(Row("Мед. допуск:", MedCheckText.Text));
        doc.Blocks.Add(Row("Тех. проверка ТС:", TechCheckText.Text));

        doc.Blocks.Add(H("4. МАРШРУТ", 13, bold: true));
        doc.Blocks.Add(Row("Откуда:", PointFromText.Text));
        doc.Blocks.Add(Row("Куда:", PointToText.Text));
        doc.Blocks.Add(Row("Выезд (план):", DeparturePlanText.Text));
        doc.Blocks.Add(Row("Прибытие (план):", ArrivalPlanText.Text));
        doc.Blocks.Add(Row("Выезд (факт):", "_____________"));
        doc.Blocks.Add(Row("Прибытие (факт):", "_____________"));

        doc.Blocks.Add(H("5. ЗАДАНИЕ", 13, bold: true));
        doc.Blocks.Add(Row("№ Заказа:", OrderNumText.Text));
        doc.Blocks.Add(Row("Грузополучатель:", ClientText.Text));
        doc.Blocks.Add(Row("Наименование груза:", CargoText.Text));
        doc.Blocks.Add(Row("Масса / Объём:", $"{WeightText.Text} / {VolumeText.Text}"));

        doc.Blocks.Add(new Paragraph(new Run(new string('_', 80))) { Margin = new Thickness(0, 16, 0, 4) });
        doc.Blocks.Add(Row("Диспетчер:", $"______________________________   {DateTime.Now:dd.MM.yyyy}"));
        doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 8, 0, 0) });
        doc.Blocks.Add(Row("Водитель (подпись):", "______________________________   М.П."));

        return doc;
    }
}
