namespace Azure.ExpirationHandler.Func
{
    public class TaggingOptions
    {
        public int ExpirationWindowInDays { get; set; }
        public string ExpirationTagKey { get; set; }
        public string DefaultOwner { get; set; }
        public string DefaultTaggedByName { get; set; }
    }
}