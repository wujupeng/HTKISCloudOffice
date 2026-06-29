namespace HTKISCloudOffice.Application.DTOs;

public class DeviceBindingFilter
{
    public Guid? user_id { get; set; }
    public bool? is_active { get; set; }
    public int page { get; set; } = 1;
    public int page_size { get; set; } = 20;
}