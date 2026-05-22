using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Audit;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

namespace ConSecOrg.Client.ViewModels.Audit;

public partial class AuditLogViewModel(
    IAuditApiService auditService,
    NotificationService notifications) : BasePageViewModel
{
    public override string Title => "Журнал аудита";

    [ObservableProperty] private ObservableCollection<AuditLogDto> _logs = [];
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private string? _chainVerifyResult;
    [ObservableProperty] private bool _chainIsIntact;

    // ── Filters ────────────────────────────────────────────────────────────────
    [ObservableProperty] private DateTime? _filterFrom;
    [ObservableProperty] private DateTime? _filterTo;
    [ObservableProperty] private string _filterAction = string.Empty;

    public IReadOnlyList<string> ActionOptions { get; } =
        ["", "Login", "Logout", "Create", "Update", "Delete", "Register", "AccountLocked"];

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var action = string.IsNullOrEmpty(FilterAction) ? null : FilterAction;
            var result = await auditService.GetLogsAsync(CurrentPage, 50, FilterFrom, FilterTo, action);
            Logs = new ObservableCollection<AuditLogDto>(result.Items);
            HasNextPage = result.HasNext;
        });
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync() { CurrentPage = 1; await LoadAsync(); }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        FilterFrom = null;
        FilterTo = null;
        FilterAction = string.Empty;
        CurrentPage = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync() { CurrentPage++; await LoadAsync(); }

    [RelayCommand]
    private async Task PrevPageAsync() { if (CurrentPage > 1) { CurrentPage--; await LoadAsync(); } }

    [RelayCommand]
    private async Task VerifyChainAsync()
    {
        await ExecuteAsync(async () =>
        {
            var result = await auditService.VerifyChainAsync();
            ChainIsIntact = result.IsIntact;
            ChainVerifyResult = result.Message;

            if (result.IsIntact)
                notifications.Success("Целостность", result.Message);
            else
                notifications.Error("Нарушение целостности!", result.Message);
        });
    }

    [RelayCommand]
    private async Task RecomputeChainAsync()
    {
        await ExecuteAsync(async () =>
        {
            var msg = await auditService.RecomputeChainAsync();
            ChainVerifyResult = msg;
            notifications.Success("Пересчёт завершён", msg);
        });
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Экспорт журнала аудита",
            Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
            FileName = $"audit_export_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        await ExecuteAsync(async () =>
        {
            var bytes = await auditService.ExportCsvAsync();
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            notifications.Success("Экспорт", $"Журнал сохранён: {dialog.FileName}");
        });
    }
}
