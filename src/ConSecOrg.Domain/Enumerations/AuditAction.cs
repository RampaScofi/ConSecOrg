namespace ConSecOrg.Domain.Enumerations;

public enum AuditAction
{
    // Auth
    Login,
    Logout,
    Register,
    TokenRefreshed,
    PasswordChanged,
    EmailChanged,
    AccountLocked,
    AccountUnlocked,
    RoleAssigned,
    SessionRevoked,
    AccessDenied,
    DeviceMismatch,

    // Generic CRUD (legacy / fallback)
    Create,
    Read,
    Update,
    Delete,
    SecureDelete,

    // Notes
    NoteCreated,
    NoteUpdated,
    NoteViewed,
    NoteDeleted,
    NoteSecureDeleted,
    NoteExpired,
    NoteTimerSet,

    // Tasks
    TaskCreated,
    TaskUpdated,
    TaskMoved,
    TaskCompleted,
    TaskDeleted,

    // Projects / Boards
    ProjectCreated,
    ProjectUpdated,
    ProjectDeleted,
    ProjectJoined,
    ProjectLeft,
    BoardCreated,
    BoardUpdated,
    BoardDeleted,

    // Columns
    ColumnCreated,
    ColumnUpdated,
    ColumnDeleted,
    ColumnReordered,

    // Contacts
    ContactAdded,
    ContactUpdated,
    ContactDeleted,
    ContactRequestSent,
    ContactRequestAccepted,
    ContactRequestDeclined,

    // Data
    DataExported,
    AuditViewed,
    AuditChainVerified,
}
