# ConSecOrg — Защищённый электронный органайзер

**Дипломный проект** | Максутов Р.Ф. | КИ22-02/2Б | СФУ ИКИТ  
**Руководитель**: Туговиков В.Б.  
**Тема**: Разработка защищённого электронного органайзера  
**ТЗ-источник**: `2_МДКП_МаксутовРФ.docx`

---

## Описание проекта

Десктопное приложение на C#/.NET 8 с двумя режимами работы:

- **Персональный** — локальное хранение данных (SQL Server LocalDB), без аутентификации, PIN-защита
- **Корпоративный** — клиент-сервер, ASP.NET Core Web API, полная RBAC, аудит, JWT, SignalR

Все данные шифруются по ГОСТ Р 34.12-2015 «Кузнечик» (CTR mode), хэши и HMAC — ГОСТ Р 34.11-2012 «Стрибог-256». Привязка к устройству, таймер гарантированного уничтожения записей.

---

## Технологический стек

| Слой           | Технологии |
|----------------|-----------|
| Язык           | C# 12, .NET 8 |
| UI (клиент)    | WPF + Material Design Themes 5.x + MahApps.Metro 2.x |
| MVVM           | CommunityToolkit.Mvvm 8.x |
| Сервер         | ASP.NET Core Web API 8.x |
| БД             | Microsoft SQL Server + LocalDB (персональный режим) |
| ORM            | Entity Framework Core 8.x |
| CQRS           | MediatR 12.x |
| Валидация      | FluentValidation 11.x |
| Маппинг        | AutoMapper 13.x |
| Криптография   | BouncyCastle.Cryptography 2.x (ГОСТ) |
| Аутентификация | JWT Bearer (JWT 7.x) |
| Логирование    | Serilog + Serilog.Sinks.MSSqlServer |
| Real-time      | SignalR (Kanban синхронизация) |
| Markdown       | Markdig 0.37 + MdXaml 1.x |
| HTTP клиент    | RestSharp 110.x + Polly 8.x |
| Трей           | H.NotifyIcon.Wpf 2.x |
| Тесты          | xUnit 2.x + Moq 4.x + FluentAssertions 6.x |

---

## Архитектура (Clean Architecture)

```
┌─────────────────────────────────────────────────────────┐
│                    ConSecOrg.Client (WPF)                │
│           Views ← ViewModels → Services                 │
│        ThemeEngine | NavigationService | SessionService  │
└────────────────────────┬────────────────────────────────┘
                         │ HTTP / LocalDB
┌────────────────────────▼────────────────────────────────┐
│                  ConSecOrg.Server (ASP.NET Core)         │
│   Controllers → Application → Domain → Infrastructure   │
│      JWT Auth | Rate Limiting | SignalR | Serilog        │
└────────────────────────┬────────────────────────────────┘
                         │ EF Core
┌────────────────────────▼────────────────────────────────┐
│              Microsoft SQL Server / LocalDB              │
│   users | notes | tasks | contacts | audit_logs | ...   │
└─────────────────────────────────────────────────────────┘
```

### Слои (Clean Architecture)

```
ConSecOrg.Domain        — Entities, ValueObjects, Interfaces, Exceptions
ConSecOrg.Application   — CQRS Commands/Queries, Validators, Behaviors
ConSecOrg.Infrastructure— EF Core, Repositories, GOST Crypto, JWT, Device
ConSecOrg.Server        — ASP.NET Core API, Controllers, Middleware, SignalR
ConSecOrg.Client        — WPF, ViewModels, Views, ThemeEngine, Services
ConSecOrg.Shared        — DTOs, Enums, Constants, Pagination
```

---

## RBAC Роли

| Роль     | Права |
|----------|-------|
| User     | CRUD своих заметок/задач/контактов |
| Manager  | + чтение данных команды, управление задачами |
| Auditor  | Чтение всего + доступ к логам аудита |
| Admin    | Полный доступ + управление пользователями |

---

## Криптография (ГОСТ)

| Операция       | Алгоритм |
|----------------|----------|
| Шифрование     | ГОСТ Р 34.12-2015 «Кузнечик» CTR, ключ 256 бит |
| Хэширование    | ГОСТ Р 34.11-2012 «Стрибог-256» |
| HMAC           | HMAC-Стрибог-256 (проверка целостности) |
| KDF            | PBKDF2-HMAC-Стрибог-256, 100 000 итераций |
| Device binding | Стрибог-256(CPU+Disk+BIOS serial) |
| Хранение паролей | PBKDF2, хранится только verifier[0..31] |

**Важно**: сначала HMAC-проверка, потом расшифровка. При несовпадении HMAC — `IntegrityViolationException`, данные не расшифровываются.

---

## База данных

**Корпоративный режим**: `Server=localhost;Database=ConSecOrg;Trusted_Connection=True;`  
**Персональный режим**: `Server=(localdb)\MSSQLLocalDB;Database=ConSecOrg_Personal;`

### Таблицы
- `roles` — роли и JSON-права
- `users` — пользователи, хэш пароля, device_id, счётчик попыток
- `user_settings` — тема, цвет акцента, шрифт, layout JSON
- `categories` — категории заметок (цвет, иконка)
- `notes` — зашифрованное содержимое (VARBINARY), nonce, HMAC, уровень безопасности, таймер
- `tags` + `note_tags` — теги
- `task_items` — задачи Kanban (зашифрованное описание, колонка, позиция)
- `contacts` — контакты (все поля зашифрованы)
- `sessions` — JWT сессии (хэш refresh-токена)
- `audit_logs` — аудит с hash-chain (Стрибог-256 цепочка)

---

## API Endpoints (prefix: /api/v1)

```
POST   /auth/login          POST   /auth/logout
POST   /auth/register       POST   /auth/refresh
POST   /auth/change-password GET   /auth/me

GET/POST         /notes              GET    /notes/expiring
GET/PUT/DELETE   /notes/{id}         POST   /notes/{id}/timer
GET              /notes/search

GET/POST         /tasks              PATCH  /tasks/{id}/move
GET/PUT/DELETE   /tasks/{id}         GET    /tasks/due

GET/POST         /contacts           GET    /contacts/search
GET/PUT/DELETE   /contacts/{id}

GET              /users              POST   /users/{id}/lock
GET/DELETE       /users/{id}         POST   /users/{id}/unlock
PUT              /users/{id}/settings POST  /users/{id}/role

GET              /audit              GET    /audit/verify
GET              /audit/export

GET              /dashboard/security
```

**SignalR**: `/hubs/board` — события `TaskMoved`, `NoteExpiring`

---

## Статус реализации

### Фаза 1: Solution + Domain Layer ✅
- [x] Создан CLAUDE.md
- [x] Создана структура .NET Solution (ConSecOrg.slnx + 10 проектов)
- [x] ConSecOrg.Domain — BaseEntity, AuditableEntity, ValueObject
- [x] ConSecOrg.Domain — Entities (User, Note, TaskItem, Contact, Category, Tag, AuditLog, Session, Role, UserSettings)
- [x] ConSecOrg.Domain — ValueObjects (EncryptedContent, SecurityLevel, DeviceFingerprint, PasswordHash, HashChainEntry)
- [x] ConSecOrg.Domain — Enumerations (UserRole, TaskStatus, TaskPriority, AuditAction)
- [x] ConSecOrg.Domain — Interfaces (Repositories, Services, ICurrentUserContext)
- [x] ConSecOrg.Domain — Exceptions (DomainException, IntegrityViolationException, NoteExpiredException, DeviceMismatchException)

