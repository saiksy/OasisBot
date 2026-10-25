using System;
using RSBot.Core;
using RSBot.Core.Components;
using SDUI.Controls;

namespace RSBot.Views;

internal partial class AboutDialog : UIWindowBase
{
    public AboutDialog()
    {
        InitializeComponent();
        labelName.Text = Program.AssemblyTitle;
        labelDescription.Text = Program.AssemblyDescription;
        labelVersion.Text = Program.AssemblyVersion;

        LanguageManager.Translate(this, Kernel.Language);
    }

    private void buttonOk_Click(object sender, EventArgs e)
    {
        Close();
    }
}
