namespace DispatcherARM.Domain.Enums;

/// <summary>
/// Источник создания заказа
/// </summary>
public enum OrderSource
{
    /// <summary>Создан вручную диспетчером</summary>
    Manual = 0,

    /// <summary>Импортирован из CSV-файла</summary>
    Csv = 1,

    /// <summary>Получен через веб-форму (localhost)</summary>
    Web = 2,

    /// <summary>Получен из 1С:Предприятие</summary>
    OneC = 3
}