### Фаза 2: Криптография (ГОСТ) ✅
- [x] BouncyCastle NuGet подключён к Infrastructure
- [x] KuznechikEngine.cs — чистая реализация ГОСТ Р 34.12-2015 на C# (без зависимости BouncyCastle)
- [x] StreebogHasher.cs — Gost3411_2012_256Digest + 512Digest (BouncyCastle)
- [x] HmacStreebog.cs — HMAC поверх Стрибог-256
- [x] KdfService.cs — PBKDF2-Стрибог-256, 100 000 итераций
- [x] SecureBytes.cs — pinned memory, обнуление на Dispose
- [x] GostCryptoService.cs — полная реализация ICryptoService (Encrypt-then-MAC)
- [x] WindowsDeviceService.cs — WMI fingerprint (CPU+Disk+BIOS → Стрибог-256)
- [x] Тесты по официальным тест-векторам ГОСТ (Фаза 10)

### Фаза 3: Infrastructure — БД и репозитории ✅
- [x] AppDbContext.cs + SaveChangesAsync аудит-перехват
- [x] Все IEntityTypeConfiguration (OwnsOne для EncryptedContent)
- [x] Репозитории (User, Note, Task, Contact, AuditLog)
- [x] UnitOfWork.cs
- [x] HashChainService.cs
- [x] JwtTokenService.cs, PasswordHasher.cs
- [x] EF Core начальная миграция (20260423201119_Initial)
- [ ] Seed данные (роли) — через `dotnet ef database update`

### Фаза 4: Application Layer (CQRS) ✅
- [x] Pipeline Behaviors (Validation, Logging)
- [x] Auth feature: Login, Logout, Register, RefreshToken, ChangePassword, GetCurrentUser
- [x] Notes feature: Create, Update, Delete, SetTimer, GetNotes, Search
- [x] Tasks feature: Create, Update, Move, Delete, GetByBoard, GetDue
- [x] Contacts feature: CRUD
- [x] Users feature: Settings, Lock/Unlock, AssignRole, GetUsers, GetSessions
- [x] Audit feature: GetLogs, VerifyChain
- [x] Dashboard feature: GetSecurityDashboard
- [x] IEncryptionKeyStore + ICurrentUserContext для ключей шифрования

### Фаза 5: ASP.NET Core Server ✅
- [x] Program.cs — полная конфигурация (JWT, Serilog, built-in OpenAPI, SignalR, RateLimit)
- [x] Все Controllers (Auth, Notes, Tasks, Contacts, Users, Audit, Dashboard)
- [x] BoardHub.cs (SignalR) — TaskMoved, JoinBoard, LeaveBoard
- [x] Middleware (ExceptionHandling, RequestLogging, DeviceValidation)
- [x] appsettings.json структура + user-secrets для JWT Secret
- [ ] Server integration tests (Фаза 10)

### Фаза 6: WPF Client — архитектура ✅
- [x] App.xaml.cs — IHostBuilder + DI + ThemeEngine инициализация
- [x] ThemeEngine.cs — динамический ResourceDictionary (Light/Dark/Custom, HSL)
- [x] NavigationService.cs — stack-based navigation для внутренних страниц Shell
- [x] NotificationService.cs — event-based уведомления
- [x] ModeService.cs — Personal/Corporate переключение
- [x] SessionService.cs — JWT lifecycle, secure key storage
- [x] ApiClient.cs — RestSharp, реализует все 6 API-интерфейсов
- [x] AppViewModel.cs — управление сценами ModeSelection/Login/Shell
- [x] MainWindow.xaml — ContentControl + DataTemplates
- [x] ModeSelectionView.xaml (две карточки: Personal/Corporate)
- [x] LoginView.xaml (форма с PasswordBox)

### Фаза 7: UI модули ✅
- [x] Notes: NotesListView (карточки + поиск + hover-эффекты) + NoteEditorView (двухпанельный Markdown + MdXaml)
- [x] Tasks: KanbanBoardView (4 колонки, YouGile-стиль, быстрое добавление задач в каждой колонке)
- [x] Calendar: CalendarView — месячный вид, выбор дня, подсветка сегодня и выходных
- [x] Contacts: ContactsView (master-detail, inline редактирование)
- [x] Dashboard: SecurityDashboardView (тайлы, уровни безопасности, аудит-лента)
- [x] Audit: AuditLogView (DataGrid + пагинация + кнопка Verify)
- [x] Settings: SettingsView — тема + акцент + фоновое изображение с opacity + размер шрифта

### Редизайн UI (2026-04-24) ✅
- [x] Современный Sidebar (240px, скруглённые кнопки, active-state подсветка с акцентом, аватар)
- [x] В персональном режиме скрыты: Контакты, Безопасность, Аудит (только Notes/Tasks/Calendar/Settings)
- [x] ThemeEngine — поддержка фонового изображения + opacity overlay
- [x] Generic.xaml — новые стили: FlatIconButton, KanbanColumn, DropShadow на карточках
- [x] Конвертеры: FirstLetterConverter, StringEqualityConverter
- [x] NoteEditorView — таймер уничтожения переработан (toggle + DatePicker + HH:MM, работает при создании и редактировании)
- [x] KanbanBoardView — YouGile-стиль: dots-индикаторы, бейджи с количеством, поле быстрого добавления
- [x] NotesListView — hover-подсветка рамки акцентом, иконка таймера на карточках с ExpiresAt
- [x] CalendarViewModel + CalendarView — новый модуль

### Исправленные баги (2026-04-24)
- [x] **Создание заметки**: убран `NotEmpty()` у Content в `CreateNoteCommandValidator` — заметки с пустым телом теперь сохраняются
- [x] **Ключ шифрования**: `CurrentUserContext.GetEncryptionKey()` возвращал `key[..32]` вместо полных 64 байт — исправлено
- [x] **KDF "Streebog not recognised"**: `Pkcs5S2ParametersGenerator.GenerateDerivedParameters("Streebog")` не работал в BouncyCastle 2.x — заменено собственной реализацией PBKDF2-HMAC-Стрибог-256
- [x] **MaterialDesign ресурс**: `MaterialDesignTheme.Defaults.xaml` переименован в `MaterialDesign2.Defaults.xaml` в MD 5.x — исправлено в App.xaml

### Фаза 8: Персональный режим ✅
- [x] PersonalDbContext.cs — EF Core → `(localdb)\MSSQLLocalDB`, база ConSecOrg_Personal, EnsureCreated
- [x] LocalNotesService.cs — реализует INotesApiService с ГОСТ-шифрованием + SecureDelete
- [x] LocalTasksService.cs — реализует ITasksApiService с зашифрованным описанием
- [x] NotesServiceProxy / TasksServiceProxy — делегирование к API или Local на основе ModeService
- [x] LocalUserStore.cs — PIN-хранилище в %AppData%\ConSecOrg\local_config.json (userId + salt + verifier)
- [x] PinViewModel.cs — логика создания/проверки PIN + PBKDF2-ключ в SessionService
- [x] PinView.xaml — экран ввода PIN (create / verify + сброс)
- [x] SessionService.SetPersonalSession() — синтетическая сессия для personal mode
- [x] Logout в personal mode → ShowPin() (не ShowLogin())

### Фаза 9: Фоновые сервисы ✅
- [x] DestructionTimerService.cs — PeriodicTimer 60 сек, SecureDelete при истечении
- [x] H.NotifyIcon.Wpf — иконка в трее, сворачивание в трей (Window_Closing), контекстное меню (Открыть / Заблокировать / Выход)
- [x] App.xaml.cs — TryGetService<T>() для доступа к DI из MainWindow.xaml.cs
- [x] DestructionTimerWidget.xaml — круговой счётчик (green→orange→red) — реализован как UserControl с Canvas + ArcSegment + DispatcherTimer

