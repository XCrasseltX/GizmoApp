using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GizmoApp.Models;

namespace GizmoApp.Service
{
    public static class ChatManager
    {
        private static List<Chat> _chats = new()
        {
            new Chat
            {
                ChatId = "smarthome",
                Title = "🏠 Smarthome",
                LastUsed = DateTime.UtcNow.AddMinutes(-10)
            },
            new Chat
            {
                ChatId = "dev",
                Title = "💻 Entwicklung",
                LastUsed = DateTime.UtcNow.AddMinutes(-30)
            },
            new Chat
            {
                ChatId = "test",
                Title = "🧪 Testchat",
                LastUsed = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        private static Chat? _activeChat;

        public static IEnumerable<Chat> GetAllChats()
        {
            return _chats.OrderByDescending(c => c.LastUsed);
        }

        public static void SetActiveChat(string chatId)
        {
            var chat = _chats.FirstOrDefault(c => c.ChatId == chatId);
            if (chat != null)
            {
                _activeChat = chat;
                chat.LastUsed = DateTime.UtcNow;
            }
        }

        public static Chat? GetActiveChat()
        {
            return _activeChat ?? _chats.OrderByDescending(c => c.LastUsed).FirstOrDefault();
        }

        public static string? GetActiveChatId()
        {
            return GetActiveChat()?.ChatId;
        }

        public static void ClearConversationId()
        {
            GetActiveChat()!.ConversationId = null;
        }
    }
}
