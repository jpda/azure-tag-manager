namespace Azure.ExpirationHandler.Func
{
    public class DeletionOptions
    {
        public bool Commit { get; set; }
        public bool NotifyBeforeDeletion { get; set; }
        public bool PersistTemplateBeforeDeletion { get; set; }
        public string PersistenceStorageContainer { get; set; }
    }
}