### Улучшения UI/UX (2026-04-24 сессия 2) ✅
- [x] **Тёмная тема — читаемость**: ThemeEngine добавлен `NavActiveForeground` (белый в dark, тёмный в light); Generic.xaml + ShellView.xaml обновлены — исправлен чёрный текст на тёмном фоне
- [x] **Hover анимации**: `SidebarNavButton` — плавная анимация фона через `ColorAnimation` на именованном `SolidColorBrush`
- [x] **SidebarNavButtonActive**: левая акцентная полоса (`BorderThickness="2,0,0,0"`) + `AccentAlpha2Brush` фон
- [x] **Settings краш**: `Image.Source` пустой строки → `ImagePathConverter` (безопасная конвертация с try/catch), `AccentHex` инициализируется из ThemeEngine.Instance.AccentColor
- [x] **Notes сортировка**: toolbar с пилл-кнопками (Новые / Старые / A–Я / Уровень), мгновенная сортировка без перезагрузки
- [x] **Notes виды**: toggle между карточками (WrapPanel) и списком (compact rows)
- [x] **Calendar — создание заметок**: клик на любой день → NoteEditorViewModel с заголовком «Заметка на {день} {месяц}»
- [x] **Calendar — точки заметок**: `NoteCount` на `CalendarDay`, загрузка заметок и группировка по дате, отображение dot + count под числом
- [x] **Регистрация**: RegisterView + RegisterViewModel + DataTemplate в MainWindow.xaml — уже были реализованы; LoginView содержит кнопку «Нет аккаунта? Зарегистрироваться»
- [x] **Стили Generic.xaml**: добавлены `NoteCard`, `PillButton`, `PillButtonActive`, `ChipBadge`; обновлён `ModeCard` с hover-эффектом
- [x] **Исправлены build warnings**: убраны неиспользуемые параметры `session` (SettingsViewModel) и `notifications` (LoginViewModel)

### UX улучшения (2026-04-26 сессия 2) ✅
- [x] **ModeSelectionView**: полный редизайн — читаемый текст (TextElement.Foreground), цветные карточки, ГОСТ бейдж снизу
- [x] **Drag-and-drop колонок Kanban**: WPF DragDrop API в KanbanBoardView.xaml.cs + PersistColumnOrderCommand
- [x] **Удаление суб-досок**: DeleteBoardCommand в ShellViewModel + подтверждение через MessageBox.Show; кнопка удаления в ShellView.xaml
- [x] **Подтверждение удаления везде**: DeleteColumn, DeleteTask (KanbanBoardViewModel), DeleteNoteAsync (NotesListViewModel) — все с MessageBox.Show YesNo
- [x] **TaskDetailViewModel + TaskDetailDialog**: просмотр полной инфо задачи при клике (приоритет/дедлайн/теги/описание), EditCommand, CloseCommand
- [x] **SettingsView редизайн**: вкладки «Внешний вид» (theme toggle, акцент) + «Профиль» (аватар, выход, ГОСТ инфо)
- [x] **SettingsViewModel**: добавлены SetThemeCommand, LogoutCommand, DisplayName, ModeName, IsCorporate; DI через конструктор
- [x] **Calendar — неделя вид**: WeekDayViewModel, WeekTaskItem, BuildWeekDays, LoadWeekTasksAsync, переключатель Месяц/Неделя, PrevWeek/NextWeek
- [x] **Notes — фильтры**: SecurityLevelFilter (4 цветных пилля), FilterHasTimer, ClearFilters; фильтр-бар под сортировкой в NotesListView

### Фаза 10: Тесты ✅
- [x] KuznechikEngine тесты — официальный тест-вектор ГОСТ Р 34.12-2015 (Encrypt + Decrypt + CTR round-trip)
- [x] GostCryptoService тесты — round-trip, двойное шифрование с разным nonce, tamper ciphertext/nonce/hmac → IntegrityViolationException
- [x] HashChainService тесты — valid chain 5 записей, tamper CurrentHash/PreviousHash/Action поле, reversed order
- [x] LoginCommandHandler тесты — valid credentials, unknown user, locked account, wrong password, increment failures, 5th failure locks + AccountLocked audit, success resets counter
- [x] **Исправлен баг KuznechikEngine.RInv**: неверная формула LInvTable[i, v]=GfMul(v, L[15-i]) → исправлено на LTable[i, v]=GfMul(v, L[i]), т.к. L[15]=1 (тривиальное деление)
- [ ] NoteRepository.SecureDelete тест [не критично]
- [ ] DeviceValidationMiddleware тест [не критично]
- [ ] API integration tests [не критично]

---

## Принятые архитектурные решения

| Решение | Причина |
|---------|---------|
| WPF + Material Design Themes | Зрелый фреймворк, большая экосистема, MaterialDesignThemes даёт Bitrix24-подобный вид |
| SQL Server LocalDB (персональный) | Та же схема что и корпоративный, не нужен отдельный сервер, поставляется с VS |
| BouncyCastle для ГОСТ | Единственная поддерживаемая .NET библиотека с Кузнечик + Стрибог |
| OwnsOne для EncryptedContent | Три отдельные колонки (encrypted/nonce/hmac) вместо JSON — лучше для индексов и TDE |
| MediatR CQRS | Структурные преимущества без over-engineering, каждый use case в отдельном классе |
| JWT + RefreshToken | Стандарт для корпоративного API, поддержка device validation через claim |
| PBKDF2 100 000 итераций | По требованию ТЗ (ГОСТ), защита от brute-force |
| Hash-chain в audit_logs | Обнаружение вмешательства в логи аудита (Стрибог-256) |
| SignalR для Kanban | Real-time синхронизация доски у всех пользователей корпоративного режима |
| TaskItem (не Task) | `Task` зарезервировано в C# для async, используем `TaskItem` |

---

## Известные ограничения

- Привязка к устройству (device binding) работает только на Windows (WMI)
- Персональный режим требует установленного SQL Server LocalDB (поставляется с Visual Studio)
- ГОСТ-алгоритмы реализованы через BouncyCastle (не нативно в .NET)
- `JwtSettings.Secret` должен быть установлен через `dotnet user-secrets` или переменную окружения — никогда не коммитить в git

---

## Запуск приложения (Windows CMD)

### Предварительные требования

| Компонент | Версия | Установка |
|-----------|--------|-----------|
| .NET SDK | 10.0+ | https://dotnet.microsoft.com/download |
| SQL Server | 2019+ или Express | https://www.microsoft.com/sql-server |
| SQL Server LocalDB | — | Входит в состав Visual Studio 2022 |

Проверить наличие LocalDB — открыть cmd и выполнить:
```cmd
sqllocaldb info
```
Если не установлен — установить через Visual Studio Installer → компонент "SQL Server Express LocalDB".

---

### Режим 1: Персональный (только клиент, без сервера)

Открыть **cmd** (Win+R → `cmd` → Enter):

```cmd
cd e:\ProjectVSCode\ConSecOrg
dotnet run --project src\ConSecOrg.Client
```

**Первый запуск:**
- Выбрать "Персональный" на экране выбора режима
- Придумать PIN-код (мин. 4 символа) и подтвердить
- База данных `ConSecOrg_Personal` создастся в LocalDB автоматически
- Работа полностью офлайн, сервер не нужен

**Повторный запуск:**
- Выбрать "Персональный" → ввести PIN → готово

---

### Режим 2: Корпоративный (клиент + сервер)

Нужно **два окна cmd** — одно для сервера, одно для клиента.

#### Окно 1 — Сервер

