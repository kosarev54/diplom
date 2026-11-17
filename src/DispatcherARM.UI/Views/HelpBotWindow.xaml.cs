using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace DispatcherARM.UI.Views;

public class ChatMessage
{
    public string Text    { get; set; } = "";
    public bool   IsBot   { get; set; }
    public bool   IsUser  => !IsBot;
}

public partial class HelpBotWindow : Window
{
    private readonly ObservableCollection<ChatMessage> _messages = new();

    // База ответов бота
    private static readonly Dictionary<string, string> _faq = new(StringComparer.OrdinalIgnoreCase)
    {
        ["order"]    = "📦 Как создать заказ:\n1. Перейдите в раздел «Заказы»\n2. Нажмите «+ Добавить заказ»\n3. Заполните данные клиента, адрес, вес, цену доставки\n4. Нажмите «Сохранить»\nЗаказ появится в списке со статусом «Новый».",
        ["route"]    = "🗺️ Как построить маршрут:\n1. Перейдите в раздел «Маршруты»\n2. Нажмите «Построить маршрут»\n3. Выберите заказы, водителя, ТС и алгоритм\n4. Нажмите «Построить маршрут»\nМаршрут сразу появится на карте!",
        ["money"]    = "💳 Как вывести деньги:\n1. Перейдите в «Мой кошелёк»\n2. Нажмите «Вывести на карту»\n3. Введите сумму и номер карты (16 цифр)\n4. Подтвердите операцию\nСредства поступят за 1-3 рабочих дня.",
        ["roles"]    = "👥 Роли в системе:\n• 👑 Администратор — управление всем, подтверждение регистраций, назначение маршрутов\n• 📋 Диспетчер — создание заказов, построение маршрутов, отчёты\n• 🚗 Водитель — выполнение заказов, просмотр маршрутов, кошелёк",
        ["complete"] = "✅ Как отметить выполнение заказа:\n1. Откройте раздел «Заказы»\n2. Выберите нужный заказ (статус «В пути» или «Назначен»)\n3. Нажмите кнопку «✓ Доставлен»\n4. Укажите способ оплаты и подтвердите\nАдминистратор подтвердит и начислит средства.",
        ["map"]      = "🗺️ Карта:\n• Синие маркеры — новые заказы\n• Оранжевые — назначенные\n• Фиолетовые — в пути\n• Зелёные — выполненные\n• Линии — маршруты (жирные = активные, пунктир = завершённые)\nКликните на маршрут в списке для выделения на карте.",
        ["calc"]     = "🧮 Калькулятор маршрута:\nНажмите кнопку «🧮 Калькулятор» в разделе маршрутов.\nВведите дистанцию, количество доставок и тарифы.\nСистема автоматически посчитает: топливо + работа водителя + накладные расходы.",
        ["pass"]     = "🔐 Забыли пароль?\nОбратитесь к администратору системы для сброса пароля.",
        ["reg"]      = "📝 Регистрация:\nНа экране входа нажмите «Нет аккаунта? Зарегистрироваться».\nЗаполните форму и отправьте заявку.\nПосле одобрения администратором вы сможете войти.",
        ["hi"]       = "👋 Привет! Я АРМ-Помощник. Задайте мне любой вопрос о системе или выберите тему выше!",
        ["help"]     = "Я могу помочь с:\n• Созданием заказов\n• Построением маршрутов\n• Работой с картой\n• Выводом средств\n• Ролями пользователей\n• Регистрацией\n\nПросто спросите!",
    };

    public HelpBotWindow()
    {
        InitializeComponent();
        ChatList.ItemsSource = _messages;
        AddBotMessage("👋 Привет! Я ваш помощник в АРМ Диспетчера.\n\nЯ отвечу на вопросы о работе с системой. Выберите тему выше или задайте свой вопрос!");
    }

    private void AddBotMessage(string text)
    {
        _messages.Add(new ChatMessage { Text = text, IsBot = true });
        ScrollToBottom();
    }

    private void AddUserMessage(string text)
    {
        _messages.Add(new ChatMessage { Text = text, IsBot = false });
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Dispatcher.InvokeAsync(() => ChatScroll.ScrollToBottom(),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Send_Click(object sender, RoutedEventArgs e) => ProcessInput();
    private void Input_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) ProcessInput(); }

    private void ProcessInput()
    {
        var input = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;
        AddUserMessage(input);
        InputBox.Clear();

        // Простой поиск по ключевым словам
        var lower = input.ToLower();
        string? answer = null;

        if      (lower.Contains("заказ") || lower.Contains("order"))
            answer = _faq["order"];
        else if (lower.Contains("маршрут") || lower.Contains("route"))
            answer = _faq["route"];
        // ── P1-13 FIX: «вывод на карту» / «карточку» — про деньги; «карта» отдельно ──
        else if (lower.Contains("деньг") || lower.Contains("вывод") ||
                 lower.Contains("баланс") || lower.Contains("зарплат") ||
                 lower.Contains("кошелёк") || lower.Contains("карточ") ||
                 (lower.Contains("карту") && (lower.Contains("вывод") || lower.Contains("перевод"))))
            answer = _faq["money"];
        else if (lower.Contains("роль") || lower.Contains("пользовател"))
            answer = _faq["roles"];
        else if (lower.Contains("выполн") || lower.Contains("завершить") || lower.Contains("отметить"))
            answer = _faq["complete"];
        // «карта», «map», «карте», «картой» — географическая карта
        else if (lower.Contains("карт") || lower.Contains("map"))
            answer = _faq["map"];
        else if (lower.Contains("калькул"))
            answer = _faq["calc"];
        else if (lower.Contains("пароль"))
            answer = _faq["pass"];
        else if (lower.Contains("регистр"))
            answer = _faq["reg"];
        else if (lower.Contains("привет") || lower.Contains("hi") || lower.Contains("hello"))
            answer = _faq["hi"];
        else if (lower.Contains("помог") || lower.Contains("help"))
            answer = _faq["help"];
        else answer = "🤔 Я не уверен, как ответить на этот вопрос. Попробуйте выбрать тему из кнопок выше.\n\nОбратитесь к администратору системы за помощью.";

        AddBotMessage(answer);
    }

    private void QuickQ_Click(object sender, RoutedEventArgs e)
    {
        var btn = (System.Windows.Controls.Button)sender;
        var tag = btn.Tag?.ToString() ?? "";
        var question = btn.Content?.ToString() ?? "";
        AddUserMessage(question);
        if (_faq.TryGetValue(tag, out var answer))
            AddBotMessage(answer);
    }
}
