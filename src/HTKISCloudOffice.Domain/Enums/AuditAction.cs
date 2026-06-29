namespace HTKISCloudOffice.Domain.Enums;

public enum AuditAction
{
    Login,
    Logout,
    PermissionChange,
    AppLaunch,
    FileDelete,
    DeviceBind,
    DeviceUnbind,
    FileUpload,
    FileDownload,
    FilePreview,
    ConnectionCreate,
    ConnectionDelete
}