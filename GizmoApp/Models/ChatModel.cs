using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GizmoApp.Models
{
    public class Chat
    {
        public string ChatId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Neuer Chat";
        public string? ConversationId { get; set; }
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    }
}
