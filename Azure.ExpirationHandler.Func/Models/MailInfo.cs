using System.Collections.Generic;

namespace Azure.ExpirationHandler.Func.Models
{
    public class MailInfo
    {
        public List<string> To { get; set; }
        public bool BccAll { get; set; }
        public string MailBody { get; set; }
        public string Subject { get; set; }
    }
}
