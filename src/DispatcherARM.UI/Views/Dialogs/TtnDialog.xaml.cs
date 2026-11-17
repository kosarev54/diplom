using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DispatcherARM.Domain.Entities;

namespace DispatcherARM.UI.Views.Dialogs;

/// <summary>Строка ТТН для DataGrid</summary>
public class TtnItem
{
    public int     RowNumber    { get; set; }
    public string  Name        { get; set; } = "";
    public string? ArticleNumber { get; set; }
    public double  Quantity    { get; set; }
    public string  Unit        { get; set; } = "шт";
    public decimal UnitPrice   { get; set; }
    public decimal TotalPrice  => (decimal)Quantity * UnitPrice;
}

/// <summary>
/// Товарно-транспортная накладная (форма 1-Т)
/// Постановление Госкомстата РФ №78 от 28.11.1997
/// </summary>
public partial class TtnDialog : Window
{
    private readonly Order   _order;
    private readonly Driver? _driver;
    private readonly string  _ttnNumber;
    private List<TtnItem>    _items = new();
    private decimal          _totalExVat;

    public TtnDialog(Order order, Driver? driver)
    {
        InitializeComponent();
        _order  = order;
        _driver = driver;
        _ttnNumber = $"ТТН-{DateTime.Now:yyyyMMdd}-{order.Id:D4}";
        FillForm();
    }

    private void FillForm()
    {
        // Шапка
        TtnNumberText.Text = _ttnNumber;
        TtnDateText.Text   = DateTime.Now.ToString("dd.MM.yyyy");

        // Грузоотправитель — наша компания
        ShipperNameText.Text    = "ООО «Транспортно-логистическая компания»";
        ShipperAddressText.Text = "г. Москва, ул. Складская, д. 1, стр. 2";
        ShipperPhoneText.Text   = "+7 (495) 000-00-00";

        // Грузополучатель — клиент из заказа
        ReceiverNameText.Text    = _order.ClientName;
        ReceiverAddressText.Text = _order.Address;
        ReceiverPhoneText.Text   = _order.ClientPhone ?? "—";

        // Перевозчик
        CarrierNameText.Text = "ООО «Транспортно-логистическая компания»";
        DriverNameText.Text  = _driver?.FullName ?? "—";
        VehicleText.Text     = _driver?.AssignedVehicle != null
            ? $"{_driver.AssignedVehicle.Model}  /  {_driver.AssignedVehicle.LicensePlate}"
            : "—";

        // Перевозка
        LoadingPointText.Text   = "г. Москва, ул. Складская, д. 1 (Склад ТЛК)";
        LoadingTimeText.Text    = DateTime.Now.ToString("dd.MM.yyyy  08:00");
        UnloadingPointText.Text = _order.Address;
        UnloadingTimeText.Text  = _order.DeliveryDate.HasValue
            ? _order.DeliveryDate.Value.ToString("dd.MM.yyyy") + "  до 18:00"
            : "по согласованию";

        // Маршрут
        RouteFromText.Text = LoadingPointText.Text;
        RouteToText.Text   = _order.Address;
        WeightText.Text    = $"{_order.Weight:F1} кг";
        DeliveryPriceText.Text = $"{_order.DeliveryPrice:N2} ₽";

        // Товарные позиции
        if (_order.Items != null && _order.Items.Any())
        {
            int row = 1;
            _items = _order.Items.Select(i => new TtnItem
            {
                RowNumber     = row++,
                Name          = i.Name,
                ArticleNumber = i.ArticleNumber,
                Quantity      = i.Quantity,
                Unit          = i.Unit,
                UnitPrice     = i.UnitPrice
            }).ToList();
        }
        else
        {
            // Если товарных позиций нет — создаём обобщённую строку
            _items = new List<TtnItem>
            {
                new()
                {
                    RowNumber     = 1,
                    Name          = string.IsNullOrWhiteSpace(_order.Notes)
                        ? "Сборный груз (согласно заказу)"
                        : _order.Notes,
                    ArticleNumber = _order.OrderNumber,
                    Quantity      = 1,
                    Unit          = "партия",
                    UnitPrice     = _order.DeliveryPrice > 0 ? _order.DeliveryPrice : _order.Weight > 0
                        ? (decimal)_order.Weight * 50m : 1000m
                }
            };
        }

        ItemsGrid.ItemsSource = _items;

        // Итого
        _totalExVat = _items.Sum(i => i.TotalPrice);
        var vat   = Math.Round(_totalExVat * 0.20m, 2);
        var total = _totalExVat + vat;

        TotalExVatText.Text = $"{_totalExVat:N2} ₽";
        VatText.Text        = $"{vat:N2} ₽";
        TotalText.Text      = $"{total:N2} ₽";
        TotalInWordsText.Text = $"Итого наименований {_items.Count}, на сумму {total:N2} руб.";
    }

