# DispatcherARM — АРМ диспетчера службы доставки

WPF-приложение (.NET 8) для автоматизации работы диспетчера: заказы, маршруты,
водители, автопарк, финансы и оптимизация маршрутов.

## Быстрый старт

```bash
docker compose -f docker/docker-compose.yml up -d
dotnet run --project src/DispatcherARM.UI
```

Учётная запись администратора создаётся автоматически при первом запуске
(см. `SeedData`). Демо-режим (SQLite, без Docker) включается в
`appsettings.json` / на странице «Настройки».