```cmd
cd e:\ProjectVSCode\ConSecOrg

REM Первый раз: задать JWT-секрет (выполнить один раз)
cd src\ConSecOrg.Server
dotnet user-secrets set "JwtSettings:Secret" "ConSecOrgSecret2024SuperKey!!"
cd ..\..

REM Первый раз: создать таблицы в SQL Server (выполнить один раз)
dotnet ef database update --project src\ConSecOrg.Infrastructure --startup-project src\ConSecOrg.Server

REM Запустить сервер
dotnet run --project src\ConSecOrg.Server
```

Сервер запустится и будет ждать запросов:
- `http://localhost:5205` — основной адрес
- `http://localhost:5205/swagger` — документация API (Swagger UI)

> Строку подключения к БД можно изменить в `src\ConSecOrg.Server\appsettings.json`  
> → `ConnectionStrings` → `DefaultConnection`  
> По умолчанию: `Server=localhost;Database=ConSecOrg;Trusted_Connection=True;`

#### Окно 2 — Клиент

```cmd
cd e:\ProjectVSCode\ConSecOrg

```

**Подключение:**
- Выбрать "Корпоративный"
- URL сервера: `http://localhost:5205`
- Нажать "Подключиться" → войти с учётными данными администратора

---

### Данные для входа (корпоративный режим)

#### Создание первого администратора (выполнить один раз после миграции)

Открыть новое окно cmd пока сервер запущен:

```cmd
curl -X POST http://localhost:5205/api/v1/auth/bootstrap ^
  -H "Content-Type: application/json" ^
  -d "{\"username\":\"admin\",\"email\":\"admin@company.ru\",\"password\":\"Admin1234!@#$\",\"roleId\":\"\"}"
```

Или через **Swagger UI** (`http://localhost:5205/swagger`):
1. Найти `POST /api/v1/auth/bootstrap`
2. Нажать "Try it out"
3. Вставить тело запроса:
```json
{
  "username": "admin",
  "email": "admin@company.ru",
  "password": "Admin1234!@#$",
  "roleId": ""
}
```
4. Нажать Execute — вернёт GUID созданного пользователя

> Эндпоинт работает только один раз — если пользователи уже есть, вернёт 409 Conflict.  
> Пароль должен быть не менее 12 символов.

#### Учётные данные по умолчанию

| Поле | Значение |
|------|----------|
| Логин | `admin` |
| Пароль | `Admin1234!@#$` |
| Роль | Admin (полный доступ) |

#### Создание дополнительных пользователей

После входа администратора — через Swagger или клиент:

```cmd
curl -X POST http://localhost:5205/api/v1/auth/register ^
  -H "Content-Type: application/json" ^
  -H "Authorization: Bearer ВАШ_JWT_ТОКЕН" ^
  -d "{\"username\":\"user1\",\"email\":\"user1@company.ru\",\"password\":\"User1234!@#$\",\"roleId\":\"44444444-4444-4444-4444-444444444444\"}"
```

ID ролей (фиксированные, заданы в миграции):

| Роль | ID |
|------|----|
| Admin | `11111111-1111-1111-1111-111111111111` |
| Manager | `22222222-2222-2222-2222-222222222222` |
| Auditor | `33333333-3333-3333-3333-333333333333` |
| User | `44444444-4444-4444-4444-444444444444` |

---

### Сборка и тесты

```cmd
cd e:\ProjectVSCode\ConSecOrg

REM Собрать весь solution
dotnet build ConSecOrg.slnx

REM Запустить все тесты
dotnet test ConSecOrg.slnx

REM Только крипто-тесты (ГОСТ тест-векторы)
dotnet test tests\ConSecOrg.Infrastructure.Tests

REM Только API-тесты
dotnet test tests\ConSecOrg.Server.Tests
```

---

### Запуск из Visual Studio 2022

1. Открыть `ConSecOrg.slnx` (двойной клик)
2. **Персональный режим** — в Solution Explorer правой кнопкой на `ConSecOrg.Client` → "Set as Startup Project" → F5
3. **Корпоративный режим** — правой кнопкой на Solution → "Configure Startup Projects":
   - `ConSecOrg.Server` → Action: **Start**
   - `ConSecOrg.Client` → Action: **Start**
   - Нажать OK → F5 (запустятся оба)

---

## Последние действия

