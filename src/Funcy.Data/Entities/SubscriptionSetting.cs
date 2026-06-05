using System.ComponentModel.DataAnnotations;

namespace Funcy.Data.Entities;

public class SubscriptionSetting
{
    [Key]
    public string SubscriptionId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsHidden { get; set; }
}
