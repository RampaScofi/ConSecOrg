# Документация проекта ConSecOrg

**Защищённый электронный органайзер**  
Дипломный проект | Максутов Р.Ф. | КИ22-02/2Б | СФУ ИКИТ  
Руководитель: Туговиков В.Б.

---

## Содержание

1. [Обзор проекта](#1-обзор-проекта)
2. [Архитектура](#2-архитектура)
3. [Структура папок](#3-структура-папок)
4. [ConSecOrg.Domain — доменный слой](#4-consecorgdomain--доменный-слой)
5. [ConSecOrg.Application — прикладной слой (CQRS)](#5-consecorgapplication--прикладной-слой-cqrs)
6. [ConSecOrg.Infrastructure — инфраструктурный слой](#6-consecorginfrastructure--инфраструктурный-слой)
7. [ConSecOrg.Server — веб-сервер (ASP.NET Core)](#7-consecorgserver--веб-сервер-aspnet-core)
8. [ConSecOrg.Shared — общие контракты](#8-consecorgshared--общие-контракты)
9. [ConSecOrg.Client — WPF-клиент](#9-consecorgclient--wpf-клиент)
10. [Тестовые проекты](#10-тестовые-проекты)
11. [База данных — схема таблиц](#11-база-данных--схема-таблиц)
12. [Криптография (ГОСТ)](#12-криптография-гост)
13. [Режимы работы](#13-режимы-работы)
14. [API-эндпоинты](#14-api-эндпоинты)
15. [Навигация в клиенте](#15-навигация-в-клиенте)
16. [Темизация и внешний вид](#16-темизация-и-внешний-вид)
17. [Зависимости (NuGet)](#17-зависимости-nuget)
18. [Запуск и развёртывание](#18-запуск-и-развёртывание)
19. [Иконки приложения](#19-иконки-приложения)

---

## 1. Обзор проекта

ConSecOrg — защищённый электронный органайзер с двумя режимами работы:

- **Персональный режим** — все данные хранятся локально в SQL Server LocalDB. Нет аутентификации через сервер, доступ защищён PIN-кодом. Подходит для личного использования без интернета.
- **Корпоративный режим** — клиент-серверная архитектура. WPF-приложение подключается к ASP.NET Core Web API. Поддерживается ролевой доступ (RBAC), аудит всех действий, совместная работа над проектами, real-time синхронизация через SignalR.

Все данные шифруются по российским криптостандартам ГОСТ:
- Шифрование: ГОСТ Р 34.12-2015 «Кузнечик» (режим CTR, ключ 256 бит)
- Хэширование и HMAC: ГОСТ Р 34.11-2012 «Стрибог-256»
- KDF: PBKDF2-HMAC-Стрибог-256, 100 000 итераций

---

## 2. Архитектура

Проект построен по принципам **Clean Architecture** (чистой архитектуры):

```
┌─────────────────────────────────────────────────────────┐
│                ConSecOrg.Client (WPF)                   │
│          Views ↔ ViewModels → Services                  │
│     ThemeEngine │ NavigationService │ SessionService    │
└───────────────────────┬─────────────────────────────────┘
                        │ HTTP (RestSharp) / LocalDB (EF Core)
┌───────────────────────▼─────────────────────────────────┐
│            ConSecOrg.Server (ASP.NET Core)              │
│  Controllers → MediatR → Application → Infrastructure  │
│     JWT Auth │ Rate Limiting │ SignalR │ Serilog        │
└───────────────────────┬─────────────────────────────────┘
                        │ Entity Framework Core
┌───────────────────────▼─────────────────────────────────┐
│          Microsoft SQL Server / LocalDB                 │
│  users │ notes │ tasks │ contacts │ audit_logs │ ...   │
└─────────────────────────────────────────────────────────┘
```

### Зависимости между проектами

```
ConSecOrg.Domain        ← никаких зависимостей (ядро)
ConSecOrg.Shared        ← никаких зависимостей (DTOs)
ConSecOrg.Application   → Domain, Shared
ConSecOrg.Infrastructure → Domain, Application, Shared
ConSecOrg.Server        → Application, Infrastructure, Shared
ConSecOrg.Client        → Shared (+ Infrastructure для PersonalDbContext)
```

---

## 3. Структура папок

```
ConSecOrg/
├── ConSecOrg.slnx              ← файл решения Visual Studio
├── CLAUDE.md                   ← инструкции для AI-ассистента, трекер прогресса
├── DOCUMENTATION.md            ← этот файл
├── src/
│   ├── ConSecOrg.Domain/       ← доменный слой: сущности, интерфейсы, исключения
│   ├── ConSecOrg.Application/  ← прикладной слой: CQRS handlers, validators
│   ├── ConSecOrg.Infrastructure/ ← инфраструктура: EF Core, криптография, JWT
│   ├── ConSecOrg.Server/       ← ASP.NET Core Web API сервер
│   ├── ConSecOrg.Client/       ← WPF десктоп-клиент
│   └── ConSecOrg.Shared/       ← общие DTOs, enum'ы, константы
└── tests/
    ├── ConSecOrg.Domain.Tests/
    ├── ConSecOrg.Application.Tests/
    ├── ConSecOrg.Infrastructure.Tests/
    └── ConSecOrg.Server.Tests/
```

---

## 4. ConSecOrg.Domain — доменный слой

Самый внутренний слой архитектуры. **Не зависит ни от каких других слоёв проекта.** Содержит бизнес-сущности, value objects, интерфейсы и исключения.

### 4.1 Common/ — базовые классы

| Файл | Назначение |
|------|-----------|
| `BaseEntity.cs` | Абстрактный базовый класс для всех сущностей. Содержит свойство `Id` типа `Guid`. Реализует сравнение по идентификатору (не по ссылке). |
| `AuditableEntity.cs` | Расширяет `BaseEntity`, добавляя поля аудита: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`. Все изменяемые сущности наследуют этот класс. |
| `ValueObject.cs` | Базовый класс для value objects (объектов-значений). Реализует структурное сравнение через `GetEqualityComponents()`. |

### 4.2 Entities/ — доменные сущности

| Файл | Описание |
|------|----------|
| `User.cs` | Пользователь системы. Поля: `Username`, `Email`, `PasswordHash` (хранится как value object), `Salt`, `RoleId`, `DeviceId` (отпечаток устройства), `IsLocked`, `FailedLoginAttempts`, `LastLoginAt`, `LastLoginIp`. Методы: `RecordFailedLogin()`, `ResetFailedLogins()`, `Lock()`, `ChangeEmail()`. |
| `Note.cs` | Заметка. Содержит зашифрованное содержимое (`Content` типа `EncryptedContent`), заголовок, уровень безопасности (`SecurityLevel`), флаги `IsPinned`, `IsTemplate`, дату самоуничтожения `ExpiresAt`, ссылку на категорию. |
| `TaskItem.cs` | Задача Kanban-доски. Поля: `Title`, зашифрованное описание (`Description`), `Status`, `Priority`, `BoardColumn`, `ColumnPosition`, `DueDate`. Название `TaskItem` (не `Task`) — т.к. `Task` зарезервирован в C# для async. |
| `Contact.cs` | Контакт. Все персональные поля (имя, email, телефон, заметки) хранятся в зашифрованном виде (`EncryptedContent`). Поле `LinkedUserId` (Guid?) связывает контакт с реальным пользователем системы — используется для открытия чата. |
| `Category.cs` | Категория заметок. Поля: `Name`, `Color` (HEX), `Icon`, `UserId`. |
| `Tag.cs` | Тег (метка) для заметок. Поле `Name`. Связь с заметками — many-to-many через `note_tags`. |
| `AuditLog.cs` | Запись журнала аудита. Содержит: `UserId`, `Action` (из `AuditAction` enum), `EntityType`, `EntityId`, `Timestamp`, `Status`, `IpAddress`, `SequenceNum`, а также `PreviousHash` и `CurrentHash` — для криптографической цепочки (hash-chain). |
| `Session.cs` | JWT-сессия пользователя. Хранит хэш refresh-токена (`TokenHash`), IP-адрес, User-Agent, device_id, время истечения `ExpiresAt`. |
| `Role.cs` | Роль пользователя. Поля: `Name`, `Permissions` (JSON строка с правами). |
| `UserSettings.cs` | Настройки пользователя: тема (`Theme`), акцентный цвет (`AccentColor`), размер шрифта (`FontSize`), JSON настроек раскладки. |
| `SharedProject.cs` | Совместный проект (корпоративный режим). Поля: `Name`, `Description`, `InviteCode` (уникальный код для вступления), `OwnerId`. Содержит коллекцию участников (`SharedProjectMember`). |
| `SharedProjectColumn.cs` | Колонка Kanban-доски совместного проекта. Поля: `Name`, `Order`, `Color`, `ProjectId`. |
| `SharedProjectTask.cs` | Задача совместного проекта. Поля: `Title`, `Description`, `Status`, `Priority`, `DueDate`, `ProjectId`, `ColumnId`, `AssignedToUserId`. |
| `ChatMessage.cs` | Сообщение чата. Поля: `SenderUserId`, `ProjectId?` (проектный чат), `GroupChatId?` (групповой чат), `ToUserId?` (личный чат), `SentAt`. Открытый текст `Text` не хранится — только зашифрованные поля: `TextCipher` (VARBINARY), `TextNonce`, `TextHmac` (ГОСТ Р 34.12-2015). Вложения: `AttachmentFileId`, `AttachmentFileName`, `AttachmentSize`. |
| `GroupChat.cs` | Групповой чат (корпоративный режим). Поля: `Name`, `CreatedByUserId`, `CreatedAt`. Навигация: `Members` (коллекция `GroupChatMember`). |
| `GroupChatMember.cs` | Участник группового чата. Поля: `GroupChatId`, `UserId`, `JoinedAt`. |
| `ContactRequest.cs` | Заявка на добавление в контакты. Поля: `SenderId`, `ReceiverId`, `Status` (Pending/Accepted/Declined), `CreatedAt`. |

### 4.3 ValueObjects/ — объекты-значения

| Файл | Описание |
|------|----------|
| `EncryptedContent.cs` | Контейнер для зашифрованных данных. Содержит: `CipherText` (byte[]) — шифртекст, `Nonce` (byte[16]) — одноразовое число для режима CTR, `Hmac` (byte[32]) — HMAC для проверки целостности, `AlgorithmId` (string) — идентификатор алгоритма ("GOST-R-34.12-2015/CTR"). |
| `DeviceFingerprint.cs` | Отпечаток устройства. `Value` — Base64 строка хэша Стрибог-256 от серийных номеров CPU, диска, BIOS. Используется для привязки аккаунта к конкретному компьютеру. |
| `HashChainEntry.cs` | Запись hash-chain для аудита. Содержит `PreviousHash` и `CurrentHash`. Текущий хэш вычисляется от предыдущего хэша + данных записи — так образуется цепочка, защищающая от подделки. |
| `PasswordHash.cs` | Хранит результат PBKDF2: `Hash` (byte[32]) и `Salt` (byte[32]). |

### 4.4 Enumerations/ — перечисления

| Файл | Значения |
|------|---------|
| `AuditAction.cs` | 41 действие для аудита: Login, Logout, Register, NoteCreated, NoteUpdated, NoteDeleted, NoteSecureDeleted, NoteExpired, TaskCreated, TaskMoved, ProjectCreated, ContactAdded, DataExported, AuditChainVerified и другие. |
| `SecurityLevel.cs` | Уровень безопасности данных: Public=0 (зелёный), Internal=1 (синий), Confidential=2 (оранжевый), Secret=3 (красный). |
| `TaskItemStatus.cs` | Статус задачи: ToDo, InProgress, Review, Done. |
| `TaskPriority.cs` | Приоритет задачи: Low, Normal, High, Critical. |
| `UserRole.cs` | Роль пользователя: User, Manager, Auditor, Admin. |

### 4.5 Interfaces/ — интерфейсы

#### Repositories/

| Интерфейс | Назначение |
|-----------|-----------|
| `IUserRepository.cs` | Методы работы с пользователями: GetByIdAsync, GetByUsernameAsync, GetByEmailAsync, GetByIdsAsync, GetAllAsync, AddAsync, UpdateAsync, AnyUsersExistAsync. |
| `INoteRepository.cs` | CRUD для заметок + GetExpiringAsync (истекающие заметки), SecureDeleteAsync (безопасное удаление с перезаписью). |
| `ITaskRepository.cs` | CRUD для задач + GetByBoardAsync, GetDueAsync (задачи с дедлайном). |
| `IContactRepository.cs` | CRUD для контактов. |
| `IAuditLogRepository.cs` | Методы аудита: AddAsync, GetPagedAsync (с фильтрами), GetLastAsync, GetBySequenceRangeAsync, GetMaxSequenceAsync. |
| `IUnitOfWork.cs` | Единица работы — оборачивает транзакцию. Метод `CommitAsync()`. |

#### Services/

| Интерфейс | Назначение |
|-----------|-----------|
| `ICryptoService.cs` | Методы криптографии: Encrypt(byte[] plaintext, byte[] key), Decrypt(EncryptedContent, byte[] key), Hash256(byte[]), ComputeHmac(byte[], byte[]), VerifyHmac(byte[], byte[], byte[]). |
| `IDeviceService.cs` | Получение отпечатка устройства: GetFingerprint() → DeviceFingerprint. |
| `IHashChainService.cs` | Вычисление и проверка hash-chain: ComputeEntry(), VerifyChain(). |
| `IKdfService.cs` | Производная ключа: DeriveKey(password, salt) → byte[64] (32 байта ключа + 32 байта HMAC-ключа). |
| `ICurrentUserContext.cs` | Контекст текущего пользователя: UserId, Role, GetEncryptionKey(). Реализован на уровне Domain (интерфейс) и Infrastructure/Server (реализация). |

### 4.6 Exceptions/ — исключения домена

| Файл | Когда выбрасывается |
|------|-------------------|
| `DomainException.cs` | Базовый класс для всех доменных исключений. |
| `IntegrityViolationException.cs` | HMAC-проверка при дешифровании не совпала — данные были изменены. |
| `NoteExpiredException.cs` | Попытка открыть уже уничтоженную заметку. |
| `DeviceMismatchException.cs` | Device fingerprint не совпадает с записанным в базе. |

---

## 5. ConSecOrg.Application — прикладной слой (CQRS)

Реализует паттерн **CQRS** (Command Query Responsibility Segregation) через библиотеку MediatR. Каждый сценарий использования — отдельный класс Command или Query с соответствующим Handler.

### 5.1 DependencyInjection.cs

Регистрирует в DI-контейнере всё содержимое Application: `MediatR`, `FluentValidation`, `AutoMapper`. Вызывается из `Program.cs` сервера.

### 5.2 Common/Behaviors/ — pipeline behaviors (перехватчики)

Выполняются для каждого запроса MediatR перед вызовом Handler'а.

| Файл | Назначение |
|------|-----------|
| `ValidationBehavior.cs` | Запускает FluentValidation для входящего запроса. Если есть ошибки — бросает `ValidationException` (→ 400 Bad Request). |
| `LoggingBehavior.cs` | Логирует начало и конец выполнения каждого запроса (имя запроса + время выполнения). |

### 5.3 Common/Exceptions/ — прикладные исключения

| Файл | HTTP-статус | Описание |
|------|------------|----------|
| `ValidationException.cs` | 400 | Ошибки валидации. Содержит словарь `Errors` — поля → список сообщений. |
| `NotFoundException.cs` | 404 | Запрошенный ресурс не найден. |
| `ForbiddenException.cs` | 403 | Недостаточно прав. |
| `AccountLockedException.cs` | 423 | Аккаунт заблокирован. |

### 5.4 Common/Helpers/

| Файл | Назначение |
|------|-----------|
| `AuditHelper.cs` | Вспомогательный статический класс для записи в журнал аудита из Handler'ов. Метод `WriteAsync()` принимает контекст, действие, тип сущности, id и статус — и создаёт `AuditLog` запись с вычисленным hash-chain. |

### 5.5 Common/Interfaces/

| Файл | Назначение |
|------|-----------|
| `ICurrentUserContext.cs` | (в Application — копия) Дублирует доменный интерфейс для использования в Handler'ах без прямой зависимости от Infrastructure. |
| `IEncryptionKeyStore.cs` | Хранилище ключей шифрования текущего сеанса: `GetKey(userId)`, `SetKey(userId, key)`, `ClearKey(userId)`. |
| `IJwtTokenService.cs` | Генерация и валидация JWT: `GenerateTokens()`, `ValidateRefreshToken()`, `GetPrincipalFromExpiredToken()`. |
| `IPasswordHasher.cs` | Хэширование и проверка пароля: `Hash(password)`, `Verify(password, hash, salt)`. |

### 5.6 Common/Mappings/

| Файл | Назначение |
|------|-----------|
| `MappingProfile.cs` | AutoMapper профили для преобразования Domain → DTO и обратно. Например: `Note → NoteDto`, `User → UserInfoDto`, `AuditLog → AuditLogDto`. |

### 5.7 Features/ — функциональные возможности (CQRS)

Каждая папка = отдельный модуль системы. Каждый файл = один сценарий использования.

#### Auth/ — аутентификация

| Файл | Описание |
|------|----------|
| `LoginCommand.cs` | Вход в систему. Валидатор проверяет Username и Password не пустые. Handler: найти пользователя → проверить блокировку → PBKDF2-сравнение пароля → проверить device_id (корп. режим) → создать JWT → создать Session → аудит Login. |
| `LogoutCommand.cs` | Выход. Handler удаляет Session из БД, пишет аудит Logout. |
| `RegisterCommand.cs` | Регистрация пользователя. В корпоративном режиме — только Admin. Возможна саморегистрация через отдельный эндпоинт. |
| `RefreshTokenCommand.cs` | Обновление JWT. Проверяет refresh token через хэш в БД. |
| `ChangePasswordCommand.cs` | Смена пароля. Проверяет старый пароль, затем хэширует и сохраняет новый. |
| `GetCurrentUserQuery.cs` | Возвращает данные текущего авторизованного пользователя (`UserInfoDto`). |

#### Notes/ — заметки

| Файл | Описание |
|------|----------|
| `CreateNoteCommand.cs` | Создание заметки. Handler шифрует Content через `ICryptoService.Encrypt()` с ключом пользователя. Пишет аудит `NoteCreated`. |
| `UpdateNoteCommand.cs` | Обновление заметки. Проверяет владельца, перешифровывает Content, пишет аудит `NoteUpdated`. |
| `DeleteNoteCommand.cs` | Удаление заметки. Вызывает `SecureDeleteAsync` (перезапись данных в БД перед DELETE). Пишет аудит `NoteSecureDeleted`. |
| `SetDestructionTimerCommand.cs` | Устанавливает дату самоуничтожения `ExpiresAt`. Пишет аудит `NoteTimerSet`. |
| `GetNotesQuery.cs` | Постраничный список заметок пользователя с фильтрами (category, securityLevel, search). |
| `GetNoteByIdQuery.cs` | Получить конкретную заметку. Дешифрует Content перед возвратом. Пишет аудит `NoteViewed`. |
| `SearchNotesQuery.cs` | Полнотекстовый поиск по заголовкам заметок. |

#### Tasks/ — задачи

| Файл | Описание |
|------|----------|
| `CreateTaskCommand.cs` | Создание задачи Kanban. Шифрует Description, пишет аудит `TaskCreated`. |
| `UpdateTaskCommand.cs` | Обновление задачи. Перешифровывает Description, пишет аудит `TaskUpdated`. |
| `MoveTaskCommand.cs` | Перемещение задачи в другую колонку Kanban. Пишет аудит `TaskMoved`. |
| `DeleteTaskCommand.cs` | Удаление задачи. Пишет аудит `TaskDeleted`. |
| `GetTasksByBoardQuery.cs` | Все задачи для Kanban-доски пользователя. Дешифрует Description для каждой. |
| `GetTasksDueQuery.cs` | Задачи с дедлайном в ближайшие N дней. |

#### Contacts/ — контакты

| Файл | Описание |
|------|----------|
| `CreateContactCommand.cs` | Создание контакта. Шифрует все поля (имя, email, телефон, заметки) по отдельности. Принимает опциональный `LinkedUserId` — ссылку на пользователя системы. |
| `UpdateContactCommand.cs` | Обновление контакта. Перешифровывает изменённые поля. |
| `DeleteContactCommand.cs` | Удаление контакта. |
| `GetContactsQuery.cs` | Список контактов текущего пользователя. Дешифрует все поля. Включает `LinkedUserId` в DTO. |
| `GetContactByIdQuery.cs` | Получить конкретный контакт. |

#### Users/ — управление пользователями

| Файл | Описание |
|------|----------|
| `UpdateUserSettingsCommand.cs` | Сохранение настроек пользователя (тема, цвет, шрифт). |
| `LockUserCommand.cs` | Блокировка пользователя (Admin only). |
| `UnlockUserCommand.cs` | Разблокировка пользователя (Admin only). |
| `AssignRoleCommand.cs` | Назначение роли пользователю (Admin only). |
| `GetUsersQuery.cs` | Список всех пользователей (Admin/Manager). |
| `GetUserSettingsQuery.cs` | Настройки текущего пользователя. |
| `GetUserSessionsQuery.cs` | Активные сессии пользователя. |

#### Audit/ — аудит

| Файл | Описание |
|------|----------|
| `GetAuditLogsQuery.cs` | Постраничный журнал аудита с фильтрами: `userId`, `dateFrom`, `dateTo`, `action`. Подгружает имена пользователей (`GetByIdsAsync`). |
| `VerifyAuditChainQuery.cs` | Проверка целостности журнала аудита. Пересчитывает hash-chain от первой до последней записи. Возвращает `(Ok=true)` или `(Ok=false, TamperedAt=N)`. |

#### Dashboard/ — дашборд безопасности

| Файл | Описание |
|------|----------|
| `GetSecurityDashboardQuery.cs` | Сводная информация: алгоритм шифрования, последний вход (дата + IP), количество активных сессий, распределение заметок по уровням безопасности, последние 10 событий аудита. |

---

## 6. ConSecOrg.Infrastructure — инфраструктурный слой

Реализует интерфейсы доменного слоя. Отвечает за работу с БД (EF Core), криптографию (ГОСТ через BouncyCastle), JWT-токены, хэширование паролей.

### 6.1 DependencyInjection.cs

Расширение `IServiceCollection`, регистрирующее все реализации Infrastructure: `AppDbContext`, репозитории, `GostCryptoService`, `WindowsDeviceService`, `JwtTokenService`, `PasswordHasher`, `HashChainService`, `KdfService`.

### 6.2 Crypto/ — криптография ГОСТ

| Файл | Назначение |
|------|-----------|
| `KuznechikEngine.cs` | Чистая реализация блочного шифра ГОСТ Р 34.12-2015 «Кузнечик» на C#. Содержит таблицы подстановки (π, π⁻¹), матрицу линейного преобразования L, операции над полем GF(2⁸). Режим CTR реализован в `GostCryptoService`. |
| `StreebogHasher.cs` | Обёртка над `Gost3411_2012_256Digest` и `Gost3411_2012_512Digest` из BouncyCastle. Методы `Hash256(byte[])` и `Hash512(byte[])`. |
| `HmacStreebog.cs` | HMAC поверх Стрибог-256. Используется для проверки целостности шифртекста (Encrypt-then-MAC схема). |
| `KdfService.cs` | PBKDF2 с PRF = HMAC-Стрибог-256, 100 000 итераций. Метод `DeriveKey(password, salt)` → byte[64]. Первые 32 байта — ключ шифрования, следующие 32 — ключ HMAC. Собственная реализация PBKDF2 (BouncyCastle 2.x не поддерживает "Streebog" как строку в `Pkcs5S2ParametersGenerator`). |
| `SecureBytes.cs` | Контейнер для секретных данных (ключей) в памяти. Использует `GCHandle.Alloc(data, GCHandleType.Pinned)` для закрепления массива. При `Dispose()` — обнуляет массив и освобождает дескриптор, предотвращая утечку ключей через GC или swap. |
| `GostCryptoService.cs` | Главная реализация `ICryptoService`. **Шифрование**: генерирует случайный Nonce (16 байт), шифрует KuznechikEngine в режиме CTR, затем вычисляет HMAC-Стрибог-256 от (Nonce‖CipherText). **Дешифрование**: СНАЧАЛА проверяет HMAC — при несовпадении бросает `IntegrityViolationException`, данные никогда не дешифруются при нарушении целостности. |

### 6.3 Device/

| Файл | Назначение |
|------|-----------|
| `WindowsDeviceService.cs` | Собирает аппаратный отпечаток через WMI: `Win32_Processor.ProcessorId`, `Win32_DiskDrive.SerialNumber`, `Win32_BIOS.SerialNumber`. Объединяет в строку `"CPU|DISK|BIOS"`, вычисляет Стрибог-256, возвращает Base64 строку как `DeviceFingerprint`. |

### 6.4 Persistence/ — работа с базой данных

#### AppDbContext.cs

Главный EF Core контекст. `DbSet<T>` для всех сущностей. Переопределён `SaveChangesAsync` для автоматического заполнения `CreatedAt`/`UpdatedAt` у `AuditableEntity`.

#### Configurations/ — настройки маппинга EF Core

Каждый файл — класс `IEntityTypeConfiguration<T>` для конкретной сущности. Определяет имена таблиц и колонок, индексы, ограничения, и главное — `OwnsOne` для `EncryptedContent`:

```csharp
builder.OwnsOne(n => n.Content, ec => {
    ec.Property(x => x.CipherText).HasColumnName("content_encrypted");
    ec.Property(x => x.Nonce).HasColumnName("content_nonce");
    ec.Property(x => x.Hmac).HasColumnName("content_hmac");
});
```

| Файл | Таблица |
|------|---------|
| `UserConfiguration.cs` | `users` |
| `NoteConfiguration.cs` | `notes` (OwnsOne для Content) |
| `TaskItemConfiguration.cs` | `task_items` (OwnsOne для Description) |
| `ContactConfiguration.cs` | `contacts` (OwnsOne для Name, Email, Phone, Notes; `linked_user_id`) |
| `AuditLogConfiguration.cs` | `audit_logs` |
| `RoleConfiguration.cs` | `roles` + seed данные ролей с фиксированными GUID |
| `ContactRequestConfiguration.cs` | `contact_requests` |
| `SharedProjectConfiguration.cs` | `shared_projects`, `shared_project_members` |

#### Repositories/ — репозитории

| Файл | Описание |
|------|----------|
| `UserRepository.cs` | Реализация `IUserRepository`. Поиск по username/email, получение списка по GUID'ам. |
| `NoteRepository.cs` | Реализация `INoteRepository`. Включает `SecureDeleteAsync` — обнуляет шифртекст через raw SQL `UPDATE notes SET content_encrypted = CRYPT_GEN_RANDOM(...)` перед удалением. |
| `TaskRepository.cs` | Реализация `ITaskRepository`. Фильтрация по пользователю, статусу, дедлайну. |
| `ContactRepository.cs` | Реализация `IContactRepository`. |
| `AuditLogRepository.cs` | Реализация `IAuditLogRepository`. Поддерживает пагинацию, фильтрацию по дате и действию, получение последней записи для hash-chain. |
| `UnitOfWork.cs` | Реализация `IUnitOfWork`. Оборачивает транзакцию EF Core, вызывает `SaveChangesAsync`. |

#### Migrations/ — EF Core миграции

Файлы миграций базы данных. Каждая миграция — пара файлов: `{timestamp}_{Name}.cs` (изменения) и `{timestamp}_{Name}.Designer.cs` (снимок модели).

| Миграция | Что добавляет |
|----------|--------------|
| `20260423201503_Initial` | Все базовые таблицы (users, notes, tasks, contacts, roles, sessions, audit_logs, categories, tags) |
| `20260427204804_AddCollaborationFeatures` | `shared_projects`, `shared_project_members`, `contact_requests` |
| `20260507090025_AddSharedProjectTasks` | `shared_project_tasks` |
| `20260507091429_AddChatAndAssignee` | `chat_messages`, поле `AssignedToUserId` в `shared_project_tasks` |
| `20260507134936_AddSharedProjectColumns` | `shared_project_columns` |
| `20260507135117_AddSharedColumnsAndTaskColumnId` | `ColumnId` в `shared_project_tasks` |
| `20260508200000_AddLinkedUserIdToContacts` | `linked_user_id` в `contacts` |
| `AddGroupChatAndFileAttachment` | `group_chats`, `group_chat_members`; поля `GroupChatId`, `AttachmentFileId`, `AttachmentSize` в `chat_messages` |
| `AddChatLastRead` | `chat_last_reads` — метки прочтения сообщений (chatKey + userId + lastReadAt) |
| `AddChatMessageEncryption` | Поля `text_cipher`, `text_nonce`, `text_hmac` в `chat_messages`; столбец `Text` удалён |
| `AppDbContextModelSnapshot.cs` | Текущий снимок полной схемы БД (генерируется EF Core автоматически) |

### 6.5 Crypto/ChatEncryptionService.cs

Шифрование текста сообщений чата на сервере. При сохранении: `GostCryptoService.Encrypt(Encoding.UTF8.GetBytes(text), serverKey)` → `TextCipher/TextNonce/TextHmac`. При чтении (`MapDto`): HMAC-проверка + дешифрование → открытый `Text`. Ключ шифрования сообщений — отдельный серверный ключ (не ключ пользователя), хранится в конфигурации. Все новые сообщения сохраняются только в зашифрованном виде.

### 6.6 Security/ — безопасность

| Файл | Назначение |
|------|-----------|
| `JwtTokenService.cs` | Генерация access-токена (60 мин) и refresh-токена (7 дней). Claims: `NameIdentifier` (userId), `Role`, `device_id`, `session_id`. Валидация токена при обновлении. |
| `PasswordHasher.cs` | PBKDF2-HMAC-Стрибог-256. `Hash(password)` → `(hash[32], salt[32])`. `Verify(password, hash, salt)` — constant-time сравнение. |
| `HashChainService.cs` | `ComputeEntry(prev, current)` — Стрибог-256 от (prevHash ‖ timestamp ‖ action ‖ entityId ‖ userId). `VerifyChain(logs)` — пересчёт цепочки, возвращает первую нарушенную запись. |
| `EncryptionKeyStore.cs` | `ConcurrentDictionary<Guid, byte[]>` в памяти. Хранит 64-байтный производный ключ (KDF output) на время сессии. `ClearKey()` обнуляет массив. |

---

## 7. ConSecOrg.Server — веб-сервер (ASP.NET Core)

ASP.NET Core 10 Web API. Принимает HTTP-запросы от WPF-клиента, обрабатывает через MediatR, возвращает JSON.

### 7.1 Program.cs

Точка входа сервера. Настраивает:
- **JWT Bearer Authentication** — секрет из `JwtSettings:Secret` (user-secrets или env)
- **Rate Limiting** — sliding window: `/auth/login` → 5 запросов/мин на IP; остальные → 200 запросов/мин на пользователя
- **Serilog** — структурированное логирование в консоль и SQL Server
- **SignalR** — хаб `/hubs/board` и `/hubs/projects`
- **CORS** — разрешён любой Origin (для WPF-клиента)
- **Swagger / OpenAPI** — документация API
- **Middleware стек** — `ExceptionHandlingMiddleware` → `DeviceValidationMiddleware` → Auth → Controllers
- **DateTime JSON** — `UtcDateTimeJsonConverter` гарантирует Z-суффикс в JSON

### 7.2 Controllers/ — контроллеры

Все контроллеры наследуют `ControllerBase` и помечены `[ApiController]` и `[Authorize]`.

| Контроллер | Маршрут | Описание |
|-----------|---------|----------|
| `AuthController.cs` | `/api/v1/auth` | Login, Logout, Register, RefreshToken, ChangePassword, ChangeEmail, GetCurrentUser, Bootstrap (первый admin), RegisterSelf |
| `NotesController.cs` | `/api/v1/notes` | CRUD заметок, поиск, SetTimer, GetExpiring |
| `TasksController.cs` | `/api/v1/tasks` | CRUD задач, MoveTask, GetDue |
| `ContactsController.cs` | `/api/v1/contacts` | CRUD контактов, поиск |
| `ContactRequestsController.cs` | `/api/v1/contacts/requests` | Отправка/принятие/отклонение заявок в контакты, список входящих/исходящих |
| `UsersController.cs` | `/api/v1/users` | Список пользователей, Lock/Unlock, AssignRole, GetSessions, DeleteSession, UserSearch |
| `AuditController.cs` | `/api/v1/audit` | Журнал аудита, VerifyChain, ExportCsv |
| `DashboardController.cs` | `/api/v1/dashboard` | GetSecurityDashboard |
| `SharedProjectsController.cs` | `/api/v1/shared-projects` | CRUD совместных проектов, Join по invite-коду, Leave, GetMembers |
| `SharedProjectTasksController.cs` | `/api/v1/shared-projects/{id}/tasks` | Задачи совместного проекта |
| `SharedProjectColumnsController.cs` | `/api/v1/shared-projects/{id}/columns` | Колонки Kanban совместного проекта |
| `ChatController.cs` | `/api/v1/chat` | Отправка сообщений, история проектного/личного чата, список диалогов. **DELETE** `/messages/{id}` — удаление своего сообщения (проверка SenderUserId) + SignalR broadcast `MessageDeleted` во все группы где было сообщение. Сообщения шифруются ГОСТ при сохранении и дешифруются при отдаче через `ChatEncryptionService`. |
| `GroupChatController.cs` | `/api/v1/group-chats` | CRUD групповых чатов: создание, получение, список участников, добавление/удаление участника, удаление. SignalR уведомление при добавлении участника (`JoinGroupChat`). |
| `FilesController.cs` | `/api/v1/files` | Upload (до 50 МБ, GUID-хранилище в `/uploads/{userId}/{guid}{ext}`, безопасный путь) и Download по `fileId`. |

### 7.3 Hubs/ — SignalR хабы

| Файл | Описание |
|------|----------|
| `BoardHub.cs` | Единственный SignalR хаб `/hubs/board`. Методы на сервере: `JoinProject(projectId)` / `LeaveProject(projectId)` — подписка на обновления Kanban-доски; `JoinGroupChat(groupChatId)` / `LeaveGroupChat(groupChatId)` — подписка на групповой чат; `MarkDirectRead(partnerId)` — уведомление о прочтении личных сообщений. События, рассылаемые клиентам: `TaskMoved`, `SharedTaskCreated/Updated/Deleted`, `SharedColumnCreated/Updated/Deleted`, `ChatMessageReceived`, `MessagesRead`, `MessageDeleted`. Вспомогательные статические методы: `ProjectGroup(id)`, `UserGroup(id)`, `GroupChatGroup(id)`. |

### 7.4 Infrastructure/

| Файл | Описание |
|------|----------|
| `CurrentUserContext.cs` | Реализация `ICurrentUserContext` для ASP.NET Core. Извлекает данные из JWT claims: `ClaimTypes.NameIdentifier` → UserId, `ClaimTypes.Role` → Role. Получает ключ шифрования из `IEncryptionKeyStore`. |
| `UtcDateTimeJsonConverter.cs` | Кастомный `JsonConverter<DateTime?>`. Обеспечивает, что все DateTime в JSON имеют суффикс `Z` (UTC), предотвращая проблемы с часовым поясом на клиенте. |

### 7.5 Middleware/ — промежуточное ПО

| Файл | Назначение |
|------|-----------|
| `ExceptionHandlingMiddleware.cs` | Перехватывает все необработанные исключения. Преобразует их в JSON-ответы с кодами HTTP и **русскоязычными сообщениями**: `ValidationException` → 400, `NotFoundException` → 404, `ForbiddenException` → 403, `AccountLockedException` → 423, `UnauthorizedAccessException` → 401, `IntegrityViolationException` → 422, `DeviceMismatchException` → 403, остальные → 500. |
| `DeviceValidationMiddleware.cs` | Проверяет заголовок `X-Device-Fingerprint` у авторизованных запросов (корп. режим). Если device_id пользователя уже записан в БД и не совпадает с заголовком — бросает `DeviceMismatchException`. |

### 7.6 appsettings.json

Конфигурация сервера. **Не содержит** секретных значений.

```json
{
  "ConnectionStrings": { "DefaultConnection": "Server=localhost;Database=ConSecOrg;..." },
  "JwtSettings": { "Issuer", "Audience", "AccessTokenExpiryMinutes": 60, "RefreshTokenExpiryDays": 7 },
  "SecuritySettings": { "MaxFailedLoginAttempts": 5, "PasswordMinLength": 12, "RequireDeviceBinding": true },
  "CryptoSettings": { "KdfIterations": 100000, "Algorithm": "GOST-R-34.12-2015" }
}
```

`JwtSettings:Secret` задаётся через `dotnet user-secrets` или переменную окружения — **никогда не коммитится в git**.

---

## 8. ConSecOrg.Shared — общие контракты

Библиотека без бизнес-логики. Содержит только DTO (Data Transfer Objects), enum'ы и константы. Подключается и клиентом, и сервером — нельзя добавлять зависимости от ASP.NET или WPF.

### 8.1 DTOs/ — объекты передачи данных

#### Auth/
| DTO | Назначение |
|-----|-----------|
| `LoginRequestDto.cs` | `{ Username, Password }` для входа |
| `LoginResponseDto.cs` | `{ AccessToken, RefreshToken, ExpiresAt, User }` — ответ на логин |
| `RegisterRequestDto.cs` | `{ Username, Email, Password, RoleId? }` |
| `RefreshRequestDto.cs` | `{ RefreshToken }` |
| `ChangePasswordRequestDto.cs` | `{ OldPassword, NewPassword }` |
| `ChangeEmailRequestDto.cs` | `{ NewEmail, Password }` |
| `UserInfoDto.cs` | `{ Id, Username, Email, Role, LastLoginAt, DeviceId }` |

#### Notes/
| DTO | Назначение |
|-----|-----------|
| `NoteDto.cs` | Данные заметки для клиента (Title, Content расшифрован, SecurityLevel, ExpiresAt, ...) |
| `CreateNoteRequestDto.cs` | `{ Title, Content, SecurityLevel, CategoryId?, ExpiresAt? }` |
| `UpdateNoteRequestDto.cs` | Те же поля, опциональные |
| `SetTimerRequestDto.cs` | `{ ExpiresAt }` |

#### Tasks/
| DTO | Назначение |
|-----|-----------|
| `TaskItemDto.cs` | Данные задачи (Title, Description расшифрован, Status, Priority, DueDate, ...) |
| `CreateTaskRequestDto.cs` | `{ Title, Description?, Status, Priority, DueDate? }` |
| `UpdateTaskRequestDto.cs` | Те же поля, опциональные |
| `MoveTaskRequestDto.cs` | `{ NewStatus, NewPosition }` |

#### Contacts/
| DTO | Назначение |
|-----|-----------|
| `ContactDto.cs` | Данные контакта (поля расшифрованы). Включает `LinkedUserId?` для открытия чата. |
| `CreateContactRequestDto.cs` | `{ Name, Email?, Phone?, Notes?, LinkedUserId? }` |
| `UpdateContactRequestDto.cs` | Те же поля, опциональные |
| `ContactRequestDto.cs` | `{ Id, SenderId, SenderUsername, ReceiverId, Status, CreatedAt }` |

#### Projects/ (совместные проекты и чат)
| DTO | Назначение |
|-----|-----------|
| `SharedProjectDto.cs` | `{ Id, Name, Description, InviteCode, OwnerId, MemberCount }` |
| `SharedProjectTaskDto.cs` | Задача совместного проекта с AssignedToUsername |
| `SharedProjectColumnDto.cs` | `{ Id, Name, Order, Color, ProjectId }` |
| `ChatMessageDto.cs` | `{ Id, Text, SenderUserId, SenderUsername, ProjectId?, GroupChatId?, ToUserId?, AttachmentUrl?, AttachmentFileId?, AttachmentFileName?, AttachmentSize?, SentAt, IsMine }`. `Text` — уже расшифрованный открытый текст. |
| `ChatSummaryDto.cs` | Сводка диалога для списка чатов: `{ ChatType (Direct/Project/Group), Title, LastMessageText, LastMessageAt, UnreadCount, ChatKey }` |
| `SendChatMessageDto.cs` | `{ ProjectId?, GroupChatId?, ToUserId?, Text, AttachmentFileId?, AttachmentFileName?, AttachmentSize? }` |
| `GroupChatDto.cs` | `{ Id, Name, CreatedByUserId, MemberCount, Members[] }` |
| `UploadFileResponseDto.cs` | `{ FileId, FileName, Url, Size }` — ответ на загрузку файла |

#### Users/
| DTO | Назначение |
|-----|-----------|
| `UserDto.cs` | `{ Id, Username, Email, Role, IsLocked, LastLoginAt }` |
| `UserSearchDto.cs` | `{ Id, Username, Email }` — результат поиска пользователей |
| `UserSettingsDto.cs` | `{ Theme, AccentColor, FontSize }` |
| `SessionDto.cs` | `{ Id, IpAddress, UserAgent, CreatedAt, ExpiresAt }` |
| `AssignRoleRequestDto.cs` | `{ RoleId }` |
| `UpdateSettingsRequestDto.cs` | `{ Theme, AccentColor, FontSize }` |

#### Audit/
| DTO | Назначение |
|-----|-----------|
| `AuditLogDto.cs` | Запись журнала: `{ Id, Username, Action, EntityType, EntityId, Timestamp, Status, IpAddress }` |
| `AuditChainVerifyResultDto.cs` | `{ IsValid, TamperedAtSequence? }` |

#### Dashboard/
| DTO | Назначение |
|-----|-----------|
| `SecurityDashboardDto.cs` | `{ Algorithm, LastLoginAt, LastLoginIp, ActiveSessionCount, NoteCountsByLevel, RecentAuditEvents }` |

### 8.2 Enums/

Дублируют доменные перечисления в виде DTO enum'ов для сериализации:
- `SecurityLevelDto.cs` — Public, Internal, Confidential, Secret
- `TaskStatusDto.cs` — ToDo, InProgress, Review, Done
- `TaskPriorityDto.cs` — Low, Normal, High, Critical
- `UserRoleDto.cs` — User, Manager, Auditor, Admin

### 8.3 Constants/

| Файл | Назначение |
|------|-----------|
| `ApiRoutes.cs` | Константы маршрутов API: `Auth.Login`, `Notes.GetAll`, и т.д. Используется в клиенте для типобезопасных запросов. |

### 8.4 Pagination/

| Файл | Назначение |
|------|-----------|
| `PagedRequest.cs` | `{ Page, PageSize, Search? }` — параметры постраничного запроса |
| `PagedResponse<T>.cs` | `{ Items, TotalCount, Page, PageSize }` — постраничный ответ |

---

## 9. ConSecOrg.Client — WPF-клиент

Десктопное приложение на WPF (.NET 10-windows). Архитектурный паттерн — **MVVM** через CommunityToolkit.Mvvm. Material Design Themes (Material Design 2.x) для визуального стиля.

### 9.1 App.xaml / App.xaml.cs

**App.xaml** — декларация ресурсов приложения. Подключает:
- `MaterialDesign2.Defaults.xaml` — стили Material Design
- `MahApps.Metro` — расширенные контролы Windows
- `Generic.xaml` — стили и конвертеры ConSecOrg

**App.xaml.cs** — точка входа. Строит `IHostBuilder` с DI-контейнером. Регистрирует:
- Все View, ViewModel'и, Services как `Singleton`/`Transient`
- `ApiClient` реализующий все API-интерфейсы
- Proxy-сервисы для переключения Personal/Corporate режима
- `ThemeEngine`, `NavigationService`, `SessionService`, `ModeService`
- Фоновые службы: `DestructionTimerService`

Метод `TryGetService<T>()` — безопасный доступ к DI из code-behind.

### 9.2 MainWindow.xaml / MainWindow.xaml.cs

Главное окно приложения. Содержит:
- `TransitionContentControl` — контейнер с анимацией FadeIn для смены сцен
- `DataTemplate` для каждой сцены: `ModeSelectionViewModel`, `LoginViewModel`, `RegisterViewModel`, `PinViewModel`, `ShellViewModel`
- `ToastOverlay` — панель для всплывающих уведомлений (правый нижний угол)
- Интеграция с H.NotifyIcon.Wpf — иконка в системном трее

`MainWindow.xaml.cs`:
- `Window_Closing` — при нажатии × окно **не закрывается**, а сворачивается в трей
- `TrayIcon_TrayMouseDoubleClick` — показывает окно из трея
- Подписка на событие `NotificationService.ShowToastRequested` — показывает Toast-уведомления

### 9.3 Controls/ — переиспользуемые элементы управления

| Файл | Описание |
|------|----------|
| `DestructionTimerWidget.xaml` | Кастомный UserControl — круговой обратный отсчёт. Canvas с ArcSegment показывает прогресс дуги. Цвет меняется: зелёный (>30 мин) → оранжевый (>5 мин) → красный (<5 мин). `DependencyProperty ExpiresAt`. `DispatcherTimer` обновляется каждую секунду. |
| `DestructionTimerWidget.xaml.cs` | Code-behind виджета таймера. |
| `ToastNotification.cs` | Класс `ToastNotificationHelper` с методом `Show(panel, message, level)` — создаёт анимированный Border справа снизу, автоматически скрывается через 3.5 сек. |
| `TransitionContentControl.cs` | Наследник `ContentControl` с переопределённым `OnContentChanged` — добавляет FadeIn (180ms, CubicEase) при смене содержимого. |

### 9.4 Infrastructure/ — инфраструктура клиента

#### Api/ — HTTP API-клиент

| Файл | Назначение |
|------|-----------|
| `ApiClient.cs` | Единственная реализация всех API-интерфейсов. Использует RestSharp. Главный метод `SendAsync<T>` — автоматически добавляет JWT заголовок, перехватывает сетевые ошибки и HTTP-коды, переводит их в **русские сообщения** через `BuildRussianError()`. Polly retry — 2 попытки при `503 Service Unavailable`. |
| `IAuthApiService.cs` | Интерфейс: Login, Logout, Register, Refresh, ChangePassword, ChangeEmail, GetCurrentUser, Bootstrap, RegisterSelf |
| `INotesApiService.cs` | Интерфейс: GetAll (с фильтрами), GetById, Create, Update, Delete, SetTimer, GetExpiring, Search |
| `ITasksApiService.cs` | Интерфейс: GetAll, Create, Update, Move, Delete, GetDue |
| `IContactsApiService.cs` | Интерфейс: GetAll, GetById, Create, Update, Delete, Search, SendRequest, AcceptRequest, DeclineRequest, GetIncomingRequests, GetOutgoingRequests |
| `IUsersManagementApiService.cs` | Интерфейс: GetAll, GetById, Lock, Unlock, AssignRole, GetSessions, DeleteSession |
| `IUserSearchApiService.cs` | Интерфейс: Search(query) → UserSearchDto[] |
| `IAuditApiService.cs` | Интерфейс: GetLogs (с фильтрами), VerifyChain, ExportCsv |
| `IDashboardApiService.cs` | Интерфейс: GetSecurityDashboard |
| `ISharedProjectsApiService.cs` | Интерфейс: GetAll, Create, Join, Leave, Delete, GetMembers, GetTasks, GetColumns, CreateTask, UpdateTask, DeleteTask, CreateColumn, UpdateColumn, DeleteColumn |
| `IChatApiService.cs` | Интерфейс: `SendMessage`, `GetProjectMessages`, `GetDirectMessages`, `GetGroupMessages`, `GetChats`, `UploadFile`, `DownloadFile`, `DeleteChatMessageAsync`, `CreateGroupChat`, `GetGroupChats`, `AddGroupMember`, `RemoveGroupMember`, `DeleteGroupChat` |

#### Local/ — локальный режим (Personal)

| Файл | Назначение |
|------|-----------|
| `PersonalDbContext.cs` | EF Core контекст для `(localdb)\MSSQLLocalDB`. Строка подключения — `ConSecOrg_Personal`. Вызывает `EnsureCreated()` при старте — автоматически создаёт базу данных. |
| `LocalUserStore.cs` | Хранилище PIN-кода в `%AppData%\ConSecOrg\{userId}\local_config.json`. Содержит соль и verifier (PBKDF2 от PIN). Методы: `CreatePin()`, `VerifyPin()`, `GetOrCreateUserId()`. |
| `LocalNotesService.cs` | Реализует `INotesApiService` для персонального режима. Работает напрямую с `PersonalDbContext`. Шифрует через `ICryptoService` с ключом из `SessionService`. `SecureDeleteAsync` — обнуляет поля в LocalDB перед удалением. |
| `LocalTasksService.cs` | Реализует `ITasksApiService` для персонального режима. Работает с PersonalDbContext. Шифрует описание задач. |
| `NotesServiceProxy.cs` | Паттерн Proxy. `INotesApiService`, который делегирует вызовы либо в `ApiClient` (корп.), либо в `LocalNotesService` (персонал.) в зависимости от `ModeService.IsPersonal`. |
| `TasksServiceProxy.cs` | Аналогично для задач. |

### 9.5 Services/ — сервисы клиента

| Файл | Назначение |
|------|-----------|
| `ModeService.cs` | Хранит текущий режим работы (`IsPersonal`/`IsCorporate`) и URL сервера (`ServerUrl`). Метод `SetCorporate(url)`, `SetPersonal()`. |
| `SessionService.cs` | JWT lifecycle. Хранит `AccessToken` в памяти (SecureBytes). `SetCorporateSession(response)` — сохраняет токены и запускает таймер авто-обновления. `SetPersonalSession(userId, key)` — синтетическая сессия без JWT. `Logout()` — обнуляет ключи, очищает данные. |
| `NavigationService.cs` | Stack-based навигация для внутренних страниц Shell. `NavigateTo<T>()`, `GoBack()`. Используется для переходов между Notes, Tasks, Calendar и т.д. |
| `NotificationService.cs` | Event-based уведомления. События: `ShowToast(message, level)`, `NoteExpiringSoon(title, minutes)`, `NoteDestroyedByTimer(title)`. Поднимает `ShowToastRequested` → `MainWindow` показывает Toast. |
| `DestructionTimerService.cs` | `BackgroundService`. `PeriodicTimer` каждые 60 секунд. Загружает заметки с `ExpiresAt ≤ UtcNow + 30 мин`. Уведомляет за 30 мин и 5 мин. При `ExpiresAt ≤ UtcNow` — вызывает `SecureDeleteAsync` и поднимает `NoteTimerExpired` событие. `HashSet<Guid> _alreadyProcessed` — каждая заметка обрабатывается ровно один раз. |
| `BoardHubClient.cs` | SignalR клиент для `/hubs/board` (задачи Kanban). `EnsureConnectedAsync()` — ленивое подключение с автореконнектом. `JoinBoard(boardId)` / `LeaveBoard(boardId)` — подписка. `NotifyTaskMovedAsync` — сигнализирует о перемещении задачи. Событие `TaskMoved` → `KanbanBoardViewModel` перезагружает доску. |
| `SharedProjectsHubClient.cs` | Единый SignalR клиент для `/hubs/board` (совместные проекты и чаты). События задач: `SharedTaskCreated/Updated/Deleted/Moved`; события колонок: `SharedColumnCreated/Updated/Deleted/Reordered`; события чата: `ChatMessageReceived`, `MessagesRead`, **`MessageDeleted`** (Guid messageId) — удаляет сообщение из UI через `Dispatcher.BeginInvoke`. Методы: `EnsureConnectedAsync`, `SubscribeAsync(projectId)`, `JoinGroupChatAsync(groupChatId)`, `LeaveGroupChatAsync`, `NotifyDirectReadAsync`. |
| `ProjectService.cs` | Управляет пользовательскими проектами/досками (Kanban). Хранит конфигурацию в `%AppData%\ConSecOrg\{userId}\projects.json`. Содержит `ObservableCollection<BoardItem>` — доски в сайдбаре. `TaskProjectMapping` — привязка task_id к projectId+boardId. Методы: `CreateProject`, `DeleteProject`, `AddBoard`, `DeleteBoard`, `SetColumnColor`, `LoadForUser`. |
| `NoteMetaService.cs` | Хранит дополнительные метаданные заметок (теги, дата) в `note_meta.json`. Расширяет NoteDto без изменения схемы БД. |
| `TaskMetaService.cs` | Хранит метаданные задач в `task_meta.json`: теги, описание, `IsCompleted`, подзадачи (`SubTaskItem[]`), `AssigneeUserId`. Методы: `GetTags`, `SetTags`, `GetIsCompleted`, `SetIsCompleted`, `GetSubTasks`, `AddSubTask`, `ToggleSubTask`, `GetAssignee`, `SetAssignee`. |
| `UserSettingsService.cs` | Загружает/сохраняет настройки пользователя (тема, акцент, шрифт) с сервера и применяет их через `ThemeEngine`. |
| `ChatService.cs` | Вспомогательный сервис для инициализации чата — определяет `LinkedUserId` у контакта и открывает нужный тип чата. |

### 9.6 Themes/ — темизация

| Файл | Назначение |
|------|-----------|
| `ThemeEngine.cs` | Главный движок тем. Singleton. Метод `Apply(isDark, accentHex, backgroundImagePath)` — динамически перестраивает `_dynamicTheme` (ResourceDictionary): заполняет ~20 именованных ресурсов-кистей (`PrimaryBackground`, `SecondaryBackground`, `AccentBrush`, `CardBackground`, `SidebarBackground`, `NavActiveBackground`, `NavActiveForeground`, `BorderBrush`, `AccentAlpha2Brush`, и др.). Вычисляет тёмный вариант акцента через HSL (−20% яркость). Вызывает `PaletteHelper.SetTheme()` для обновления Material Design кнопок/чекбоксов. |
| `Generic.xaml` | Главный ResourceDictionary со всеми стилями проекта. Содержит: `BoolToVisibilityConverter`, `InvertedBoolToVisibilityConverter`, `ImagePathConverter`, `DateTimeToRelativeConverter`, `SecurityLevelToColorConverter`, `HexColorToBrushConverter`, `HexToAlpha30Converter`, `FirstLetterConverter`, `InverseBoolConverter`, и другие конвертеры. Стили: `FlatIconButton`, `SidebarNavButton`, `SidebarNavButtonActive`, `KanbanCard`, `PillButton`, `PillButtonActive`, `ChipBadge`, `NoteCard`, `ModeCard`, `ColorSwatchButton`, `CircleCheckBox`, `MaterialDesignOutlinedTextBox`, `MaterialDesignOutlinedComboBox` с hint-текстом и т.д. |
| `BindingProxy.cs` | `Freezable`-обёртка для передачи DataContext из родительского контекста в `ContextMenu` (который не наследует DataContext автоматически в WPF). |

#### Converters/ — конвертеры значений (IValueConverter)

| Файл | Преобразование |
|------|----------------|
| `BoolToVisibilityConverter.cs` | `bool → Visibility` (true → Visible, false → Collapsed) |
| `DateTimeToRelativeConverter.cs` | `DateTime → "только что" / "5 мин назад" / "3 ч. назад" / "вчера" / "14 мая"`. Обрабатывает `Kind=Unspecified` как UTC. |
| `EqualityMultiConverter.cs` | `IMultiValueConverter` — сравнивает два объекта, возвращает bool. Используется для подсветки активного проекта в сайдбаре. |
| `FirstLetterConverter.cs` | `string → string` — первая буква верхнего регистра. Для аватара (круг с буквой). |
| `HexColorToBrushConverter.cs` | `"#E53935" → SolidColorBrush` |
| `HexToAlpha30Converter.cs` | `"#E53935" → SolidColorBrush(Alpha=0x4D)` — 30% прозрачность для бейджей колонок. |
| `ImagePathConverter.cs` | `string (URL) → BitmapImage`. Поддерживает абсолютные и относительные URI. При ошибке возвращает null (не крашится). |
| `InverseBoolConverter.cs` | `bool → bool` инверсия |
| `NullToVisibilityConverter.cs` | `null → Collapsed, not null → Visible` |
| `SecurityLevelToColorConverter.cs` | `SecurityLevel → SolidColorBrush` (Public=зелёный, Internal=синий, Confidential=оранжевый, Secret=красный) |
| `SecurityLevelToRussianConverter.cs` | `SecurityLevel → string` по-русски (Открытый, Внутренний, Конфиденциальный, Секретный) |
| `StringEqualityConverter.cs` | `string → bool` — сравнивает со значением из параметра конвертера |
| `StringEqualityToVisibilityConverter.cs` | `string → Visibility` — видимость при совпадении строки |

### 9.7 ViewModels/ — модели представления

#### Base/

| Файл | Описание |
|------|----------|
| `BaseViewModel.cs` | Базовый ViewModel. Наследует `ObservableObject` (CommunityToolkit.Mvvm). Свойства: `IsLoading`, `ErrorMessage`. Методы: `RunSafeAsync(action)` — оборачивает async действие, ставит `IsLoading=true`, ловит исключения в `ErrorMessage`. |
| `BasePageViewModel.cs` | Базовая страница. Добавляет `Title`, виртуальный `OnActivatedAsync()` — вызывается при навигации на страницу. |

#### AppViewModel.cs

Корневой ViewModel. Управляет сценами: `ModeSelectionViewModel`, `LoginViewModel`, `RegisterViewModel`, `PinViewModel`, `ShellViewModel`. `CurrentScene` — текущая активная сцена. Методы: `ShowModeSelection()`, `ShowLogin()`, `ShowPin()`, `ShowShell()`. Подписан на `SessionService.SessionExpired` → возвращает на экран входа.

#### Startup/

| Файл | Описание |
|------|----------|
| `ModeSelectionViewModel.cs` | Экран выбора режима. Два поля для URL-сервера. `SelectPersonalCommand`, `SelectCorporateCommand(url)`. |
| `LoginViewModel.cs` | Форма входа. `Username`, `Password`. `LoginCommand` — вызывает `IAuthApiService.LoginAsync()`, при успехе → `AppViewModel.ShowShell()`. |
| `RegisterViewModel.cs` | Форма регистрации. Саморегистрация через `RegisterSelfAsync()`. |
| `PinViewModel.cs` | PIN экран для персонального режима. Два режима: `Create` (создать PIN + подтвердить) и `Verify` (ввести существующий). При верном PIN вычисляет PBKDF2-ключ и передаёт в `SessionService.SetPersonalSession()`. |

#### Shell/

| Файл | Описание |
|------|----------|
| `ShellViewModel.cs` | Главная оболочка приложения после входа. Управляет: `CurrentPage` (активная страница), `SidebarItems` (пункты навигации), `Projects` (`ObservableCollection<BoardItem>` — Kanban-доски пользователя). Команды навигации: `NavigateNotesCommand`, `NavigateTasksCommand`, `NavigateCalendarCommand`, и т.д. `IsAdminOrAuditor` — видимость Аудит/Безопасность для обычных пользователей. `LoadUserDataAsync()` — при входе загружает настройки и применяет тему. `DeleteBoardCommand` — удаление суб-доски с подтверждением. |
| `SharedProjectsViewModel.cs` | Список совместных проектов. Методы Join, Create, Leave. |
| `SharedMemberInfo.cs` | Вспомогательный ViewModel для отображения участника совместного проекта (аватар, username, роль). |

#### Notes/

| Файл | Описание |
|------|----------|
| `NotesListViewModel.cs` | Список заметок. `_allNotes` — полный список, `FilteredNotes` — после фильтрации и сортировки. Фильтры: `SecurityLevelFilter` (Public/Internal/Confidential/Secret), `FilterHasTimer`, строка поиска. Сортировка: по дате (новые/старые), по алфавиту, по уровню. Виды: карточки (WrapPanel) или список (compact). Подписан на `DestructionTimerService.NoteTimerExpired` → удаляет из UI. `DeleteNoteAsync()` — с `ConfirmDialog`. |
| `NoteEditorViewModel.cs` | Редактор заметки (создание и редактирование). `Title`, `Content` (Markdown), `SecurityLevel`, `CategoryId`, `ExpiresAt`. Поддержка пресетов таймера (1ч/8ч/24ч/72ч). Сохраняет UTC-время для таймера. |

#### Tasks/

| Файл | Описание |
|------|----------|
| `KanbanBoardViewModel.cs` | Главный ViewModel Kanban-доски. `BoardTabs` — вкладки досок активного проекта. `Columns` — `ObservableCollection<KanbanColumnViewModel>`. Загружает задачи через прокси, группирует по статусу. Команды: `CreateColumnCommand`, `PersistColumnOrderCommand`. Подключается к SignalR (`BoardHubClient`) в корп. режиме. `OnRemoteTaskMoved` → перезагружает доску. |
| `KanbanColumnViewModel.cs` | Один столбец Kanban. `Tasks` — `ObservableCollection<TaskCardViewModel>`. `Color` — HEX цвет заголовка. `ShowColorPicker`, `SetColorCommand` — смена цвета с палитрой 12 цветов. `CreateTaskCommand` (быстрое добавление), `DeleteColumnCommand` (с подтверждением). |
| `TaskCardViewModel.cs` | Карточка задачи. Оборачивает `TaskItemDto`. `IsCompleted` — через `TaskMetaService`. `Tags` — из `TaskMetaService`. `Priority` с цветным бейджем. `SubTasks`, `IsSubTasksExpanded`, `SubTaskProgress` (0-1 для ProgressBar). Команды: `ToggleCompleteCommand`, `EditCommand`, `DeleteCommand`, `AddSubTaskCommand`, `ToggleSubTaskCommand`, `RemoveSubTaskCommand`. |
| `TaskDetailViewModel.cs` | Детальный просмотр задачи в диалоге. Показывает все поля, теги, подзадачи. `EditCommand` → открывает `TaskEditDialog`. |
| `TaskEditViewModel.cs` | Форма создания/редактирования задачи: Title, Priority, DueDate, Tags (chips с удалением), Description (многострочный). |
| `MyTasksViewModel.cs` | Страница «Мои задачи». Вкладки: «На мне», «Порученные», «Приватные», «Избранные». Таблица с колонками: задача+путь, автор, дата, дедлайн, стикеры. Поиск, сортировка по колонкам. |
| `ChatPanelViewModel.cs` | Боковая панель чата для персонального Kanban (не используется в корп. режиме). |
| `SubTaskEditViewModel.cs` | ViewModel для диалога создания подзадачи. |

#### Calendar/

| Файл | Описание |
|------|----------|
| `CalendarViewModel.cs` | Двурежимный календарь. **Месяц**: `CalendarDay[42]` — 6 строк × 7 дней. `NoteCount` на каждый день. `CalendarDayTask[]` — до 3 баров задач (с дедлайном в этот день) + "+ ещё N". **Неделя**: `WeekDayViewModel[7]` — 7 дней с `WeekTaskItem[]` (task bars по всему диапазону CreatedAt → DueDate). Клик по дню — боковая панель со списком задач дня и кнопками «Создать задачу» / «Создать заметку». |

#### Contacts/

| Файл | Описание |
|------|----------|
| `ContactsViewModel.cs` | Master-detail список контактов. `FilterQuery` — поиск в реальном времени по имени/email/телефону. Вкладки: Контакты, Входящие заявки, Исходящие. `AcceptRequestAsync` — создаёт контакт с `LinkedUserId = request.SenderId`. `AutoAddMutualContactsAsync` — проверяет входящие заявки и автодобавляет взаимных контактов. |

#### Company/ (корпоративные функции)

| Файл | Описание |
|------|----------|
| `CompanyViewModel.cs` | Страница «Моя компания». Создание/вступление в совместные проекты. WrapPanel карточек проектов с invite-кодом. |
| `SharedProjectBoardViewModel.cs` | Kanban-доска совместного проекта. Аналогична `KanbanBoardViewModel` но работает с `ISharedProjectsApiService`. Колонки — `SharedProjectColumnDto`. Задачи — `SharedTaskCardViewModel`. |
| `SharedChatPanelViewModel.cs` | Боковая панель чата (проектный + личный). `OpenProject(projectId)` / `OpenDirect(otherUserId)` / `OpenChatList()`. Emoji picker (96 эмодзи). Прикрепление файлов (до 20 МБ). `ResolveAttachmentUrl()` — дополняет относительный URL сервером из `ModeService.ServerUrl`. Подписан на `SharedProjectsHubClient.ChatMessageReceived`. |
| `SharedTaskEditViewModel.cs` | Форма создания/редактирования задачи совместного проекта. Для Admin/Manager — выбор исполнителя из `ObservableCollection<SharedMemberInfo>`. Для User — автоназначение. |

#### Chats/ (корпоративный режим)

| Файл | Описание |
|------|----------|
| `ChatsViewModel.cs` | Полностраничный мессенджер. **Список диалогов**: все диалоги (личные, проектные, групповые) со сводкой последнего сообщения, временем, счётчиком непрочитанных. **Сообщения**: `_rawMessages` (ObservableCollection<ChatMsgVm>) → `ChatItems` (группировка по дате-разделителям). `ChatMsgVm` — вложенный класс со свойствами: `Id`, `Text`, `SenderName`, `IsMine`, `SentAt`, `HasAttachment`, `IsImageAttachment` (проверка расширения .png/.jpg/.gif/.webp), `IsNonImageAttachment`, `AttachmentUrl` (`{ServerUrl}/api/v1/files/{fileId}`). **Отправка**: текст + опциональный файл через `UploadFileAsync`. **Inline-превью**: изображения отображаются прямо в пузырьке (кликабельно для скачивания), остальные файлы — кнопка загрузки. **Удаление сообщений**: `DeleteMessageAsync(msg)` — `DELETE /api/v1/chat/messages/{id}` (только свои), SignalR событие `MessageDeleted` → `OnMessageDeleted(Guid)` → `Dispatcher.BeginInvoke(RemoveMessageLocally)`. **Контекстное меню**: правый клик на пузырьке — «Копировать текст» (всегда) + «Удалить» (только свои). **Создание группы**: `CreateGroupChatAsync`. **Реал-тайм**: подписка на `SharedProjectsHubClient.ChatMessageReceived` и `MessageDeleted`. Emoji picker (96 символов). |

#### Dashboard/

| Файл | Описание |
|------|----------|
| `SecurityDashboardViewModel.cs` | Загружает `SecurityDashboardDto` и отображает: алгоритм шифрования, последний вход, активные сессии, заметки по уровням, ленту последних аудит-событий. |

#### Audit/

| Файл | Описание |
|------|----------|
| `AuditLogViewModel.cs` | Журнал аудита с пагинацией. Фильтры: DateFrom, DateTo, Action (ComboBox с 41 действием). Кнопки: «Применить», «Сбросить», «Проверить цепочку», «Экспорт CSV» (SaveFileDialog → `/api/v1/audit/export`). |

#### Settings/

| Файл | Описание |
|------|----------|
| `SettingsViewModel.cs` | Настройки. Вкладки: «Внешний вид» (тема Light/Dark, акцентный цвет из палитры + HEX, фоновое изображение + opacity, размер шрифта), «Профиль» (аватар, DisplayName, режим, ГОСТ-информация). Команды: `SetThemeCommand`, `SetAccentCommand`, `LogoutCommand`, `ChangePasswordCommand`, `ChangeEmailCommand`. |
| `ChangePasswordDialogViewModel.cs` | Смена пароля: OldPassword, NewPassword, ConfirmNewPassword. |
| `ChangeEmailDialogViewModel.cs` | Смена email: NewEmail, Password (для подтверждения). |

#### Reports/

| Файл | Описание |
|------|----------|
| `ReportsViewModel.cs` | 4 вкладки отчётов: 1) Общий (таблица задач с фильтрацией, `GeneralReportRow`); 2) Таблицы (`SavedReport` — сохранённые шаблоны); 3) Задачи сотрудников (`EmployeeTaskRow` — с именем исполнителя); 4) Время в колонках (Gantt chart, фильтр по проекту/доске). `ReportConfigDialog` — настройки отчёта (даты, группировка). `AddColumnCommand` — добавление колонок в таблицу. |

#### Users/

| Файл | Описание |
|------|----------|
| `UsersManagementViewModel.cs` | Управление пользователями (Admin). Таблица: аватар, username, email, RoleComboBox, статус (Active/Locked), LastLogin, кнопка Lock/Unlock. `AssignRoleAsync`, `LockAsync`, `UnlockAsync`. |

### 9.8 Views/ — представления (XAML)

#### Shell/
| Файл | Описание |
|------|----------|
| `ShellView.xaml` | Главный контейнер. 3 колонки: Sidebar (240px) + TransitionContentControl (страницы) + SharedChatPanel (0-380px). Sidebar содержит: аватар пользователя, навигационные кнопки (каждая со стилем `SidebarNavButton`/`SidebarNavButtonActive`), секцию «ПРОЕКТЫ» с `ItemsControl` для Kanban-досок. |
| `SharedProjectsDialog.xaml` | Диалог управления совместными проектами (устаревший, заменён `CompanyView`). |

#### Startup/
| Файл | Описание |
|------|----------|
| `ModeSelectionView.xaml` | Экран выбора режима. 2 карточки: «Персональный» (иконка замка, описание) и «Корпоративный» (иконка сети + TextBox для URL). ГОСТ-бейдж снизу. |
| `LoginView.xaml` | Форма входа. Logo, TextBox Username, PasswordBox Password, кнопка «Войти», ссылка «Нет аккаунта?». |
| `RegisterView.xaml` | Форма регистрации. Username, Email, Password, ConfirmPassword. |
| `PinView.xaml` | PIN экран. Показывает «Создайте PIN» или «Введите PIN». 2 PasswordBox (второй только при создании). |

#### Notes/
| Файл | Описание |
|------|----------|
| `NotesListView.xaml` | Список заметок. Toolbar: поиск, сортировка (пилль-кнопки), переключатель карточки/список, фильтры по уровню безопасности (4 цветных пилля), «С таймером». `WrapPanel` карточек или `ListView` компактных строк. На карточках — `DestructionTimerWidget` если есть ExpiresAt. |
| `NoteEditorView.xaml` | Двухпанельный редактор. Слева — TextBox (Markdown), справа — `FlowDocumentScrollViewer` (MdXaml рендеринг). Toolbar: SecurityLevel ComboBox (с русскими названиями и цветными кружками), категория, кнопка таймера (DatePicker + TimePicker + пресеты). |

#### Tasks/
| Файл | Описание |
|------|----------|
| `KanbanBoardView.xaml` | Канбан-доска. Шапка с вкладками досок. Горизонтальный `ItemsControl` колонок. Каждая колонка: заголовок `[●][Name][Badge][⋮]`, `[+ Добавить задачу]` кнопка, вертикальный список карточек. `ContextMenu` колонки: Переименовать, Удалить, цветовая палитра 12 цветов (UniformGrid). Карточки: circle checkbox + название + стикер приоритета + дедлайн бейдж + expand/collapse подзадач + ProgressBar прогресса подзадач. Drag-and-drop через `DragDrop.DoDragDrop`. |
| `TaskEditDialog.xaml` | Диалог создания/редактирования задачи. Поля: название, Priority ComboBox (с цветными иконками флага), DatePicker дедлайна, теги (chip-input), описание. |
| `TaskDetailDialog.xaml` | Просмотр полной информации задачи. |
| `MyTasksView.xaml` | Страница «Мои задачи». Вкладки + таблица + поиск. |
| `SubTaskEditDialog.xaml` | Диалог добавления подзадачи. |

#### Calendar/
| Файл | Описание |
|------|----------|
| `CalendarView.xaml` | Переключатель Месяц/Неделя. **Месяц**: `UniformGrid Columns=7` с ячейками дней. В каждой ячейке: число, маркер «сегодня», task bars (до 3 + "+ ещё N"), `NoteCount` точка. **Неделя**: `Grid 7 колонок` с task bars по полной ширине диапазона дат. Боковая панель при клике на день — список задач + кнопки создания. |

#### Contacts/
| Файл | Описание |
|------|----------|
| `ContactsView.xaml` | Master-detail. Слева: поле поиска + список контактов. Справа: форма редактирования выбранного контакта (inline). Вкладки: Контакты / Входящие заявки / Исходящие заявки. Кнопка «Написать» — открывает чат если есть `LinkedUserId`. |

#### Company/
| Файл | Описание |
|------|----------|
| `CompanyView.xaml` | Страница компании. Create/Join блок + WrapPanel проектов. Каждый проект — карточка с Name, InviteCode, количеством участников, кнопками Открыть/Выйти. |
| `SharedProjectBoardView.xaml` | Kanban для совместного проекта. Аналогична `KanbanBoardView` но с поддержкой назначения исполнителей. Полноценная секция подзадач: рекурсивный `DataTemplate` `SharedCardSubTaskTemplate`, BindingProxy, expand/collapse, inline добавление. |
| `SharedChatPanelView.xaml` | Боковая панель чата. Header с заголовком + кнопка назад/закрыть. Режим «Список чатов» — `ItemsControl` диалогов. Режим «Сообщения» — `ItemsControl` сообщений (аватар + bubble + изображение/файл). Input строка: кнопка emoji + прикрепить файл + TextBox + кнопка отправить. Popup с 96 emoji. |
| `SharedTaskEditDialog.xaml` | Диалог задачи совместного проекта с выбором исполнителя. |

#### Chats/ (корпоративный режим)
| Файл | Описание |
|------|----------|
| `ChatsView.xaml` | Двухпанельный мессенджер. **Левая панель** (список чатов): `ItemsControl` диалогов — аватар (круг с первой буквой), название, последнее сообщение, время, бейдж непрочитанных. **Правая панель** (сообщения): `ItemsControl` с `ChatItems` — разделители дат и пузырьки сообщений. Пузырёк «своих» сообщений: `StackPanel[hover-кнопка удаления (красный TrashCanOutline) + Border с ContextMenu]`. ContextMenu через паттерн `Tag=ChatsViewModel` + `PlacementTarget.Tag.Command` (пересекает visual tree граница). Пункты: «Копировать текст» (всегда) и «Удалить» (красный, только свои). Inline-превью изображений (MaxWidth=320, клик=скачать); иначе — кнопка-загрузка с иконкой и именем файла. **Input строка**: кнопка emoji (ContextMenu с 96 символами) + кнопка прикрепить файл (FileDialog) + TextBox + кнопка отправить. Диалог «Создать группу» через `MaterialDesign.DialogHost`. |

#### Dashboard/
| Файл | Описание |
|------|----------|
| `SecurityDashboardView.xaml` | Material Design Cards: алгоритм шифрования (зелёный замок), последний вход (IP, время), активные сессии, распределение заметок по уровням (цветные полоски), лента аудита. |

#### Audit/
| Файл | Описание |
|------|----------|
| `AuditLogView.xaml` | DataGrid с пагинацией. Фильтры: DatePicker «С даты» / «По дату» + ComboBox «Действие». Кнопки: «Применить», «Сбросить», «Проверить целостность», «Экспорт CSV». Зелёный/красный баннер результата проверки chain. |

#### Settings/
| Файл | Описание |
|------|----------|
| `SettingsView.xaml` | 2 вкладки. «Внешний вид»: toggle тёмная/светлая тема, акцент-цвет (10 кнопок + HEX поле + превью), фоновое изображение (путь + SliderOpacity), размер шрифта. «Профиль»: аватар (круг с буквой), имя, режим, кнопки Сменить пароль / Сменить email / Выйти, блок безопасности (алгоритм, количество итераций KDF). |
| `ChangePasswordDialog.xaml` | Три PasswordBox: старый, новый, повтор. |
| `ChangeEmailDialog.xaml` | TextBox нового email + PasswordBox для подтверждения. |

#### Dialogs/
| Файл | Описание |
|------|----------|
| `ConfirmDialog.xaml` | Современный Material Design диалог подтверждения. Принимает `Message` в конструкторе. Кнопки «Да» (акцент) / «Отмена». Возвращает `DialogResult=true` при подтверждении. Используется везде вместо `MessageBox.Show`. |
| `NoteDetailDialog.xaml` | Быстрый просмотр содержимого заметки. |
| `PinPromptDialog.xaml` | Запрос PIN в контексте диалога (не полноэкранный). |

#### Reports/
| Файл | Описание |
|------|----------|
| `ReportsView.xaml` | 4 вкладки: Общий отчёт (DataGrid + фильтры + Export), Таблицы (кастомные отчёты), Задачи сотрудников (Bar chart + таблица), Время в колонках (Gantt chart на Canvas + фильтр). |
| `ReportConfigDialog.xaml` | Настройки периода и параметров отчёта. |

#### Users/
| Файл | Описание |
|------|----------|
| `UsersManagementView.xaml` | DataGrid пользователей. Колонки: аватар, Username, Email, Role (ComboBox), Статус (пилль), LastLogin, кнопка Lock/Unlock. Только для Admin. |

---

## 10. Тестовые проекты

### tests/ConSecOrg.Infrastructure.Tests/

Самый важный тестовый проект — покрывает криптографию.

| Файл | Тесты |
|------|-------|
| `Crypto/KuznechikEngineTests.cs` | 7 тестов: официальный тест-вектор ГОСТ Р 34.12-2015 (Encrypt 128-битного блока), обратное дешифрование, CTR round-trip. |
| `Crypto/GostCryptoServiceTests.cs` | 12 тестов: round-trip (Encrypt→Decrypt), два шифрования с разным nonce не дают одинаковый результат, изменение 1 байта CipherText/Nonce/HMAC → `IntegrityViolationException`. |
| `Security/HashChainServiceTests.cs` | 8 тестов: корректная цепочка из 5 записей, подмена CurrentHash/PreviousHash/Action обнаруживается, обратный порядок не проходит. |

### tests/ConSecOrg.Application.Tests/

| Файл | Тесты |
|------|-------|
| `Auth/LoginCommandHandlerTests.cs` | 10 тестов: успешный вход, неизвестный пользователь (NotFoundException), заблокированный аккаунт (AccountLockedException), неверный пароль (исключение), инкремент счётчика, 5-я неудача → блокировка + аудит AccountLocked, успех сбрасывает счётчик. |

### tests/ConSecOrg.Domain.Tests/, tests/ConSecOrg.Server.Tests/

Заготовки для будущих тестов (`UnitTest1.cs` с placeholder-тестом).

---

## 11. База данных — схема таблиц

### Корпоративный режим
**Connection string**: `Server=localhost;Database=ConSecOrg;Trusted_Connection=True;TrustServerCertificate=True;`

### Персональный режим
**Connection string**: `Server=(localdb)\MSSQLLocalDB;Database=ConSecOrg_Personal;Trusted_Connection=True;`

### Таблицы

| Таблица | Описание |
|---------|----------|
| `roles` | Роли: id, name, permissions (JSON). 4 записи: Admin/Manager/Auditor/User с фиксированными GUID. |
| `users` | Пользователи: username, email, password_hash, salt, role_id, device_id, is_locked, failed_attempts, last_login_at, last_login_ip. |
| `user_settings` | Настройки: user_id (1:1), theme, accent_color, font_size, layout_settings (JSON). |
| `categories` | Категории заметок: user_id, name, color, icon. |
| `notes` | Заметки: title, content_encrypted/nonce/hmac (VARBINARY), security_level, is_pinned, is_template, expires_at. |
| `tags` + `note_tags` | Теги и связи many-to-many с заметками. |
| `task_items` | Задачи: title, description_encrypted/nonce/hmac, status, priority, board_column, column_position, due_date. |
| `contacts` | Контакты: name/email/phone/notes — все зашифрованы (6 VARBINARY колонок), linked_user_id. |
| `sessions` | JWT сессии: token_hash (Стрибог-256 от refresh token), ip_address, user_agent, device_id, expires_at. |
| `audit_logs` | Аудит: action, entity_type, entity_id, timestamp, status, ip_address, sequence_num (IDENTITY), previous_hash, current_hash. |
| `contact_requests` | Заявки: sender_id, receiver_id, status (Pending/Accepted/Declined). |
| `shared_projects` | Проекты: name, description, invite_code (уникальный), owner_id. |
| `shared_project_members` | Участники: project_id, user_id, joined_at. |
| `shared_project_columns` | Колонки Kanban: project_id, name, order, color. |
| `shared_project_tasks` | Задачи проекта: title, description, status, priority, due_date, assigned_to_user_id, column_id. |
| `chat_messages` | Сообщения: `sender_user_id`, `project_id?`, `group_chat_id?`, `to_user_id?`, `sent_at`. Шифрование: `text_cipher/text_nonce/text_hmac` (VARBINARY, ГОСТ). Вложения: `attachment_file_id`, `attachment_file_name`, `attachment_size`. |
| `group_chats` | Групповые чаты: `name`, `created_by_user_id`, `created_at`. |
| `group_chat_members` | Участники групп: `group_chat_id`, `user_id`, `joined_at`. |
| `chat_last_reads` | Метки прочтения: `user_id`, `chat_key` (строка типа `"user:{id:N}"`), `last_read_at`. |

---

## 12. Криптография (ГОСТ)

### Схема шифрования

```
Пароль + Соль (32 байта)
        ↓ PBKDF2-HMAC-Стрибог-256 (100 000 итераций)
64 байта производного ключа
        ├── [0..31] encryptionKey  → GostCryptoService.Encrypt
        └── [32..63] hmacKey       → HmacStreebog.ComputeHmac

Шифрование записи:
Nonce (16 байт, RandomNumberGenerator)
plaintext + encryptionKey → KuznechikEngine (CTR mode) → ciphertext
hmacKey + (Nonce ‖ ciphertext) → HmacStreebog → HMAC (32 байта)
Хранится: EncryptedContent { ciphertext, nonce, hmac }

Дешифрование:
1. VerifyHmac(nonce ‖ ciphertext, hmacKey, stored_hmac) — СНАЧАЛА!
2. При несовпадении: IntegrityViolationException (данные не расшифровываются)
3. KuznechikEngine CTR decrypt с тем же nonce
```

### Привязка к устройству

При первом входе device_id записывается в `users.device_id`. При последующих входах `DeviceValidationMiddleware` сверяет `X-Device-Fingerprint` заголовок с записью. Несовпадение → `DeviceMismatchException` → 403.

### Hash-chain аудита

```
audit_log[0]: previous_hash = 0x00..00 (32 нулевых байта)
              current_hash  = Стрибог-256(prev_hash ‖ timestamp ‖ action ‖ entity_id ‖ user_id)

audit_log[N]: previous_hash = audit_log[N-1].current_hash
              current_hash  = Стрибог-256(prev_hash ‖ timestamp ‖ action ‖ entity_id ‖ user_id)
```

`VerifyAuditChainQuery` пересчитывает цепочку с нуля. Подмена любой записи → несовпадение хэшей → `TamperedAt = N`.

---

## 13. Режимы работы

### Персональный режим

```
Пользователь → PinView (создание/ввод PIN)
    ↓ PBKDF2(PIN, salt) → 64-байтный ключ
SessionService.SetPersonalSession(userId, key)
    ↓
NotesServiceProxy → LocalNotesService → PersonalDbContext (LocalDB)
TasksServiceProxy → LocalTasksService → PersonalDbContext (LocalDB)
    ↓
GostCryptoService шифрует/дешифрует данные на клиенте
```

**Нет JWT, нет сервера, нет подключения к сети.** PIN-код не хранится — хранится только verifier (PBKDF2 output), из которого ключ можно воссоздать.

### Корпоративный режим

```
Пользователь → LoginView → POST /api/v1/auth/login
    ↓ AccessToken (JWT 60 мин) + RefreshToken (7 дней)
SessionService.SetCorporateSession(response)
    ↓ ApiClient добавляет Authorization: Bearer {token} к каждому запросу
    ↓ ApiClient добавляет X-Device-Fingerprint заголовок
NotesServiceProxy → ApiClient → HTTP → ConSecOrg.Server → MediatR → Infrastructure
```

Ключ шифрования хранится в `EncryptionKeyStore` (сервер) — извлекается из него при каждой операции через `ICurrentUserContext.GetEncryptionKey()`.

---

## 14. API-эндпоинты

Все эндпоинты начинаются с `/api/v1`. Все (кроме `/auth/login`, `/auth/register-self`, `/auth/bootstrap`) требуют `Authorization: Bearer {token}`.

### Auth `/api/v1/auth`
| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/login` | Вход |
| POST | `/logout` | Выход |
| POST | `/register` | Регистрация (Admin only) |
| POST | `/register-self` | Саморегистрация (AllowAnonymous) |
| POST | `/refresh` | Обновить JWT |
| POST | `/change-password` | Сменить пароль |
| POST | `/change-email` | Сменить email |
| GET | `/me` | Текущий пользователь |
| POST | `/bootstrap` | Создать первого admin (только если БД пуста) |

### Notes `/api/v1/notes`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/` | Список (paged, ?category=, ?level=, ?search=) |
| GET | `/{id}` | Одна заметка |
| POST | `/` | Создать |
| PUT | `/{id}` | Обновить |
| DELETE | `/{id}` | Удалить (secure delete) |
| POST | `/{id}/timer` | Установить таймер уничтожения |
| GET | `/expiring` | Истекающие заметки |

### Tasks `/api/v1/tasks`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/` | Список задач |
| POST | `/` | Создать |
| PUT | `/{id}` | Обновить |
| PATCH | `/{id}/move` | Переместить в другую колонку |
| DELETE | `/{id}` | Удалить |
| GET | `/due` | Задачи с ближайшим дедлайном |

### Contacts `/api/v1/contacts`
| Метод | Путь | Описание |
|-------|------|----------|
| GET/POST | `/` | Список / Создать |
| GET/PUT/DELETE | `/{id}` | Конкретный контакт |
| GET | `/search?q=` | Поиск |
| POST | `/requests` | Отправить заявку |
| POST | `/requests/{id}/accept` | Принять заявку |
| POST | `/requests/{id}/decline` | Отклонить заявку |
| GET | `/requests/incoming` | Входящие заявки |
| GET | `/requests/outgoing` | Исходящие заявки |

### Users `/api/v1/users`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/` | Все пользователи (Admin/Manager) |
| PUT | `/{id}/settings` | Настройки пользователя |
| POST | `/{id}/lock` / `/unlock` | Блокировка (Admin) |
| POST | `/{id}/role` | Назначить роль (Admin) |
| GET | `/{id}/sessions` | Сессии |
| DELETE | `/{id}/sessions/{sid}` | Отозвать сессию |
| GET | `/search?q=` | Поиск пользователей |

### Audit `/api/v1/audit`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/` | Журнал аудита (paged, ?dateFrom=, ?dateTo=, ?action=) |
| GET | `/verify` | Проверить hash-chain |
| GET | `/export` | Экспорт CSV |

### Совместные проекты `/api/v1/shared-projects`
| Метод | Путь | Описание |
|-------|------|----------|
| GET/POST | `/` | Список / Создать |
| POST | `/join` | Вступить по invite-коду |
| DELETE | `/{id}` | Удалить проект |
| POST | `/{id}/leave` | Покинуть проект |
| GET | `/{id}/members` | Участники |
| GET/POST/PUT/DELETE | `/{id}/tasks` | Задачи проекта |
| GET/POST/PUT/DELETE | `/{id}/columns` | Колонки Kanban |

### Чат `/api/v1/chat`
| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/messages` | Отправить сообщение (шифруется ГОСТ при сохранении) |
| GET | `/projects/{projectId}/messages` | История проектного чата |
| GET | `/direct/{userId}/messages` | История личного чата |
| GET | `/group/{groupChatId}/messages` | История группового чата |
| GET | `/chats` | Список всех диалогов со сводкой |
| DELETE | `/messages/{id}` | Удалить своё сообщение + SignalR broadcast `MessageDeleted` |

### Групповые чаты `/api/v1/group-chats`
| Метод | Путь | Описание |
|-------|------|----------|
| GET/POST | `/` | Список / Создать |
| GET | `/{id}` | Информация о группе |
| GET/DELETE | `/{id}/members` | Участники / Удалить |
| POST | `/{id}/members` | Добавить участника |
| DELETE | `/{id}` | Удалить группу |

### Файлы `/api/v1/files`
| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/upload` | Загрузить файл (до 50 МБ), возвращает `{ FileId, FileName, Url, Size }` |
| GET | `/{fileId}` | Скачать файл |

### SignalR (`/hubs/board`)
| Событие | Направление | Описание |
|---------|-------------|----------|
| `JoinProject(projectId)` | Клиент → Сервер | Подписка на события Kanban-проекта |
| `LeaveProject(projectId)` | Клиент → Сервер | Отписка |
| `JoinGroupChat(groupChatId)` | Клиент → Сервер | Подписка на групповой чат |
| `LeaveGroupChat(groupChatId)` | Клиент → Сервер | Отписка |
| `MarkDirectRead(partnerId)` | Клиент → Сервер | Уведомить партнёра о прочтении |
| `ChatMessageReceived` | Сервер → Клиент | Новое сообщение |
| `MessagesRead` | Сервер → Клиент | Партнёр прочитал сообщения |
| `MessageDeleted` | Сервер → Клиент | Сообщение удалено (Guid messageId) |
| `SharedTaskCreated/Updated/Deleted/Moved` | Сервер → Клиент | Изменения задачи |
| `SharedColumnCreated/Updated/Deleted/Reordered` | Сервер → Клиент | Изменения колонки |

---

## 15. Навигация в клиенте

### Сцены (управляет AppViewModel)

```
ModeSelectionView  →  LoginView / PinView
                         ↓
                      ShellView
```

### Страницы (управляет NavigationService внутри Shell)

```
ShellView содержит:
├── NotesListView / NoteEditorView   (Notes модуль)
├── KanbanBoardView                  (Tasks)
├── CalendarView                     (Calendar)
├── ContactsView                     (Contacts)
├── ChatsView                        (Chats, корп.)
├── CompanyView / SharedProjectBoardView  (Company, корп.)
├── SecurityDashboardView            (Dashboard, Admin/Auditor)
├── AuditLogView                     (Audit, Admin/Auditor)
├── ReportsView                      (Reports)
├── MyTasksView                      (Мои задачи)
├── UsersManagementView              (Users, Admin only)
└── SettingsView                     (Settings)
```

Правая панель `SharedChatPanelView` накладывается поверх содержимого при открытии чата.

---

## 16. Темизация и внешний вид

### ThemeEngine

`ThemeEngine.cs` — синглтон. При вызове `Apply(isDark, accentHex, bgImagePath)`:

1. Вычисляет палитру из accentHex: primary accent, dark accent (HSL яркость -20%), alpha-20%, alpha-10% варианты.
2. Заполняет `_dynamicTheme` (ResourceDictionary) кистями:

| Ресурс | Тёмная тема | Светлая тема |
|--------|-------------|--------------|
| `PrimaryBackground` | #1E1E1E | #FFFFFF |
| `SecondaryBackground` | #252526 | #F5F5F5 |
| `CardBackground` | #2D2D30 | #FFFFFF |
| `SidebarBackground` | #1B1B1D | #F0F0F0 |
| `PrimaryForeground` | #FFFFFF | #212121 |
| `SecondaryForeground` | #9E9E9E | #757575 |
| `BorderBrush` | #3F3F46 | #E0E0E0 |
| `AccentBrush` | ← из accentHex | ← из accentHex |
| `NavActiveForeground` | #FFFFFF | #212121 |

3. Вызывает `PaletteHelper.SetTheme()` для обновления Material Design кнопок.
4. Если задан `bgImagePath` — добавляет полупрозрачный `ImageBrush` overlay.

### Акцентные цвета (10 предустановленных)

`#2196F3` (синий), `#4CAF50` (зелёный), `#F44336` (красный), `#FF9800` (оранжевый), `#9C27B0` (фиолетовый), `#00BCD4` (голубой), `#009688` (изумрудный), `#3F51B5` (индиго), `#E91E63` (розовый), `#607D8B` (серо-синий).

---

## 17. Зависимости (NuGet)

### ConSecOrg.Domain
Нет внешних зависимостей.

### ConSecOrg.Application
- `MediatR 12.x` — CQRS
- `FluentValidation 11.x` + `FluentValidation.DependencyInjectionExtensions` — валидация
- `AutoMapper 13.x` + `AutoMapper.Extensions.Microsoft.DependencyInjection` — маппинг
- `Microsoft.Extensions.Logging.Abstractions` — логирование

### ConSecOrg.Infrastructure
- `Microsoft.EntityFrameworkCore.SqlServer 10.x` — ORM
- `Microsoft.EntityFrameworkCore.Tools 10.x` — EF CLI
- `BouncyCastle.Cryptography 2.x` — ГОСТ криптография
- `Serilog 3.x` + `Serilog.Sinks.MSSqlServer` — логирование
- `System.IdentityModel.Tokens.Jwt 8.x` + `Microsoft.IdentityModel.Tokens` — JWT

### ConSecOrg.Server
- `Microsoft.AspNetCore.Authentication.JwtBearer 10.x` — JWT Auth
- `Serilog.AspNetCore 10.x` — структурированное логирование запросов
- `Microsoft.AspNetCore.SignalR 1.x` (встроен в ASP.NET Core 10) — real-time
- `Swashbuckle.AspNetCore 10.x` — Swagger UI

### ConSecOrg.Client
- `CommunityToolkit.Mvvm 8.x` — MVVM фреймворк
- `MaterialDesignThemes 5.x` — UI kit
- `MahApps.Metro 2.x` + `MahApps.Metro.IconPacks 6.x` — иконки и контролы
- `Microsoft.Extensions.Hosting 10.x` — DI контейнер
- `Markdig 1.x` — парсер Markdown
- `MdXaml 1.x` — Markdown → FlowDocument (WPF)
- `RestSharp 114.x` — HTTP клиент
- `Microsoft.AspNetCore.SignalR.Client 10.x` — SignalR клиент
- `H.NotifyIcon.Wpf 2.x` — иконка в системном трее
- `Microsoft.EntityFrameworkCore.SqlServer 10.x` — для PersonalDbContext

### Тестовые проекты
- `xUnit 2.x` + `xUnit.runner.visualstudio`
- `Moq 4.x` — mock объекты
- `FluentAssertions 6.x` — удобные assert'ы
- `Microsoft.EntityFrameworkCore.InMemory 8.x` — in-memory БД для тестов
- `Bogus 35.x` — генерация тестовых данных

---

## 18. Запуск и развёртывание

### Предварительные требования

| Компонент | Версия |
|-----------|--------|
| .NET SDK | 10.0+ |
| SQL Server | 2019+ или Express |
| SQL Server LocalDB | Входит в Visual Studio 2022 |

### Персональный режим (без сервера)

```cmd
cd e:\ProjectVSCode\ConSecOrg
dotnet run --project src\ConSecOrg.Client
```

База данных `ConSecOrg_Personal` создаётся автоматически в LocalDB при первом запуске.

### Корпоративный режим

**Шаг 1** — задать JWT-секрет (один раз):
```cmd
cd src\ConSecOrg.Server
dotnet user-secrets set "JwtSettings:Secret" "ConSecOrgSecret2024SuperKey!!"
cd ..\..
```

**Шаг 2** — применить миграции (один раз):
```cmd
dotnet ef database update --project src\ConSecOrg.Infrastructure --startup-project src\ConSecOrg.Server
```

**Шаг 3** — запустить сервер:
```cmd
dotnet run --project src\ConSecOrg.Server
```

Сервер: `http://localhost:5205`, Swagger: `http://localhost:5205/swagger`

**Шаг 4** — создать первого admin (один раз через Swagger):
```json
POST /api/v1/auth/bootstrap
{ "username": "admin", "email": "admin@company.ru", "password": "Admin1234!@#$", "roleId": "" }
```

**Шаг 5** — запустить клиент:
```cmd
dotnet run --project src\ConSecOrg.Client
```

### ID ролей (фиксированные GUID из миграции)

| Роль | GUID |
|------|------|
| Admin | `11111111-1111-1111-1111-111111111111` |
| Manager | `22222222-2222-2222-2222-222222222222` |
| Auditor | `33333333-3333-3333-3333-333333333333` |
| User | `44444444-4444-4444-4444-444444444444` |

### Сборка и тесты

```cmd
dotnet build ConSecOrg.slnx          # сборка всего решения
dotnet test ConSecOrg.slnx           # все тесты
dotnet test tests\ConSecOrg.Infrastructure.Tests  # только ГОСТ тест-векторы
```

---

---

## 19. Иконки приложения

Файлы исходных изображений: `ConSecOrg.png` (клиент) и `ConSecOrgServer.png` (сервер) — в корне проекта. Формат PNG с прозрачным фоном.

### Генерация ICO

Из каждого PNG генерируется многоразмерный ICO-файл (PowerShell + `System.Drawing`). ICO содержит 6 размеров: 16×16, 32×32, 48×48, 64×64, 128×128, 256×256 — PNG-блобы внутри ICO (современный формат Windows, полная поддержка прозрачности).

| Исходник | ICO файл | Где используется |
|----------|----------|-----------------|
| `ConSecOrg.png` | `src/ConSecOrg.Client/Assets/Icons/app.ico` | `<ApplicationIcon>` в .csproj → иконка exe; `Icon="Assets/Icons/app.ico"` в MainWindow.xaml → заголовок окна + панель задач; `IconSource="/Assets/Icons/app.ico"` в `tb:TaskbarIcon` → системный трей |
| `ConSecOrgServer.png` | `src/ConSecOrg.Server/Resources/server.ico` | `<ApplicationIcon>` в .csproj → иконка Server.exe; копируется в output (`CopyToOutputDirectory=PreserveNewest`) |

### Публикация (self-contained single-file)

```cmd
dotnet publish src\ConSecOrg.Client -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish/Client
dotnet publish src\ConSecOrg.Server -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish/Server
```

Результат: `publish/Client/ConSecOrg.Client.exe` (~264 МБ) и `publish/Server/ConSecOrg.Server.exe` (~122 МБ). Оба exe включают иконки, ассемблерные метаданные (Company, Version, Copyright) и весь .NET runtime — запускаются без предустановленного .NET.

---

*Документация актуальна на 2026-05-14. Автор: Максутов Р.Ф.*
