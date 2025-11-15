using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using GizmoApp.Service;

namespace GizmoApp.Views
{
    public partial class ChatSelectorPage : ContentPage
    {
	    public ChatSelectorPage()
	    {
		    InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await ChatManager.LoadFromLocalAsync();
            LoadChats();
        }

        private void LoadChats()
        {
            ChatListContainer.Children.Clear();

            var chats = ChatManager.GetAllChats().ToList();



            var BorderStyle = (Style)Application.Current.Resources["ButtonBorder"];

            if (!chats.Any())
            {
                var emptyLabel = new Label
                {
                    Text = "Keine Chats vorhanden",
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Margin = new Thickness(10)
                };
                ChatListContainer.Children.Add(emptyLabel);
                return;
            }

            foreach (var chat in chats)
            {
                var localChat = chat; // closure-safe copy
                var btn = new Button
                {
                    Text = localChat.Title,
                    Command = new Command(async () =>
                    {
                        ChatManager.SetActiveChat(localChat.ChatId);
                        await Navigation.PopModalAsync();
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
        private async void OnNewChatClicked(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Erstelle neuen Chat");

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