- **2026-04-23** — Создан CLAUDE.md, план проекта, изучен МДКП документ
- **2026-04-23** — Фазы 1–7 реализованы: Domain, Infrastructure (ГОСТ криптография), Application (CQRS), Server (ASP.NET Core), WPF Client (все Views + ViewModels, ThemeEngine, DI)
- **2026-04-23** — EF Core миграция Initial создана, БД создана в SQL Server RAMPA, первый admin создан
- **2026-04-24** — Исправлены критические баги: PBKDF2-Стрибог-256, ключ шифрования 64 байт, создание заметок
- **2026-04-24** — Редизайн WPF UI: современный Sidebar, Calendar view, фоновые изображения, таймер уничтожения, YouGile-стиль Kanban
- **2026-04-24** — Фаза 8 реализована: PersonalDbContext (LocalDB), LocalNotesService/LocalTasksService, proxy-паттерн, LocalUserStore (PIN), PinView/PinViewModel, SessionService.SetPersonalSession
- **2026-04-24 (сессия 2)** — Фазы 9–10 + тесты: иконка в трее (H.NotifyIcon), DestructionTimerService, KuznechikEngine.RInv fix, 41 тест (ГОСТ тест-векторы, HashChain, Login handler)
- **2026-04-24 (сессия 2)** — UX улучшения: читаемость dark theme (NavActiveForeground), hover анимации, Settings краш исправлен (ImagePathConverter), Notes сортировка + переключение вида, Calendar → создание заметок + точки активности, регистрация (уже реализована)
- **2026-04-24** — Фаза 9 реализована: H.NotifyIcon.Wpf интегрирован в MainWindow (иконка в трее, сворачивание вместо закрытия, контекстное меню)
- **2026-04-24** — Фаза 10 реализована: 41 тест (KuznechikEngine 7, GostCryptoService 12, HashChainService 8, LoginCommandHandler 10 + 4 из UnitTest1), все проходят. Исправлен баг в KuznechikEngine.RInv (неверные коэффициенты LInvTable → LTable)
- **2026-04-25** — YouGile-стиль Kanban редизайн: динамические колонки (создать/переименовать/удалить), ProjectService с ColumnModel+StatusKey, KanbanColumnViewModel, KanbanBoardViewModel переработан, KanbanBoardView — solid colored headers с белым текстом + ElementName bindings, HexColorToBrushConverter. NoteMetaService + теги/приоритет в NoteEditorView.
- **2026-04-26** — Детальный план YouGile pixel-perfect редизайна записан в CLAUDE.md: анализ 6 скринов, открытые колонки, контекстное меню с палитрой 12 цветов, стикеры приоритета, TaskEditDialog, круглые чекбоксы, сайдбар. Порядок реализации задокументирован.
- **2026-04-26 (сессия 2)** — UX редизайн по скринам YouGile: 1) ModeSelectionView полностью переработан (читаемость, цветные карточки, ГОСТ бейдж); 2) Drag-and-drop колонок Kanban (WPF DragDrop в code-behind KanbanBoardView); 3) Удаление суб-досок из Sidebar + подтверждение удаления везде (MessageBox.Show YesNo); 4) TaskDetailViewModel + TaskDetailDialog (просмотр полной инфо по задаче при клике); 5) SettingsView полный редизайн — вкладки «Внешний вид» + «Профиль и аккаунт» (аватар, выход, безопасность), SettingsViewModel получил SetThemeCommand/LogoutCommand/DisplayName/ModeName/IsCorporate; 6) Calendar — неделя/месяц вид + WeekDayViewModel + WeekTaskItem + task bars; 7) Notes — фильтры по уровню безопасности (4 пилля с цветом) + «С таймером» + «Сбросить».
- **2026-04-27** — 5 UX исправлений: 1) **Sidebar обновление после удаления проекта** — добавлен Projects в ShellViewModel напрямую, XAML привязки исправлены; 2) **PinView hint text** — добавлен HintAssist.Foreground=SecondaryForeground + Foreground=PrimaryForeground на оба PasswordBox; 3) **Task ⋮ контекстное меню** — заменены кнопки Edit/Delete на DotsButton с ContextMenu (Создать подзадачу/Выполнена/Редактировать/Удалить), PlacementTarget.Tag trick для команд; 4) **Подзадачи** — SubTaskItem + SubTasks в TaskMetaService, управление подзадачами в TaskDetailViewModel + TaskDetailDialog (список с кружочками + поле добавления + счётчик прогресса); 5) **Календарь — день + задачи** — CalendarDayTask в каждой ячейке (до 3 баров + "+ ещё N"), клик по дню открывает боковую панель со списком задач + кнопки "Создать задачу" / "Создать заметку".
- **2026-04-27 (сессия 3)** — 5 исправлений: 1) **Мои задачи — краш** — заменены `<Setter Property="Style">` в DataTrigger на индивидуальные сеттеры Background/Foreground/FontWeight во всех 4 вкладках MyTasksView.xaml (WPF не поддерживает смену Style через DataTrigger); 2) **Подзадачи — чекбокс** — убрана двойная инверсия `sub.IsCompleted = !sub.IsCompleted` в TaskCardViewModel.ToggleSubTask (TaskMetaService уже переключает тот же объект по ссылке); 3) **Прогресс подзадач** — добавлен SubTaskProgress (double 0-1) в TaskCardViewModel + зелёный ProgressBar (MaterialDesignLinearProgressBar) в карточке Kanban; 4) **Календарь — бары задач** — LoadMonthTasksAsync и LoadWeekTasksAsync теперь добавляют задачу ко ВСЕМ дням между CreatedAt и DueDate (не только в день дедлайна); 5) **Чёрный текст в тёмной теме** — добавлен Foreground=PrimaryForeground на все MaterialDesignOutlinedComboBox и MaterialDesignOutlinedDatePicker в NoteEditorView.xaml и TaskEditDialog.xaml; дефолтный Foreground добавлен в ItemTemplate TextBlock.
- **2026-04-27 (сессия 4)** — 7 фич: 1) **Аудит исправлен** — JwtTokenService теперь использует ClaimTypes.Role вместо кастомного "role", CurrentUserContext обновлён; 2) **Уровни безопасности по-русски** — NoteEditorView ComboBox с DataTemplate (Открытый/Внутренний/Конфиденциальный/Секретный); 3) **Смена пароля** — ChangePasswordDialogViewModel + ChangePasswordDialog + ChangePasswordCommand в SettingsViewModel; 4) **Смена email** — ChangeEmailDialogViewModel + ChangeEmailDialog + ChangeEmailCommand + server endpoint POST /auth/change-email + User.ChangeEmail(); 5) **Саморегистрация** — POST /auth/register-self (AllowAnonymous, User роль), RegisterViewModel обновлён; 6) **Поиск пользователей + заявки в контакты** — GET /api/v1/users/search, POST /api/v1/contacts/requests, accept/decline endpoints, ContactRequestsController, ContactsViewModel + ContactsView расширены (панель поиска + панель заявок); 7) **Совместные проекты** — SharedProject + SharedProjectMember entities, EF migration AddCollaborationFeatures применена, SharedProjectsController, SharedProjectsViewModel + SharedProjectsDialog + кнопка в sidebar.
- **2026-04-27 (сессия 2)** — 7 улучшений: 1) **ConfirmDialog** — современный Material Design диалог удаления вместо Windows XP MessageBox.Show; все 5 мест заменены; 2) **Sidebar refresh** исправлен (ObservableCollection<BoardItem> в ProjectService + Projects property в ShellViewModel); 3) **Подзадачи expand/collapse** — IsSubTasksExpanded в TaskCardViewModel, inline список в KanbanBoardView карточке с кнопкой ChevronRight/Down + прогресс; 4) **Kanban sort buttons** — убран «Исполнитель», активные кнопки «Дедлайн» + «Приоритет» с SortMode active state + SortByDeadlineCommand/SortByPriorityCommand/ClearSortCommand; 5) **Global hint fix** — Generic.xaml implicit styles для TextBox/PasswordBox/ComboBox/DatePicker с HintAssist.Foreground=SecondaryForeground; 6) **Мои задачи** страница — MyTasksViewModel + MyTasksView (вкладки 4 шт., таблица с чекбоксом/звёздой/тайтл+путь/дата/дедлайн пилль/приоритет/теги/⋮ меню, поиск, сортировка по колонкам); зарегистрирована в DI + ShellView DataTemplate + NavMyTasks стиль + кнопка сайдбара «Мои задачи»; 7) **LocalDB объяснение** — см. ниже.
- **2026-04-28** — 4 исправления и новая функция: 1) **Изоляция проектов** — `ProjectService.LoadForUser(userId)` вызывается в `ShellViewModel.Initialize()`, у каждого пользователя свои данные в `%AppData%\ConSecOrg\{userId}\`; 2) **SharedProjects баг** — `SharedProjectsController.CurrentUserId` и `UsersController` использовали `User.FindFirst("sub")!.Value` — краш т.к. ASP.NET Core маппит sub → ClaimTypes.NameIdentifier; исправлено через `FindFirstValue(ClaimTypes.NameIdentifier) ?? FindFirstValue("sub")`; 3) **Аудит/Безопасность скрыты** у обычных пользователей (User/Manager): `IsAdminOrAuditor` property в ShellViewModel, sidebar кнопки используют это свойство; 4) **Страница Отчёты** — ReportsViewModel (4 модели данных: GeneralReportRow/SavedReport/GanttTask/EmployeeTaskRow) + ReportsView.xaml (4 вкладки: Общий/Таблицы/Задачи сотрудников/Время в колонках) с Gantt Chart на Canvas + Bar Chart + фильтрация; зарегистрирован в DI.
- **2026-04-28 (сессия 2)** — 3 новые функции: 1) **Страница «Моя компания»** — CompanyViewModel + CompanyView.xaml (2 колонки: Create/Join project + WrapPanel проектов с invite-кодом); ISharedProjectsApiService; заменяет старый диалог SharedProjectsDialog; кнопка в sidebar для всех корп. пользователей; 2) **Страница «Пользователи»** — UsersManagementViewModel + UsersManagementView.xaml (таблица: аватар/username/email/role-combobox/статус-пилл/last-login/lock-unlock кнопка); IUsersManagementApiService + ApiClient методы (GetAll, AssignRole, Lock, Unlock); кнопка в sidebar только для Admin; 3) **InverseBoolConverter** — добавлен и зарегистрирован в Generic.xaml; всё собирается без ошибок.
- **2026-05-07** — 6 улучшений: 1) **Исправлен ContactRequestsController** — `User.FindFirst("sub")!.Value` → `FindFirstValue(ClaimTypes.NameIdentifier)`, ошибка при отправке заявки в контакты устранена; 2) **Hint text видимость** — все 3 места с `HintAssist.Foreground=AccentBrush` (TaskDetailDialog, KanbanBoardView ×2) исправлены на `SecondaryForeground`; 3) **Акцентный цвет везде** — ThemeEngine.Apply() теперь выставляет `PrimaryHueMidBrush`, `PrimaryHueLightBrush`, `PrimaryHueDarkBrush`, `SecondaryHueMidBrush` и Foreground-варианты, поэтому все кнопки/чекбоксы MaterialDesign подхватывают выбранный акцентный цвет; 4) **SharedTaskEditViewModel + SharedTaskEditDialog** — полный диалог создания/редактирования задачи в совместных проектах (приоритет/дедлайн/теги/описание + роль-зависимое назначение: Admin/Manager выбирают из участников, User = автоназначение); 5) **Подзадачи в совместных проектах** — `SharedTaskCardViewModel` расширен полным набором subtask-команд (AddTopSubTask, AddChildSub, ToggleSubTask, RemoveSubTask, expand/collapse, прогресс бар) через `TaskMetaService`; 6) **SharedProjectBoardView** обновлён: «Редактировать» в ⋮-меню задачи, subtask-секция на карточке (прогресс бар + expand + рекурсивный список + inline input), кнопка PencilPlus для открытия полного диалога рядом с quick-add.
- **2026-05-08 (сессия 2)** — 3 новые функции: 1) **DestructionTimerWidget** — `Controls/DestructionTimerWidget.xaml` + code-behind: UserControl с Canvas, ArcSegment (прогресс дуга зеленый→оранжевый→красный), DispatcherTimer каждую секунду, DependencyProperty `ExpiresAt`, scale-адаптивный прогресс (24ч / 1ч / 10м шкала), замена иконки таймера на NotesListView (card view + list view); 2) **SignalR Kanban** — `BoardHubClient.cs` (EnsureConnectedAsync, JoinBoard/LeaveBoard, NotifyTaskMovedAsync, TaskMoved event + автореконнект); `KanbanBoardViewModel` подключается при навигации в корпоративном режиме, слушает `OnRemoteTaskMoved` → перезагружает доску, после MoveTaskToColumn → NotifyTaskMovedAsync; зарегистрирован в DI как Singleton; 3) **Все пункты "Возможные улучшения" выполнены** — чеклист CLAUDE.md обновлён.
- **2026-05-08** — 6 улучшений: 1) **Поиск по контактам** — добавлен `FilterQuery` + `ClearFilterCommand` в `ContactsViewModel`, поиск по имени/email/телефону в реальном времени, поле поиска в `ContactsView` с кнопкой сброса; 2) **Экспорт аудита в CSV** — `ExportCsvAsync()` в `IAuditApiService`/`ApiClient`, `ExportCsvCommand` в `AuditLogViewModel` (SaveFileDialog), кнопка «Экспорт CSV» в `AuditLogView`; 3) **Фильтры в журнале аудита** — DatePicker «С даты»/«По дату» + ComboBox действий (Login/Register/...), кнопки «Применить»/«Сбросить», поддержка на сервере (`action` параметр в `AuditController`+`GetAuditLogsQuery`+`AuditLogRepository`); 4) **Имена пользователей в аудите** — `GetByIdsAsync` в `IUserRepository`/`UserRepository`, handler загружает usernames для всех записей на странице; 5) **Анимации переходов** — `TransitionContentControl` (FadeIn 180ms + CubicEase) заменяет `ContentControl` в `ShellView.xaml`; 6) **Toast уведомления** — `ToastNotificationHelper` + `ToastOverlay` в `MainWindow.xaml`/`MainWindow.xaml.cs` — уведомления теперь реально отображаются (слайд-ин справа, авто-скрытие через 3.5 сек, цветовая кодировка по уровню).
- **2026-05-08 (сессия 3)** — 4 исправления: 1) **Таймер уничтожения — спам уведомлений** — `DestructionTimerService` добавлен `HashSet<Guid> _alreadyProcessed` — каждая заметка обрабатывается ровно один раз; 2) **Таймер уничтожения — реальное удаление** — `LocalNotesService.DeleteNoteAsync` переписан: secure-overwrite в отдельном try/catch, удаление через raw SQL `DELETE FROM notes WHERE id = {0}` (минует EF change tracker Singleton), детач трекнутой сущности; `DestructionTimerService` вызывает новый `_notifications.NoteDestroyedByTimer()` который файрит `NoteTimerExpired` event; 3) **Автоудаление из UI** — `NotesListViewModel` подписывается на `NoteTimerExpired` → фильтрует `_allNotes` → `ApplySortAndFilter()` через Dispatcher; конвертирован из primary constructor в обычный для поддержки event subscription; 4) **Подзадачи в совместных проектах** — `SharedProjectBoardView.xaml` полностью переписан subtask-секция: рекурсивный `SharedCardSubTaskTemplate` (идентичен `CardSubTaskTemplate` в KanbanBoardView), BindingProxy `sharedCardProxy`, expand/collapse, круглые чекбоксы, прогресс-бар, ⋮ меню, inline add-child, connector line; добавлен `SaveSubTitleCommand` в `SharedTaskCardViewModel`; code-behind получил `CardSubDots_Click` + `CardSubTaskTitle_LostFocus`; исправлен мусорный байт в начале `KanbanBoardView.xaml` (XML ошибка MC3000).
- **2026-05-08 (сессия 4)** — 6 исправлений временной синхронизации и таймера уничтожения: 1) **Корень UTC-бага** — `LocalNotesService.DeleteNoteAsync` детач EF-трекнутой сущности мог кидать исключение из фонового потока (EF DbContext не потокобезопасен), исключение ловилось но `_alreadyProcessed.Add` уже произошёл → нотификация не файрилась, UI не обновлялся; детач обёрнут в отдельный try/catch; 2) **`_alreadyProcessed` перенесён после успешного удаления** в `DestructionTimerService` — теперь при ошибке нота будет ретраиться на следующем тике; 3) **`BeginInvoke` вместо `Invoke`** в `DestructionTimerService` и `NotesListViewModel.OnNoteTimerExpired` — избегаем вложенного Dispatcher.Invoke; 4) **`DateTimeToRelativeConverter` исправлен** — `Unspecified` kind обрабатывается как UTC (не `ToUniversalTime()` который трактует Unspecified как Local и вычитает UTC+3) → `"только что"` вместо `"3 ч. назад"` для новых записей; 5) **`NoteEditorViewModel` таймер** — `_presetExpiresUtc = DateTime.UtcNow.AddSeconds(seconds)` хранит точное UTC-время для пресетов (не усекается до минут); `ExpiresAt` возвращает UTC напрямую; UI-поля (TimerDate/Hour/Minute) показывают эквивалентное локальное время; 6) **Сервер UTC JSON** — `UtcDateTimeJsonConverter` + `UtcNullableDateTimeJsonConverter` добавлены в `Program.cs` (`AddJsonOptions`) → все DateTime в JSON-ответах теперь имеют `Z`-суффикс → клиент RestSharp получает `Kind=Utc` вместо `Unspecified`.
- **2026-05-08 (сессия 5)** — Расширение аудита + улучшения отчётов: 1) **AuditAction расширен до 41 действия** — добавлены группы: Auth (Register, TokenRefreshed, EmailChanged), Notes (NoteCreated, NoteUpdated, NoteViewed, NoteDeleted, NoteSecureDeleted, NoteExpired, NoteTimerSet), Tasks (TaskCreated, TaskUpdated, TaskMoved, TaskCompleted, TaskDeleted), Projects (ProjectCreated/Updated/Deleted/Joined/Left), Boards (BoardCreated/Updated/Deleted), Columns (ColumnCreated/Updated/Deleted/Reordered), Contacts (ContactAdded/Updated/Deleted, ContactRequestSent/Accepted/Declined), Data (DataExported, AuditViewed, AuditChainVerified); 2) **AuditHelper.WriteAsync** вызывается в CreateNote/UpdateNote/DeleteNote/CreateTask/UpdateTask/MoveTask/DeleteTask command handlers с правильными AuditAction; 3) **MoveTaskCommand + DeleteTaskCommand** получили `ICryptoService crypto` в конструктор для AuditHelper; 4) **SharedProjectsController** — добавлен `ICryptoService`, приватный `AuditAsync()` helper, аудит при Create/Join/Delete/Leave проекта; 5) **ContactRequestsController** — добавлен аналогичный `WriteAuditAsync()`, аудит при Send/Accept/Decline заявки; 6) **Отчёты — исполнители** — `EmployeeTaskRow.HasAssignee` + `AssignedToUsername`, данные из `TaskMetaService.GetAssignee()`; 7) **Отчёты — совместные проекты** — `ReportsViewModel` загружает задачи из shared проектов через `ISharedProjectsApiService`, `AllTasksCombined = _allTasks + _sharedTasks`; 8) **ReportConfigDialog** — новый WPF Window с DatePicker DateFrom/DateTo + CheckBox ShowOpen/ShowCompleted/GroupByProject, открывается через `ConfigureReportCommand`; 9) **AddColumnCommand** в ReportsViewModel — активная кнопка «Добавить колонку», циклически добавляет ИСПОЛНИТЕЛЬ/ОБНОВЛЁН/ДЕДЛАЙН; 10) **Фильтр по доскам в «Время в колонках»** — `TimeBoards ObservableCollection`, `SelectedTimeBoard`, `OnSelectedTimeProjectChanged` заполняет список досок, `BuildTimeTab()` фильтрует по projectId + boardId; 11) **Directory.Build.props** — глобальный `NuGetAuditSuppress` для AutoMapper GHSA-rvv3-g6hj-g44x (все версии затронуты, фикса нет).

## YouGile-стиль Kanban редизайн (2026-04-25) — ПЛАН И РЕАЛИЗАЦИЯ

**Цель**: скопировать визуальный дизайн YouGile (ru.yougile.com/crm): цветные колонки, красивые карточки с тегами, левый сайдбар с проектами, вкладки досок внутри проекта.

### Что меняется

| Файл | Изменение |
|------|-----------|
| `ProjectService.cs` | Добавить `BoardItem` — суб-доски внутри проекта; расширить маппинг задач (taskId → projectId + boardId) |
| `KanbanBoardViewModel.cs` | `BoardTabItem` + `BoardTabs` ObservableCollection, фильтрация по активной доске |
| `ShellViewModel.cs` | Принять `ProjectService`, `SelectedProjectId`, команды `NavigateProjectCommand`/`NavigateBoardCommand` |
| `EqualityMultiConverter.cs` | Новый IMultiValueConverter для сравнения объектов в XAML DataTrigger (подсветка активного проекта в сайдбаре) |
| `Generic.xaml` | Зарегистрировать `EqualityMultiConverter`; стили `BoardTabButton`/`BoardTabButtonActive` |
| `KanbanBoardView.xaml` | **Полный редизайн**: шапка с вкладками проекта/досок, фильтр-бар, колонки с цветными полупрозрачными заголовками (320px), карточки с цветными тегами + дата-бейдж с иконкой + кнопки |
| `ShellView.xaml` | **Редизайн сайдбара**: раздел «ПРОЕКТЫ» с ItemsControl, подсветка активного проекта, вложенные суб-доски, inline создание проекта |

### Статус
- [x] CLAUDE.md — план записан
- [x] `ProjectService.cs` — BoardItem, доски в ProjectItem, TaskProjectMapping, расширенные методы
- [x] `EqualityMultiConverter.cs` — новый конвертер
- [x] `KanbanBoardViewModel.cs` — BoardTabItem, BoardTabs, SelectedBoardTab, UpdateBoardTabs
- [x] `ShellViewModel.cs` — ProjectService, SelectedProjectId, NavigateProjectCommand, NavigateBoardCommand
- [x] `Generic.xaml` — EqualityMultiConverter + BoardTabButton стили
- [x] `KanbanBoardView.xaml` — YouGile редизайн (вкладки, цветные колонки, новые карточки)
- [x] `ShellView.xaml` — YouGile сайдбар (раздел Проекты, доски, подсветка)

---

## Дальнейшие планы

### Проект завершён (все основные фазы 1-10)

### Возможные улучшения
- [x] Drag-and-drop в Kanban (WPF DragDrop API) — реализовано
- [x] Фильтрация заметок по уровню безопасности и таймеру — реализовано
- [x] Calendar — неделя вид с task bars — реализовано
- [x] Анимации переходов между страницами — TransitionContentControl (FadeIn 180ms)
- [x] Поиск по контактам в ContactsView — фильтрация в реальном времени
- [x] Экспорт аудита в CSV — SaveFileDialog + server endpoint
- [x] Фильтры дат и действий в журнале аудита — DatePicker + ComboBox
- [x] Toast-уведомления — ToastNotificationHelper (слайд + цветовая кодировка)
- [x] SignalR real-time обновление Kanban в корпоративном режиме — BoardHubClient, TaskMoved broadcast + remote reload
- [x] DestructionTimerWidget.xaml — декоративный круговой счётчик обратного отсчёта (в note cards)

---

## YouGile Pixel-Perfect Редизайн (следующая сессия)

> **Источник**: 6 скриншотов YouGile (ru.yougile.com/crm) от пользователя 2026-04-26.
> **Цель**: воспроизвести дизайн точь-в-точь — колонки, карточки, сайдбар, контекстное меню, стикеры-приоритеты.

### Анализ скринов — что именно нужно скопировать

#### Скрин 1 & 2 — Kanban доска + контекстное меню колонки

**Колонки (НЕ сплошной цветной заголовок — открытый стиль):**
```
[●dot] В работе  [5-badge]  [TableIcon]  [⋮]
+ Добавить задачу
─────────────────────
Карточка 1
Карточка 2
```
- Колонка = открытый transparent вертикальный блок, БЕЗ скруглённого контейнера
- Цветная точка Ellipse 10px = цвет колонки
- Бейдж счётчика = полупрозрачный (цвет колонки + Alpha 30%)
- Фон самой колонки: почти прозрачный (фоновое изображение просвечивает)
- «+ Добавить задачу» сразу ПОД заголовком (зелёный AccentBrush текст)

**Контекстное меню ⋮ колонки:**
```
✏  Переименовать
🗂  Архивировать все задачи
✅  Архивировать выполненные
↔  Переместить
⎘  Дублировать
↑↓  Сортировать задачи по...  ▶
──────────────────────────────────
ЦВЕТ КОЛОНКИ
● ● ● ● ● ●   (ряд 1)
● ● ● ● ● ●   (ряд 2)
──────────────────────────────────
🗑  Удалить   (красный)
```
Палитра 12 цветов: `#607D8B #E53935 #FF9800 #FDD835 #66BB6A #2E7D32`
                   `#37474F #B71C1C #6D4C41 #00897B #1565C0 #6A1B9A`

