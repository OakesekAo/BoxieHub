using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BoxieHub.Client.Models.Enums
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum value)
        {
            string? displayName = value.GetType()
                .GetMember(value.ToString())
                .FirstOrDefault()?
                .GetCustomAttribute<DisplayAttribute>()?
            .GetName();

            if (string.IsNullOrEmpty(displayName))
            {
                return value.ToString();
            }
            else
            {
                return displayName;
            }

        }
    }
}
