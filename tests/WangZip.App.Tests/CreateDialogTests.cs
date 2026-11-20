using System.Windows.Controls;
using WangZip.App.Dialogs;
using WangZip.Core;

namespace WangZip.App.Tests;

public class CreateDialogTests
{
    [Fact]
    public void Choosing_7z_disables_zip_only_options_and_changes_extension()
    {
        StaHost.Run(() =>
        {
            var dialog = new CreateDialog(["photos"], new AppSettings());
            try
            {
                var format = Assert.IsType<ComboBox>(dialog.FindName("FormatCombo"));
                var split = Assert.IsType<ComboBox>(dialog.FindName("SplitCombo"));
                var output = Assert.IsType<TextBox>(dialog.FindName("OutputBox"));
                Assert.EndsWith(".zip", output.Text);
                Assert.True(split.IsEnabled);

                format.SelectedIndex = 1; // 7z
                Assert.Equal(CreateFormat.SevenZip, dialog.Options.Format);
                Assert.EndsWith(".7z", output.Text);
                Assert.False(split.IsEnabled);
                Assert.Null(dialog.Options.Password);
                Assert.Equal(0, dialog.Options.SplitSizeBytes);

                format.SelectedIndex = 3; // TAR.GZ
                Assert.EndsWith(".tar.gz", output.Text);

                format.SelectedIndex = 0;
                Assert.EndsWith(".zip", output.Text);
                Assert.True(split.IsEnabled);
            }
            finally
            {
                dialog.Close();
            }

            return 0;
        });
    }
}