    // ─── Экспорт в RTF (открывается в Word) ────────────────────────────────────
    private void SaveToWord_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title    = "Сохранить ТТН",
            Filter   = "RTF документ (*.rtf)|*.rtf|Текстовый файл (*.txt)|*.txt",
            FileName = $"ТТН_{_ttnNumber.Replace(":", "-").Replace("/", "-").Replace(" ", "_")}"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            if (dlg.FilterIndex == 1)
                File.WriteAllText(dlg.FileName, BuildRtf(), Encoding.Default);
            else
                File.WriteAllText(dlg.FileName, BuildTxt(), Encoding.UTF8);

            var r = MessageBox.Show($"ТТН сохранена:\n{dlg.FileName}\n\nОткрыть?",
                "Сохранено", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (r == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    private string BuildRtf()
    {
        var sb  = new StringBuilder();
        var ru  = new System.Globalization.CultureInfo("ru-RU");
        var vat = Math.Round(_totalExVat * 0.20m, 2);

        sb.AppendLine(@"{\rtf1\ansi\ansicpg1251\deff0");
        sb.AppendLine(@"{\fonttbl{\f0\froman\fcharset204 Times New Roman;}{\f1\fswiss\fcharset204 Arial;}}");
        sb.AppendLine(@"{\colortbl;\red30\green58\blue95;\red0\green0\blue0;\red107\green123\blue141;}");
        sb.AppendLine(@"\paperw12240\paperh15840\margl1800\margr1440\margt1440\margb1440");

        void L(string text, int size = 20, bool bold = false, bool center = false)
        {
            sb.Append(center ? @"\pard\qc" : @"\pard\ql");
            sb.Append($@"\f1\fs{size}");
            if (bold) sb.Append(@"\b");
            sb.Append($" {E(text)}");
            if (bold) sb.Append(@"\b0");
            sb.AppendLine(@"\par");
        }

        L("ТОВАРНО-ТРАНСПОРТНАЯ НАКЛАДНАЯ", 28, bold: true, center: true);
        L($"Форма 1-Т   |   № {_ttnNumber}   |   Дата: {DateTime.Now:dd.MM.yyyy}", 18, center: true);
        sb.AppendLine(@"\pard\par");

        L("СТОРОНЫ ДОГОВОРА:", 18, bold: true);
        L($"Грузоотправитель: {E(ShipperNameText.Text)}, {E(ShipperAddressText.Text)}", 18);
        L($"Грузополучатель:  {E(ReceiverNameText.Text)}, {E(ReceiverAddressText.Text)}", 18);
        L($"Перевозчик:       {E(CarrierNameText.Text)}", 18);
        L($"Водитель:         {E(DriverNameText.Text)}  ТС: {E(VehicleText.Text)}", 18);
        sb.AppendLine(@"\pard\par");

        L("ТОВАРНЫЙ РАЗДЕЛ:", 18, bold: true);
        // Таблица товаров (RTF table)
        sb.AppendLine(@"\pard\trowd\trgaph108");
        sb.Append(@"\cellx500\cellx4000\cellx5000\cellx5800\cellx6800\cellx7900\cellx9000");
        sb.AppendLine(@"\b\f1\fs18 \pard\intbl №\cell Наименование\cell Артикул\cell Кол-во\cell Ед.\cell Цена\cell Сумма\cell\row\b0");
        int n = 1;
        foreach (var item in _items)
        {
            sb.Append(@"\pard\intbl ");
            sb.Append($@"\f1\fs18 {n++}\cell {E(item.Name)}\cell {E(item.ArticleNumber ?? "—")}\cell");
            sb.Append($@" {item.Quantity:F2}\cell {E(item.Unit)}\cell {item.UnitPrice:N2}\cell {item.TotalPrice:N2}\cell\row");
            sb.AppendLine();
        }
        sb.AppendLine(@"\pard\par");

        L($"Итого без НДС: {_totalExVat:N2} руб.", 18, bold: true);
        L($"НДС (20%):     {vat:N2} руб.", 18);
        L($"ИТОГО с НДС:   {_totalExVat + vat:N2} руб.", 20, bold: true);
        sb.AppendLine(@"\pard\par");

        L("ТРАНСПОРТНЫЙ РАЗДЕЛ:", 18, bold: true);
        L($"Откуда: {E(RouteFromText.Text)}", 18);
        L($"Куда:   {E(RouteToText.Text)}", 18);
        L($"Масса груза: {_order.Weight:F1} кг  |  Стоимость перевозки: {_order.DeliveryPrice:N2} руб.", 18);
        sb.AppendLine(@"\pard\par");

        L("Грузоотправитель: _________________ / _________________   М.П.", 18);
        L("Водитель-экспедитор: _____________ / _________________", 18);
        L("Грузополучатель: _________________ / _________________   М.П.", 18);
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string E(string? s) => string.IsNullOrEmpty(s) ? "—" :
        string.Join("", s.Select(c => c > 127 ? $@"\u{(int)c}?" :
            c == '\\' ? @"\\" : c == '{' ? @"\{" : c == '}' ? @"\}" : c.ToString()));

    private string BuildTxt()
    {
        var sb  = new StringBuilder();
        var sep = new string('─', 70);
        sb.AppendLine(new string('═', 70));
        sb.AppendLine("       ТОВАРНО-ТРАНСПОРТНАЯ НАКЛАДНАЯ (Форма 1-Т)");
        sb.AppendLine($"  № {_ttnNumber}   Дата: {DateTime.Now:dd.MM.yyyy}");
        sb.AppendLine(new string('═', 70));
        sb.AppendLine($"Грузоотправитель: {ShipperNameText.Text}");
        sb.AppendLine($"Адрес:            {ShipperAddressText.Text}");
        sb.AppendLine(sep);
        sb.AppendLine($"Грузополучатель:  {ReceiverNameText.Text}");
        sb.AppendLine($"Адрес:            {ReceiverAddressText.Text}");
        sb.AppendLine($"Телефон:          {ReceiverPhoneText.Text}");
        sb.AppendLine(sep);
        sb.AppendLine($"Перевозчик:       {CarrierNameText.Text}");
        sb.AppendLine($"Водитель:         {DriverNameText.Text}");
        sb.AppendLine($"ТС / Гос. номер:  {VehicleText.Text}");
        sb.AppendLine(sep);
        sb.AppendLine("ТОВАРНЫЙ РАЗДЕЛ:");
        sb.AppendLine($"{"№",-3} {"Наименование",-35} {"Арт.",-12} {"Кол-во",-8} {"Ед.",-5} {"Цена",-10} {"Сумма",-10}");
        sb.AppendLine(new string('-', 85));
        foreach (var item in _items)
            sb.AppendLine($"{item.RowNumber,-3} {item.Name,-35} {item.ArticleNumber ?? "—",-12} {item.Quantity,-8:F2} {item.Unit,-5} {item.UnitPrice,-10:N2} {item.TotalPrice,-10:N2}");
        sb.AppendLine(sep);
        var vat = Math.Round(_totalExVat * 0.20m, 2);
        sb.AppendLine($"Итого без НДС:    {_totalExVat:N2} руб.");
        sb.AppendLine($"НДС (20%):        {vat:N2} руб.");
        sb.AppendLine($"ИТОГО с НДС:      {_totalExVat + vat:N2} руб.");
        sb.AppendLine(sep);
        sb.AppendLine($"Откуда: {RouteFromText.Text}");
        sb.AppendLine($"Куда:   {RouteToText.Text}");
        sb.AppendLine($"Масса груза: {_order.Weight:F1} кг   Стоимость перевозки: {_order.DeliveryPrice:N2} руб.");
        sb.AppendLine(new string('═', 70));
        sb.AppendLine("Грузоотправитель: ___________________    М.П.");
        sb.AppendLine("Водитель-экспедитор: _________________");
        sb.AppendLine("Грузополучатель: ____________________    М.П.");
        return sb.ToString();
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var pd = new PrintDialog();
        if (pd.ShowDialog() != true) return;

        var doc = new FlowDocument
        {
            FontFamily  = new FontFamily("Times New Roman"),
            FontSize    = 11,
            PagePadding = new Thickness(60, 40, 60, 40)
        };

        Paragraph H(string t, bool bold = false, TextAlignment align = TextAlignment.Left, double size = 12)
            => new Paragraph(new Run(t)) { FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
               TextAlignment = align, FontSize = size, Margin = new Thickness(0, 3, 0, 3) };

        doc.Blocks.Add(H("ТОВАРНО-ТРАНСПОРТНАЯ НАКЛАДНАЯ  (Форма 1-Т)", bold: true, align: TextAlignment.Center, size: 15));
        doc.Blocks.Add(H($"№ {_ttnNumber}   от {DateTime.Now:dd.MM.yyyy}", align: TextAlignment.Center));
        doc.Blocks.Add(new Paragraph(new Run(new string('_', 90))) { Margin = new Thickness(0, 6, 0, 6) });

        doc.Blocks.Add(H($"Грузоотправитель: {ShipperNameText.Text},  {ShipperAddressText.Text}"));
        doc.Blocks.Add(H($"Грузополучатель:  {ReceiverNameText.Text},  {ReceiverAddressText.Text}"));
        doc.Blocks.Add(H($"Перевозчик:       {CarrierNameText.Text}"));
        doc.Blocks.Add(H($"Водитель:         {DriverNameText.Text}   ТС: {VehicleText.Text}"));
        doc.Blocks.Add(new Paragraph(new Run(new string('_', 90))) { Margin = new Thickness(0, 4, 0, 4) });

        var tbl = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
        tbl.Columns.Add(new TableColumn { Width = new GridLength(30) });
        tbl.Columns.Add(new TableColumn { Width = new GridLength(200) });
        tbl.Columns.Add(new TableColumn { Width = new GridLength(70) });
        tbl.Columns.Add(new TableColumn { Width = new GridLength(55) });
        tbl.Columns.Add(new TableColumn { Width = new GridLength(40) });
        tbl.Columns.Add(new TableColumn { Width = new GridLength(65) });
        tbl.Columns.Add(new TableColumn { Width = new GridLength(70) });

        var rowGroup = new TableRowGroup();
        TableRow MakeRow(params string[] cells)
        {
            var row = new TableRow();
            foreach (var c in cells)
            {
                var cell = new TableCell(new Paragraph(new Run(c)))
                    { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) };
                row.Cells.Add(cell);
            }
            return row;
        }
        rowGroup.Rows.Add(MakeRow("№", "Наименование", "Артикул", "Кол-во", "Ед.", "Цена", "Сумма"));
        foreach (var item in _items)
            rowGroup.Rows.Add(MakeRow(item.RowNumber.ToString(), item.Name, item.ArticleNumber ?? "—",
                item.Quantity.ToString("F2"), item.Unit, item.UnitPrice.ToString("N2"), item.TotalPrice.ToString("N2")));
        tbl.RowGroups.Add(rowGroup);
        doc.Blocks.Add(tbl);

        var vat = Math.Round(_totalExVat * 0.20m, 2);
        doc.Blocks.Add(new Paragraph(new Run(new string('_', 90))) { Margin = new Thickness(0, 4, 0, 2) });
        doc.Blocks.Add(H($"Итого без НДС: {_totalExVat:N2} руб.   НДС (20%): {vat:N2} руб.   ИТОГО: {_totalExVat + vat:N2} руб.", bold: true));
        doc.Blocks.Add(new Paragraph(new Run(new string('_', 90))) { Margin = new Thickness(0, 6, 0, 4) });
        doc.Blocks.Add(H("Грузоотправитель: ___________________ / ___________________   М.П."));
        doc.Blocks.Add(H("Водитель-экспедитор: _________________ / ___________________"));
        doc.Blocks.Add(H("Грузополучатель: ____________________ / ___________________   М.П."));

        pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"ТТН {_ttnNumber}");
    }
}
