using Microsoft.Azure.WebJobs;
using SendGrid.Helpers.Mail;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twilio.Rest.Api.V2010.Account;

using System.Linq;
using Azure.ExpirationHandler.Func.Models;

namespace Azure.ExpirationHandler.Func
{
    public class Notifications
    {
        [FunctionName("Mailer")]
        public async Task SendMail([QueueTrigger("%OutboxQueueName%", Connection = "OutboxQueueStorageAccount")] MailInfo mail, [SendGrid(ApiKey = "SendGridApiKey")] IAsyncCollector<SendGridMessage> outbox)
        {
            var message = new SendGridMessage();
            message.AddTos(mail.To.Select(x => new EmailAddress(x)).ToList());
            message.SetFrom(new EmailAddress("notify@azman.io", "azman.io notify"));

            message.AddContent("text/html", mail.MailBody);
            message.SetSubject(mail.Subject);

            await outbox.AddAsync(message);
        }

        [FunctionName("Texter")]
        public async Task SendSms(List<string> targets, IAsyncCollector<CreateMessageOptions> outbox)
        {
            // You must initialize the CreateMessageOptions variable with the "To" phone number.
            //CreateMessageOptions smsText = new CreateMessageOptions(new PhoneNumber("+1704XXXXXXX"));

            // A dynamic message can be set instead of the body in the output binding. In this example, we use
            // the order information to personalize a text message.
            //smsText.Body = msg;

            //await message.AddAsync(smsText);
        }
    }
}