using DictationApp.Models;
using DictationApp.Services;
using System.Windows;
using System.Windows.Input;

namespace DictationApp.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            LoginError.Visibility = Visibility.Collapsed;
            var username = LoginUsername.Text.Trim();
            var password = LoginPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowLoginError("Please enter username and password.");
                return;
            }

            var user = DataService.Authenticate(username, password);
            if (user == null)
            {
                ShowLoginError("Invalid username or password.");
                return;
            }

            OpenDashboard(user);
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            RegError.Visibility = Visibility.Collapsed;

            var displayName = RegDisplayName.Text.Trim();
            var username = RegUsername.Text.Trim();
            var password = RegPassword.Password;
            var roleText = (RegRole.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
            var role = roleText == "Reviewer" ? UserRole.Reviewer : UserRole.Speaker;

            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                RegError.Text = "All fields are required.";
                RegError.Visibility = Visibility.Visible;
                return;
            }
            if (password.Length < 6)
            {
                RegError.Text = "Password must be at least 6 characters.";
                RegError.Visibility = Visibility.Visible;
                return;
            }

            bool ok = DataService.RegisterUser(username, password, displayName, role);
            if (!ok)
            {
                RegError.Text = "Username already taken. Choose another.";
                RegError.Visibility = Visibility.Visible;
                return;
            }

            var user = DataService.Authenticate(username, password)!;
            OpenDashboard(user);
        }

        private void LoginPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SignInButton_Click(sender, new RoutedEventArgs());
        }

        private void ShowLoginError(string message)
        {
            LoginError.Text = message;
            LoginError.Visibility = Visibility.Visible;
        }

        private void OpenDashboard(User user)
        {
            Window dashboard = user.Role == UserRole.Speaker
                ? new SpeakerWindow(user)
                : new ReviewerWindow(user);

            dashboard.Show();
            Close();
        }
    }
}
