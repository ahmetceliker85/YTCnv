using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;

namespace YTCnv;

public partial class TitleAuthor : Popup
{
    public string resultTitle = "";
    public string resultAuthor = "";

    public TitleAuthor(string title, string author)
    {
        InitializeComponent();

        TitleEntry.Text = title;
        AuthorEntry.Text = author;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        resultTitle = TitleEntry.Text?.Trim() ?? string.Empty;
        resultAuthor = AuthorEntry.Text?.Trim() ?? string.Empty;

        await CloseAsync();
    }
}