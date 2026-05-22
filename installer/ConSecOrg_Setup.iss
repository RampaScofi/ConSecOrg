; ConSecOrg — Защищённый электронный органайзер
; Inno Setup Script — создаёт полноценный установщик Windows
;
; Требования: Inno Setup 6.x (https://jrsoftware.org/isinfo.php)
; Запуск сборки: iscc ConSecOrg_Setup.iss

#define MyAppName "ConSecOrg"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Максутов Р.Ф."
#define MyAppExeName "ConSecOrg.Client.exe"
#define MyAppServerExe "ConSecOrg.Server.exe"
#define MyAppURL "https://github.com/RampaScofi/ConSecOrg"
#define MySourceDir "..\publish"

[Setup]
; Уникальный GUID установки — не менять при обновлениях
AppId={{A7B3C2D1-E4F5-6789-ABCD-EF0123456789}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Куда устанавливать по умолчанию
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; Иконки и брендинг
; SetupIconFile=ConSecOrg_Icon.ico        ; раскомментировать если добавить иконку
WizardStyle=modern

; Куда сохранять installer.exe
OutputDir=Output
OutputBaseFilename=ConSecOrg_Setup_v{#MyAppVersion}

; Сжатие
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Требования
MinVersion=10.0.19041
ArchitecturesInstallIn64BitMode=x64compatible

; Запрос прав администратора
PrivilegesRequired=admin

; Не запускать несколько экземпляров установки
AppMutex={#MyAppName}SetupMutex

; Поддержка обновления (деинсталляция старой версии перед установкой)
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
russian.SelectComponents=Выберите компоненты для установки
russian.ClientDesc=Клиентское приложение (WPF)
russian.ServerDesc=Серверная часть (ASP.NET Core Web API)
russian.LaunchClient=Запустить {#MyAppName} после установки
russian.CreateDesktopIcon=Создать ярлык на рабочем столе
russian.ServerNote=Для работы корпоративного режима необходим Microsoft SQL Server
english.SelectComponents=Select components to install
english.ClientDesc=Client application (WPF)
english.ServerDesc=Server component (ASP.NET Core Web API)
english.LaunchClient=Launch {#MyAppName} after installation
english.CreateDesktopIcon=Create desktop shortcut

[Types]
Name: "full"; Description: "Полная установка (клиент + сервер)"
Name: "client"; Description: "Только клиент"
Name: "server"; Description: "Только сервер"
Name: "custom"; Description: "Выборочная установка"; Flags: iscustom

[Components]
Name: "client"; Description: "{cm:ClientDesc}"; Types: full client; Flags: fixed
Name: "server"; Description: "{cm:ServerDesc}"; Types: full server

[Tasks]
Name: "desktopicon_client"; Description: "{cm:CreateDesktopIcon} (клиент)"; GroupDescription: "Ярлыки:"; Components: client
Name: "desktopicon_server"; Description: "{cm:CreateDesktopIcon} (сервер)"; GroupDescription: "Ярлыки:"; Components: server
Name: "startmenu"; Description: "Добавить в меню Пуск"; GroupDescription: "Меню Пуск:"; Flags: checkedonce
Name: "autostart_server"; Description: "Запускать сервер при входе в Windows"; GroupDescription: "Автозапуск:"; Components: server; Flags: unchecked

[Dirs]
Name: "{app}\Client"; Components: client
Name: "{app}\Server"; Components: server
Name: "{app}\Server\LatoFont"; Components: server
Name: "{localappdata}\ConSecOrg"; Flags: uninsneveruninstall

[Files]
; ── Клиент ─────────────────────────────────────────────────────────────────────
Source: "{#MySourceDir}\Client\*"; DestDir: "{app}\Client"; \
    Flags: ignoreversion recursesubdirs createallsubdirs; Components: client

; ── Сервер ─────────────────────────────────────────────────────────────────────
Source: "{#MySourceDir}\Server\*"; DestDir: "{app}\Server"; \
    Flags: ignoreversion recursesubdirs createallsubdirs; Components: server

; ── Вспомогательные файлы ──────────────────────────────────────────────────────
Source: "{#MySourceDir}\README.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme skipifsourcedoesntexist

[Icons]
; ── Меню Пуск ──────────────────────────────────────────────────────────────────
Name: "{group}\ConSecOrg (клиент)"; \
    Filename: "{app}\Client\{#MyAppExeName}"; \
    IconFilename: "{app}\Client\{#MyAppExeName}"; \
    Components: client; Tasks: startmenu

Name: "{group}\ConSecOrg Сервер"; \
    Filename: "{app}\Server\{#MyAppServerExe}"; \
    IconFilename: "{app}\Server\{#MyAppServerExe}"; \
    Components: server; Tasks: startmenu

Name: "{group}\README"; \
    Filename: "{app}\README.txt"; \
    Tasks: startmenu; Check: FileExists(ExpandConstant('{app}\README.txt'))

Name: "{group}\Удалить ConSecOrg"; \
    Filename: "{uninstallexe}"; \
    Tasks: startmenu

; ── Рабочий стол ───────────────────────────────────────────────────────────────
Name: "{autodesktop}\ConSecOrg"; \
    Filename: "{app}\Client\{#MyAppExeName}"; \
    IconFilename: "{app}\Client\{#MyAppExeName}"; \
    Components: client; Tasks: desktopicon_client

Name: "{autodesktop}\ConSecOrg Сервер"; \
    Filename: "{app}\Server\{#MyAppServerExe}"; \
    IconFilename: "{app}\Server\{#MyAppServerExe}"; \
    Components: server; Tasks: desktopicon_server

[Registry]
; ── Автозапуск сервера ──────────────────────────────────────────────────────────
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "ConSecOrgServer"; \
    ValueData: """{app}\Server\{#MyAppServerExe}"""; \
    Flags: uninsdeletevalue; Tasks: autostart_server; Components: server

; ── Запись для деинсталлятора (ARP) ────────────────────────────────────────────
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; \
    ValueType: string; ValueName: "InstallPath"; \
    ValueData: "{app}"; Flags: uninsdeletekey

[Run]
; Применить миграцию БД (только для сервера)
Filename: "{app}\Server\{#MyAppServerExe}"; Parameters: "--migrate-only"; \
    Description: "Создать/обновить базу данных"; \
    Flags: shellexec waituntilterminated; Components: server; \
    StatusMsg: "Инициализация базы данных..."; \
    Check: ServerAppsettingsExists

; Запустить клиент после установки
Filename: "{app}\Client\{#MyAppExeName}"; \
    Description: "{cm:LaunchClient}"; \
    Flags: nowait postinstall skipifsilent; Components: client

[UninstallRun]
; Остановить запущенный сервер перед удалением
Filename: "taskkill"; Parameters: "/F /IM {#MyAppServerExe}"; Flags: shellexec runhidden; Components: server
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; Flags: shellexec runhidden; Components: client

[Code]
var
  SqlServerPage: TInputQueryWizardPage;
  DbNamePage: TInputQueryWizardPage;

function ServerAppsettingsExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\Server\appsettings.json'));
end;

procedure InitializeWizard;
begin
  // Страница настройки подключения к SQL Server
  SqlServerPage := CreateInputQueryPage(wpSelectDir,
    'Настройка базы данных',
    'Подключение к Microsoft SQL Server',
    'Введите имя SQL Server. Для локального сервера обычно используется ' +
    '(local) или .\SQLEXPRESS. Оставьте пустым, если настроите вручную.');
  SqlServerPage.Add('Имя SQL Server:', False);
  SqlServerPage.Values[0] := '(local)';

  DbNamePage := CreateInputQueryPage(SqlServerPage.ID,
    'Настройка базы данных',
    'Имя базы данных',
    'Введите имя базы данных ConSecOrg. По умолчанию: ConSecOrg');
  DbNamePage.Add('Имя базы данных:', False);
  DbNamePage.Values[0] := 'ConSecOrg';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  // Пропустить страницы БД если сервер не выбран
  if (PageID = SqlServerPage.ID) or (PageID = DbNamePage.ID) then
    Result := not IsComponentSelected('server');
end;

function UpdateAppsettings: Boolean;
var
  AppSettingsPath, ServerName, DbName, ConnStr, ContentStr: string;
  Content: AnsiString;
begin
  Result := True;
  if not IsComponentSelected('server') then Exit;

  AppSettingsPath := ExpandConstant('{app}\Server\appsettings.json');
  if not FileExists(AppSettingsPath) then Exit;

  ServerName := SqlServerPage.Values[0];
  DbName := DbNamePage.Values[0];

  if ServerName = '' then ServerName := '(local)';
  if DbName = '' then DbName := 'ConSecOrg';

  ConnStr := 'Server=' + ServerName + ';Database=' + DbName +
             ';Trusted_Connection=True;TrustServerCertificate=True;';

  if not LoadStringFromFile(AppSettingsPath, Content) then
  begin
    Result := False;
    Exit;
  end;

  ContentStr := Content;
  StringChange(ContentStr,
    '"DefaultConnection": "Server=localhost;Database=ConSecOrg;Trusted_Connection=True;TrustServerCertificate=True;"',
    '"DefaultConnection": "' + ConnStr + '"');

  SaveStringToFile(AppSettingsPath, ContentStr, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    UpdateAppsettings;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  // Валидация имени сервера
  if CurPageID = SqlServerPage.ID then
  begin
    if Trim(SqlServerPage.Values[0]) = '' then
    begin
      MsgBox('Введите имя SQL Server или оставьте значение по умолчанию (local).', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataPath: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Предложить удалить пользовательские данные
    AppDataPath := ExpandConstant('{localappdata}\ConSecOrg');
    if DirExists(AppDataPath) then
    begin
      if MsgBox('Удалить пользовательские данные (настройки, кэш)?'#13#10 +
                AppDataPath, mbConfirmation, MB_YESNO) = IDYES then
        DelTree(AppDataPath, True, True, True);
    end;
  end;
end;
