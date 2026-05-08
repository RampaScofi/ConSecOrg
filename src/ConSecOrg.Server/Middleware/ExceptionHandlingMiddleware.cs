using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace ConSecOrg.Server.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleAsync(ctx, ex);
        }
    }

    private static async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        ctx.Response.ContentType = "application/json";

        var (status, message, errors) = ex switch
        {
            ValidationException ve => (HttpStatusCode.BadRequest, "Ошибка валидации данных.", ve.Errors),
            NotFoundException nfe => (HttpStatusCode.NotFound, LocalizeNotFound(nfe.Message), null),
            ForbiddenException fe => (HttpStatusCode.Forbidden, "Недостаточно прав для выполнения этого действия.", null),
            AccountLockedException => ((HttpStatusCode)423, "Аккаунт заблокирован. Обратитесь к администратору.", null),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Неверный логин или пароль.", null),
            IntegrityViolationException => (HttpStatusCode.UnprocessableEntity, "Ошибка целостности данных. Возможно, данные были изменены.", null),
            DeviceMismatchException => (HttpStatusCode.Forbidden, "Устройство не распознано. Доступ запрещён.", null),
            _ => (HttpStatusCode.InternalServerError, "Внутренняя ошибка сервера. Попробуйте позже.", null)
        };

        ctx.Response.StatusCode = (int)status;

        var body = errors is not null
            ? new { message, errors }
            : (object)new { message };

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private static string LocalizeNotFound(string originalMessage)
    {
        // Map common entity names to Russian
        if (originalMessage.Contains("Note", StringComparison.OrdinalIgnoreCase)) return "Заметка не найдена.";
        if (originalMessage.Contains("Task", StringComparison.OrdinalIgnoreCase)) return "Задача не найдена.";
        if (originalMessage.Contains("Contact", StringComparison.OrdinalIgnoreCase)) return "Контакт не найден.";
        if (originalMessage.Contains("User", StringComparison.OrdinalIgnoreCase)) return "Пользователь не найден.";
        if (originalMessage.Contains("Project", StringComparison.OrdinalIgnoreCase)) return "Проект не найден.";
        if (originalMessage.Contains("Session", StringComparison.OrdinalIgnoreCase)) return "Сессия не найдена.";
        return "Запрошенный объект не найден.";
    }
}
