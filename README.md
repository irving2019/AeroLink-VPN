# ✈️ AeroLink VPN

![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![Linux](https://img.shields.io/badge/Linux-FCC624?style=for-the-badge&logo=linux&logoColor=black)
![.NET](https://img.shields.io/badge/.NET%2010.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Avalonia UI](https://img.shields.io/badge/Avalonia%20UI-171717?style=for-the-badge&logo=avalonia&logoColor=white)
![Go](https://img.shields.io/badge/Go-00ADD8?style=for-the-badge&logo=go&logoColor=white)

**AeroLink VPN** — это современный, быстрый и кроссплатформенный VPN-клиент с графическим интерфейсом, созданный для надежного обхода глубокого анализа трафика (DPI) и систем блокировок (включая ТСПУ). 

Клиент объединяет под капотом мощь модифицированного протокола **AmneziaWG** и **Xray (VLESS)**, предоставляя пользователю удобный интерфейс без необходимости копаться в консоли.

---

## ✨ Ключевые возможности

- 🛡️ **Продвинутая обфускация:** Полная поддержка новейших параметров обфускации AmneziaWG (Jc, Jmin, Jmax, S1, S2, H1-H4), скрывающих WireGuard трафик от провайдеров.
- ⚡ **Поддержка VLESS/Xray:** Встроенный парсер ссылок (формата `vpn://` / `vless://`) и управление Xray-ядром.
- 🌐 **Умный DNS-резолвинг:** Встроенная в Go-ядро система разрешения доменных имен для конечных точек (Endpoints), предотвращающая сбои при нестандартных конфигурациях серверов.
- 🖥️ **Современный UI:** Красивый и отзывчивый интерфейс, построенный на базе фреймворка [Avalonia UI](https://avaloniaui.net/).
- 🪟🐧 **Кроссплатформенность:** Полноценная поддержка Windows и Linux (поддержка macOS и Android находится в стадии разработки).

---

## 🚀 Установка и запуск

### Для Windows
1. Перейдите в раздел [Releases](../../releases) и скачайте актуальный архив `AeroLink-Windows-v1.0.0.zip`.
2. Распакуйте архив в удобное для вас место.
3. Запустите `AeroLink.exe`.
> **Важно:** Для корректного создания виртуального сетевого интерфейса (Wintun) приложению необходимы **права Администратора**.

### Для Linux
1. Скачайте пакет `aerolink-linux-v1.0.0.deb` из раздела [Releases](../../releases).
2. Установите его через ваш пакетный менеджер или терминал:
   ```bash
   sudo dpkg -i aerolink-linux-v1.0.0.deb
   sudo apt-get install -f # Если потребуются зависимости
3.Запустите приложение из меню приложений или через консоль.

Важно: Ядру VPN требуются права суперпользователя (root) для маршрутизации трафика и работы с TUN-интерфейсом.

🛠️ Сборка из исходников (Для разработчиков)
Если вы хотите собрать проект самостоятельно, вам понадобятся:

.NET 10.0 SDK

Go 1.21+ (для компиляции кастомного ядра)

Шаг 1: Сборка Go-ядра
В приложении используется кастомная обертка над amneziawg-go.

Bash
cd aerolinkwg-go
go mod tidy
go build -o ../AeroLink/Core/amneziawg.exe main.go # Для Windows
# или
go build -o ../AeroLink/Core/amneziawg-linux main.go # Для Linux
Шаг 2: Сборка UI (Avalonia)
Bash
cd ../AeroLink
dotnet publish -c Release -r win-x64 --self-contained true # Для Windows
# или
dotnet publish -c Release -r linux-x64 --self-contained true # Для Linux
Убедитесь, что бинарные файлы ядер (amneziawg, xray, wintun.dll и файлы гео-данных) находятся в папке Core рядом с исполняемым файлом UI.

🚧 Известные ограничения (WIP)
Android / macOS: Мобильная и яблочная версии приложения в данный момент находятся в активной разработке. Присутствуют нюансы работы с системными API для поднятия TUN-интерфейса.

Требование Root/Admin: На текущем этапе архитектура ядер требует повышенных привилегий при каждом запуске туннеля.

👨‍💻 Автор
Vladimir Pilipenko — C#/.NET Developer

Разработка архитектуры, интеграция Go-ядра (JNI/C-Shared) и построение интерфейса на Avalonia.
