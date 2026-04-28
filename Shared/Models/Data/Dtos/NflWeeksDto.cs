namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class NflWeeksDto {
    public int Id { get; set; }
    public int NflWeek { get; set; }
    public int Season { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
}
