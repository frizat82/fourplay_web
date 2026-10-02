namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class NotificationPreferencesDto
{
    public bool NotifyMineBloodyDuringGame { get; set; }
    public bool NotifyMineBloodyAtFinal { get; set; }
    public bool NotifyMineCoveringDuringGame { get; set; }
    public bool NotifyMineCoveringAtFinal { get; set; }

    public bool NotifyOthersBloodyDuringGame { get; set; }
    public bool NotifyOthersBloodyAtFinal { get; set; }
    public bool NotifyOthersCoveringDuringGame { get; set; }
    public bool NotifyOthersCoveringAtFinal { get; set; }

    public bool NotifyWeekResult { get; set; }
}
