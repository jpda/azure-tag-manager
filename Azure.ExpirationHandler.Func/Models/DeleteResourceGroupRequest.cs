namespace Azure.ExpirationHandler.Models
{
    public class DeleteResourceGroupRequest
    {
        public string ResourceGroupName { get; set; }
        public string SubscriptionId { get; set; }
    }
}
