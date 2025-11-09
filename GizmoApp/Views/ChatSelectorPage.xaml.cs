using System.Diagnostics;

using GizmoApp.Service;

namespace GizmoApp.Views
{
    public partial class ChatSelectorPage : ContentPage
    {
	    public ChatSelectorPage()
	    {
		    InitializeComponent();
            LoadChats();
        }
        private void LoadChats()
        {
            ChatListContainer.Children.Clear();

            var chats = ChatManager.GetAllChats(); // musst du noch bauen

            var BorderStyle = (Style)Application.Current.Resources["ButtonBorder"];

            foreach (var chat in chats)
            {
                var btn = new Button
                {
                    Text = chat.Title,
                    Command = new Command(async () =>
                    {
                        ChatManager.SetActiveChat(chat.ChatId); // musst du bauen
                        await Navigation.PopModalAsync(); // schlieﬂt das Overlay
                    })
                };

                var border = new Border
                {
                    Style = BorderStyle,
                    Content = btn
                };

                ChatListContainer.Children.Add(border);
            }

        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Settings button clicked");
            await Navigation.PushModalAsync(new SettingsPage());
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}