#### Скрин 3 — Настройки доски (Передача задач)

Modal dialog с левым меню: Основные, Внешний вид, Стикеры, Передача задач, Шаблоны задач.
«Передача задач» = автодействия при перемещении задачи в колонку.
Для нашего приложения: только «Прикрепить стикер при перемещении».

#### Скрин 4 — Стикеры (кастомные поля задач)

Стикер «Приоритет» = Набор заданных состояний с цветом:
| Состояние | Текст | Цвет фона | Цвет текста |
|-----------|-------|-----------|-------------|
| Critical | Критично! | #FFEBEE | #C62828 |
| High | Важно | #FFF3E0 | #E65100 |
| Normal | Нормально | #E8F5E9 | #2E7D32 |
| Low | Не важно | #F5F5F5 | #616161 |

В тёмной теме: фон = `#33{RGB цвета}` (полупрозрачно).
Иконка на стикере: MaterialDesign `ChartBar` (📶).
Вид на карточке: `[📶 Нормально]` — pill с иконкой и текстом.

#### Скрин 5 — «Мои задачи» (табличный вид)

Таблица всех задач. Вкладки: Задачи на мне / Порученные мной / Приватные / Избранные.
Столбцы: ⊙ НАЗВАНИЕ ЗАДАЧИ + путь (Проект/Доска/Колонка), АВТОР, ДАТА, ДЕДЛАЙН (цветной pill), СТИКЕРЫ.
*Пока не реализовывать — отдельная страница.*

