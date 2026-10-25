using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RSBot.Core;
using RSBot.Core.Components;
using SDUI;
using SDUI.Controls;

namespace RSBot.Views.Dialog;

public partial class ProfileSelectionDialog : UIWindowBase
{
    public ProfileSelectionDialog()
    {
        InitializeComponent();

        LoadProfiles();
        checkSaveSelection.Checked = !ProfileManager.ShowProfileDialog;
        BackColor = ColorScheme.BackColor;

        LanguageManager.Translate(this, Kernel.Language);
    }

    /// <summary>
    ///     Gets the selected profile.
    /// </summary>
    /// <value>
    ///     The selected profile.
    /// </value>
    public string SelectedProfile { get; private set; }

    #region Methods

    private void LoadProfiles()
    {
        comboProfiles.Items.Clear();
        // ProfileManager now ensures "Default" exists

        comboProfiles.Items.AddRange(ProfileManager.Profiles);
        comboProfiles.SelectedItem = ProfileManager.SelectedProfile;
    }

    private string CreateNewProfile()
    {
        var newProfileTitle = LanguageManager.GetLangOrDefault("NewProfileTitle", "New profile");
        var inputDialog = new InputDialog(
            newProfileTitle,
            newProfileTitle,
            LanguageManager.GetLangOrDefault("NewProfileContent", "Please enter a profile name")
        );
        if (inputDialog.ShowDialog() != DialogResult.OK)
            return string.Empty;

        var profile = (string)inputDialog.Value;

        if (profile.LastIndexOfAny(Path.GetInvalidFileNameChars(), 0) != -1)
        {
            MessageBox.Show(
                LanguageManager.GetLangOrDefault("ProfileNameInvalidChars", "The profile name contains invalid characters!"),
                LanguageManager.GetLangOrDefault("ProfileInvalidNameTitle", "Invalid name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return string.Empty;
        }

        if (ProfileManager.ProfileExists(profile))
        {
            MessageBox.Show(
                string.Format(
                    LanguageManager.GetLangOrDefault("ProfileAlreadyExists", "The profile name '{0}' already exists!"),
                    profile
                ),
                LanguageManager.GetLangOrDefault("ProfileInvalidNameTitle", "Invalid name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return string.Empty;
        }

        string[] reservedNames = { "Profiles", "Default", "Settings" };
        if (reservedNames.Any(n => n.Equals(profile, StringComparison.InvariantCultureIgnoreCase)))
        {
            MessageBox.Show(
                string.Format(
                    LanguageManager.GetLangOrDefault("ProfileNameReserved", "The name '{0}' is reserved and cannot be used!"),
                    profile
                ),
                LanguageManager.GetLangOrDefault("ProfileReservedNameTitle", "Reserved name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return string.Empty;
        }

        if (ProfileManager.Add(profile, false))
            return profile;

        return string.Empty;
    }

    #endregion Methods

    #region Events

    private void comboProfiles_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (comboProfiles.SelectedItem == null)
            return;

        SelectedProfile = (string)comboProfiles.SelectedItem;
    }

    private void checkSaveSelection_CheckedChanged(object sender, EventArgs e)
    {
        ProfileManager.ShowProfileDialog = !checkSaveSelection.Checked;
    }

    private void buttonDeleteProfile_Click(object sender, EventArgs e)
    {
        var selectedProfile = (string)comboProfiles.SelectedItem;
        if (selectedProfile == "Default") //Default
        {
            MessageBox.Show(
                LanguageManager.GetLangOrDefault("ProfileDeleteDefault", "You can not delete the default profile!"),
                LanguageManager.GetLangOrDefault("ProfileDeleteDefaultTitle", "Default profile"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return;
        }

        if (ProfileManager.SelectedProfile == (string)comboProfiles.SelectedItem) //Active profile?
        {
            MessageBox.Show(
                LanguageManager.GetLangOrDefault("ProfileDeleteActive", "You can not delete the active profile!"),
                LanguageManager.GetLangOrDefault("ProfileDeleteActiveTitle", "Profile active"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return;
        }

        if (
            MessageBox.Show(
                string.Format(
                    LanguageManager.GetLangOrDefault(
                        "ProfileDeleteConfirm",
                        "Do you want to delete the profile {0} from the list?\nThis will not delete the user data."
                    ),
                    comboProfiles.SelectedItem
                ),
                LanguageManager.GetLangOrDefault("ProfileDeleteConfirmTitle", "Delete profile?"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question
            ) != DialogResult.OK
        )
            return;

        ProfileManager.Remove((string)comboProfiles.SelectedItem);

        LoadProfiles();
    }

    private void buttonCreateProfile_Click(object sender, EventArgs e)
    {
        SelectedProfile = CreateNewProfile();

        LoadProfiles();
    }

    #endregion Events
}
