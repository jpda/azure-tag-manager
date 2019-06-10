namespace Azure.ExpirationHandler.Func
{
    public class DeletionOptions
    {
        /// <summary>
        /// Indicates if the deletion should actually be processed or merely logged that the targeted resource would be deleted.
        /// </summary>
        public bool Commit { get; set; }

        /// <summary>
        /// Enables sending notifications to specific parties when a resource group is deleted.
        /// </summary>
        public bool NotifyOnDeletion { get; set; }

        /// <summary>
        /// Enables exporting templates to storage before deletion.
        /// </summary>
        public bool ExportTemplateBeforeDeletion { get; set; }

        /// <summary>
        /// Container for storing exported templates. Use the relative path to the root storage account, e.g., <c>/containername/path/to/folder</c>
        /// </summary>
        public string PersistenceStorageContainer { get; set; }

        /// <summary>
        /// If <see langword="true"/>, requires a successful resource group export before deletion. If <see langword="false"/>, resource group deletion is not prevented on export failure.
        /// </summary>
        public bool RequireExport { get; set; }
    }
}