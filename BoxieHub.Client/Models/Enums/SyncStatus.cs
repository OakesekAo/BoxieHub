using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Client.Models.Enums
{
    public enum SyncStatus
    {
        Pending,
        [Display(Name = "In Progress")] InProgress,
        Completed,
        Failed,
    }
}
