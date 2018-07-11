using System;
using System.Collections.Generic;

namespace Azure.ExpirationHandler.Types
{
    public class TagGenerationRequest
    {
        public string Operation { get; set; }
        public string GroupName { get; set; }
        public string User { get; set; }
        public string DateCreated { get; set; }
        public string SubscriptionId { get; set; }
        public Dictionary<string, string> DesiredTags { get; set; }
    }
}
