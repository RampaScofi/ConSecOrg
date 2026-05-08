namespace ConSecOrg.Shared.Constants;

public static class ApiRoutes
{
    private const string Base = "/api/v1";

    public static class Auth
    {
        public const string Login = $"{Base}/auth/login";
        public const string Logout = $"{Base}/auth/logout";
        public const string Register = $"{Base}/auth/register";
        public const string Refresh = $"{Base}/auth/refresh";
        public const string ChangePassword = $"{Base}/auth/change-password";
        public const string Me = $"{Base}/auth/me";
    }

    public static class Notes
    {
        public const string Base = $"{ApiRoutes.Base}/notes";
        public const string ById = $"{Base}/{{id}}";
        public const string Timer = $"{Base}/{{id}}/timer";
        public const string Expiring = $"{Base}/expiring";
    }

    public static class Tasks
    {
        public const string Base = $"{ApiRoutes.Base}/tasks";
        public const string ById = $"{Base}/{{id}}";
        public const string Move = $"{Base}/{{id}}/move";
        public const string Due = $"{Base}/due";
    }

    public static class Contacts
    {
        public const string Base = $"{ApiRoutes.Base}/contacts";
        public const string ById = $"{Base}/{{id}}";
        public const string Search = $"{Base}/search";
    }

    public static class Users
    {
        public const string Base = $"{ApiRoutes.Base}/users";
        public const string ById = $"{Base}/{{id}}";
        public const string Settings = $"{Base}/{{id}}/settings";
        public const string Lock = $"{Base}/{{id}}/lock";
        public const string Unlock = $"{Base}/{{id}}/unlock";
        public const string Role = $"{Base}/{{id}}/role";
        public const string Sessions = $"{Base}/{{id}}/sessions";
        public const string SessionById = $"{Base}/{{id}}/sessions/{{sid}}";
    }

    public static class Audit
    {
        public const string Base = $"{ApiRoutes.Base}/audit";
        public const string Verify = $"{Base}/verify";
        public const string Export = $"{Base}/export";
    }

    public static class Dashboard
    {
        public const string Security = $"{ApiRoutes.Base}/dashboard/security";
    }

    public static class Hubs
    {
        public const string Board = "/hubs/board";
    }
}
