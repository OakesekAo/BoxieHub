using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Client.Models.Enums
{
    public enum ScheduleType
    {
        Manual,
        [Display(Name = "One Time")] OneTime,
        Recurring,
        Daily,
        Weekly,
        Monthly,
    }
}