#### Скрин 6 — Статистика

Задачи по сотрудникам. *Не реализовывать — корпоративная функция.*

---

### Приоритизированный план реализации

#### ВЫСОКИЙ ПРИОРИТЕТ

**Задача 1 — Открытые колонки + контекстное меню с палитрой**

Файл: `KanbanBoardView.xaml`, `KanbanColumnViewModel.cs`, `KanbanBoardViewModel.cs`

- Убрать `Border CornerRadius="12"` вокруг колонки → заменить на `StackPanel` с `Width="300"` и `Margin="0,0,16,0"`
- Заголовок: `[● Ellipse][Name][Badge][TableIcon][DotsButton]` в Grid
- DotsButton → открывает `ContextMenu` (WPF стандартный)
- В ContextMenu: MenuItem + Separator + цветовая палитра (UniformGrid 2x6 кнопок-кругов)
- `ProjectService.SetColumnColor(Guid colId, string hex, Guid? projectId, Guid? boardId)`
- `KanbanColumnViewModel.Color` → setter вызывает OnPropertyChanged

**Задача 2 — Переделать карточки задач**

Новый вид карточки:
```
┌─────────────────────────────────────────┐
│ [⊙]  Название задачи                [⋮] │
│       может быть длинным                 │
│                                          │
│   [📶 Нормально]  [📅 14 Дек]           │
└─────────────────────────────────────────┘
```
- `[⊙]` = круглый CheckBox (ControlTemplate круглый), `IsChecked` → `IsCompleted` в TaskMeta
- При IsCompleted=true: заголовок зачёркнут + иконка галочки зелёная
- `[⋮]` = DotsButton → ContextMenu: Редактировать, Переместить, Удалить
- Стикер приоритета: pill `[ChartBar icon][текст]` с цветом по таблице выше
- Дедлайн: pill `[CalendarOutline][дата]`, красный если прошло
- Теги из TaskMetaService: серые mini-chips
- УБРАТЬ кнопки ← → (перемещение через контекстное меню или drag-drop)

