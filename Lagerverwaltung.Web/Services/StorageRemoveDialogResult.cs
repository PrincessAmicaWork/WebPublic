namespace Lagerverwaltung.Web.Services;

public enum StorageRemoveAction
{
    Delete = 0,
    Destroy = 1,
    ForceDelete = 2
}

public record StorageRemoveDialogResult(
    StorageRemoveAction Action,
    string Comment,
    int ConfirmationPositionId);