**Задача 3 — TaskMetaService + TaskEditDialog**

`TaskMetaService.cs` поля:
```json
{ "taskId": { "Tags": ["tag"], "Description": "", "IsCompleted": false } }
```

`TaskEditDialog.xaml` (открывается из «+ Добавить задачу» и «⋮ → Редактировать»):
- Заголовок диалога: «Новая задача» / «Редактировать»
- TextBox — Название (крупный, Hint: «Введите название задачи»)
- Строка: [Priority ComboBox с цветными стикерами] [DatePicker дедлайна]
- TextBox tags input + chips под ним
- MultiLine TextBox — Описание
- Кнопки: [Создать/Сохранить] [Отмена]

#### СРЕДНИЙ ПРИОРИТЕТ

**Задача 4 — Сайдбар YouGile-стиль**

`ShellView.xaml`:
- Верх: аватар (FirstLetter) + Username + режим (Персональный/Корп.)
- Навигация без «ГЛАВНОЕ» лейбла — просто иконки + текст
- Проекты с разворачиванием (уже есть, но улучшить визуал)
- Фон сайдбара = полупрозрачный (SidebarBackground) — фоновое изображение просвечивает
- Active item = акцентный цвет + левая полоска 3px

**Задача 5 — «Создать колонку» кнопка-карточка справа**

Вместо StackPanel с инпутом — карточка-кнопка:
- Пустой блок шириной ~220px, пунктирная рамка, «+ Создать колонку» текст по центру
- При нажатии — inline input с кнопкой Создать (как сейчас, но визуально как карточка)

---

### Технические детали

**HexToAlpha30Converter** — `#E53935` → `SolidColorBrush(Color.FromArgb(0x4D, R, G, B))` (30% alpha)

**ContextMenu палитра 12 цветов (XAML WrapPanel в MenuItem)**:
```xml
<MenuItem Header="ЦВЕТ КОЛОНКИ" IsEnabled="False" FontSize="10" Foreground="Gray"/>
<MenuItem>
  <MenuItem.Template>
    <ControlTemplate>
      <UniformGrid Columns="6" Margin="10,4">
        <Button Tag="#E53935" Command="...SetColumnColorCommand" CommandParameter="{Binding Tag}">
          <Ellipse Width="20" Height="20" Fill="#E53935"/>
        </Button>
        ...
      </UniformGrid>
    </ControlTemplate>
  </MenuItem.Template>
</MenuItem>
```

**Круглый CheckBox стиль** (ControlTemplate):
```xml
<Style x:Key="CircleCheckBox" TargetType="CheckBox">
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate>
        <Ellipse Width="18" Height="18"
                 Stroke="{DynamicResource BorderBrush}"
                 StrokeThickness="1.5"
                 Fill="Transparent"/>
        <!-- Triggers: IsChecked=True → Fill=AccentBrush + показать галочку -->
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

---

## Улучшения Kanban (2026-04-26) ✅

### Задачи A+B — полные карточки задач РЕАЛИЗОВАНЫ
- [x] **TaskMetaService.cs** — хранилище тегов/описания/IsCompleted в `task_meta.json`
- [x] **TaskCardViewModel.cs** — обёртка вокруг `TaskItemDto` с `Tags`, `IsCompleted` из TaskMetaService
- [x] **KanbanColumnViewModel** — `Tasks` теперь `ObservableCollection<TaskCardViewModel>` + `SetColorCommand`, `ToggleColorPickerCommand`, `PersistColor` callback + `ShowColorPicker`
- [x] **TaskEditViewModel.cs** — полный диалог с Priority/DueDate/Tags/Description
- [x] **TaskEditDialog.xaml** — MaterialDesign диалог (Priority ComboBox с цветными иконками, DatePicker, теги-chips, описание)
- [x] **KanbanBoardViewModel** — все команды переведены на `TaskCardViewModel`; `PersistColor` callback для персистентности цвета колонок
- [x] **KanbanBoardView.xaml** — карточки: circle checkbox (завершение), теги chips, приоритет badge с иконкой флага, дедлайн badge, ✏🗑 кнопки; колонки: inline color picker (12 цветов)
- [x] **Generic.xaml** — добавлен `ColorSwatchButton` style + `HexToAlpha30Converter`
- [x] **App.xaml.cs** — зарегистрированы `TaskMetaService` (Singleton) + `TaskEditViewModel` (Singleton)

### Оставшиеся возможности (не критично)
- Drag-and-drop в Kanban (WPF DragDrop API)
- TaskDetailView — полноэкранный просмотр при двойном клике
- Таймер уничтожения для задач
